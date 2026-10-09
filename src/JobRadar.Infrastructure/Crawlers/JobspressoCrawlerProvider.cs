using System.Text.RegularExpressions;
using CodeHollow.FeedReader;
using JobRadar.Application.Abstractions;
using JobRadar.Application.Models;
using JobRadar.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace JobRadar.Infrastructure.Crawlers;

/// <summary>
/// Dedicated crawler provider for Jobspresso (curated remote tech and software careers).
/// </summary>
public sealed class JobspressoCrawlerProvider : IJobCrawlerProvider
{
    private readonly ILogger<JobspressoCrawlerProvider> _logger;

    public string ProviderName => "Jobspresso";

    private const string FeedUrl = "https://jobspresso.co/category/remote-software-jobs/feed/";

    public JobspressoCrawlerProvider(ILogger<JobspressoCrawlerProvider> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredJobDto>> CrawlJobsAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<DiscoveredJobDto>();

        try
        {
            _logger.LogInformation("Fetching curated remote tech jobs from Jobspresso RSS...");
            var feed = await FeedReader.ReadAsync(FeedUrl, cancellationToken);

            foreach (var item in feed.Items)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (string.IsNullOrWhiteSpace(item.Link) || string.IsNullOrWhiteSpace(item.Title)) continue;

                var title = item.Title.Trim();
                var company = "Jobspresso Curated Employer";

                // Format often: "Title at Company"
                var atIndex = title.LastIndexOf(" at ", StringComparison.OrdinalIgnoreCase);
                if (atIndex > 0 && atIndex < title.Length - 4)
                {
                    company = title[(atIndex + 4)..].Trim();
                    title = title[..atIndex].Trim();
                }

                var cleanDesc = StripHtml(item.Description ?? item.Content ?? title);
                var expLevel = InferExperienceLevel(title);
                var empType = InferEmploymentType(cleanDesc, title);
                var postedDate = item.PublishingDate ?? DateTime.UtcNow;
                var skills = ExtractSkills(title + " " + cleanDesc);

                results.Add(new DiscoveredJobDto(
                    Title: title,
                    CompanyName: company,
                    Description: cleanDesc,
                    Location: "Remote (Global)",
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

            _logger.LogInformation("Discovered {Count} remote software jobs from Jobspresso.", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Jobspresso RSS feed.");
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
        if (lower.Contains("lead") || lower.Contains("principal") || lower.Contains("architect") || lower.Contains("head"))
            return ExperienceLevel.Lead;
        if (lower.Contains("senior") || lower.Contains("sr.") || lower.Contains("staff"))
            return ExperienceLevel.Senior;
        if (lower.Contains("junior") || lower.Contains("jr.") || lower.Contains("entry") || lower.Contains("intern"))
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

    private static List<string> ExtractSkills(string text)
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
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(kw)}\b", RegexOptions.IgnoreCase))
            {
                skills.Add(kw);
            }
        }

        return skills.Take(8).ToList();
    }
}
