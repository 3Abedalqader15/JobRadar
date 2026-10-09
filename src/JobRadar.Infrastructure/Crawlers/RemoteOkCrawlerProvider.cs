using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Crawlers;

/// <summary>
/// World-class crawler provider for RemoteOK (https://remoteok.com/api),
/// one of the premier global remote tech job platforms.
/// </summary>
public sealed class RemoteOkCrawlerProvider : IJobCrawlerProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RemoteOkCrawlerProvider> _logger;

    public string ProviderName => "RemoteOK";

    public RemoteOkCrawlerProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<RemoteOkCrawlerProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("JobCrawler");
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredJobDto>();

        try
        {
            _logger.LogInformation("Fetching live remote tech jobs from RemoteOK API...");

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://remoteok.com/api");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("RemoteOK API responded with HTTP {StatusCode}", response.StatusCode);
                return results;
            }

            var items = await response.Content.ReadFromJsonAsync<List<RemoteOkJobItem>>(cancellationToken: cancellationToken);

            if (items == null || items.Count <= 1)
            {
                _logger.LogWarning("No job items returned from RemoteOK API.");
                return results;
            }

            // Item 0 is usually legal/disclaimer notice; actual jobs start at index 1
            foreach (var item in items.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(item.Position) || string.IsNullOrWhiteSpace(item.Company))
                {
                    continue;
                }

                var cleanTitle = item.Position.Trim();
                var cleanDesc = StripHtml(item.Description ?? cleanTitle);
                var url = !string.IsNullOrWhiteSpace(item.Url)
                    ? item.Url
                    : $"https://remoteok.com/remote-jobs/{item.Id}";

                var skills = item.Tags != null && item.Tags.Count > 0
                    ? item.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim().ToLowerInvariant()).Distinct().ToList()
                    : ExtractSkills(cleanTitle + " " + cleanDesc);

                DateTime postedAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(item.Date) && DateTime.TryParse(item.Date, out var parsedDate))
                {
                    postedAt = parsedDate.ToUniversalTime();
                }

                results.Add(new DiscoveredJobDto(
                    Title: cleanTitle,
                    CompanyName: item.Company.Trim(),
                    Description: cleanDesc,
                    Location: !string.IsNullOrWhiteSpace(item.Location) ? item.Location : "Worldwide (Remote)",
                    IsRemote: true,
                    EmploymentType: EmploymentType.FullTime,
                    ExperienceLevel: InferExperienceLevel(cleanTitle),
                    SalaryMin: item.SalaryMin > 0 ? item.SalaryMin : null,
                    SalaryMax: item.SalaryMax > 0 ? item.SalaryMax : null,
                    SalaryCurrency: "USD",
                    ExternalApplyUrl: url,
                    PostedAt: postedAt,
                    Skills: skills,
                    ProviderName: ProviderName
                ));
            }

            _logger.LogInformation("Discovered {Count} remote tech jobs from RemoteOK.", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crawl jobs from RemoteOK API.");
        }

        return results;
    }

    private static ExperienceLevel InferExperienceLevel(string title)
    {
        var lower = title.ToLowerInvariant();
        if (lower.Contains("lead") || lower.Contains("principal") || lower.Contains("architect") || lower.Contains("staff"))
            return ExperienceLevel.Lead;
        if (lower.Contains("senior") || lower.Contains("sr.") || lower.Contains("sr "))
            return ExperienceLevel.Senior;
        if (lower.Contains("junior") || lower.Contains("jr.") || lower.Contains("entry") || lower.Contains("intern"))
            return ExperienceLevel.EntryLevel;

        return ExperienceLevel.MidLevel;
    }

    private static List<string> ExtractSkills(string text)
    {
        var knownTech = new[]
        {
            "python", "react", "c#", ".net", "dotnet", "asp.net", "javascript", "typescript",
            "node.js", "angular", "vue", "golang", "go", "java", "spring", "aws", "azure",
            "docker", "kubernetes", "sql", "postgresql", "mongodb", "graphql", "devops", "rust",
            "flutter", "swift", "kotlin", "ruby", "rails", "php", "laravel", "tailwind"
        };

        var extracted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tech in knownTech)
        {
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(tech)}\b", RegexOptions.IgnoreCase))
            {
                extracted.Add(tech);
            }
        }
        return extracted.Take(6).ToList();
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var stripped = Regex.Replace(html, "<.*?>", " ");
        stripped = System.Net.WebUtility.HtmlDecode(stripped);
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }

    private sealed class RemoteOkJobItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("position")]
        public string? Position { get; set; }

        [JsonPropertyName("company")]
        public string? Company { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("date")]
        public string? Date { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("salary_min")]
        public decimal? SalaryMin { get; set; }

        [JsonPropertyName("salary_max")]
        public decimal? SalaryMax { get; set; }
    }
}
