using System.Globalization;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

// settings.json stores these as numbers: keep the values when adding or reordering members.
public enum SortField
{
    Name = 0,
    InstalledAt = 1,
    LastPlayed = 2,
}

public enum SortDirection
{
    Ascending = 0,
    Descending = 1,
}

/// <param name="Field">What to sort by.</param>
/// <param name="Direction">
/// For dates, ascending means oldest first. Games without a date always go last.
/// </param>
public readonly record struct SortOrder(SortField Field, SortDirection Direction)
{
    public static SortOrder Default { get; } = new(SortField.Name, SortDirection.Ascending);
}

public static class GameSorter
{
    public static IReadOnlyList<Game> Sort(IEnumerable<Game> games, SortOrder order, CultureInfo? culture = null)
    {
        var titles = TitleComparer(culture);
        int CompareTitles(Game a, Game b) => titles.Compare(a.Title, b.Title);

        var list = games.ToList();
        var sign = order.Direction == SortDirection.Ascending ? 1 : -1;

        Comparison<Game> comparison = order.Field switch
        {
            SortField.Name => (a, b) => sign * CompareTitles(a, b),
            SortField.InstalledAt => (a, b) => CompareDates(a.InstalledAt, b.InstalledAt, sign) is var c and not 0 ? c : CompareTitles(a, b),
            SortField.LastPlayed => (a, b) => CompareDates(a.LastPlayedAt, b.LastPlayedAt, sign) is var c and not 0 ? c : CompareTitles(a, b),
            _ => throw new ArgumentOutOfRangeException(nameof(order)),
        };

        // List.Sort is unstable; the title tie-breaker keeps the order deterministic.
        list.Sort(comparison);
        return list;
    }

    /// <summary>How titles sort: by the culture's rules, case and accents aside.</summary>
    public static IComparer<string> TitleComparer(CultureInfo? culture = null) =>
        (culture ?? CultureInfo.CurrentCulture).CompareInfo.GetStringComparer(CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);

    private static int CompareDates(DateTimeOffset? a, DateTimeOffset? b, int sign) => (a, b) switch
    {
        (null, null) => 0,
        (null, _) => 1,
        (_, null) => -1,
        _ => sign * a.Value.CompareTo(b.Value),
    };
}
