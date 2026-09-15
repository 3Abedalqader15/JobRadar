namespace JobRadar.Application.Abstractions;

/// <summary>
/// Orchestrates the ingestion of job postings from all enabled sources.
/// Implementations live in Infrastructure and are triggered by Hangfire jobs.
/// </summary>
public interface IJobIngestionService
{
    /// <summary>
    /// Fetches new raw posts from all active <see cref="Domain.Entities.Source"/> records
    /// and persists them as <see cref="Domain.Entities.RawPost"/> entities.
    /// </summary>
    Task IngestAllSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingests raw posts from a single source identified by <paramref name="sourceId"/>.
    /// </summary>
    Task IngestSourceAsync(Guid sourceId, CancellationToken cancellationToken = default);
}
