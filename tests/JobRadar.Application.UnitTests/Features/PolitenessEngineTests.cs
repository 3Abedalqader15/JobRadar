using FluentAssertions;
using JobRadar.Infrastructure.Crawlers.Politeness;
using Xunit;

namespace JobRadar.Application.UnitTests.Features;

public class PolitenessEngineTests
{
    [Fact]
    public void CrawlerUserAgentOptions_BuildHeaderString_FormatsCorrectly()
    {
        var options = new CrawlerUserAgentOptions
        {
            BotName = "JobRadarBot",
            Version = "1.0",
            ContactUrl = "https://my-legit-domain.com/bot",
            ContactEmail = "team@my-legit-domain.com"
        };

        var header = options.BuildHeaderString();
        header.Should().Be("JobRadarBot/1.0 (+https://my-legit-domain.com/bot; team@my-legit-domain.com)");
    }

    [Theory]
    [InlineData("", "valid@domain.com", "valid https:// URL")]
    [InlineData("http://insecure.com", "valid@domain.com", "valid https:// URL")]
    [InlineData("https://example.com/bot", "valid@domain.com", "placeholder or unverified")]
    [InlineData("https://jobradar.io/bot", "valid@domain.com", "placeholder or unverified")]
    [InlineData("https://domain.com/bot", "", "valid email")]
    [InlineData("https://domain.com/bot", "invalid-email", "valid email")]
    [InlineData("https://domain.com/bot", "bot@example.com", "placeholder or unverified")]
    [InlineData("https://domain.com/bot", "bot@jobradar.io", "placeholder or unverified")]
    public void CrawlerUserAgentOptions_Validate_ThrowsOnInvalidOrPlaceholderInputs(string url, string email, string expectedMessagePart)
    {
        var options = new CrawlerUserAgentOptions
        {
            ContactUrl = url,
            ContactEmail = email
        };

        var act = () => options.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{expectedMessagePart}*");
    }

    [Fact]
    public void CachedRobotsTxt_Parse_ParsesRulesAndCrawlDelayCorrectly()
    {
        var robotsContent = """
            User-agent: *
            Disallow: /admin
            Disallow: /private/
            Allow: /private/public-doc
            Crawl-delay: 5

            User-agent: SpecificBadBot
            Disallow: /
            """;

        var parsed = PolitenessEngine.CachedRobotsTxt.Parse(robotsContent, DateTimeOffset.UtcNow.AddHours(24));

        parsed.IsAllowed("/search", "JobRadarBot").Should().BeTrue();
        parsed.IsAllowed("/jobpost/detail/554", "JobRadarBot").Should().BeTrue();
        parsed.IsAllowed("/admin", "JobRadarBot").Should().BeFalse();
        parsed.IsAllowed("/admin/users", "JobRadarBot").Should().BeFalse();
        parsed.IsAllowed("/private/secret", "JobRadarBot").Should().BeFalse();
        parsed.IsAllowed("/private/public-doc", "JobRadarBot").Should().BeTrue(); // Specific Allow overrides Disallow

        parsed.GetCrawlDelay("JobRadarBot").Should().Be(5.0);
    }

    [Fact]
    public void CachedRobotsTxt_DisallowAll_BlocksAllPaths()
    {
        var disallowAll = PolitenessEngine.CachedRobotsTxt.DisallowAll(DateTimeOffset.UtcNow.AddHours(1));

        disallowAll.IsAllowed("/search", "JobRadarBot").Should().BeFalse();
        disallowAll.IsAllowed("/", "JobRadarBot").Should().BeFalse();
    }

    [Fact]
    public void CachedRobotsTxt_AllowAll_PermitsAllPaths()
    {
        var allowAll = PolitenessEngine.CachedRobotsTxt.AllowAll(DateTimeOffset.UtcNow.AddHours(24));

        allowAll.IsAllowed("/search", "JobRadarBot").Should().BeTrue();
        allowAll.IsAllowed("/admin", "JobRadarBot").Should().BeTrue();
    }
}
