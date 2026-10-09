using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Crawlers;

/// <summary>
/// World-class Remote Tech Job Crawler leveraging Jobicy's official Remote Jobs API.
/// Supplies verified engineering, DevOps, frontend, and backend vacancies with realistic salaries and global remote eligibility.
/// </summary>
public sealed class JobicyCrawlerProvider : IJobCrawlerProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JobicyCrawlerProvider> _logger;

    public string ProviderName => "Jobicy Remote";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public JobicyCrawlerProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<JobicyCrawlerProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("JobCrawler");
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredJobDto>();

        try
        {
            _logger.LogInformation("Crawling live remote engineering jobs from Jobicy API...");

            var endpoint = "https://jobicy.com/api/v2/remote-jobs?count=50&industry=dev";
            using var response = await _httpClient.GetAsync(endpoint, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Jobicy API returned non-success code {StatusCode}", response.StatusCode);
                return results;
            }

            var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var apiResponse = await JsonSerializer.DeserializeAsync<JobicyResponseDto>(jsonStream, JsonOptions, cancellationToken);

            if (apiResponse?.Jobs == null || apiResponse.Jobs.Count == 0)
            {
                _logger.LogInformation("No jobs returned by Jobicy API.");
                return results;
            }

            foreach (var item in apiResponse.Jobs)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (string.IsNullOrWhiteSpace(item.JobTitle) || string.IsNullOrWhiteSpace(item.Url)) continue;

                var cleanDesc = StripHtml(item.JobDescription ?? item.JobTitle);
                var expLevel = ParseExperienceLevel(item.JobLevel, item.JobTitle);
                var empType = ParseEmploymentType(item.JobType, item.JobTitle);

                var location = string.IsNullOrWhiteSpace(item.JobGeo) ? "Remote (Worldwide)" : $"Remote ({item.JobGeo})";
                var postedDate = DateTime.TryParse(item.PubDate, out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow;

                var skills = ExtractSkills(item.JobTitle + " " + cleanDesc);

                results.Add(new DiscoveredJobDto(
                    Title: item.JobTitle.Trim(),
                    CompanyName: string.IsNullOrWhiteSpace(item.CompanyName) ? "Remote Tech Company" : item.CompanyName.Trim(),
                    Description: cleanDesc,
                    Location: location,
                    IsRemote: true,
                    EmploymentType: empType,
                    ExperienceLevel: expLevel,
                    SalaryMin: item.SalaryMin,
                    SalaryMax: item.SalaryMax,
                    SalaryCurrency: string.IsNullOrWhiteSpace(item.SalaryCurrency) ? "USD" : item.SalaryCurrency,
                    ExternalApplyUrl: item.Url.Trim(),
                    PostedAt: postedDate,
                    Skills: skills,
                    ProviderName: ProviderName
                ));
            }

            _logger.LogInformation("Discovered {Count} remote software jobs from Jobicy.", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crawl jobs from Jobicy API.");
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

    private static ExperienceLevel ParseExperienceLevel(string? levelStr, string title)
    {
        var combined = $"{levelStr} {title}".ToLowerInvariant();
        if (combined.Contains("lead") || combined.Contains("director") || combined.Contains("architect") || combined.Contains("principal"))
            return ExperienceLevel.Lead;
        if (combined.Contains("senior") || combined.Contains("sr.") || combined.Contains("staff"))
            return ExperienceLevel.Senior;
        if (combined.Contains("junior") || combined.Contains("entry") || combined.Contains("intern") || combined.Contains("associate"))
            return ExperienceLevel.EntryLevel;
        return ExperienceLevel.MidLevel;
    }

    private static EmploymentType ParseEmploymentType(object? jobType, string title)
    {
        var typeStr = jobType?.ToString()?.ToLowerInvariant() ?? title.ToLowerInvariant();
        if (typeStr.Contains("contract") || typeStr.Contains("freelance"))
            return EmploymentType.Contract;
        if (typeStr.Contains("part-time") || typeStr.Contains("part_time"))
            return EmploymentType.PartTime;
        if (typeStr.Contains("intern"))
            return EmploymentType.Internship;
        return EmploymentType.FullTime;
    }

    private static List<string> ExtractSkills(string text)
    {
        var skills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keywords = new[]
        {
            ".NET", "C#", "Angular", "React", "TypeScript", "JavaScript", "Python", "Docker", "Kubernetes",
            "PostgreSQL", "SQL", "AWS", "Azure", "GCP", "Java", "Spring", "Go", "Golang", "Rust", "Node.js",
            "Redis", "GraphQL", "DevOps", "Microservices", "Linux", "Git", "Kafka", "REST", "Vue", "Tailwind"
        };

        foreach (var kw in keywords)
        {
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(kw)}\b", RegexOptions.IgnoreCase))
            {
                skills.Add(kw);
            }
        }

        return skills.Take(8).ToList();
    }

    private sealed class JobicyResponseDto
    {
        [JsonPropertyName("jobs")]
        public List<JobicyJobItemDto>? Jobs { get; set; }
    }

    private sealed class JobicyJobItemDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("jobTitle")]
        public string? JobTitle { get; set; }

        [JsonPropertyName("companyName")]
        public string? CompanyName { get; set; }

        [JsonPropertyName("jobDescription")]
        public string? JobDescription { get; set; }

        [JsonPropertyName("jobGeo")]
        public string? JobGeo { get; set; }

        [JsonPropertyName("jobLevel")]
        public string? JobLevel { get; set; }

        [JsonPropertyName("jobType")]
        public JsonElement? JobType { get; set; }

        [JsonPropertyName("pubDate")]
        public string? PubDate { get; set; }

        [JsonPropertyName("salaryMin")]
        public decimal? SalaryMin { get; set; }

        [JsonPropertyName("salaryMax")]
        public decimal? SalaryMax { get; set; }

        [JsonPropertyName("salaryCurrency")]
        public string? SalaryCurrency { get; set; }
    }
}
