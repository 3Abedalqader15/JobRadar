namespace JobRadar.Infrastructure.Crawlers.Politeness;

public interface IPolitenessEngine
{
    /// <summary>
    /// Checks whether the specified URL is permitted by robots.txt for the configured bot user-agent.
    /// </summary>
    Task<bool> IsAllowedAsync(Uri uri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an HTTP GET request respecting robots.txt permissions, host-level single concurrency,
    /// crawl-delay rate limiting, exponential backoff on 429/503/5xx, and conditional GET headers.
    /// </summary>
    Task<PolitenessResponse> GetAsync(
        Uri uri,
        string? etag = null,
        DateTimeOffset? ifModifiedSince = null,
        CancellationToken cancellationToken = default);
}

public sealed record PolitenessResponse(
    bool IsAllowed,
    bool NotModified,
    int StatusCode,
    string? Content,
    string? ETag,
    DateTimeOffset? LastModified,
    string? ErrorMessage = null
);
