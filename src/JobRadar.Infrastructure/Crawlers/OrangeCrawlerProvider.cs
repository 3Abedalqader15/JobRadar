using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Common;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using JobRadar.Infrastructure.Crawlers.Politeness;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Crawlers;

/// <summary>
/// Two-stage crawler for Orange Jordan career portal (jobs.orange.jo).
/// Uses IPolitenessEngine for robots.txt evaluation, single-concurrency throttling,
/// crawl delay (minimum 2.0s), and User-Agent identification.
/// Operates in DRY-RUN mode by default to log discovered vacancies without database insertion.
/// </summary>
public sealed class OrangeCrawlerProvider : IJobCrawlerProvider
{
    private readonly IPolitenessEngine _politenessEngine;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrangeCrawlerProvider> _logger;

    public string ProviderName => "Orange Jordan";

    private const string BaseUrl = "https://jobs.orange.jo";
    private const string SearchUrl = "https://jobs.orange.jo/search";

    // Regexes for search/listing page enumeration
    private static readonly Regex DetailLinkRegex = new(
        @"href=[""'](?<url>/jobpost/detail/(?<id>\d+))[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Regexes for detail page extraction
    private static readonly Regex TitleRegex = new(
        @"<h1[^>]*class=[""'][^""']*headline[^""']*[""][^>]*>[\s\S]*?<span[^>]*class=[""'][^""']*orange-txt[^""']*[""][^>]*>(?<title>[^<]+)</span>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TitleFallbackRegex = new(
        @"<h1[^>]*class=[""'][^""']*headline[^""']*[""][^>]*>(?<title>[^<]+)</h1>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CollapseOneRegex = new(
        @"id=[""']collapseOne[""'][\s\S]*?<div[^>]*class=[""'][^""']*card-body[^""']*[""][^>]*>(?<content>[\s\S]*?)</div>\s*</div>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CollapseTwoRegex = new(
        @"id=[""']collapseTwo[""'][\s\S]*?<div[^>]*class=[""'][^""']*card-body[^""']*[""][^>]*>(?<content>[\s\S]*?)</div>\s*</div>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CollapseThreeRegex = new(
        @"id=[""']collapseThree[""'][\s\S]*?<div[^>]*class=[""'][^""']*card-body[^""']*[""][^>]*>(?<content>[\s\S]*?)</div>\s*</div>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ApplyButtonRegex = new(
        @"class=[""'][^""']*job-post-apply[^""']*[""][\s\S]*?<a[^>]*href=[""'](?<url>[^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public OrangeCrawlerProvider(
        IPolitenessEngine politenessEngine,
        IConfiguration configuration,
        ILogger<OrangeCrawlerProvider> logger)
    {
        _politenessEngine = politenessEngine;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default)
    {
        var dryRun = _configuration.GetValue<bool>("Crawler:DryRun", true);
        var results = new List<DiscoveredJobDto>();

        try
        {
            _logger.LogInformation(
                "Starting Orange Jordan crawl ({Mode}) via PolitenessEngine...",
                dryRun ? "DRY-RUN ONLY" : "ACTIVE INGESTION");

            // Stage 1: Enumerate vacancy links from search page
            var searchUri = new Uri(SearchUrl);
            var searchResponse = await _politenessEngine.GetAsync(searchUri, cancellationToken: cancellationToken);

            if (!searchResponse.IsAllowed)
            {
                _logger.LogWarning("Orange Jordan search page disallowed by robots.txt: {Url}", SearchUrl);
                return results;
            }

            if (searchResponse.StatusCode != 200 || string.IsNullOrWhiteSpace(searchResponse.Content))
            {
                _logger.LogWarning("Failed to retrieve Orange Jordan search page. Status: {Code}", searchResponse.StatusCode);
                return results;
            }

            var matches = DetailLinkRegex.Matches(searchResponse.Content);
            var distinctJobUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in matches)
            {
                var relativeUrl = match.Groups["url"].Value;
                var absoluteUrl = relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? relativeUrl
                    : $"{BaseUrl}{relativeUrl}";

                distinctJobUrls.Add(absoluteUrl);
            }

            _logger.LogInformation("Discovered {Count} unique Orange Jordan vacancy URLs.", distinctJobUrls.Count);

            // Stage 2: Fetch and parse each detail page
            foreach (var detailUrl in distinctJobUrls)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var detailUri = new Uri(detailUrl);
                var detailResponse = await _politenessEngine.GetAsync(detailUri, cancellationToken: cancellationToken);

                if (!detailResponse.IsAllowed || detailResponse.StatusCode != 200 || string.IsNullOrWhiteSpace(detailResponse.Content))
                {
                    _logger.LogWarning("Skipping Orange detail URL {Url} (Status {Code}, Allowed: {Allowed})",
                        detailUrl, detailResponse.StatusCode, detailResponse.IsAllowed);
                    continue;
                }

                var html = detailResponse.Content;

                // Extract Title
                var titleMatch = TitleRegex.Match(html);
                var title = titleMatch.Success
                    ? EvidenceVerificationService.StripHtml(titleMatch.Groups["title"].Value)
                    : (TitleFallbackRegex.Match(html).Success
                        ? EvidenceVerificationService.StripHtml(TitleFallbackRegex.Match(html).Groups["title"].Value)
                        : "Orange Jordan Vacancy");

                // Extract Description, Skills, Requirements
                var descMatch = CollapseOneRegex.Match(html);
                var skillsMatch = CollapseTwoRegex.Match(html);
                var reqMatch = CollapseThreeRegex.Match(html);

                var descriptionPart = descMatch.Success ? descMatch.Groups["content"].Value : string.Empty;
                var skillsPart = skillsMatch.Success ? skillsMatch.Groups["content"].Value : string.Empty;
                var reqPart = reqMatch.Success ? reqMatch.Groups["content"].Value : string.Empty;

                var combinedHtml = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(descriptionPart))
                {
                    combinedHtml.AppendLine("<h3>Job Description</h3>");
                    combinedHtml.AppendLine(descriptionPart);
                }
                if (!string.IsNullOrWhiteSpace(skillsPart))
                {
                    combinedHtml.AppendLine("<h3>Skills</h3>");
                    combinedHtml.AppendLine(skillsPart);
                }
                if (!string.IsNullOrWhiteSpace(reqPart))
                {
                    combinedHtml.AppendLine("<h3>Requirements</h3>");
                    combinedHtml.AppendLine(reqPart);
                }

                var fullDescription = combinedHtml.ToString();
                if (string.IsNullOrWhiteSpace(fullDescription))
                {
                    fullDescription = EvidenceVerificationService.StripHtml(html);
                }

                // Extract External Apply URL
                var applyMatch = ApplyButtonRegex.Match(html);
                var externalApplyUrl = applyMatch.Success && !string.IsNullOrWhiteSpace(applyMatch.Groups["url"].Value)
                    ? (applyMatch.Groups["url"].Value.StartsWith("http") ? applyMatch.Groups["url"].Value : $"{BaseUrl}{applyMatch.Groups["url"].Value}")
                    : detailUrl;

                // Compute Content Hash (SHA-256)
                var contentToHash = $"{title}|{EvidenceVerificationService.StripHtml(fullDescription)}";
                var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contentToHash))).ToLowerInvariant();

                // Extract skills tokens
                var skillsList = new List<string>();
                if (!string.IsNullOrWhiteSpace(skillsPart))
                {
                    var strippedSkills = EvidenceVerificationService.StripHtml(skillsPart);
                    var skillItems = strippedSkills.Split(new[] { '\n', ',', '•', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var s in skillItems)
                    {
                        if (s.Length > 2 && s.Length < 40 && !skillsList.Contains(s))
                        {
                            skillsList.Add(s);
                        }
                    }
                }

                if (dryRun)
                {
                    _logger.LogInformation(
                        "[DRY-RUN] Orange Vacancy Discovered: Title='{Title}', ApplyUrl='{Url}', DescLength={Len}, SkillsCount={SkillsCount}, ContentHash='{Hash}'. (Database mutation skipped).",
                        title, externalApplyUrl, fullDescription.Length, skillsList.Count, contentHash);
                }
                else
                {
                    results.Add(new DiscoveredJobDto(
                        Title: title,
                        CompanyName: "Orange Jordan",
                        Description: fullDescription,
                        Location: "Amman, Jordan",
                        IsRemote: false,
                        EmploymentType: EmploymentType.FullTime,
                        ExperienceLevel: ExperienceLevel.MidLevel,
                        SalaryMin: null,
                        SalaryMax: null,
                        SalaryCurrency: "JOD",
                        ExternalApplyUrl: externalApplyUrl,
                        PostedAt: DateTime.UtcNow,
                        Skills: skillsList,
                        ProviderName: ProviderName));
                }
            }

            _logger.LogInformation("Orange Jordan crawl finished. Discovered {Count} vacancies ({Mode}).",
                distinctJobUrls.Count, dryRun ? "Dry-run mode, 0 written to DB" : $"{results.Count} mapped for ingestion");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while crawling Orange Jordan jobs.");
        }

        return results;
    }
}
