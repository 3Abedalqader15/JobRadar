using JobRadar.Application.Messages;
using JobRadar.Domain.Enums;
using JobRadar.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobRadar.Workers.Jobs;

/// <summary>
/// Defense-in-depth backlog sweep job.
/// Detects any raw posts in 'New' status that were not processed (e.g. due to worker restarts
/// or transient broker disconnects) and republishes them to MassTransit.
/// Ensures strict idempotency by checking if a Job already exists before publishing.
/// </summary>
public sealed class RawPostBacklogSweepJob
{
    private readonly AppDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<RawPostBacklogSweepJob> _logger;

    public RawPostBacklogSweepJob(
        AppDbContext db,
        IPublishEndpoint publishEndpoint,
        ILogger<RawPostBacklogSweepJob> logger)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting RawPostBacklogSweepJob check...");

        // Fetch pending raw posts that have not completed extraction
        var pendingPosts = await _db.RawPosts
            .Where(r => r.ProcessingStatus == RawPostStatus.New)
            .OrderBy(r => r.FetchedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (pendingPosts.Count == 0)
        {
            _logger.LogInformation("No pending raw posts in backlog.");
            return;
        }

        _logger.LogInformation("Found {Count} pending raw posts in backlog. Dispatching to RabbitMQ...", pendingPosts.Count);

        int republished = 0;
        int resolvedDuplicates = 0;

        foreach (var rawPost in pendingPosts)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Strict idempotency: check if Job already exists for this raw post
            var jobExists = await _db.Jobs.AnyAsync(j => j.RawPostId == rawPost.Id, cancellationToken);
            if (jobExists)
            {
                _logger.LogInformation("Job already exists for RawPost {RawPostId}. Marking Processed.", rawPost.Id);
                rawPost.MarkProcessed();
                resolvedDuplicates++;
                continue;
            }

            // Publish message to RabbitMQ exchange
            var ev = new RawPostCreatedEvent(rawPost.Id, rawPost.SourceId, rawPost.RawUrl, rawPost.FetchedAt);
            await _publishEndpoint.Publish(ev, cancellationToken);
            republished++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "RawPostBacklogSweepJob finished: Republished={Republished}, ResolvedDuplicates={Duplicates}.",
            republished, resolvedDuplicates);
    }
}
