using System.Text.RegularExpressions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Crawlers;

/// <summary>
/// Crawler provider for Bayt.com Jordan (one of the largest job boards across MENA with heavy Jordanian coverage).
/// Periodically monitors and extracts vacancies posted for Jordan.
/// </summary>
public sealed class BaytJordanCrawlerProvider : IJobCrawlerProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BaytJordanCrawlerProvider> _logger;

    public string ProviderName => "Bayt Jordan";

    public BaytJordanCrawlerProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<BaytJordanCrawlerProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("JobCrawler");
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredJobDto>();

        try
        {
            _logger.LogInformation("Crawling live jobs from Bayt.com Jordan...");

            var targetUrl = "https://www.bayt.com/en/jordan/jobs/";
            using var response = await _httpClient.GetAsync(targetUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bayt.com responded with status code {StatusCode}", response.StatusCode);
                return results;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
            {
                return results;
            }

            // Extract job cards matching Bayt's URL patterns
            // Typical Bayt job links: href="/en/jordan/jobs/...-12345/" or "https://www.bayt.com/en/jordan/jobs/..."
            var jobRegex = new Regex(
                @"<a[^>]*href=[""'](?<url>/en/jordan/jobs/[^""']+)[""'][^>]*>(?<title>[^<]+)</a>",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var matches = jobRegex.Matches(html);
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in matches)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var relUrl = match.Groups["url"].Value.Trim();
                var rawTitle = match.Groups["title"].Value.Trim();

                if (string.IsNullOrWhiteSpace(rawTitle) || rawTitle.Length < 3) continue;
                if (rawTitle.Equals("view all", StringComparison.OrdinalIgnoreCase) ||
                    rawTitle.Equals("apply now", StringComparison.OrdinalIgnoreCase) ||
                    rawTitle.StartsWith("page", StringComparison.OrdinalIgnoreCase)) continue;

                var fullUrl = relUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? relUrl
                    : $"https://www.bayt.com{relUrl}";

                if (!seenUrls.Add(fullUrl)) continue;

                var expLevel = InferExperienceLevel(rawTitle);
                var empType = InferEmploymentType(rawTitle);
                var isRemote = rawTitle.Contains("remote", StringComparison.OrdinalIgnoreCase) ||
                               rawTitle.Contains("عمل عن بعد", StringComparison.OrdinalIgnoreCase);

                var skills = ExtractSkills(rawTitle);

                results.Add(new DiscoveredJobDto(
                    Title: System.Net.WebUtility.HtmlDecode(rawTitle),
                    CompanyName: "Bayt.com Verified Employer",
                    Description: $"{rawTitle} in Jordan listed on Bayt.com. Follow the application link for full criteria and direct submission.",
                    Location: "Amman, Jordan",
                    IsRemote: isRemote,
                    EmploymentType: empType,
                    ExperienceLevel: expLevel,
                    SalaryMin: null,
                    SalaryMax: null,
                    SalaryCurrency: "JOD",
                    ExternalApplyUrl: fullUrl,
                    PostedAt: DateTime.UtcNow,
                    Skills: skills,
                    ProviderName: ProviderName
                ));

                if (results.Count >= 25) break; // Keep batch balanced
            }

            _logger.LogInformation("Discovered {Count} jobs from Bayt Jordan.", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crawl jobs from Bayt.com Jordan.");
        }

        return results;
    }

    private static ExperienceLevel InferExperienceLevel(string title)
    {
        var lower = title.ToLowerInvariant();
        if (lower.Contains("director") || lower.Contains("vp") || lower.Contains("executive"))
            return ExperienceLevel.Executive;
        if (lower.Contains("lead") || lower.Contains("manager") || lower.Contains("supervisor") || lower.Contains("architect"))
            return ExperienceLevel.Lead;
        if (lower.Contains("senior") || lower.Contains("sr.") || lower.Contains("خبير"))
            return ExperienceLevel.Senior;
        if (lower.Contains("junior") || lower.Contains("entry") || lower.Contains("intern") || lower.Contains("متدرب"))
            return ExperienceLevel.EntryLevel;
        return ExperienceLevel.MidLevel;
    }

    private static EmploymentType InferEmploymentType(string title)
    {
        var lower = title.ToLowerInvariant();
        if (lower.Contains("part time") || lower.Contains("part-time")) return EmploymentType.PartTime;
        if (lower.Contains("contract") || lower.Contains("freelance")) return EmploymentType.Contract;
        if (lower.Contains("intern") || lower.Contains("internship")) return EmploymentType.Internship;
        return EmploymentType.FullTime;
    }

    private static List<string> ExtractSkills(string text)
    {
        var skills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keywords = new[]
        {
            "Software", "Frontend", "Backend", "Full Stack", "Mobile", "QA", "Sales", "Marketing",
            "Accountant", "Finance", "HR", "Customer Service", "Civil Engineer", "Mechanical Engineer",
            "Supply Chain", "Data Analyst"
        };

        foreach (var kw in keywords)
        {
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(kw)}\b", RegexOptions.IgnoreCase))
            {
                skills.Add(kw);
            }
        }

        return skills.ToList();
    }
}
