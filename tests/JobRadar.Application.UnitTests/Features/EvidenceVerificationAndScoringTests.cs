using FluentAssertions;
using JobRadar.Application.Common;
using JobRadar.Infrastructure.Services;
using Xunit;

namespace JobRadar.Application.UnitTests.Features;

public class EvidenceVerificationAndScoringTests
{
    [Fact]
    public void NormalizeText_NormalizesEnglishHtmlEntitiesAndWhitespace()
    {
        // Arrange
        var raw = "  <div>Senior <b>.NET</b> Engineer &amp; Architect   with 5+ &lt;years&gt; experience!  </div>  ";

        // Act
        var normalized = EvidenceVerificationService.NormalizeText(raw);

        // Assert
        normalized.Should().Be("senior .net engineer & architect with 5+ <years> experience!");
    }

    [Fact]
    public void NormalizeText_NormalizesArabicAlefYaTaMarbutaDiacriticsAndTatweel()
    {
        // Arrange: Arabic text with different alef forms (إ, أ, آ), ta marbuta (ة), ya/maqsura (ى), harakat, and tatweel
        var textWithDiacritics = "مُطَوِّرُ بَرْمَجِيَّاتٍ أَرْدُنِيٌّ؛ خِبْرَةٌ فِي إِدَارَةِ قَوَاعِدِ البَيَانَاتِ وَالـتَّـطْـبِـيـقَـاتِ عَلَى سَحَابَةِ الحَوْسَبَةِ";

        // Act
        var normalized = EvidenceVerificationService.NormalizeText(textWithDiacritics);

        // Assert
        // Alef variants unified to ا
        // Diacritics removed
        // Tatweel removed
        // Ta marbuta normalized to ه
        // Ya / alef maqsura unified to ي
        normalized.Should().NotContain("أ");
        normalized.Should().NotContain("إ");
        normalized.Should().NotContain("ة");
        normalized.Should().NotContain("\u0640"); // Tatweel
        normalized.Should().Contain("مطور برمجيات اردني");
        normalized.Should().Contain("اداره قواعد البيانات");
        normalized.Should().Contain("والتطبيقات");
        normalized.Should().Contain("سحابه");
    }

    [Fact]
    public void IsQuotePresentInJobDescription_AcceptsExactSubstringMatch_EnglishAndArabic()
    {
        // Arrange
        var englishJd = "The role requires at least 5 years of experience in ASP.NET Core, EF Core, and PostgreSQL.";
        var arabicJd = "يشترط خبرة لا تقل عن خمس سنوات في بناء تطبيقات الويب باستخدام الدوت نت وقواعد البيانات";

        // Act & Assert - English exact
        EvidenceVerificationService.IsQuotePresentInJobDescription(
            "experience in ASP.NET Core, EF Core",
            EvidenceVerificationService.NormalizeText(englishJd)
        ).Should().BeTrue();

        // Act & Assert - Arabic exact with varying alef/diacritics
        EvidenceVerificationService.IsQuotePresentInJobDescription(
            "خِبْرَةٌ لَا تَقِلُّ عَنْ خَمْسِ سَنَوَاتٍ",
            EvidenceVerificationService.NormalizeText(arabicJd)
        ).Should().BeTrue();
    }

    [Fact]
    public void IsQuotePresentInJobDescription_AcceptsHighTokenOverlapMatch_Above90Percent()
    {
        // Arrange
        var jd = "Strong hands-on expertise in Docker, Kubernetes, CI/CD pipelines, and microservices architecture.";
        var normalizedJd = EvidenceVerificationService.NormalizeText(jd);

        // Quote with minor wording difference (10 out of 10 or 9 out of 10 tokens match)
        var quoteWithMinorDiff = "expertise in Docker Kubernetes CI/CD pipelines and microservices architecture";

        // Act
        var isMatch = EvidenceVerificationService.IsQuotePresentInJobDescription(quoteWithMinorDiff, normalizedJd, tokenOverlapThreshold: 0.90);

        // Assert
        isMatch.Should().BeTrue();
    }

    [Fact]
    public void IsQuotePresentInJobDescription_RejectsFabricatedQuotes_BelowOverlapThreshold()
    {
        // Arrange
        var jd = "We require C# and ASP.NET Core backend development skills.";
        var normalizedJd = EvidenceVerificationService.NormalizeText(jd);

        // Fabricated quote not found in JD
        var hallucinatedQuote = "Must have extensive experience with Rust, Solana, and distributed blockchain smart contracts.";

        // Act
        var isMatch = EvidenceVerificationService.IsQuotePresentInJobDescription(hallucinatedQuote, normalizedJd, tokenOverlapThreshold: 0.90);

        // Assert
        isMatch.Should().BeFalse();
    }

