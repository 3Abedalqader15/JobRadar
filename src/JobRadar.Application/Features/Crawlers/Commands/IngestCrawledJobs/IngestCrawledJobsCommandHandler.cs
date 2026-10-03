using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Features.Crawlers.Commands.IngestCrawledJobs;

public sealed class IngestCrawledJobsCommandHandler : IRequestHandler<IngestCrawledJobsCommand, int>
{
    private readonly IEnumerable<IJobCrawlerProvider> _crawlers;
    private readonly IJobRepository _jobRepository;
    private readonly ISourceRepository _sourceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJobEmbeddingChannel _embeddingChannel;
    private readonly IJobRealtimeNotifier _realtimeNotifier;
    private readonly ILogger<IngestCrawledJobsCommandHandler> _logger;

    public IngestCrawledJobsCommandHandler(
        IEnumerable<IJobCrawlerProvider> crawlers,
        IJobRepository jobRepository,
        ISourceRepository sourceRepository,
        IUnitOfWork unitOfWork,
        IJobEmbeddingChannel embeddingChannel,
        IJobRealtimeNotifier realtimeNotifier,
        ILogger<IngestCrawledJobsCommandHandler> logger)
    {
        _crawlers = crawlers;
        _jobRepository = jobRepository;
        _sourceRepository = sourceRepository;
        _unitOfWork = unitOfWork;
        _embeddingChannel = embeddingChannel;
        _realtimeNotifier = realtimeNotifier;
        _logger = logger;
    }

    public async Task<int> Handle(IngestCrawledJobsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Starting IngestCrawledJobsCommand ===");
        int totalNewJobs = 0;

        var targetCrawlers = string.IsNullOrWhiteSpace(request.ProviderName)
            ? _crawlers
            : _crawlers.Where(c => c.ProviderName.Equals(request.ProviderName, StringComparison.OrdinalIgnoreCase));

        foreach (var crawler in targetCrawlers)
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
                var source = await _sourceRepository.GetByNameAsync(sourceName, cancellationToken);
                if (source == null)
                {
                    source = Source.Create(
                        sourceName,
                        SourceType.CompanyCareersPage,
                        "https://jobradar.io/crawlers/" + crawler.ProviderName.ToLowerInvariant(),
                        null,
                        15);
                    source.Approve();
                    await _sourceRepository.AddAsync(source, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                // 2. Pre-fetch existing URLs to prevent duplicate posts (Phase 1 Deduplication)
                var incomingUrls = discovered
                    .Select(d => d.ExternalApplyUrl)
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .Distinct()
                    .ToList();

                var existingUrls = await _jobRepository.GetExistingUrlsAsync(incomingUrls, cancellationToken);
                var existingUrlSet = new HashSet<string>(existingUrls, StringComparer.OrdinalIgnoreCase);

                // 3. Process and ingest each new job (Phase 2 Deduplication)
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
                    var titleExists = await _jobRepository.ExistsByTitleAndCompanyAsync(jobDto.Title, jobDto.CompanyName, cancellationToken);
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

                    // 5. Add Job with skills and commit
                    await _jobRepository.AddWithSkillsAsync(job, jobDto.Skills, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    existingUrlSet.Add(jobDto.ExternalApplyUrl);
                    crawlerNewJobs++;
                    totalNewJobs++;

                    _logger.LogInformation("Created job post: {Title} at {Company} (ID: {JobId})", job.Title, job.CompanyName, job.Id);

                    // 6. Enqueue for background semantic embedding generation
                    await _embeddingChannel.EnqueueAsync(job.Id, cancellationToken);

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
                await _sourceRepository.UpdateAsync(source, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Crawler {ProviderName} finished. Ingested {Count} new jobs.", crawler.ProviderName, crawlerNewJobs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during crawl execution for {ProviderName}", crawler.ProviderName);
            }
        }

        _logger.LogInformation("=== IngestCrawledJobsCommand Finished: {Total} new jobs created ===", totalNewJobs);
        return totalNewJobs;
    }
}
