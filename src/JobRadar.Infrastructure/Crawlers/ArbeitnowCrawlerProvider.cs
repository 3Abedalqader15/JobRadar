using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Crawlers;

public sealed class ArbeitnowCrawlerProvider : IJobCrawlerProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ArbeitnowCrawlerProvider> _logger;

    public string ProviderName => "Arbeitnow";

    public ArbeitnowCrawlerProvider(IHttpClientFactory httpClientFactory, ILogger<ArbeitnowCrawlerProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("JobCrawler");
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredJobDto>();

        try
        {
            _logger.LogInformation("Fetching live tech jobs from Arbeitnow API...");
            var response = await _httpClient.GetFromJsonAsync<ArbeitnowApiResponse>(
                "https://www.arbeitnow.com/api/job-board-api",
                cancellationToken);

            if (response?.Data == null || response.Data.Count == 0)
            {
                _logger.LogWarning("No jobs returned from Arbeitnow API.");
                return results;
            }

            foreach (var item in response.Data)
            {
                if (string.IsNullOrWhiteSpace(item.Url) || string.IsNullOrWhiteSpace(item.Title))
                {
                    continue;
                }

                var cleanDesc = StripHtml(item.Description ?? item.Title);
                var expLevel = InferExperienceLevel(item.Title);
                var empType = InferEmploymentType(item.JobTypes, item.Title);
                var postedDate = item.CreatedAt.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(item.CreatedAt.Value).UtcDateTime
                    : DateTime.UtcNow;

                var skills = item.Tags != null && item.Tags.Count > 0
                    ? item.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList()
                    : ExtractSkillsFromText(item.Title + " " + cleanDesc);

                results.Add(new DiscoveredJobDto(
                    Title: item.Title.Trim(),
                    CompanyName: string.IsNullOrWhiteSpace(item.CompanyName) ? "Tech Company" : item.CompanyName.Trim(),
                    Description: cleanDesc,
                    Location: string.IsNullOrWhiteSpace(item.Location) ? (item.Remote ? "Remote" : "Global") : item.Location.Trim(),
                    IsRemote: item.Remote,
                    EmploymentType: empType,
                    ExperienceLevel: expLevel,
                    SalaryMin: null,
                    SalaryMax: null,
                    SalaryCurrency: "USD",
                    ExternalApplyUrl: item.Url.Trim(),
                    PostedAt: postedDate,
                    Skills: skills,
                    ProviderName: ProviderName
                ));
            }

            _logger.LogInformation("Discovered {Count} jobs from Arbeitnow.", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crawl jobs from Arbeitnow API.");
        }

        return results;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var stripped = Regex.Replace(html, "<.*?>", " ");
        stripped = System.Net.WebUtility.HtmlDecode(stripped);
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }

    private static ExperienceLevel InferExperienceLevel(string title)
    {
        var lower = title.ToLowerInvariant();
        if (lower.Contains("lead") || lower.Contains("principal") || lower.Contains("architect") || lower.Contains("head of") || lower.Contains("director"))
            return ExperienceLevel.Lead;
        if (lower.Contains("senior") || lower.Contains("sr.") || lower.Contains("sr ") || lower.Contains("staff"))
            return ExperienceLevel.Senior;
        if (lower.Contains("junior") || lower.Contains("jr.") || lower.Contains("entry") || lower.Contains("intern") || lower.Contains("graduate"))
            return ExperienceLevel.EntryLevel;
        return ExperienceLevel.MidLevel;
    }

    private static EmploymentType InferEmploymentType(List<string>? jobTypes, string title)
    {
        if (jobTypes != null && jobTypes.Count > 0)
        {
            var combined = string.Join(" ", jobTypes).ToLowerInvariant();
            if (combined.Contains("contract") || combined.Contains("freelance")) return EmploymentType.Contract;
            if (combined.Contains("part")) return EmploymentType.PartTime;
            if (combined.Contains("intern")) return EmploymentType.Internship;
        }

        var lowerTitle = title.ToLowerInvariant();
        if (lowerTitle.Contains("contract") || lowerTitle.Contains("freelance")) return EmploymentType.Contract;
        if (lowerTitle.Contains("part-time") || lowerTitle.Contains("part time")) return EmploymentType.PartTime;
        if (lowerTitle.Contains("intern")) return EmploymentType.Internship;

        return EmploymentType.FullTime;
    }

    private static List<string> ExtractSkillsFromText(string text)
    {
        var skills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keywords = new[]
        {
            ".NET", "C#", "Angular", "React", "TypeScript", "JavaScript", "Python", "Docker", "Kubernetes",
            "PostgreSQL", "SQL", "AWS", "Azure", "GCP", "Java", "Spring", "Go", "Golang", "Rust", "Node.js",
            "Redis", "GraphQL", "DevOps", "Microservices", "Linux", "Git", "Kafka", "REST"
        };

        foreach (var kw in keywords)
        {
            var pattern = $@"\b{Regex.Escape(kw)}\b";
            if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
            {
                skills.Add(kw);
            }
        }

        return skills.Take(8).ToList();
    }

    private sealed class ArbeitnowApiResponse
    {
        [JsonPropertyName("data")]
        public List<ArbeitnowJobItem> Data { get; set; } = new();
    }

    private sealed class ArbeitnowJobItem
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("company_name")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("remote")]
        public bool Remote { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("job_types")]
        public List<string>? JobTypes { get; set; }

        [JsonPropertyName("created_at")]
        public long? CreatedAt { get; set; }
    }
}