    [Fact]
    public void VerifyMissingKeywords_FiltersFabricatedQuotesAndRetainsVerifiedOnes()
    {
        // Arrange
        var jd = "Requirements: Must have solid experience with Docker and Kubernetes container orchestration.";
        var appId = Guid.NewGuid();

        var missingList = new List<MissingKeywordItem>
        {
            new("Docker", "solid experience with Docker and Kubernetes"),
            new("AWS Cloud", "Candidate must be AWS certified Solutions Architect with Terraform"), // Fabricated
            new("Kubernetes", "container orchestration")
        };

        // Act
        var (verified, droppedCount) = EvidenceVerificationService.VerifyMissingKeywords(missingList, jd, appId);

        // Assert
        verified.Should().HaveCount(2);
        verified.Select(v => v.Keyword).Should().Contain("Docker");
        verified.Select(v => v.Keyword).Should().Contain("Kubernetes");
        verified.Select(v => v.Keyword).Should().NotContain("AWS Cloud");
        droppedCount.Should().Be(1);
    }

    [Fact]
    public void CvMatchWeightsOptions_ValidatesSumEquals100()
    {
        // Arrange - Valid
        var validOptions = new CvMatchWeightsOptions
        {
            RequiredSkills = 40,
            ExperienceSeniority = 25,
            Domain = 20,
            NiceToHave = 15
        };

        // Act & Assert
        validOptions.ValidateWeights().Should().BeTrue();
        var actValid = () => validOptions.Validate();
        actValid.Should().NotThrow();

        // Arrange - Invalid sum != 100
        var invalidOptions = new CvMatchWeightsOptions
        {
            RequiredSkills = 50,
            ExperienceSeniority = 50,
            Domain = 20,
            NiceToHave = 10
        };

        // Act & Assert
        invalidOptions.ValidateWeights().Should().BeFalse();
        var actInvalid = () => invalidOptions.Validate();
        actInvalid.Should().Throw<InvalidOperationException>()
            .WithMessage("*must sum to exactly 100*");
    }

    [Fact]
    public void CalculateWeightedScore_ComputesAccurateWeightedScore_WhenAllSubScoresPresent()
    {
        // Arrange
        var weights = new CvMatchWeightsOptions
        {
            RequiredSkills = 40,
            ExperienceSeniority = 25,
            Domain = 20,
            NiceToHave = 15
        };

        // Act: (100*40 + 80*25 + 60*20 + 40*15) / 100 = (4000 + 2000 + 1200 + 600) / 100 = 7800 / 100 = 78
        var (score, breakdownJson) = CvMatchScoreCalculator.CalculateWeightedScore(
            requiredSkillsScore: 100,
            experienceSeniorityScore: 80,
            niceToHaveScore: 40,
            domainScore: 60,
            weights: weights);

        // Assert
        score.Should().Be(78);
        breakdownJson.Should().Contain("\"calculated_match_score\":78");
        breakdownJson.Should().Contain("\"required_skills\":100");
        breakdownJson.Should().Contain("\"renormalized_weight_sum\":100");
    }

    [Fact]
    public void CalculateWeightedScore_RenormalizesWeights_WhenNiceToHaveIsNull()
    {
        // Arrange: Weights 40, 25, 20, 15.
        // If NiceToHave is null, non-null weights sum = 40 + 25 + 20 = 85.
        var weights = new CvMatchWeightsOptions
        {
            RequiredSkills = 40,
            ExperienceSeniority = 25,
            Domain = 20,
            NiceToHave = 15
        };

        // Subscores: Required = 90, Experience = 80, Domain = 70, NiceToHave = null
        // Weighted sum = (90*40) + (80*25) + (70*20) = 3600 + 2000 + 1400 = 7000
        // Renormalized score = 7000 / 85 = 82.35 -> 82
        var (score, breakdownJson) = CvMatchScoreCalculator.CalculateWeightedScore(
            requiredSkillsScore: 90,
            experienceSeniorityScore: 80,
            niceToHaveScore: null,
            domainScore: 70,
            weights: weights);

        // Assert
        score.Should().Be(82);
        breakdownJson.Should().Contain("\"nice_to_have\":null");
        breakdownJson.Should().Contain("\"renormalized_weight_sum\":85");
    }

    [Fact]
    public void HtmlContentSanitizer_StripsChromeNavHeaderFooterAndScriptsCompletely()
    {
        // Arrange
        var html = """
            <!DOCTYPE html>
            <html>
            <head>
              <style>.hero { color: red; }</style>
              <script>console.log("analytics");</script>
            </head>
            <body>
              <nav><a href="/home">Home</a><a href="/jobs">Jobs</a></nav>
              <header><h1>Header Navigation</h1></header>
              <main>
                <h2>Software Engineer Role</h2>
                <p>We are hiring a C# Developer &amp; architect.</p>
              </main>
              <aside>Sidebar advertisements</aside>
              <footer>&copy; 2026 Company Inc. All Rights Reserved.</footer>
            </body>
            </html>
            """;

        // Act
        var sanitized = HtmlContentSanitizer.Sanitize(html);

        // Assert: Chrome completely removed
        sanitized.Should().NotContain("analytics");
        sanitized.Should().NotContain("color: red");
        sanitized.Should().NotContain("Header Navigation");
        sanitized.Should().NotContain("Sidebar advertisements");
        sanitized.Should().NotContain("All Rights Reserved");
        sanitized.Should().NotContain("<nav>");
        sanitized.Should().NotContain("<header>");
        sanitized.Should().NotContain("<footer>");

        // Main content preserved and entities decoded
        sanitized.Should().Contain("Software Engineer Role");
        sanitized.Should().Contain("We are hiring a C# Developer & architect.");
    }
}
