using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Crawlers;

public sealed class RemotiveCrawlerProvider : IJobCrawlerProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RemotiveCrawlerProvider> _logger;

    public string ProviderName => "Remotive";

    public RemotiveCrawlerProvider(IHttpClientFactory httpClientFactory, ILogger<RemotiveCrawlerProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("JobCrawler");
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredJobDto>();

        try
        {
            _logger.LogInformation("Fetching remote developer jobs from Remotive API...");
            var response = await _httpClient.GetFromJsonAsync<RemotiveApiResponse>(
                "https://remotive.com/api/remote-jobs?category=software-dev&limit=35",
                cancellationToken);

            if (response?.Jobs == null || response.Jobs.Count == 0)
            {
                _logger.LogWarning("No jobs returned from Remotive API.");
                return results;
            }

            foreach (var item in response.Jobs)
            {
                if (string.IsNullOrWhiteSpace(item.Url) || string.IsNullOrWhiteSpace(item.Title))
                {
                    continue;
                }

                var cleanDesc = StripHtml(item.Description ?? item.Title);
                var expLevel = InferExperienceLevel(item.Title);
                var empType = InferEmploymentType(item.JobType, item.Title);
                var (salMin, salMax) = ParseSalary(item.Salary);

                DateTime postedDate = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(item.PublicationDate) &&
                    DateTime.TryParse(item.PublicationDate, out var parsedDate))
                {
                    postedDate = parsedDate.ToUniversalTime();
                }

                var skills = item.Tags != null && item.Tags.Count > 0
                    ? item.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList()
                    : ExtractSkillsFromText(item.Title + " " + cleanDesc);

                results.Add(new DiscoveredJobDto(
                    Title: item.Title.Trim(),
                    CompanyName: string.IsNullOrWhiteSpace(item.CompanyName) ? "Tech Startup" : item.CompanyName.Trim(),
                    Description: cleanDesc,
                    Location: string.IsNullOrWhiteSpace(item.CandidateRequiredLocation) ? "Remote (Worldwide)" : item.CandidateRequiredLocation.Trim(),
                    IsRemote: true,
                    EmploymentType: empType,
                    ExperienceLevel: expLevel,
                    SalaryMin: salMin,
                    SalaryMax: salMax,
                    SalaryCurrency: "USD",
                    ExternalApplyUrl: item.Url.Trim(),
                    PostedAt: postedDate,
                    Skills: skills,
                    ProviderName: ProviderName
                ));
            }

            _logger.LogInformation("Discovered {Count} jobs from Remotive.", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crawl jobs from Remotive API.");
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

    private static (decimal? min, decimal? max) ParseSalary(string? salaryStr)
    {
        if (string.IsNullOrWhiteSpace(salaryStr)) return (null, null);

        try
        {
            // Match patterns like "$100k - $140k" or "$100,000 - $140,000" or "$100k"
            var matches = Regex.Matches(salaryStr, @"\$?(\d+)(?:,(\d{3}))?(?:\s*k|\s*K)?");
            if (matches.Count >= 2)
            {
                var min = ParseMatchValue(matches[0].Value);
                var max = ParseMatchValue(matches[1].Value);
                if (min > 0 && max > 0 && min <= max)
                {
                    return (min, max);
                }
            }
            else if (matches.Count == 1)
            {
                var val = ParseMatchValue(matches[0].Value);
                if (val > 0) return (val, null);
            }
        }
        catch
        {
            // Ignore parse errors on free-text salary
        }

        return (null, null);
    }

    private static decimal ParseMatchValue(string s)
    {
        var clean = s.Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Trim().ToLowerInvariant();
        if (clean.EndsWith('k'))
        {
            if (decimal.TryParse(clean[..^1], out var kVal))
            {
                return kVal * 1000m;
            }
        }
        if (decimal.TryParse(clean, out var val))
        {
            return val;
        }
        return 0m;
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

    private static EmploymentType InferEmploymentType(string? jobType, string title)
    {
        var combined = (jobType + " " + title).ToLowerInvariant();
        if (combined.Contains("contract") || combined.Contains("freelance")) return EmploymentType.Contract;
        if (combined.Contains("part_time") || combined.Contains("part-time") || combined.Contains("part time")) return EmploymentType.PartTime;
        if (combined.Contains("intern")) return EmploymentType.Internship;
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

    private sealed class RemotiveApiResponse
    {
        [JsonPropertyName("jobs")]
        public List<RemotiveJobItem> Jobs { get; set; } = new();
    }

    private sealed class RemotiveJobItem
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("company_name")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("candidate_required_location")]
        public string? CandidateRequiredLocation { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("job_type")]
        public string? JobType { get; set; }

        [JsonPropertyName("publication_date")]
        public string? PublicationDate { get; set; }

        [JsonPropertyName("salary")]
        public string? Salary { get; set; }
    }
}
