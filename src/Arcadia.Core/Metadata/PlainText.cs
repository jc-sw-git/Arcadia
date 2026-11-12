using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Arcadia.Core.Metadata;

/// <summary>
/// A provider's description as plain text (#493): stores write HTML with images, links, lists and scripts. Arcadia keeps
/// only the words: block ends become line breaks, list items lines of their own, every tag goes, scripts and styles with
/// their content, entities are decoded, and nothing that could be clicked or loaded is left.
/// </summary>
public static partial class PlainText
{
    /// <summary>The longest description Arcadia keeps; a longer one ends with "…".</summary>
    public const int MaxLength = 8000;

    /// <summary>The text of <paramref name="html"/>, at most <paramref name="maxLength"/> characters; null if none is left.</summary>
    public static string? Of(string? html, int maxLength = MaxLength)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        // A huge answer is cut before the regular expressions see it; the text it keeps is far shorter anyway.
        var text = html.Length > maxLength * 8 ? html[..(maxLength * 8)] : html;
        text = Hidden().Replace(text, " ");
        text = Comment().Replace(text, " ");
        text = LineBreak().Replace(text, "\n");
        text = ListItem().Replace(text, "\n• ");
        text = BlockEnd().Replace(text, "\n\n");
        text = Tag().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = Clean(text);
        if (text.Length == 0)
        {
            return null;
        }

        return text.Length <= maxLength ? text : text[..(maxLength - 1)].TrimEnd() + "…";
    }

    /// <summary>Control and invisible characters out, spaces and blank lines folded, every line trimmed.</summary>
    private static string Clean(string text)
    {
        var lines = new List<string>();
        var blank = false;
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = Spaces().Replace(WithoutControls(raw), " ").Trim();
            if (line.Length == 0 || line == "•")
            {
                blank = lines.Count > 0;
                continue;
            }

            if (blank)
            {
                lines.Add(string.Empty);
                blank = false;
            }

            lines.Add(line);
        }

        return string.Join('\n', lines);
    }

    private static string WithoutControls(string line)
    {
        var kept = new StringBuilder(line.Length);
        foreach (var c in line)
        {
            if (c is '\t' or '\u00A0')
            {
                kept.Append(' ');
            }
            else if (!char.IsControl(c) && char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format)
            {
                kept.Append(c);
            }
        }

        return kept.ToString();
    }

    [GeneratedRegex(@"<(script|style|iframe|object|noscript)\b.*?(</\1\s*>|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Hidden();

    [GeneratedRegex(@"<!--.*?(-->|$)", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"</?(p|div|h[1-6]|ul|ol|blockquote|table|tr)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex(@"<[^>]*(>|$)")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[  ]{2,}")]
    private static partial Regex Spaces();
}
