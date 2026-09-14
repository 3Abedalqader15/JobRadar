using JobRadar.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace JobRadar.Workers.Jobs;

/// <summary>
/// Hangfire job that triggers ingestion of all enabled job sources.
/// Scheduled every 6 hours by default (configurable via Hangfire dashboard).
/// </summary>
public sealed class JobIngestionJob
{
    private readonly IJobIngestionService _ingestionService;
    private readonly ILogger<JobIngestionJob> _logger;

    public JobIngestionJob(
        IJobIngestionService ingestionService,
        ILogger<JobIngestionJob> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting full job ingestion at {UtcNow}", DateTime.UtcNow);

        try
        {
            await _ingestionService.IngestAllSourcesAsync(cancellationToken);
            _logger.LogInformation("Job ingestion completed successfully at {UtcNow}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job ingestion failed at {UtcNow}", DateTime.UtcNow);
            throw; // Rethrow so Hangfire marks the job as failed and can retry
        }
    }
}
