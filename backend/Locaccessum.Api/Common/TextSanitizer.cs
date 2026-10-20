using System.Text.RegularExpressions;

namespace Locaccessum.Api.Common;

/// <summary>
/// Minimal defense-in-depth sanitization for free-text fields that are stored and echoed back by
/// the API. The current frontend already renders this content safely (a plain React text node,
/// never <c>dangerouslySetInnerHTML</c>), so this is a second layer protecting any future consumer
/// that might not escape it: a regex-based tag strip, not a full HTML parser. If a field ever needs
/// to support real user-authored HTML, replace this with a proper allow-list sanitizer (e.g. the
/// HtmlSanitizer NuGet package) instead of hardening the regex further.
/// </summary>
public static partial class TextSanitizer
{
    /// <summary>Removes HTML tags, keeping their inner text (e.g. "&lt;b&gt;220V&lt;/b&gt;" becomes "220V").</summary>
    public static string? StripHtmlTags(string? input) => input is null ? null : HtmlTagPattern().Replace(input, string.Empty);

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTagPattern();
}
