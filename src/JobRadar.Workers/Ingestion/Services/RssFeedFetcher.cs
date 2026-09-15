using System.Threading.Tasks;
using CodeHollow.FeedReader;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Collections.Generic;
using System.Linq;

namespace JobRadar.Workers.Ingestion.Services;

public class RssFeedFetcher
{
    private readonly ILogger<RssFeedFetcher> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public RssFeedFetcher(ILogger<RssFeedFetcher> logger)
    {
        _logger = logger;
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "Error fetching RSS feed. Retrying {RetryCount} in {DelaySeconds}s", retryCount, timeSpan.TotalSeconds);
                });
    }

    public async Task<List<ParsedRssItem>> FetchAsync(string feedUrl)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            _logger.LogInformation("Fetching RSS feed from {Url}", feedUrl);
            var feed = await FeedReader.ReadAsync(feedUrl);
            
            return feed.Items.Select(item => new ParsedRssItem(
                Title: item.Title,
                Content: item.Description ?? item.Content ?? string.Empty,
                Link: item.Link,
                PublishDate: item.PublishingDate
            )).ToList();
        });
    }
}

public record ParsedRssItem(string Title, string Content, string? Link, DateTime? PublishDate);
