using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Crawlers;

/// <summary>
/// Dedicated job crawler for Bank of Jordan leveraging the public Workable recruitment API.
/// Fetches all active vacancies at Bank of Jordan and maps them into structured DiscoveredJobDto objects.
/// </summary>
public sealed class BankOfJordanCrawlerProvider : IJobCrawlerProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BankOfJordanCrawlerProvider> _logger;

    public string ProviderName => "Bank of Jordan";

    public BankOfJordanCrawlerProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<BankOfJordanCrawlerProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("JobCrawler");
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredJobDto>();

        try
        {
            _logger.LogInformation("Fetching live job vacancies for Bank of Jordan from Workable API...");

            // Workable API v3 endpoint for account job listings
            var requestBody = new { query = "" };
            var response = await _httpClient.PostAsJsonAsync(
                "https://apply.workable.com/api/v3/accounts/bank-of-jordan/jobs",
                requestBody,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<WorkableJobsResponse>(cancellationToken: cancellationToken);
                if (data?.Results != null && data.Results.Count > 0)
                {
                    foreach (var item in data.Results)
                    {
                        if (string.IsNullOrWhiteSpace(item.Shortcode) || string.IsNullOrWhiteSpace(item.Title))
                        {
                            continue;
                        }

                        var applyUrl = $"https://apply.workable.com/bank-of-jordan/j/{item.Shortcode}/";
                        var location = !string.IsNullOrWhiteSpace(item.City)
                            ? $"{item.City.Trim()}, {item.Country ?? "Jordan"}"
                            : "Amman, Jordan";

                        var description = !string.IsNullOrWhiteSpace(item.Description)
                            ? StripHtml(item.Description)
                            : $"{item.Title} opportunity at Bank of Jordan.";

                        var expLevel = InferExperienceLevel(item.Title + " " + description);
                        var empType = InferEmploymentType(item.EmploymentType, item.Title);
                        var postedDate = item.Published.HasValue
                            ? item.Published.Value.UtcDateTime
                            : DateTime.UtcNow;

                        var skills = ExtractSkills(item.Title + " " + description);

                        results.Add(new DiscoveredJobDto(
                            Title: item.Title.Trim(),
                            CompanyName: "Bank of Jordan",
                            Description: description,
                            Location: location,
                            IsRemote: item.Telecommuting ?? false,
                            EmploymentType: empType,
                            ExperienceLevel: expLevel,
                            SalaryMin: null,
                            SalaryMax: null,
                            SalaryCurrency: "JOD",
                            ExternalApplyUrl: applyUrl,
                            PostedAt: postedDate,
                            Skills: skills,
                            ProviderName: ProviderName
                        ));
                    }
                }
            }
            else
            {
                _logger.LogWarning("Workable API returned HTTP status {StatusCode}. Attempting fallback widget endpoint.", response.StatusCode);
                
                // Fallback: Workable Widget v1 API
                var fallbackResponse = await _httpClient.GetAsync(
                    "https://apply.workable.com/api/v1/widget/accounts/bank-of-jordan",
                    cancellationToken);

                if (fallbackResponse.IsSuccessStatusCode)
                {
                    var widgetData = await fallbackResponse.Content.ReadFromJsonAsync<WorkableWidgetResponse>(cancellationToken: cancellationToken);
                    if (widgetData?.Jobs != null)
                    {
                        foreach (var j in widgetData.Jobs)
                        {
                            if (string.IsNullOrWhiteSpace(j.Shortcode) || string.IsNullOrWhiteSpace(j.Title)) continue;

                            results.Add(new DiscoveredJobDto(
                                Title: j.Title.Trim(),
                                CompanyName: "Bank of Jordan",
                                Description: $"{j.Title} vacancy at Bank of Jordan, {j.City ?? "Amman"}, Jordan.",
                                Location: $"{j.City ?? "Amman"}, {j.Country ?? "Jordan"}",
                                IsRemote: j.Telecommuting ?? false,
                                EmploymentType: EmploymentType.FullTime,
                                ExperienceLevel: InferExperienceLevel(j.Title),
                                SalaryMin: null,
                                SalaryMax: null,
                                SalaryCurrency: "JOD",
                                ExternalApplyUrl: $"https://apply.workable.com/bank-of-jordan/j/{j.Shortcode}/",
                                PostedAt: j.Published ?? DateTime.UtcNow,
                                Skills: ExtractSkills(j.Title),
                                ProviderName: ProviderName
                            ));
                        }
                    }
                }
            }

            _logger.LogInformation("Discovered {Count} active jobs from Bank of Jordan.", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crawl jobs for Bank of Jordan.");
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

    private static ExperienceLevel InferExperienceLevel(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("head of") || lower.Contains("director") || lower.Contains("chief") || lower.Contains("vp"))
            return ExperienceLevel.Executive;
        if (lower.Contains("lead") || lower.Contains("manager") || lower.Contains("supervisor") || lower.Contains("architect"))
            return ExperienceLevel.Lead;
        if (lower.Contains("senior") || lower.Contains("sr.") || lower.Contains("خبير") || lower.Contains("رئيسي"))
            return ExperienceLevel.Senior;
        if (lower.Contains("junior") || lower.Contains("entry") || lower.Contains("intern") || lower.Contains("graduate") || lower.Contains("مبتدئ") || lower.Contains("متدرب"))
            return ExperienceLevel.EntryLevel;
        return ExperienceLevel.MidLevel;
    }

    private static EmploymentType InferEmploymentType(string? type, string title)
    {
        var combined = $"{type} {title}".ToLowerInvariant();
        if (combined.Contains("contract") || combined.Contains("عقد")) return EmploymentType.Contract;
        if (combined.Contains("part") || combined.Contains("جزئي")) return EmploymentType.PartTime;
        if (combined.Contains("intern") || combined.Contains("تدريب")) return EmploymentType.Internship;
        return EmploymentType.FullTime;
    }

    private static List<string> ExtractSkills(string text)
    {
        var skills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bankingKeywords = new[]
        {
            "Banking", "Credit Analysis", "Risk Management", "Compliance", "AML", "Retail Banking",
            "Corporate Banking", "Treasury", "Financial Analysis", "Accounting", "Audit",
            "C#", ".NET", "SQL", "Database", "Oracle", "Python", "Cyber Security", "DevOps",
            "Business Analysis", "Project Management", "Customer Service", "Customer Support"
        };

        foreach (var kw in bankingKeywords)
        {
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(kw)}\b", RegexOptions.IgnoreCase))
            {
                skills.Add(kw);
            }
        }

        return skills.Take(6).ToList();
    }

    private sealed class WorkableJobsResponse
    {
        [JsonPropertyName("results")]
        public List<WorkableJobItem> Results { get; set; } = new();
    }

    private sealed class WorkableJobItem
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("shortcode")]
        public string? Shortcode { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("telecommuting")]
        public bool? Telecommuting { get; set; }

        [JsonPropertyName("employment_type")]
        public string? EmploymentType { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("published")]
        public DateTimeOffset? Published { get; set; }
    }

    private sealed class WorkableWidgetResponse
    {
        [JsonPropertyName("jobs")]
        public List<WorkableWidgetJobItem> Jobs { get; set; } = new();
    }

    private sealed class WorkableWidgetJobItem
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("shortcode")]
        public string? Shortcode { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("telecommuting")]
        public bool? Telecommuting { get; set; }

        [JsonPropertyName("published")]
        public DateTime? Published { get; set; }
    }
}
