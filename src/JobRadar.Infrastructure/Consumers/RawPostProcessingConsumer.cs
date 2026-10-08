using JobRadar.Application.Abstractions;
using JobRadar.Application.Messages;
using JobRadar.Application.Models;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using JobRadar.Infrastructure.BackgroundServices;
using JobRadar.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Consumers;

/// <summary>
/// MassTransit consumer that processes <see cref="RawPostCreatedEvent"/> messages.
/// Orchestrates: LLM extraction → Job creation → embedding scheduling → event publishing.
/// </summary>
public sealed class RawPostProcessingConsumer : IConsumer<RawPostCreatedEvent>
{
    private readonly AppDbContext _db;
    private readonly ILlmExtractionService _extractor;
    private readonly IPublishEndpoint _publisher;
    private readonly ILogger<RawPostProcessingConsumer> _logger;

    public RawPostProcessingConsumer(
        AppDbContext db,
        ILlmExtractionService extractor,
        IPublishEndpoint publisher,
        ILogger<RawPostProcessingConsumer> logger)
    {
        _db = db;
        _extractor = extractor;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RawPostCreatedEvent> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        _logger.LogInformation(
            "Processing RawPost {RawPostId} from Source {SourceId}.",
            msg.RawPostId, msg.SourceId);

        // ── 1. Load the RawPost ───────────────────────────────────────────────
        var rawPost = await _db.RawPosts
            .Include(r => r.Source)
            .FirstOrDefaultAsync(r => r.Id == msg.RawPostId, ct);

        if (rawPost is null)
        {
            _logger.LogWarning("RawPost {RawPostId} not found; skipping.", msg.RawPostId);
            return;
        }

        if (rawPost.ProcessingStatus == RawPostStatus.Processed)
        {
            _logger.LogInformation("RawPost {RawPostId} is already marked Processed; skipping.", msg.RawPostId);
            return;
        }

        var jobAlreadyExists = await _db.Jobs.AnyAsync(j => j.RawPostId == msg.RawPostId, ct);
        if (jobAlreadyExists)
        {
            _logger.LogInformation("Job already exists for RawPost {RawPostId}; marking processed and skipping.", msg.RawPostId);
            rawPost.MarkProcessed();
            await _db.SaveChangesAsync(ct);
            return;
        }

        rawPost.MarkProcessing();
        await _db.SaveChangesAsync(ct);

        // ── 2. LLM Extraction ────────────────────────────────────────────────
        JobExtractionResult? extraction;
        try
        {
            extraction = await _extractor.ExtractJobAsync(rawPost.RawContent, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "Transient HTTP error during LLM extraction for RawPost {RawPostId}. Resetting to New.",
                msg.RawPostId);
            rawPost.ResetToNew();
            await _db.SaveChangesAsync(ct);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unrecoverable LLM extraction failed for RawPost {RawPostId}. Marking rejected.",
                msg.RawPostId);
            rawPost.MarkRejected();
            await _db.SaveChangesAsync(ct);
            return;
        }

        // ── 3. Reject if not a real job posting ──────────────────────────────
        if (extraction is null)
        {
            _logger.LogInformation(
                "RawPost {RawPostId} rejected: not a valid job posting or low confidence.",
                msg.RawPostId);
            rawPost.MarkRejected();
            await _db.SaveChangesAsync(ct);
            return;
        }

        // ── 4. Parse enums ───────────────────────────────────────────────────
        var employmentType = Enum.TryParse<EmploymentType>(extraction.EmploymentType, out var et)
            ? et
            : EmploymentType.FullTime;

        var experienceLevel = Enum.TryParse<ExperienceLevel>(extraction.ExperienceLevel, out var el)
            ? el
            : ExperienceLevel.MidLevel;

        // ── 5. Create Job entity ─────────────────────────────────────────────
        var job = Job.Create(
            sourceId:         msg.SourceId,
            title:            extraction.Title,
            companyName:      extraction.CompanyName,
            description:      rawPost.RawContent,
            location:         extraction.Location,
            isRemote:         extraction.IsRemote,
            employmentType:   employmentType,
            experienceLevel:  experienceLevel,
            externalApplyUrl: extraction.ExternalApplyUrl ?? rawPost.RawUrl,
            rawPostId:        msg.RawPostId);

        if (extraction.SalaryMin.HasValue && extraction.SalaryMax.HasValue)
        {
            job.UpdateSalaryRange(
                extraction.SalaryMin.Value,
                extraction.SalaryMax.Value,
                extraction.SalaryCurrency ?? "USD");
        }

        _db.Jobs.Add(job);

        // ── 6. Upsert Skills ─────────────────────────────────────────────────
        foreach (var skillName in (extraction.SkillsRequired ?? new List<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var normalised = skillName.Trim();
            if (string.IsNullOrWhiteSpace(normalised)) continue;

            var slug = normalised.ToLowerInvariant()
                .Replace(' ', '-')
                .Replace('.', '-')
                .Replace('#', 's'); // e.g. "C#" → "cs"

            var skill = await _db.Skills
                .FirstOrDefaultAsync(s => s.Name == normalised || s.Slug == slug, ct);

            if (skill is null)
            {
                try
                {
                    skill = Skill.Create(normalised, slug);
                    _db.Skills.Add(skill);
                    await _db.SaveChangesAsync(ct); // flush to get the ID
                }
                catch (DbUpdateException)
                {
                    _db.Entry(skill!).State = EntityState.Detached;
                    skill = await _db.Skills.FirstOrDefaultAsync(s => s.Slug == slug, ct);
                }
            }

            if (skill is not null)
            {
                _db.JobSkillMaps.Add(new JobSkillMap(job.Id, skill.Id));
            }
        }

        rawPost.MarkProcessed();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Job {JobId} created from RawPost {RawPostId} (Title: {Title}, Company: {Company}).",
            job.Id, msg.RawPostId, job.Title, job.CompanyName);

        // ── 7. Enqueue embedding generation (non-blocking) ───────────────────
        await EmbeddingBatchProcessor.JobIdChannel.Writer.WriteAsync(job.Id, ct);

        // ── 8. Publish JobCreatedEvent ───────────────────────────────────────
        var skillNames = (extraction.SkillsRequired ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await _publisher.Publish(new JobCreatedEvent(
            JobId:           job.Id,
            SourceId:        job.SourceId,
            RawPostId:       job.RawPostId,
            Title:           job.Title,
            CompanyName:     job.CompanyName,
            Location:        job.Location,
            IsRemote:        job.IsRemote,
            EmploymentType:  job.EmploymentType,
            ExperienceLevel: job.ExperienceLevel,
            SalaryMin:       job.SalaryMin,
            SalaryMax:       job.SalaryMax,
            SalaryCurrency:  job.SalaryCurrency,
            Skills:          skillNames,
            ExternalApplyUrl: job.ExternalApplyUrl,
            PostedAt:        job.PostedAt), ct);

        _logger.LogInformation("Published JobCreatedEvent for Job {JobId}.", job.Id);
    }
}
