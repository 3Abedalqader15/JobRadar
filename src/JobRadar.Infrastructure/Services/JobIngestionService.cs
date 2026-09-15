using JobRadar.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Services;

public class JobIngestionService(
    ISourceRepository sourceRepository,
    IUnitOfWork unitOfWork,
    ILogger<JobIngestionService> logger) : IJobIngestionService
{
    public async Task IngestAllSourcesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting ingestion for all active sources.");

        var activeSources = await sourceRepository.GetAllActiveAsync(cancellationToken);

        foreach (var source in activeSources)
        {
            await IngestSourceAsync(source.Id, cancellationToken);
        }

        logger.LogInformation("Completed ingestion for all active sources.");
    }

    public async Task IngestSourceAsync(Guid sourceId, CancellationToken cancellationToken = default)
    {
        var source = await sourceRepository.GetByIdAsync(sourceId, cancellationToken);

        if (source is null)
        {
            logger.LogWarning("Source {SourceId} not found.", sourceId);
            return;
        }

        logger.LogInformation(
            "Ingesting jobs from {SourceName} ({SourceUrl}) [Type={SourceType}]",
            source.Name, source.Url, source.Type);

        // TODO: Dispatch to the appropriate ingestion strategy based on source.Type:
        //   SourceType.RssFeed           → RSS feed parser (CodeHollow.FeedReader)
        //   SourceType.TelegramChannel   → Telegram Bot/MTProto client
        //   SourceType.CompanyCareersPage → Web scraper
        //   SourceType.ManualShare       → No-op (user-submitted manually)

        source.RecordFetch();
        await sourceRepository.UpdateAsync(source, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Recorded fetch for source {SourceName}.", source.Name);
    }
}
