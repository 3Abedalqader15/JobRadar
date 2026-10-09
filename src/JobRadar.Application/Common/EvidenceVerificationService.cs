using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace JobRadar.Application.Common;

public sealed record MissingKeywordItem(string Keyword, string EvidenceQuote);

/// <summary>
/// Verifies that evidence quotes cited by the LLM for missing keywords actually exist
/// within the source job description text, utilizing robust multilingual normalization
/// (Arabic diacritics/variants, HTML stripping, whitespace collapsing) and token overlap matching.
/// </summary>
public static class EvidenceVerificationService
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    // Arabic normalization regexes
    private static readonly Regex ArabicDiacriticsRegex = new(@"[\u064B-\u065F\u0670]", RegexOptions.Compiled);
    private static readonly Regex ArabicAlefRegex = new(@"[إأآا]", RegexOptions.Compiled);
    private static readonly Regex ArabicYaRegex = new(@"[ىي]", RegexOptions.Compiled);
    private static readonly Regex ArabicTaMarbutaRegex = new(@"ة", RegexOptions.Compiled);
    private static readonly Regex ArabicTatweelRegex = new(@"\u0640", RegexOptions.Compiled);

    /// <summary>
    /// Normalizes text by stripping HTML, decoding entities, collapsing whitespace,
    /// lowercasing, and normalizing Arabic character variants and diacritics.
    /// </summary>
    public static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // 1. Strip HTML tags
        var cleaned = HtmlTagRegex.Replace(text, " ");

        // 2. Decode HTML entities (e.g. &amp; &lt;)
        cleaned = WebUtility.HtmlDecode(cleaned);

        // 3. Lowercase
        cleaned = cleaned.ToLowerInvariant();

        // 4. Normalize Arabic variants
        cleaned = ArabicDiacriticsRegex.Replace(cleaned, string.Empty); // Remove harakat
        cleaned = ArabicTatweelRegex.Replace(cleaned, string.Empty);    // Remove kashida
        cleaned = ArabicAlefRegex.Replace(cleaned, "ا");               // Unify alef forms
        cleaned = ArabicYaRegex.Replace(cleaned, "ي");                 // Unify ya / alef maqsura
        cleaned = ArabicTaMarbutaRegex.Replace(cleaned, "ه");          // Normalize ta marbuta to ha

        // 5. Collapse whitespace
        cleaned = WhitespaceRegex.Replace(cleaned, " ").Trim();

        return cleaned;
    }

    /// <summary>
    /// Strips HTML tags and entities without converting Arabic or lowercasing.
    /// Useful for length checking and pre-LLM filtering.
    /// </summary>
    public static string StripHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var cleaned = HtmlTagRegex.Replace(text, " ");
        cleaned = WebUtility.HtmlDecode(cleaned);
        return WhitespaceRegex.Replace(cleaned, " ").Trim();
    }

    /// <summary>
    /// Validates an evidence quote against the normalized job description.
    /// Accepts if exact substring match OR token overlap >= threshold (default 0.90).
    /// </summary>
    public static bool IsQuotePresentInJobDescription(
        string? evidenceQuote,
        string normalizedJobDescription,
        double tokenOverlapThreshold = 0.90)
    {
        if (string.IsNullOrWhiteSpace(evidenceQuote) || string.IsNullOrWhiteSpace(normalizedJobDescription))
            return false;

        var normalizedQuote = NormalizeText(evidenceQuote);
        if (string.IsNullOrWhiteSpace(normalizedQuote))
            return false;

        // Check 1: Exact substring match
        if (normalizedJobDescription.Contains(normalizedQuote, StringComparison.Ordinal))
            return true;

        // Check 2: Token-overlap match above configured threshold
        var quoteTokens = normalizedQuote
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Trim(',', '.', ';', ':', '!', '?', '-', '(', ')', '"', '\'', '،', '؛', '؟'))
            .Where(t => t.Length > 0)
            .ToArray();

        if (quoteTokens.Length == 0)
            return false;

        // Extract tokens from JD into a HashSet for O(1) membership testing
        var jdTokens = new HashSet<string>(
            normalizedJobDescription
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.Trim(',', '.', ';', ':', '!', '?', '-', '(', ')', '"', '\'', '،', '؛', '؟'))
                .Where(t => t.Length > 0),
            StringComparer.Ordinal);

        int matchingCount = 0;
        foreach (var token in quoteTokens)
        {
            if (jdTokens.Contains(token))
            {
                matchingCount++;
            }
        }

        double overlap = (double)matchingCount / quoteTokens.Length;
        return overlap >= tokenOverlapThreshold;
    }

    /// <summary>
    /// Filters missing keyword objects, retaining only those whose evidence quotes can be verified.
    /// Drops and logs unverified items with the application ID.
    /// </summary>
    public static (IReadOnlyList<MissingKeywordItem> Verified, int DroppedCount) VerifyMissingKeywords(
        IEnumerable<MissingKeywordItem>? items,
        string jobDescription,
        Guid applicationId,
        ILogger? logger = null,
        double tokenOverlapThreshold = 0.90)
    {
        if (items is null)
            return (Array.Empty<MissingKeywordItem>(), 0);

        var normalizedJd = NormalizeText(jobDescription);
        var verified = new List<MissingKeywordItem>();
        int droppedCount = 0;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Keyword))
                continue;

            bool isVerified = IsQuotePresentInJobDescription(item.EvidenceQuote, normalizedJd, tokenOverlapThreshold);
            if (isVerified)
            {
                verified.Add(item);
            }
            else
            {
                droppedCount++;
                logger?.LogWarning(
                    "Dropped unverified missing keyword '{Keyword}' for application {ApplicationId}. Evidence quote was not found in job description: '{Quote}'",
                    item.Keyword, applicationId, item.EvidenceQuote);
            }
        }

        if (droppedCount > 0)
        {
            logger?.LogInformation(
                "Evidence verification for application {ApplicationId}: {VerifiedCount} retained, {DroppedCount} dropped.",
                applicationId, verified.Count, droppedCount);
        }

        return (verified, droppedCount);
    }
}
