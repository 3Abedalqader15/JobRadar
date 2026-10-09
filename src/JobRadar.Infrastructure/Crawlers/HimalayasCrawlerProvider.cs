using System.Text.RegularExpressions;
using System.Xml.Linq;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Crawlers;

/// <summary>
/// Crawler provider for Himalayas (https://himalayas.app/jobs/rss),
/// a premier modern remote tech jobs platform.
/// </summary>
public sealed class HimalayasCrawlerProvider : IJobCrawlerProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HimalayasCrawlerProvider> _logger;

    public string ProviderName => "Himalayas";

    public HimalayasCrawlerProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<HimalayasCrawlerProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("JobCrawler");
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredJobDto>();

        try
        {
            _logger.LogInformation("Fetching live remote tech jobs from Himalayas RSS feed...");

            using var response = await _httpClient.GetAsync("https://himalayas.app/jobs/rss", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Himalayas RSS responded with HTTP {StatusCode}", response.StatusCode);
                return results;
            }

            var xmlContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                return results;
            }

            var doc = XDocument.Parse(xmlContent);
            var items = doc.Descendants("item");

            foreach (var item in items)
            {
                var rawTitle = item.Element("title")?.Value?.Trim() ?? string.Empty;
                var link = item.Element("link")?.Value?.Trim() ?? string.Empty;
                var rawDesc = item.Element("description")?.Value ?? rawTitle;
                var pubDateStr = item.Element("pubDate")?.Value;

                if (string.IsNullOrWhiteSpace(rawTitle) || string.IsNullOrWhiteSpace(link))
                {
                    continue;
                }

                // Typical Himalayas title: "Senior Full Stack Engineer at Acorns" or "DevOps Engineer - Stripe"
                string title = rawTitle;
                string company = "Himalayas Partner";

                if (rawTitle.Contains(" at ", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = Regex.Split(rawTitle, @"\s+at\s+", RegexOptions.IgnoreCase);
                    title = parts[0].Trim();
                    company = parts.Length > 1 ? parts[1].Trim() : company;
                }
                else if (rawTitle.Contains(" - "))
                {
                    var parts = rawTitle.Split(" - ");
                    title = parts[0].Trim();
                    company = parts.Length > 1 ? parts[1].Trim() : company;
                }

                DateTime postedAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(pubDateStr) && DateTime.TryParse(pubDateStr, out var parsedDate))
                {
                    postedAt = parsedDate.ToUniversalTime();
                }

                var cleanDesc = StripHtml(rawDesc);
                var skills = ExtractSkills(title + " " + cleanDesc);

                results.Add(new DiscoveredJobDto(
                    Title: title,
                    CompanyName: company,
                    Description: cleanDesc,
                    Location: "Remote (Worldwide / US / EMEA)",
                    IsRemote: true,
                    EmploymentType: EmploymentType.FullTime,
                    ExperienceLevel: InferExperienceLevel(title),
                    SalaryMin: null,
                    SalaryMax: null,
                    SalaryCurrency: "USD",
                    ExternalApplyUrl: link,
                    PostedAt: postedAt,
                    Skills: skills,
                    ProviderName: ProviderName
                ));
            }

            _logger.LogInformation("Discovered {Count} remote tech jobs from Himalayas.", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to crawl jobs from Himalayas RSS.");
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
            "flutter", "swift", "kotlin", "ruby", "rails", "php", "laravel", "next.js"
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
}
