using System.Xml;
using CodeHollow.FeedReader;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Fetchers;

internal sealed class RssFetcher : ISourceFetcher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RssFetcher> _logger;

    public RssFetcher(IHttpClientFactory httpClientFactory, ILogger<RssFetcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool CanHandle(SourceType type) => type == SourceType.RssFeed;

    public async Task<IReadOnlyList<FetchedPostInfo>> FetchPostsAsync(Source source, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching RSS feed {Url}", source.Url);
        var results = new List<FetchedPostInfo>();

        try
        {
            var client = _httpClientFactory.CreateClient("JobCrawler");
            using var response = await client.GetAsync(source.Url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RSS feed {Url} returned HTTP status {StatusCode}. Skipping.", source.Url, response.StatusCode);
                return results;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("RSS feed {Url} returned empty content.", source.Url);
                return results;
            }

            var trimmed = content.TrimStart();
            if (trimmed.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("RSS feed {Url} returned HTML document instead of XML. Skipping.", source.Url);
                return results;
            }

            Feed feed;
            try
            {
                feed = FeedReader.ReadFromString(content);
            }
            catch (XmlException ex)
            {
                _logger.LogWarning(ex, "Failed to parse XML from RSS feed {Url}. Skipping.", source.Url);
                return results;
            }

            foreach (var item in feed.Items)
            {
                var rawContent = $"Title: {item.Title}\nLink: {item.Link}\nDescription: {item.Description}\nContent: {item.Content}";
                var syncId = !string.IsNullOrEmpty(item.Id) ? item.Id : item.Link;
                var fetchDate = item.PublishingDate ?? DateTime.UtcNow;

                results.Add(new FetchedPostInfo(item.Link ?? source.Url, rawContent, syncId, fetchDate));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error occurred while fetching RSS feed {Url}", source.Url);
        }

        return results;
    }
}
