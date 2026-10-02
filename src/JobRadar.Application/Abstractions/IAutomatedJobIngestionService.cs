namespace JobRadar.Application.Abstractions;

public interface IAutomatedJobIngestionService
{
    Task<int> RunCrawlCycleAsync(CancellationToken cancellationToken = default);
}
