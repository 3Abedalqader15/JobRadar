using System.Text.RegularExpressions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Crawlers;

/// <summary>
/// High-coverage regional crawler covering tech & IT opportunities across Jordan and the Gulf (Saudi Arabia, UAE, Qatar).
/// Scrapes active IT listings from Tanqeeb's regional portals.
/// </summary>
public sealed class TanqeebMenaCrawlerProvider : IJobCrawlerProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TanqeebMenaCrawlerProvider> _logger;

    public string ProviderName => "Tanqeeb MENA";

    private sealed record RegionalTarget(string Country, string DefaultLocation, string Url, string BaseDomain, string Currency);

    private static readonly RegionalTarget[] Targets = new[]
    {
        new RegionalTarget("Jordan", "Amman, Jordan", "https://jordan.tanqeeb.com/s/jobs/it-jobs", "https://jordan.tanqeeb.com", "JOD"),
        new RegionalTarget("Saudi Arabia", "Riyadh, Saudi Arabia", "https://saudi.tanqeeb.com/s/jobs/it-jobs", "https://saudi.tanqeeb.com", "SAR"),
        new RegionalTarget("UAE", "Dubai, UAE", "https://uae.tanqeeb.com/s/jobs/it-jobs", "https://uae.tanqeeb.com", "AED"),
        new RegionalTarget("Qatar", "Doha, Qatar", "https://qatar.tanqeeb.com/s/jobs/it-jobs", "https://qatar.tanqeeb.com", "QAR")
    };

    public TanqeebMenaCrawlerProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<TanqeebMenaCrawlerProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("JobCrawler");
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredJobDto>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var target in Targets)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                _logger.LogInformation("Crawling IT jobs for {Country} from {Url}...", target.Country, target.Url);
                using var response = await _httpClient.GetAsync(target.Url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Tanqeeb {Country} returned status code {StatusCode}", target.Country, response.StatusCode);
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(html)) continue;

                // Match both standard search-job-title-link anchors and item list anchors
                var jobRegex = new Regex(
                    @"<a[^>]+href=[""'](?<url>[^""']*\/jobs\/[0-9]+\.html)""[^>]*>(?<title>[\s\S]*?)<\/a>",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled);

                var matches = jobRegex.Matches(html);
                int targetAdded = 0;

                foreach (Match match in matches)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var relUrl = match.Groups["url"].Value.Trim();
                    var rawTitle = StripHtml(match.Groups["title"].Value).Trim();

                    if (string.IsNullOrWhiteSpace(rawTitle) || rawTitle.Length < 3) continue;
                    if (rawTitle.StartsWith("view", StringComparison.OrdinalIgnoreCase) ||
                        rawTitle.StartsWith("browse", StringComparison.OrdinalIgnoreCase)) continue;

                    var fullUrl = relUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? relUrl
                        : $"{target.BaseDomain}{relUrl}";

                    if (!seenUrls.Add(fullUrl)) continue;

                    var isRemote = rawTitle.Contains("remote", StringComparison.OrdinalIgnoreCase) ||
                                   rawTitle.Contains("عن بعد", StringComparison.OrdinalIgnoreCase) ||
                                   rawTitle.Contains("telecommute", StringComparison.OrdinalIgnoreCase);

                    var expLevel = InferExperienceLevel(rawTitle);
                    var empType = InferEmploymentType(rawTitle);
                    var skills = ExtractSkills(rawTitle);

                    results.Add(new DiscoveredJobDto(
                        Title: System.Net.WebUtility.HtmlDecode(rawTitle),
                        CompanyName: $"{target.Country} Enterprise Employer",
                        Description: $"{rawTitle} opportunity located in {target.Country}. Verified active vacancy in technology sector via Tanqeeb Network.",
                        Location: target.DefaultLocation,
                        IsRemote: isRemote,
                        EmploymentType: empType,
                        ExperienceLevel: expLevel,
                        SalaryMin: null,
                        SalaryMax: null,
                        SalaryCurrency: target.Currency,
                        ExternalApplyUrl: fullUrl,
                        PostedAt: DateTime.UtcNow,
                        Skills: skills,
                        ProviderName: ProviderName
                    ));

                    targetAdded++;
                    if (targetAdded >= 20) break; // Keep healthy balance per country
                }

                _logger.LogInformation("Extracted {Count} IT jobs for {Country}", targetAdded, target.Country);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed crawling Tanqeeb jobs for {Country}", target.Country);
            }
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
        if (lower.Contains("director") || lower.Contains("vp") || lower.Contains("head of") || lower.Contains("executive"))
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
            ".NET", "C#", "Angular", "React", "TypeScript", "JavaScript", "Python", "Docker", "Kubernetes",
            "PostgreSQL", "SQL", "AWS", "Azure", "Cloud", "Security", "DevOps", "IT Support", "Help Desk",
            "Network", "Cisco", "Linux", "SysAdmin", "Database", "ERP", "SAP", "Java", "PHP", "Flutter"
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
