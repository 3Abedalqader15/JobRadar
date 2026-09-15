using Hangfire;
using JobRadar.Domain.Enums;
using JobRadar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobRadar.Workers.Ingestion;

public class IngestionDispatcherJob
{
    private readonly AppDbContext _dbContext;
    private readonly IBackgroundJobClient _jobClient;
    private readonly ILogger<IngestionDispatcherJob> _logger;

    public IngestionDispatcherJob(AppDbContext dbContext, IBackgroundJobClient jobClient, ILogger<IngestionDispatcherJob> logger)
    {
        _dbContext = dbContext;
        _jobClient = jobClient;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Running IngestionDispatcherJob...");

        var now = DateTime.UtcNow;
        var sources = await _dbContext.Sources
            .Where(s => s.Status == SourceStatus.Active)
            .ToListAsync();

        var sourcesToFetch = sources.Where(s => 
            !s.LastFetchedAt.HasValue || 
            s.LastFetchedAt.Value.AddMinutes(s.FetchIntervalMinutes) <= now).ToList();

        _logger.LogInformation("Found {Count} sources to fetch out of {TotalCount} active sources.", sourcesToFetch.Count, sources.Count);

        foreach (var source in sourcesToFetch)
        {
            _jobClient.Enqueue<FetchSourceJob>(job => job.ExecuteAsync(source.Id));
        }
    }
}
