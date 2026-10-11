namespace JobRadar.Infrastructure.Crawlers.Politeness;

public sealed class CrawlerUserAgentOptions
{
    public const string SectionName = "Crawler:UserAgent";

    public string BotName { get; set; } = "JobRadarBot";
    public string Version { get; set; } = "1.0";
    public string ContactUrl { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;

    public string BuildHeaderString()
    {
        return $"{BotName}/{Version} (+{ContactUrl}; {ContactEmail})";
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ContactUrl) || !ContactUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Crawler:UserAgent:ContactUrl must be configured with a valid https:// URL.");
        }

        if (ContactUrl.Contains("example.com", StringComparison.OrdinalIgnoreCase) ||
            ContactUrl.Contains("jobradar.io", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Crawler:UserAgent:ContactUrl contains a placeholder or unverified domain: '{ContactUrl}'. An authentic URL is required.");
        }

        if (string.IsNullOrWhiteSpace(ContactEmail) || !ContactEmail.Contains('@') || !ContactEmail.Contains('.'))
        {
            throw new InvalidOperationException(
                "Crawler:UserAgent:ContactEmail must be configured with a valid email address.");
        }

        if (ContactEmail.Contains("example.com", StringComparison.OrdinalIgnoreCase) ||
            ContactEmail.Contains("jobradar.io", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Crawler:UserAgent:ContactEmail contains a placeholder or unverified domain: '{ContactEmail}'. An authentic email is required.");
        }
    }
}
