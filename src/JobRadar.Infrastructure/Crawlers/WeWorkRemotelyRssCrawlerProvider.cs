using System.Text.RegularExpressions;
using CodeHollow.FeedReader;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Crawlers;

public sealed class WeWorkRemotelyRssCrawlerProvider : IJobCrawlerProvider
{
    private readonly ILogger<WeWorkRemotelyRssCrawlerProvider> _logger;

    public string ProviderName => "WeWorkRemotely";

    private static readonly string[] FeedUrls = new[]
    {
        "https://weworkremotely.com/categories/remote-back-end-programming-jobs.rss",
        "https://weworkremotely.com/categories/remote-front-end-programming-jobs.rss",
        "https://weworkremotely.com/categories/remote-full-stack-programming-jobs.rss"
    };

    public WeWorkRemotelyRssCrawlerProvider(ILogger<WeWorkRemotelyRssCrawlerProvider> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredJobDto>();

        foreach (var feedUrl in FeedUrls)
        {
            try
            {
                _logger.LogInformation("Fetching RSS jobs from {FeedUrl}...", feedUrl);
                var feed = await FeedReader.ReadAsync(feedUrl, cancellationToken);

                foreach (var item in feed.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.Link) || string.IsNullOrWhiteSpace(item.Title))
                    {
                        continue;
                    }

                    // WWR format: "Company: Title"
                    string company = "Remote Company";
                    string title = item.Title.Trim();

                    var colonIndex = title.IndexOf(':');
                    if (colonIndex > 0 && colonIndex < title.Length - 1)
                    {
                        company = title[..colonIndex].Trim();
                        title = title[(colonIndex + 1)..].Trim();
                    }

                    var cleanDesc = StripHtml(item.Description ?? item.Content ?? title);
                    var expLevel = InferExperienceLevel(title);
                    var empType = InferEmploymentType(cleanDesc, title);
                    var postedDate = item.PublishingDate ?? DateTime.UtcNow;
                    var skills = ExtractSkillsFromText(title + " " + cleanDesc);

                    results.Add(new DiscoveredJobDto(
                        Title: title,
                        CompanyName: company,
                        Description: cleanDesc,
                        Location: "Remote (Worldwide)",
                        IsRemote: true,
                        EmploymentType: empType,
                        ExperienceLevel: expLevel,
                        SalaryMin: null,
                        SalaryMax: null,
                        SalaryCurrency: "USD",
                        ExternalApplyUrl: item.Link.Trim(),
                        PostedAt: postedDate,
                        Skills: skills,
                        ProviderName: ProviderName
                    ));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch WWR RSS feed from {FeedUrl}", feedUrl);
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
        if (lower.Contains("lead") || lower.Contains("principal") || lower.Contains("architect") || lower.Contains("head of") || lower.Contains("director"))
            return ExperienceLevel.Lead;
        if (lower.Contains("senior") || lower.Contains("sr.") || lower.Contains("sr ") || lower.Contains("staff"))
            return ExperienceLevel.Senior;
        if (lower.Contains("junior") || lower.Contains("jr.") || lower.Contains("entry") || lower.Contains("intern") || lower.Contains("graduate"))
            return ExperienceLevel.EntryLevel;
        return ExperienceLevel.MidLevel;
    }

    private static EmploymentType InferEmploymentType(string desc, string title)
    {
        var combined = (title + " " + desc).ToLowerInvariant();
        if (combined.Contains("contract") || combined.Contains("freelance")) return EmploymentType.Contract;
        if (combined.Contains("part-time") || combined.Contains("part time")) return EmploymentType.PartTime;
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
}
