using System.Net.Http;
using System.Text.RegularExpressions;
using JobRadar.Application.Abstractions;
using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Fetchers;

/// <summary>
/// Source fetcher capable of crawling and reading company career portals and vacancy announcement web pages
/// (e.g. Arab Bank, Zain, Orange, Umniah, KHCC, Universities, and Government Portals).
/// Extracts the raw vacancy text and links into RawPosts to be processed and structured by Gemini Flash LLM.
/// </summary>
internal sealed class CompanyCareersPageFetcher : ISourceFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CompanyCareersPageFetcher> _logger;

    public CompanyCareersPageFetcher(
        IHttpClientFactory httpClientFactory,
        ILogger<CompanyCareersPageFetcher> logger)
    {
        _httpClient = httpClientFactory.CreateClient("JobCrawler");
        _logger = logger;
    }

    public bool CanHandle(SourceType type) =>
        type == SourceType.CompanyCareersPage || type == SourceType.LinkedIn;

    public async Task<IReadOnlyList<FetchedPostInfo>> FetchPostsAsync(Source source, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching company careers page: {Name} ({Url})", source.Name, source.Url);

        var results = new List<FetchedPostInfo>();

        if (string.IsNullOrWhiteSpace(source.Url) ||
            !Uri.TryCreate(source.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _logger.LogWarning("Invalid or unsupported URL for source {Name} ({Id}): '{Url}'. Skipping.", source.Name, source.Id, source.Url);
            return results;
        }

        try
        {
            using var response = await _httpClient.GetAsync(source.Url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Careers page {Url} returned status code {StatusCode}", source.Url, response.StatusCode);
                return results;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
            {
                return results;
            }

            // Clean the HTML to extract human-readable text and clean links
            var cleanText = ExtractMeaningfulContent(html, source.Name);
            if (string.IsNullOrWhiteSpace(cleanText) || cleanText.Length < 30)
            {
                _logger.LogInformation("No significant job content extracted from {Name}", source.Name);
                return results;
            }

            // Use the SHA256 or hash of the cleaned text as SyncIdentifier to detect if content actually changed
            var contentHash = ComputeSha256(cleanText);

            // If the content hasn't changed since last fetch, we don't need to re-feed identical text to the LLM
            if (!string.IsNullOrEmpty(source.LastSyncIdentifier) && source.LastSyncIdentifier == contentHash)
            {
                _logger.LogInformation("Careers page {Name} has not changed since last crawl.", source.Name);
                return results;
            }

            // Create a post with the cleaned content and links
            results.Add(new FetchedPostInfo(
                RawUrl: source.Url,
                RawContent: cleanText,
                SyncIdentifier: contentHash,
                FetchedAt: DateTime.UtcNow
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch careers page for source {Name} ({Url})", source.Name, source.Url);
            throw; // Let IngestionHandler track failure count
        }

        return results;
    }

    private static string ExtractMeaningfulContent(string html, string sourceName)
    {
        // 1. Remove script, style, SVG, and noscript tags
        var cleaned = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", " ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<style[^>]*>[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<svg[^>]*>[\s\S]*?</svg>", " ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<noscript[^>]*>[\s\S]*?</noscript>", " ", RegexOptions.IgnoreCase);

        // 2. Replace breaks and block tags with newlines
        cleaned = Regex.Replace(cleaned, @"<(?:br|/p|/div|/li|/h[1-6]|/tr)[^>]*>", "\n", RegexOptions.IgnoreCase);

        // 3. Strip all remaining HTML tags
        cleaned = Regex.Replace(cleaned, @"<.*?>", " ");
        cleaned = System.Net.WebUtility.HtmlDecode(cleaned);

        // 4. Clean extra whitespaces
        var lines = cleaned.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 2 && !IsBoilerplateLine(l))
            .Take(120); // Capture top relevant content without overflowing LLM context

        var joined = string.Join("\n", lines);
        return $"Source: {sourceName}\nCareers Web Page Content:\n" + joined;
    }

    private static bool IsBoilerplateLine(string line)
    {
        var lower = line.ToLowerInvariant();
        return lower.Contains("cookie policy") ||
               lower.Contains("all rights reserved") ||
               lower.Contains("privacy policy") ||
               lower.Contains("terms of use") ||
               lower.Contains("copyright ©") ||
               lower.Contains("javascript must be enabled");
    }

    private static string ComputeSha256(string rawData)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
