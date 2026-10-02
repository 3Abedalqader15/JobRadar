using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Services;

public sealed class AutomatedJobIngestionService : IAutomatedJobIngestionService
{
    private static readonly SemaphoreSlim _crawlLock = new(1, 1);
    private readonly IEnumerable<IJobCrawlerProvider> _crawlers;
    private readonly AppDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IJobRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<AutomatedJobIngestionService> _logger;

    public AutomatedJobIngestionService(
        IEnumerable<IJobCrawlerProvider> crawlers,
        AppDbContext dbContext,
        IEmbeddingService embeddingService,
        IJobRealtimeNotifier realtimeNotifier,
        ILogger<AutomatedJobIngestionService> logger)
    {
        _crawlers = crawlers;
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    public async Task<int> RunCrawlCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!await _crawlLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation("A crawl cycle is already in progress. Skipping overlapping run.");
            return 0;
        }

        try
        {
            _logger.LogInformation("=== Starting Automated Periodic Job Crawl Cycle ===");
            int totalNewJobs = 0;

            foreach (var crawler in _crawlers)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("Executing crawler provider: {ProviderName}", crawler.ProviderName);
                    var discovered = await crawler.CrawlJobsAsync(cancellationToken);

                    if (discovered == null || discovered.Count == 0)
                    {
                        _logger.LogInformation("No jobs found by crawler {ProviderName}.", crawler.ProviderName);
                        continue;
                    }

                    // 1. Ensure System Source exists for this provider
                    var sourceName = "Crawler: " + crawler.ProviderName;
                    var source = await _dbContext.Sources.FirstOrDefaultAsync(s => s.Name == sourceName, cancellationToken);
                    if (source == null)
                    {
                        source = Source.Create(
                            sourceName,
                            SourceType.CompanyCareersPage,
                            "https://jobradar.io/crawlers/" + crawler.ProviderName.ToLowerInvariant(),
                            null,
                            15);
                        source.Approve();
                        _dbContext.Sources.Add(source);
                        try
                        {
                            await _dbContext.SaveChangesAsync(cancellationToken);
                        }
                        catch (DbUpdateException)
                        {
                            _dbContext.Entry(source).State = EntityState.Detached;
                            source = await _dbContext.Sources.FirstOrDefaultAsync(s => s.Name == sourceName, cancellationToken);
                        }
                    }

                // 2. Pre-fetch existing URLs to prevent duplicate posts
                var incomingUrls = discovered
                    .Select(d => d.ExternalApplyUrl)
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .Distinct()
                    .ToList();

                var existingUrls = await _dbContext.Jobs
                    .Where(j => incomingUrls.Contains(j.ExternalApplyUrl!))
                    .Select(j => j.ExternalApplyUrl!)
                    .ToListAsync(cancellationToken);

                var existingUrlSet = new HashSet<string>(existingUrls, StringComparer.OrdinalIgnoreCase);

                // 3. Process and ingest each new job
                int crawlerNewJobs = 0;
                foreach (var jobDto in discovered)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    if (string.IsNullOrWhiteSpace(jobDto.ExternalApplyUrl) || string.IsNullOrWhiteSpace(jobDto.Title))
                    {
                        continue;
                    }

                    // Deduplicate by URL
                    if (existingUrlSet.Contains(jobDto.ExternalApplyUrl))
                    {
                        continue;
                    }

                    // Deduplicate by normalized Title + Company
                    var normTitle = jobDto.Title.Trim().ToLowerInvariant();
                    var normCompany = jobDto.CompanyName.Trim().ToLowerInvariant();
                    var titleExists = await _dbContext.Jobs.AnyAsync(
                        j => j.Title.ToLower() == normTitle && j.CompanyName.ToLower() == normCompany,
                        cancellationToken);

                    if (titleExists)
                    {
                        existingUrlSet.Add(jobDto.ExternalApplyUrl);
                        continue;
                    }

                    // 4. Create Job Entity
                    var job = Job.Create(
                        sourceId: source.Id,
                        title: jobDto.Title,
                        companyName: jobDto.CompanyName,
                        description: jobDto.Description,
                        location: jobDto.Location,
                        isRemote: jobDto.IsRemote,
                        employmentType: jobDto.EmploymentType,
                        experienceLevel: jobDto.ExperienceLevel,
                        externalApplyUrl: jobDto.ExternalApplyUrl,
                        postedAt: jobDto.PostedAt ?? DateTime.UtcNow,
                        rawPostId: null);

                    if (jobDto.SalaryMin.HasValue && jobDto.SalaryMax.HasValue)
                    {
                        job.UpdateSalaryRange(jobDto.SalaryMin.Value, jobDto.SalaryMax.Value, jobDto.SalaryCurrency ?? "USD");
                    }
                    else if (jobDto.SalaryMin.HasValue)
                    {
                        job.UpdateSalaryRange(jobDto.SalaryMin.Value, jobDto.SalaryMin.Value, jobDto.SalaryCurrency ?? "USD");
                    }

                    _dbContext.Jobs.Add(job);

                    // 5. Upsert Skills & Map
                    foreach (var skillName in jobDto.Skills.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        var normSkill = skillName.Trim();
                        if (string.IsNullOrWhiteSpace(normSkill)) continue;

                        var skill = _dbContext.Skills.Local.FirstOrDefault(s => s.Name.Equals(normSkill, StringComparison.OrdinalIgnoreCase))
                            ?? await _dbContext.Skills.FirstOrDefaultAsync(
                                s => s.Name.ToLower() == normSkill.ToLower(),
                                cancellationToken);

                        if (skill == null)
                        {
                            var slug = normSkill.ToLowerInvariant()
                                .Replace(' ', '-')
                                .Replace('.', '-')
                                .Replace('#', 's');
                            skill = Skill.Create(normSkill, slug);
                            _dbContext.Skills.Add(skill);
                            try
                            {
                                await _dbContext.SaveChangesAsync(cancellationToken);
                            }
                            catch (DbUpdateException)
                            {
                                _dbContext.Entry(skill).State = EntityState.Detached;
                                skill = _dbContext.Skills.Local.FirstOrDefault(s => s.Name.Equals(normSkill, StringComparison.OrdinalIgnoreCase))
                                    ?? await _dbContext.Skills.FirstOrDefaultAsync(
                                        s => s.Name.ToLower() == normSkill.ToLower(),
                                        cancellationToken);
                            }
                        }

                        if (skill != null)
                        {
                            var alreadyMapped = _dbContext.JobSkillMaps.Local.Any(m => m.JobId == job.Id && m.SkillId == skill.Id);
                            if (!alreadyMapped)
                            {
                                _dbContext.JobSkillMaps.Add(new JobSkillMap(job.Id, skill.Id));
                            }
                        }
                    }

                    // 6. Generate Vector Embedding
                    try
                    {
                        var embeddingText = $"{job.Title} {job.CompanyName} {job.Location ?? ""} {job.Description}";
                        if (embeddingText.Length > 1500)
                        {
                            embeddingText = embeddingText[..1500];
                        }

                        var embedding = await _embeddingService.GenerateAsync(embeddingText, cancellationToken);
                        if (embedding != null && embedding.Length > 0)
                        {
                            job.SetEmbedding(embedding);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate embedding for job {Title}. Vector search will rely on trigram/FTS.", job.Title);
                    }

                    // Save new Job
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    existingUrlSet.Add(jobDto.ExternalApplyUrl);
                    crawlerNewJobs++;
                    totalNewJobs++;

                    _logger.LogInformation("Created job post: {Title} at {Company} (ID: {JobId})", job.Title, job.CompanyName, job.Id);

                    // 7. Broadcast in Real-Time to relevant SignalR criteria groups
                    await _realtimeNotifier.NotifyJobCreatedAsync(
                        job.Id,
                        job.Title,
                        job.CompanyName,
                        job.Location,
                        job.IsRemote,
                        job.EmploymentType,
                        job.ExperienceLevel,
                        job.SalaryMin,
                        job.SalaryMax,
                        job.SalaryCurrency,
                        jobDto.Skills,
                        job.ExternalApplyUrl,
                        job.PostedAt,
                        cancellationToken);
                }

                source.RecordFetch();
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Crawler {ProviderName} finished. Ingested {Count} new jobs.", crawler.ProviderName, crawlerNewJobs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during crawl execution for {ProviderName}", crawler.ProviderName);
            }
        }

        _logger.LogInformation("=== Automated Job Crawl Cycle Finished: {Total} new jobs created ===", totalNewJobs);
        return totalNewJobs;
    }
    finally
    {
        _crawlLock.Release();
    }
}
}
