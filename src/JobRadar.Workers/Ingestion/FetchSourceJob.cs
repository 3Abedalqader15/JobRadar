using JobRadar.Application.Features.Sources.Commands.IngestSource;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JobRadar.Workers.Ingestion;

/// <summary>
/// Hangfire job for backwards compatibility with existing serialized jobs in database.
/// Delegates to MediatR to fetch a specific source.
/// </summary>
public class FetchSourceJob
{
    private readonly ISender _sender;
    private readonly ILogger<FetchSourceJob> _logger;

    public FetchSourceJob(ISender sender, ILogger<FetchSourceJob> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public Task ExecuteAsync(Guid sourceId) => ExecuteAsync(sourceId, CancellationToken.None);

    public async Task ExecuteAsync(Guid sourceId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Triggering MediatR command to ingest source {SourceId}", sourceId);
        
        await _sender.Send(new IngestSourceCommand(sourceId), cancellationToken);
    }
}
