using JobRadar.Application.Abstractions;
using JobRadar.Application.Messages;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobRadar.Workers.Jobs;

/// <summary>
/// Hangfire job that removes job postings older than a configurable number of days.
/// Runs daily at midnight UTC by default.
/// Broadcasts throttled JobDeactivatedEvent to avoid flood/broadcast storm.
/// </summary>
public sealed class StaleJobCleanupJob
{
    private readonly IJobRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<StaleJobCleanupJob> _logger;

    // Crawled/external job postings older than 10 days will be automatically purged.
    // Direct JobRadar platform postings (posted directly by employers/admin) are strictly preserved indefinitely.
    private const int CrawledJobRetentionDays = 10;
    private const int ThrottleBatchSize = 25;
    private const int ThrottleDelayMs = 100;

    public StaleJobCleanupJob(
        IJobRepository repository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        ILogger<StaleJobCleanupJob> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-CrawledJobRetentionDays);

        _logger.LogInformation(
            "Running 10-day crawled job cleanup. Removing external/crawled postings with PostedAt < {Cutoff} while preserving all direct JobRadar postings.",
            cutoff);

        try
        {
            var all = await _repository.GetAllAsync(cancellationToken);
            var stale = all.Where(j => !j.IsDirectPlatformPost && j.PostedAt < cutoff).ToList();
            var directCount = all.Count(j => j.IsDirectPlatformPost);

            if (stale.Count == 0)
            {
                _logger.LogInformation("No expired crawled jobs found. Preserved {DirectCount} direct postings.", directCount);
                return;
            }

            foreach (var posting in stale)
            {
                await _repository.DeleteAsync(posting, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Purged {DeletedCount} crawled jobs older than {Days} days. Strictly preserved {DirectCount} official direct JobRadar posts.",
                stale.Count, CrawledJobRetentionDays, directCount);

            var now = DateTime.UtcNow;
            for (int i = 0; i < stale.Count; i += ThrottleBatchSize)
            {
                var batch = stale.Skip(i).Take(ThrottleBatchSize);
                foreach (var posting in batch)
                {
                    await _publishEndpoint.Publish(new JobDeactivatedEvent(posting.Id, now), cancellationToken);
                }

                if (i + ThrottleBatchSize < stale.Count)
                {
                    await Task.Delay(ThrottleDelayMs, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stale job cleanup failed.");
            throw;
        }
    }
}
