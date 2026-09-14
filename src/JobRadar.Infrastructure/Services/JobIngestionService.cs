using JobRadar.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Services;

public class JobIngestionService(
    IJobSourceRepository jobSourceRepository,
    IJobPostingRepository jobPostingRepository,
    IUnitOfWork unitOfWork,
    ILogger<JobIngestionService> logger) : IJobIngestionService
{
    public async Task IngestAllSourcesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting ingestion for all enabled sources.");
        
        var enabledSources = await jobSourceRepository.GetAllEnabledAsync(cancellationToken);
        
        foreach (var source in enabledSources)
        {
            await IngestSourceAsync(source.Id, cancellationToken);
        }
        
        logger.LogInformation("Completed ingestion for all enabled sources.");
    }

    public async Task IngestSourceAsync(Guid jobSourceId, CancellationToken cancellationToken = default)
    {
        var source = await jobSourceRepository.GetByIdAsync(jobSourceId, cancellationToken);
        
        if (source == null)
        {
            logger.LogWarning("Job source {JobSourceId} not found.", jobSourceId);
            return;
        }

        if (!source.IsEnabled)
        {
            logger.LogInformation("Job source {SourceName} is disabled. Skipping.", source.Name);
            return;
        }

        logger.LogInformation("Ingesting jobs from {SourceName} ({BaseUrl})", source.Name, source.BaseUrl);

        // TODO: Implement actual HTTP calls to the source API or scraper here.
        // For now, this is a placeholder implementation.
        
        // Example logic:
        // 1. Fetch raw data from source.BaseUrl
        // 2. Parse/Map to JobPosting entities
        // 3. Skip existing (by source_url)
        // 4. Generate embeddings via IVectorSearchService
        // 5. Add to repository
        
        source.RecordFetch();
        
        await jobSourceRepository.UpdateAsync(source, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        
        logger.LogInformation("Successfully ingested jobs from {SourceName}.", source.Name);
    }
}
