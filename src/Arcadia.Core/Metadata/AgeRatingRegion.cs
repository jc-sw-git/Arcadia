using System.Globalization;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Metadata;

/// <summary>
/// Which age rating comes first (#557): the board of the system's region, USK in Germany and Austria, ESRB in the USA
/// and Canada, PEGI everywhere else. The others follow in the order USK, PEGI, ESRB.
/// </summary>
public static class AgeRatingRegion
{
    /// <summary>The first board of the system's region, read once (Windows: "Land oder Region"; else the regional format's).</summary>
    public static AgeRatingBoard System { get; } = FirstBoardOf(SystemRegion());

    /// <summary>The first board of a region by its two-letter ISO code; an unknown or missing one gives PEGI.</summary>
    public static AgeRatingBoard FirstBoardOf(string? region) => region?.ToUpperInvariant() switch
    {
        "DE" or "AT" => AgeRatingBoard.Usk,
        "US" or "CA" => AgeRatingBoard.Esrb,
        _ => AgeRatingBoard.Pegi,
    };

    /// <summary>The boards in the order they show: <paramref name="first"/>, then the others in the order USK, PEGI, ESRB.</summary>
    public static IReadOnlyList<AgeRatingBoard> Order(AgeRatingBoard first) =>
        [first, .. Enum.GetValues<AgeRatingBoard>().Where(board => board != first)];

    /// <summary><paramref name="ratings"/> sorted by <see cref="Order"/>; ratings of one board keep their order.</summary>
    public static IReadOnlyList<T> Ordered<T>(IEnumerable<T> ratings, Func<T, AgeRatingBoard> boardOf, AgeRatingBoard first) =>
        [.. ratings.OrderBy(rating => RankOf(boardOf(rating), first))];

    // The enum's own order is USK, PEGI, ESRB; the region's board goes before all of them.
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
