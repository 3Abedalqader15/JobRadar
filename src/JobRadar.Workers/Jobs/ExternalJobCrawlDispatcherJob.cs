using Hangfire;
using JobRadar.Application.Features.Crawlers.Commands.IngestCrawledJobs;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JobRadar.Workers.Jobs;

/// <summary>
/// Hangfire recurring job that executes the external job crawl cycle via MediatR.
/// Decorated with [DisableConcurrentExecution] to guarantee cross-instance safety:
/// even if multiple JobRadar.Workers replicas are running, only a single instance executes
/// the crawl cycle at any given time (using Hangfire's PostgreSQL distributed storage lock).
/// </summary>
public class ExternalJobCrawlDispatcherJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<ExternalJobCrawlDispatcherJob> _logger;

    public ExternalJobCrawlDispatcherJob(
        IMediator mediator,
        ILogger<ExternalJobCrawlDispatcherJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting scheduled external job crawl cycle at {UtcNow}", DateTime.UtcNow);

        try
        {
            var count = await _mediator.Send(new IngestCrawledJobsCommand(), cancellationToken);
            _logger.LogInformation(
                "Scheduled external job crawl cycle completed: {Count} new jobs ingested at {UtcNow}",
                count, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled external job crawl cycle failed at {UtcNow}", DateTime.UtcNow);
            throw; // Hangfire will mark as failed and handle retry policies
        }
    }
}
