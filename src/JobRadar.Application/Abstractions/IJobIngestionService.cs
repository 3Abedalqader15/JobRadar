namespace JobRadar.Application.Abstractions;

/// <summary>
/// Orchestrates the ingestion of job postings from all enabled sources.
/// Implementations live in Infrastructure and are triggered by Hangfire jobs.
/// </summary>
public interface IJobIngestionService
{
    /// <summary>
    /// Fetches new job postings from all enabled <see cref="Domain.Entities.JobSource"/> records
    /// and persists them to the database. Skips duplicates by source URL.
    /// </summary>
    Task IngestAllSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingests postings from a single source identified by <paramref name="jobSourceId"/>.
    /// </summary>
    Task IngestSourceAsync(Guid jobSourceId, CancellationToken cancellationToken = default);
}
