using System.Security.Cryptography;
using System.Text;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Common;
using JobRadar.Application.Messages;
using JobRadar.Application.Models;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using JobRadar.Infrastructure.BackgroundServices;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.Prompts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Consumers;

/// <summary>
/// MassTransit consumer that processes <see cref="RawPostCreatedEvent"/> messages.
/// Orchestrates: Pre-LLM filter → LLM extraction → Job creation → embedding scheduling → event publishing.
/// </summary>
public sealed class RawPostProcessingConsumer : IConsumer<RawPostCreatedEvent>
{
    private static readonly string[] ErrorPatterns = new[]
    {
        // English
        "404 not found",
        "page not found",
        "404 - page",
        "403 forbidden",
        "access denied",
        "under maintenance",
        "site maintenance",
        "scheduled maintenance",
        "temporarily unavailable",
        "service unavailable",
        "502 bad gateway",
        "503 service",
        "the page you are looking for does not exist",
        "this page could not be found",
        "no vacancies found",
        "no current vacancies",
        "no active vacancies",
        "there are currently no open positions",

        // Arabic
        "الصفحة غير موجودة",
        "خطأ 404",
        "تحت الصيانة",
        "قيد الصيانة",
        "غير متوفر حاليا",
        "غير متاح حالياً",
        "تم نقل الصفحة أو حذفها",
        "لا توجد شواغر حالياً",
        "لا توجد وظائف شاغرة حالياً",
        "لا توجد شواغر",
        "لا توجد وظائف متاحة",
        "عفواً، لم نتمكن من العثور"
    };

    private readonly AppDbContext _db;
    private readonly ILlmExtractionService _extractor;
    private readonly IPublishEndpoint _publisher;
    private readonly IConfiguration _configuration;
    private readonly IGeminiPromptProvider _promptProvider;
    private readonly ILogger<RawPostProcessingConsumer> _logger;

    public RawPostProcessingConsumer(
        AppDbContext db,
        ILlmExtractionService extractor,
        IPublishEndpoint publisher,
        IConfiguration configuration,
        IGeminiPromptProvider promptProvider,
        ILogger<RawPostProcessingConsumer> logger)
    {
        _db = db;
        _extractor = extractor;
        _publisher = publisher;
        _configuration = configuration;
        _promptProvider = promptProvider;
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

        // ── 1b. Content Hash & Duplicate Check ───────────────────────────────
        var contentHash = ComputeSha256(rawPost.RawContent);
        if (string.IsNullOrEmpty(rawPost.ContentHash))
        {
            rawPost.SetContentHash(contentHash);
        }

        if (!string.IsNullOrWhiteSpace(rawPost.RawUrl))
        {
            var hasIdenticalPreviousFetch = await _db.RawPosts
                .AnyAsync(r => r.Id != rawPost.Id
                            && r.RawUrl == rawPost.RawUrl
                            && r.ContentHash == contentHash, ct);

            if (hasIdenticalPreviousFetch)
            {
                _logger.LogInformation(
                    "RawPost {RawPostId} content is identical to previous fetch of same URL {Url} (Hash: {Hash}). Skipping extraction.",
                    rawPost.Id, rawPost.RawUrl, contentHash);
                rawPost.MarkRejected("DuplicateContentHash: Content is identical to previous fetch of same source URL");
                await _db.SaveChangesAsync(ct);
                return;
            }
        }

        // ── 1c. Pre-LLM Filter: Minimum Content Length ───────────────────────
        var strippedContent = HtmlContentSanitizer.Sanitize(rawPost.RawContent);
        var minLength = _configuration.GetValue<int>("Ingestion:MinRawContentLength", 200);
        if (strippedContent.Length < minLength)
        {
            _logger.LogInformation(
                "RawPost {RawPostId} stripped content length ({Length}) is below minimum {MinLength}. Rejecting without calling LLM.",
                rawPost.Id, strippedContent.Length, minLength);
            rawPost.MarkRejected($"ContentTooShort: Stripped length {strippedContent.Length} is below minimum {minLength}");
            await _db.SaveChangesAsync(ct);
            return;
        }

        // ── 1d. Pre-LLM Filter: Error / Maintenance / 404 Pages ─────────────
        if (MatchesErrorOrMaintenancePattern(strippedContent, out var matchedPattern))
        {
            _logger.LogInformation(
                "RawPost {RawPostId} matched error/maintenance pattern '{Pattern}'. Rejecting without calling LLM.",
                rawPost.Id, matchedPattern);
            rawPost.MarkRejected($"ErrorOrMaintenancePage: Matched '{matchedPattern}'");
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
            rawPost.MarkRejected($"ExtractionError: {ex.Message}");
            await _db.SaveChangesAsync(ct);
            return;
        }

        // ── 3. Reject if not a real job posting ──────────────────────────────
        if (extraction is null || !extraction.IsJobPosting)
        {
            _logger.LogInformation(
                "RawPost {RawPostId} rejected: not a valid job posting or low confidence.",
                msg.RawPostId);
            rawPost.MarkRejected("NotAJobPosting: Model classified content as not a valid job posting or confidence below threshold");
            await _db.SaveChangesAsync(ct);
            return;
        }

        // ── 4. Parse enums safely (supporting Unknown) ──────────────────────
        var employmentType = (!string.IsNullOrWhiteSpace(extraction.EmploymentType) &&
                              Enum.TryParse<EmploymentType>(extraction.EmploymentType, true, out var et))
            ? et
            : EmploymentType.Unknown;

        var experienceLevel = (!string.IsNullOrWhiteSpace(extraction.ExperienceLevel) &&
                               Enum.TryParse<ExperienceLevel>(extraction.ExperienceLevel, true, out var el))
            ? el
            : ExperienceLevel.Unknown;

        var promptVersion = _promptProvider.CurrentExtractionVersion;
        rawPost.SetExtractionPromptVersion(promptVersion);

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

        job.SetContactDetails(extraction.ApplyEmail, extraction.ApplyPhone);
        job.SetExtractionPromptVersion(promptVersion);

        // Check CV match eligibility (description length < 300 or LinkedIn listing stub)
        var strippedJd = HtmlContentSanitizer.Sanitize(job.Description);
        if (strippedJd.Length < 300 || (rawPost.Source?.Type == SourceType.LinkedIn && strippedJd.Length < 400))
        {
            job.SetCvMatchEligibility(false);
            _logger.LogInformation(
                "Job {JobId} flagged as ineligible for CV matching (stripped length: {Length}, Source: {SourceType}).",
                job.Id, strippedJd.Length, rawPost.Source?.Type);
        }

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

    private static string ComputeSha256(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static bool MatchesErrorOrMaintenancePattern(string strippedContent, out string matchedPattern)
    {
        matchedPattern = string.Empty;
        if (string.IsNullOrWhiteSpace(strippedContent))
            return false;

        var normalized = EvidenceVerificationService.NormalizeText(strippedContent);
        foreach (var pattern in ErrorPatterns)
        {
            var normalizedPattern = EvidenceVerificationService.NormalizeText(pattern);
            if (normalized.Contains(normalizedPattern, StringComparison.OrdinalIgnoreCase))
            {
                matchedPattern = pattern;
                return true;
            }
        }

        return false;
    }
}
