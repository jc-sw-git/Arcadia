namespace Arcadia.Core.Search;

/// <summary>
/// A parsed search input. A leading <c>/keyword</c> limits the search to launchers;
/// several launchers are joined with <c>-</c>: <c>/steam-epic-ea hollow</c>.
/// </summary>
/// <param name="LauncherKeywords">Lower-cased keywords from the prefix, in input order, without duplicates.</param>
/// <param name="Term">The actual search term, trimmed.</param>
/// <param name="HasLauncherFilter">True when the input started with a <c>/</c> prefix.</param>
public sealed record SearchQuery(IReadOnlyList<string> LauncherKeywords, string Term, bool HasLauncherFilter)
{
    public const char PrefixMarker = '/';
    public const char KeywordSeparator = '-';

    public static SearchQuery Parse(string? input)
    {
        var text = (input ?? string.Empty).TrimStart();
        if (text.Length == 0 || text[0] != PrefixMarker)
        {
            return new SearchQuery([], text.Trim(), false);
        }

        var end = text.IndexOfAny([' ', '\t']);
        var prefix = end < 0 ? text[1..] : text[1..end];
        var term = end < 0 ? string.Empty : text[end..].Trim();

        var keywords = prefix
            .Split(KeywordSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => k.ToLowerInvariant())
            .Distinct()
            .ToList();

        return new SearchQuery(keywords, term, true);
    }
}
