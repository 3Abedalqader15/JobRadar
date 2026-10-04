using System.Text.RegularExpressions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Crawlers;

/// <summary>
/// Crawler provider for Akhtaboot (Jordan's premier local career network headquartered in Amman).
/// Periodically monitors and extracts newly posted jobs in Jordan/Amman.
/// </summary>
public sealed class AkhtabootCrawlerProvider : IJobCrawlerProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AkhtabootCrawlerProvider> _logger;

    public string ProviderName => "Akhtaboot Jordan";

    public AkhtabootCrawlerProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<AkhtabootCrawlerProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("JobCrawler");
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredJobDto>();

        try
        {
            _logger.LogInformation("Crawling live jobs from Akhtaboot Jordan (Amman)...");

            var targetUrl = "https://www.akhtaboot.com/en/jordan/jobs/amman";
            using var response = await _httpClient.GetAsync(targetUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Akhtaboot responded with status code {StatusCode}", response.StatusCode);
                return results;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(html))
            {
                return results;
            }

            // Extract job cards matching Akhtaboot's HTML patterns
            // Akhtaboot typically features links like: href="/en/jordan/jobs/amman/12345-job-title" or title links
            var jobLinkRegex = new Regex(
                @"<a[^>]*href=[""'](?<url>/en/jordan/jobs/[^""']+)[""'][^>]*>(?<title>[^<]+)</a>",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var matches = jobLinkRegex.Matches(html);
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in matches)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var relUrl = match.Groups["url"].Value.Trim();
                var rawTitle = match.Groups["title"].Value.Trim();

                if (string.IsNullOrWhiteSpace(rawTitle) || rawTitle.Length < 3) continue;
                if (rawTitle.Equals("view all", StringComparison.OrdinalIgnoreCase) ||
                    rawTitle.Equals("more", StringComparison.OrdinalIgnoreCase)) continue;

                var fullUrl = relUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? relUrl
                    : $"https://www.akhtaboot.com{relUrl}";

                if (!seenUrls.Add(fullUrl)) continue;

                // Infer metadata from title
                var expLevel = InferExperienceLevel(rawTitle);
                var empType = InferEmploymentType(rawTitle);
                var isRemote = rawTitle.Contains("remote", StringComparison.OrdinalIgnoreCase) ||
                               rawTitle.Contains("عن بعد", StringComparison.OrdinalIgnoreCase);

                var skills = ExtractSkills(rawTitle);

                results.Add(new DiscoveredJobDto(
                    Title: System.Net.WebUtility.HtmlDecode(rawTitle),
                    CompanyName: "Confidential / Akhtaboot Employer",
                    Description: $"{rawTitle} posted on Akhtaboot Jordan. Visit link for full requirements and immediate application.",
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

                if (results.Count >= 25) break; // Limit batch size per crawl cycle
            }

            _logger.LogInformation("Discovered {Count} jobs from Akhtaboot Jordan.", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crawl jobs from Akhtaboot Jordan.");
        }

        return results;
    }

    private static ExperienceLevel InferExperienceLevel(string title)
    {
        var lower = title.ToLowerInvariant();
        if (lower.Contains("lead") || lower.Contains("manager") || lower.Contains("head of") || lower.Contains("director"))
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
            "Developer", "Engineer", "Accountant", "Sales", "Marketing", "HR", "Nurse",
            ".NET", "Java", "Python", "React", "Angular", "SQL", "Graphic Design", "Project Manager"
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
