using CodeHollow.FeedReader;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Fetchers;

internal sealed class RssFetcher : ISourceFetcher
{
    private readonly ILogger<RssFetcher> _logger;

    public RssFetcher(ILogger<RssFetcher> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(SourceType type) => type == SourceType.RssFeed;

    public async Task<IReadOnlyList<FetchedPostInfo>> FetchPostsAsync(Source source, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching RSS feed {Url}", source.Url);
        
        var feed = await FeedReader.ReadAsync(source.Url, cancellationToken);
        var results = new List<FetchedPostInfo>();

        foreach (var item in feed.Items)
        {
            var rawContent = $"Title: {item.Title}\nLink: {item.Link}\nDescription: {item.Description}\nContent: {item.Content}";
            
            // For RSS, we can use the GUID or Link as the identifier to deduplicate (and date to filter old).
            // SyncIdentifier could just be the item.Id (GUID) if present, else Link.
            var syncId = !string.IsNullOrEmpty(item.Id) ? item.Id : item.Link;
            var fetchDate = item.PublishingDate ?? DateTime.UtcNow;

            results.Add(new FetchedPostInfo(item.Link ?? source.Url, rawContent, syncId, fetchDate));
        }

        // Return from oldest to newest if possible, but for RSS we just return the batch
        return results;
    }
}
