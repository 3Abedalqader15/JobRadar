using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobRadar.Infrastructure.Crawlers.Politeness;

public sealed class PolitenessEngine : IPolitenessEngine
{
    private readonly HttpClient _httpClient;
    private readonly CrawlerUserAgentOptions _options;
    private readonly ILogger<PolitenessEngine> _logger;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _hostLocks = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRequestTimes = new();
    private readonly ConcurrentDictionary<string, CachedRobotsTxt> _robotsCache = new();

    private static readonly TimeSpan DefaultRobotsCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan Host5xxBackoffDuration = TimeSpan.FromHours(1);
    private const double DefaultCrawlDelaySeconds = 2.0;

    public PolitenessEngine(
        IHttpClientFactory httpClientFactory,
        IOptions<CrawlerUserAgentOptions> options,
        ILogger<PolitenessEngine> logger)
    {
        _httpClient = httpClientFactory.CreateClient("JobCrawler");
        _options = options.Value ?? new CrawlerUserAgentOptions();
        _logger = logger;
    }

    public async Task<bool> IsAllowedAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var robots = await GetOrFetchRobotsTxtAsync(uri, cancellationToken);
        return robots.IsAllowed(uri.AbsolutePath, _options.BotName);
    }

    public async Task<PolitenessResponse> GetAsync(
        Uri uri,
        string? etag = null,
        DateTimeOffset? ifModifiedSince = null,
        CancellationToken cancellationToken = default)
    {
        var host = uri.Host.ToLowerInvariant();
        var hostLock = _hostLocks.GetOrAdd(host, _ => new SemaphoreSlim(1, 1));

        await hostLock.WaitAsync(cancellationToken);
        try
        {
            var robots = await GetOrFetchRobotsTxtAsync(uri, cancellationToken);
            if (!robots.IsAllowed(uri.AbsolutePath, _options.BotName))
            {
                _logger.LogWarning("Robots.txt disallows crawling of {Url} for bot {BotName}", uri, _options.BotName);
                return new PolitenessResponse(
                    IsAllowed: false,
                    NotModified: false,
                    StatusCode: (int)HttpStatusCode.Forbidden,
                    Content: null,
                    ETag: null,
                    LastModified: null,
                    ErrorMessage: "Disallowed by robots.txt");
            }

            // Enforce crawl-delay rate limit (minimum 2.0s or robots.txt Crawl-delay)
            var requiredDelay = TimeSpan.FromSeconds(Math.Max(DefaultCrawlDelaySeconds, robots.GetCrawlDelay(_options.BotName)));
            if (_lastRequestTimes.TryGetValue(host, out var lastReq))
            {
                var elapsed = DateTimeOffset.UtcNow - lastReq;
                if (elapsed < requiredDelay)
                {
                    var delayToWait = requiredDelay - elapsed;
                    _logger.LogDebug("Enforcing crawl delay for host {Host}: waiting {DelayMs}ms", host, delayToWait.TotalMilliseconds);
                    await Task.Delay(delayToWait, cancellationToken);
                }
            }

            // Execute HTTP GET with Retry-After and 429/503 backoff (up to 2 retries)
            const int maxRetries = 2;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                ApplyUserAgent(request);

                if (!string.IsNullOrWhiteSpace(etag))
                {
                    request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
                }
                if (ifModifiedSince.HasValue)
                {
                    request.Headers.IfModifiedSince = ifModifiedSince.Value;
                }

                _lastRequestTimes[host] = DateTimeOffset.UtcNow;
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    return new PolitenessResponse(
                        IsAllowed: true,
                        NotModified: true,
                        StatusCode: (int)HttpStatusCode.NotModified,
                        Content: null,
                        ETag: response.Headers.ETag?.Tag,
                        LastModified: response.Content.Headers.LastModified);
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests || response.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    var retryAfterSeconds = 5.0 * (attempt + 1);
                    if (response.Headers.RetryAfter != null)
                    {
                        if (response.Headers.RetryAfter.Delta.HasValue)
                            retryAfterSeconds = response.Headers.RetryAfter.Delta.Value.TotalSeconds;
                        else if (response.Headers.RetryAfter.Date.HasValue)
                            retryAfterSeconds = Math.Max(1, (response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds);
                    }

                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning("Host {Host} returned {StatusCode}. Backing off for {Seconds}s before retry...", host, response.StatusCode, retryAfterSeconds);
                        await Task.Delay(TimeSpan.FromSeconds(Math.Min(retryAfterSeconds, 60)), cancellationToken);
                        continue;
                    }
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var respEtag = response.Headers.ETag?.Tag;
                var respLastMod = response.Content.Headers.LastModified;

                return new PolitenessResponse(
                    IsAllowed: true,
                    NotModified: false,
                    StatusCode: (int)response.StatusCode,
                    Content: content,
                    ETag: respEtag,
                    LastModified: respLastMod,
                    ErrorMessage: response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            return new PolitenessResponse(
                IsAllowed: true,
                NotModified: false,
                StatusCode: (int)HttpStatusCode.TooManyRequests,
                Content: null,
                ETag: null,
                LastModified: null,
                ErrorMessage: "Max retries exceeded due to rate limiting.");
        }
        finally
        {
            hostLock.Release();
        }
    }

    private void ApplyUserAgent(HttpRequestMessage request)
    {
        try
        {
            var header = _options.BuildHeaderString();
            if (!string.IsNullOrWhiteSpace(header))
            {
                request.Headers.UserAgent.Clear();
                request.Headers.UserAgent.ParseAdd(header);
            }
        }
        catch
        {
            // Fallback safe User-Agent
            request.Headers.UserAgent.Clear();
            request.Headers.UserAgent.ParseAdd("JobRadarBot/1.0 (+https://github.com/3Abedalqader15/JobRadar; contact@jobradar-crawler.local)");
        }
    }

    private async Task<CachedRobotsTxt> GetOrFetchRobotsTxtAsync(Uri uri, CancellationToken cancellationToken)
    {
        var host = uri.Host.ToLowerInvariant();
        if (_robotsCache.TryGetValue(host, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return cached;
        }

        var robotsUri = new Uri($"{uri.Scheme}://{uri.Authority}/robots.txt");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, robotsUri);
            ApplyUserAgent(req);

            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            if (resp.StatusCode == HttpStatusCode.NotFound || resp.StatusCode == HttpStatusCode.Gone)
            {
                // 404/410 means no robots.txt -> permitted (no restrictions)
                var allowedResult = CachedRobotsTxt.AllowAll(DateTimeOffset.UtcNow.Add(DefaultRobotsCacheDuration));
                _robotsCache[host] = allowedResult;
                return allowedResult;
            }

            if ((int)resp.StatusCode >= 500 && (int)resp.StatusCode <= 599)
            {
                // 5xx means robots.txt unavailable -> back off and disallow all
                _logger.LogWarning("Robots.txt for host {Host} returned 5xx ({Status}). Backing off host.", host, resp.StatusCode);
                var disallowResult = CachedRobotsTxt.DisallowAll(DateTimeOffset.UtcNow.Add(Host5xxBackoffDuration));
                _robotsCache[host] = disallowResult;
                return disallowResult;
            }

            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                var parsed = CachedRobotsTxt.Parse(body, DateTimeOffset.UtcNow.Add(DefaultRobotsCacheDuration));
                _robotsCache[host] = parsed;
                return parsed;
            }

            // Other errors -> default to allow with shorter cache
            var fallback = CachedRobotsTxt.AllowAll(DateTimeOffset.UtcNow.AddHours(1));
            _robotsCache[host] = fallback;
            return fallback;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch robots.txt for host {Host}. Defaulting to backoff for 30m.", host);
            var errorResult = CachedRobotsTxt.DisallowAll(DateTimeOffset.UtcNow.AddMinutes(30));
            _robotsCache[host] = errorResult;
            return errorResult;
        }
    }

    public sealed class CachedRobotsTxt
    {
        public DateTimeOffset ExpiresAt { get; }
        private readonly List<RuleGroup> _ruleGroups = new();

        public CachedRobotsTxt(DateTimeOffset expiresAt)
        {
            ExpiresAt = expiresAt;
        }

        public static CachedRobotsTxt AllowAll(DateTimeOffset expiresAt) => new(expiresAt);

        public static CachedRobotsTxt DisallowAll(DateTimeOffset expiresAt)
        {
            var res = new CachedRobotsTxt(expiresAt);
            var group = new RuleGroup { UserAgent = "*" };
            group.Disallow.Add("/");
            res._ruleGroups.Add(group);
            return res;
        }

        public static CachedRobotsTxt Parse(string content, DateTimeOffset expiresAt)
        {
            var result = new CachedRobotsTxt(expiresAt);
            if (string.IsNullOrWhiteSpace(content)) return result;

            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            RuleGroup? currentGroup = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                var hashIndex = line.IndexOf('#');
                if (hashIndex >= 0) line = line[..hashIndex].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var colonIndex = line.IndexOf(':');
                if (colonIndex <= 0) continue;

                var directive = line[..colonIndex].Trim().ToLowerInvariant();
                var value = line[(colonIndex + 1)..].Trim();

                if (directive == "user-agent")
                {
                    currentGroup = new RuleGroup { UserAgent = value.ToLowerInvariant() };
                    result._ruleGroups.Add(currentGroup);
                }
                else if (currentGroup != null)
                {
                    if (directive == "disallow" && !string.IsNullOrEmpty(value))
                    {
                        currentGroup.Disallow.Add(value);
                    }
                    else if (directive == "allow" && !string.IsNullOrEmpty(value))
                    {
                        currentGroup.Allow.Add(value);
                    }
                    else if (directive == "crawl-delay" && double.TryParse(value, out var delay))
                    {
                        currentGroup.CrawlDelaySeconds = delay;
                    }
                }
            }

            return result;
        }

        public bool IsAllowed(string path, string botName)
        {
            var targetAgent = botName.ToLowerInvariant();
            var matchedGroup = _ruleGroups.FirstOrDefault(g => g.UserAgent == targetAgent)
                            ?? _ruleGroups.FirstOrDefault(g => g.UserAgent == "*");

            if (matchedGroup == null) return true;

            // Check most specific match
            string? matchedAllow = matchedGroup.Allow.Where(a => path.StartsWith(a, StringComparison.OrdinalIgnoreCase)).OrderByDescending(a => a.Length).FirstOrDefault();
            string? matchedDisallow = matchedGroup.Disallow.Where(d => path.StartsWith(d, StringComparison.OrdinalIgnoreCase)).OrderByDescending(d => d.Length).FirstOrDefault();

            if (matchedDisallow == null) return true;
            if (matchedAllow != null && matchedAllow.Length >= matchedDisallow.Length) return true;

            return false;
        }

        public double GetCrawlDelay(string botName)
        {
            var targetAgent = botName.ToLowerInvariant();
            var matchedGroup = _ruleGroups.FirstOrDefault(g => g.UserAgent == targetAgent)
                            ?? _ruleGroups.FirstOrDefault(g => g.UserAgent == "*");

            return matchedGroup?.CrawlDelaySeconds ?? DefaultCrawlDelaySeconds;
        }

        private sealed class RuleGroup
        {
            public string UserAgent { get; set; } = string.Empty;
            public List<string> Disallow { get; } = new();
            public List<string> Allow { get; } = new();
            public double? CrawlDelaySeconds { get; set; }
        }
    }
}
