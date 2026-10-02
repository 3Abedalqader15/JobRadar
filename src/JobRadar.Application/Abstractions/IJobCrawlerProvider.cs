using JobRadar.Application.Models;

namespace JobRadar.Application.Abstractions;

public interface IJobCrawlerProvider
{
    string ProviderName { get; }
    Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default);
}
