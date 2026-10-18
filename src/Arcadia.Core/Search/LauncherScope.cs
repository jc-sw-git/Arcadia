using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Search;

/// <summary>
/// The launchers a search covers. A <c>/keyword</c> prefix decides when the input has one,
/// otherwise the launcher chips do.
/// </summary>
/// <param name="LauncherIds">The covered launchers, in tab order.</param>
/// <param name="UnknownKeywords">Prefix keywords no launcher has, in input order.</param>
/// <param name="IsAll">True when every launcher is covered.</param>
public sealed record LauncherScope(IReadOnlyList<string> LauncherIds, IReadOnlyList<string> UnknownKeywords, bool IsAll)
{
    public static LauncherScope Of(SearchQuery query, IReadOnlyList<LauncherInfo> launchers, IReadOnlyCollection<string> chosenIds)
    {
        if (!query.HasLauncherFilter)
        {
            var chosen = launchers.Where(l => chosenIds.Contains(l.Id)).Select(l => l.Id).ToList();
            return new LauncherScope(chosen, [], chosen.Count == launchers.Count);
        }

        var known = launchers.Where(l => query.LauncherKeywords.Contains(l.SearchKeyword.ToLowerInvariant())).Select(l => l.Id).ToList();
        var unknown = query.LauncherKeywords.Where(k => !launchers.Any(l => l.SearchKeyword.Equals(k, StringComparison.OrdinalIgnoreCase))).ToList();
        return new LauncherScope(known, unknown, known.Count == launchers.Count);
    }

    /// <summary>
    /// The launchers after the chip of <paramref name="launcherId"/> was switched; null if that
    /// would leave none, which the search doesn't allow.
    /// </summary>
    public IReadOnlyList<string>? Toggle(string launcherId, IReadOnlyList<LauncherInfo> launchers)
    {
        var next = LauncherIds.Contains(launcherId)
            ? LauncherIds.Where(id => id != launcherId).ToList()
            : launchers.Select(l => l.Id).Where(id => id == launcherId || LauncherIds.Contains(id)).ToList();
        return next.Count == 0 ? null : next;
    }

    /// <summary>
    /// The input after the chips changed to <paramref name="launcherIds"/>. A prefix is rewritten to
    /// name them, and dropped when they are all launchers; an input without a prefix stays as it is.
    /// </summary>
    public static string Rewrite(string input, IReadOnlyCollection<string> launcherIds, IReadOnlyList<LauncherInfo> launchers)
    {
        var query = SearchQuery.Parse(input);
        if (launcherIds.Count == launchers.Count)
        {
            return query.Term;
        }

        if (!query.HasLauncherFilter)
        {
            return input;
        }

        var keywords = launchers.Where(l => launcherIds.Contains(l.Id)).Select(l => l.SearchKeyword);
        return $"{SearchQuery.PrefixMarker}{string.Join(SearchQuery.KeywordSeparator, keywords)} {query.Term}";
    }
}
