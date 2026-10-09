using System.Net;
using System.Text.RegularExpressions;

namespace JobRadar.Application.Common;

/// <summary>
/// Utility for thoroughly cleaning HTML from fetched web pages, RSS feeds, and crawler inputs
/// so that page chrome (nav, header, footer, scripts, styles) does not leak into raw stored content.
/// </summary>
public static class HtmlContentSanitizer
{
    private static readonly Regex ScriptsStylesChromeRegex = new(
        @"<(?:script|style|svg|noscript|nav|header|footer|aside)[^>]*>[\s\S]*?</(?:script|style|svg|noscript|nav|header|footer|aside)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BlockBreaksRegex = new(
        @"<(?:br|/p|/div|/li|/h[1-6]|/tr)[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RemainingTagsRegex = new(
        @"<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex MultipleNewlinesRegex = new(
        @"\n{3,}",
        RegexOptions.Compiled);

    private static readonly Regex MultipleSpacesRegex = new(
        @"[ \t]{2,}",
        RegexOptions.Compiled);

    /// <summary>
    /// Strips all scripts, styles, SVG, noscript, nav, header, footer, aside, and HTML tags,
    /// decodes HTML entities, and normalizes spacing while preserving paragraph breaks.
    /// </summary>
    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // 1. Remove script, style, svg, noscript, nav, header, footer, aside along with all their children
        var cleaned = ScriptsStylesChromeRegex.Replace(html, " ");

        // 2. Convert block boundaries to newlines
        cleaned = BlockBreaksRegex.Replace(cleaned, "\n");

        // 3. Strip all remaining tags (e.g. <a>, <span>, <b>, <i>, etc.)
        cleaned = RemainingTagsRegex.Replace(cleaned, " ");

        // 4. Decode HTML entities (&amp;, &nbsp;, &lt;, etc.)
        cleaned = WebUtility.HtmlDecode(cleaned);

        // 5. Clean up extra whitespace and newlines
        cleaned = MultipleSpacesRegex.Replace(cleaned, " ");
        cleaned = MultipleNewlinesRegex.Replace(cleaned, "\n\n");

        return cleaned.Trim();
    }
}
