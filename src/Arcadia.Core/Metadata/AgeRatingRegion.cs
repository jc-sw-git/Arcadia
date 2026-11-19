using System.Globalization;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Metadata;

/// <summary>
/// Which age rating comes first (#557): the board of the system's region, USK in Germany and Austria, ESRB in the USA
/// and Canada, PEGI everywhere else. The others follow in the order USK, PEGI, ESRB, then Steam's own rating for Germany
/// (#682): an official board's rating comes before a store's. Steam's rating for Germany shows only when the system's
/// region is Germany: Steam sets it only for its German store (not Austria or Switzerland), so elsewhere it would be a
/// rating the player's own Steam never shows.
/// </summary>
public static class AgeRatingRegion
{
    // Read once (Windows: "Land oder Region"; else the regional format's); a change applies after the restart.
    private static readonly string? Region = SystemRegion();

    /// <summary>The first board of the system's region.</summary>
    public static AgeRatingBoard System { get; } = FirstBoardOf(Region);

    /// <summary>Whether Steam's own rating for Germany shows on this system: only when its region is Germany.</summary>
    public static bool SystemShowsSteamGermany { get; } = ShowsSteamGermanyIn(Region);

    /// <summary>Whether a region by its two-letter ISO code shows Steam's own rating for Germany: only Germany does.</summary>
    public static bool ShowsSteamGermanyIn(string? region) => string.Equals(region, "DE", StringComparison.OrdinalIgnoreCase);

    /// <summary>The first board of a region by its two-letter ISO code; an unknown or missing one gives PEGI.</summary>
    public static AgeRatingBoard FirstBoardOf(string? region) => region?.ToUpperInvariant() switch
    {
        "DE" or "AT" => AgeRatingBoard.Usk,
        "US" or "CA" => AgeRatingBoard.Esrb,
        _ => AgeRatingBoard.Pegi,
    };

    /// <summary>
    /// The boards in the order they show: <paramref name="first"/>, then the others in the order USK, PEGI, ESRB, Steam DE;
    /// Steam DE only when <paramref name="showsSteamGermany"/>.
    /// </summary>
    public static IReadOnlyList<AgeRatingBoard> Order(AgeRatingBoard first, bool showsSteamGermany) =>
    [
        .. new[] { first }.Concat(Enum.GetValues<AgeRatingBoard>().Where(board => board != first))
            .Where(board => showsSteamGermany || board != AgeRatingBoard.SteamGermany),
    ];

    /// <summary><paramref name="ratings"/> sorted by <see cref="Order"/>; ratings of one board keep their order.</summary>
    public static IReadOnlyList<T> Ordered<T>(IEnumerable<T> ratings, Func<T, AgeRatingBoard> boardOf, AgeRatingBoard first) =>
        [.. ratings.OrderBy(rating => RankOf(boardOf(rating), first))];

    // The enum's own order is USK, PEGI, ESRB, Steam DE; the region's board goes before all of them.
    private static int RankOf(AgeRatingBoard board, AgeRatingBoard first) => board == first ? -1 : (int)board;

    private static string? SystemRegion()
    {
        try
        {
            // .NET reads the user's home location ("Land oder Region") on Windows, the regional format's region elsewhere.
            return RegionInfo.CurrentRegion.TwoLetterISORegionName;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
