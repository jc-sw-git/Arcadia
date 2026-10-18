using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>
/// The detail page's ring of games: the shown game in the middle, its neighbours to the sides.
/// It wraps around at both ends, so before the first game comes the last.
/// </summary>
public static class Carousel
{
    /// <summary>
    /// The index of the game <paramref name="offset"/> places away from <paramref name="index"/>.
    /// Null when there is nothing to show there: a single game has no neighbours. With two
    /// games the other one stands on both sides.
    /// </summary>
    public static int? At(int index, int offset, int count)
    {
        if (count == 0 || (count == 1 && offset != 0))
        {
            return null;
        }

        return (((index + offset) % count) + count) % count;
    }

    /// <summary>
    /// Where the shown game is in a re-sorted or rescanned list: at its new place, or, if it is
    /// gone, at the same index (clamped). -1 for an empty list.
    /// </summary>
    public static int Find(IReadOnlyList<Game> games, string key, int previousIndex)
    {
        for (var i = 0; i < games.Count; i++)
        {
            if (games[i].Key == key)
            {
                return i;
            }
        }

        return games.Count == 0 ? -1 : Math.Clamp(previousIndex, 0, games.Count - 1);
    }
}
