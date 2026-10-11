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

    public const int MinQuoteTokens = 3;

    /// <summary>
    /// Checks if a keyword or any of its known synonyms appears within the evidence quote.
    /// </summary>
    public static bool IsKeywordPresentInQuote(
        string keyword,
        string normalizedQuote,
        IReadOnlyDictionary<string, string[]>? synonyms = null)
    {
        if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(normalizedQuote))
            return false;

        var normalizedKeyword = NormalizeText(keyword);
        if (string.IsNullOrWhiteSpace(normalizedKeyword))
            return false;

        if (normalizedQuote.Contains(normalizedKeyword, StringComparison.Ordinal))
            return true;

        var keywordTokens = GetTokens(normalizedKeyword);
        var quoteTokens = GetTokens(normalizedQuote);
        var quoteTokenSet = new HashSet<string>(quoteTokens, StringComparer.Ordinal);

        // All keyword tokens appear in the quote
        if (keywordTokens.Length > 0 && keywordTokens.All(kt => quoteTokenSet.Contains(kt)))
            return true;

        // Check synonyms if provided
        if (synonyms != null)
        {
            foreach (var kvp in synonyms)
            {
                var canonical = NormalizeText(kvp.Key);
                if (canonical == normalizedKeyword || kvp.Value.Select(NormalizeText).Contains(normalizedKeyword))
                {
                    if (normalizedQuote.Contains(canonical, StringComparison.Ordinal))
                        return true;

                    foreach (var syn in kvp.Value)
                    {
                        var normSyn = NormalizeText(syn);
                        if (normalizedQuote.Contains(normSyn, StringComparison.Ordinal))
                            return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a keyword appears in the candidate's CV text.
    /// If it already appears in the CV, it is NOT missing.
    /// </summary>
    public static bool IsKeywordPresentInCv(string keyword, string normalizedCvText)
    {
        if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(normalizedCvText))
            return false;

        var normalizedKeyword = NormalizeText(keyword);
        if (string.IsNullOrWhiteSpace(normalizedKeyword))
            return false;

        if (normalizedCvText.Contains(normalizedKeyword, StringComparison.Ordinal))
            return true;

        var keywordTokens = GetTokens(normalizedKeyword);
        if (keywordTokens.Length == 0)
            return false;

        var cvTokens = new HashSet<string>(GetTokens(normalizedCvText), StringComparer.Ordinal);
        return keywordTokens.All(kt => cvTokens.Contains(kt));
    }

    /// <summary>
    /// Validates an evidence quote against the normalized job description.
    /// Requires minimum 3 tokens.
    /// Accepts if exact substring match OR ordered sliding window token overlap >= threshold.
    /// </summary>
    public static bool IsQuotePresentInJobDescription(
        string? evidenceQuote,
        string normalizedJobDescription,
        double tokenOverlapThreshold = 0.85)
    {
        if (string.IsNullOrWhiteSpace(evidenceQuote) || string.IsNullOrWhiteSpace(normalizedJobDescription))
            return false;

        var normalizedQuote = NormalizeText(evidenceQuote);
        if (string.IsNullOrWhiteSpace(normalizedQuote))
            return false;

        var quoteTokens = GetTokens(normalizedQuote);
        if (quoteTokens.Length < MinQuoteTokens)
            return false; // Requirement: minimum quote length at least 3 tokens

        // Check 1: Exact substring match
        if (normalizedJobDescription.Contains(normalizedQuote, StringComparison.Ordinal))
            return true;

        // Check 2: Ordered sliding window token overlap
        var jdTookens = GetTokens(normalizedJobDescription);
        if (jdTookens.Length < quoteTokens.Length)
            return false;

        return MatchesOrderedWindow(quoteTokens, jdTookens, maxGap: 2, threshold: tokenOverlapThreshold);
    }

    /// <summary>
    /// Checks if quote tokens appear in the JD in the same relative order with an allowed gap between consecutive tokens.
    /// </summary>
    private static bool MatchesOrderedWindow(string[] quoteTokens, string[] jdTokens, int maxGap, double threshold)
    {
        int requiredMatches = (int)Math.Ceiling(quoteTokens.Length * threshold);
        int quoteLen = quoteTokens.Length;
        int jdLen = jdTokens.Length;

        // Search for start token matches
        for (int i = 0; i < jdLen; i++)
        {
            if (jdTokens[i] != quoteTokens[0])
                continue;

            int matched = 1;
            int jdPtr = i;

            for (int q = 1; q < quoteLen; q++)
            {
                bool found = false;
                for (int gap = 1; gap <= maxGap + 1 && (jdPtr + gap) < jdLen; gap++)
                {
                    if (jdTokens[jdPtr + gap] == quoteTokens[q])
                    {
                        matched++;
                        jdPtr += gap;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // Allow skipping at most (quoteLen - requiredMatches) tokens
                    if (matched + (quoteLen - q - 1) < requiredMatches)
                        break;
                }
            }

            if (matched >= requiredMatches)
                return true;
        }

        return false;
    }

    public static string[] GetTokens(string text)
    {
        return text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.Trim(',', '.', ';', ':', '!', '?', '-', '(', ')', '"', '\'', '،', '؛', '؟'))
            .Where(t => t.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// Filters missing keyword objects, retaining only those whose evidence quotes can be verified,
    /// contain the keyword, and are not present in the candidate CV.
    /// Drops and logs unverified items with the application ID.
    /// </summary>
    public static (IReadOnlyList<MissingKeywordItem> Verified, int DroppedCount) VerifyMissingKeywords(
        IEnumerable<MissingKeywordItem>? items,
        string jobDescription,
        string? candidateCvText,
        Guid applicationId,
        ILogger? logger = null,
        double tokenOverlapThreshold = 0.85,
        IReadOnlyDictionary<string, string[]>? synonyms = null)
    {
        if (items is null)
            return (Array.Empty<MissingKeywordItem>(), 0);

        var normalizedJd = NormalizeText(jobDescription);
        var normalizedCv = NormalizeText(candidateCvText);
        var verified = new List<MissingKeywordItem>();
        int droppedCount = 0;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Keyword))
                continue;

            // 1. If keyword is already in candidate CV, it is NOT missing
            if (!string.IsNullOrEmpty(normalizedCv) && IsKeywordPresentInCv(item.Keyword, normalizedCv))
            {
                droppedCount++;
                logger?.LogInformation(
                    "Dropped missing keyword '{Keyword}' for application {ApplicationId}: candidate CV already contains this keyword.",
                    item.Keyword, applicationId);
                continue;
            }

            // 2. Evidence quote must be verified in the job description
            bool isQuotePresent = IsQuotePresentInJobDescription(item.EvidenceQuote, normalizedJd, tokenOverlapThreshold);
            if (!isQuotePresent)
            {
                droppedCount++;
                logger?.LogWarning(
                    "Dropped unverified missing keyword '{Keyword}' for application {ApplicationId}. Evidence quote not found in JD: '{Quote}'",
                    item.Keyword, applicationId, item.EvidenceQuote);
                continue;
            }

            // 3. Keyword (or synonym) must appear in the quote itself
            var normalizedQuote = NormalizeText(item.EvidenceQuote);
            bool isKeywordInQuote = IsKeywordPresentInQuote(item.Keyword, normalizedQuote, synonyms);
            if (!isKeywordInQuote)
            {
                droppedCount++;
                logger?.LogWarning(
                    "Dropped missing keyword '{Keyword}' for application {ApplicationId}. Quote does not mention keyword or synonym: '{Quote}'",
                    item.Keyword, applicationId, item.EvidenceQuote);
                continue;
            }

            verified.Add(item);
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
