using JobRadar.Application.Features.Sources.Commands.IngestSource;
using MediatR;
using Microsoft.Extensions.Logging;

namespace JobRadar.Workers.Jobs;

/// <summary>
/// Hangfire job that delegates to MediatR to fetch a specific source.
/// </summary>
public sealed class FetchSourceJob
{
    private readonly ISender _sender;
    private readonly ILogger<FetchSourceJob> _logger;

    public FetchSourceJob(ISender sender, ILogger<FetchSourceJob> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Triggering MediatR command to ingest source {SourceId}", sourceId);
        
        await _sender.Send(new IngestSourceCommand(sourceId), cancellationToken);
    }
}
