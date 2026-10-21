namespace Arcadia.Core.Search;

/// <summary>A key of the on-screen keyboard as the focus sees it.</summary>
/// <param name="Centre">Horizontal centre relative to the keyboard's middle.</param>
/// <param name="IsEmpty">Types nothing on the shown level; the focus passes over it.</param>
public readonly record struct KeySpot(double Centre, bool IsEmpty);

/// <summary>
/// Moving the focus over the on-screen keyboard's rows of differing length: left and right go to the
/// next key in the row, up and down to the key nearest in position in the next row. Keys that are
/// empty on the shown level (AltGr: most of them) are passed over, like on Steam's keyboard (owner,
/// 2026-10-21), and so are rows with no key to go to. At the keyboard's edges the focus stays.
/// </summary>
public static class KeyGrid
{
    private const double TieBreak = 0.01;

    /// <summary>The next key to the left (<paramref name="step"/> -1) or right (+1); the same key at the edge.</summary>
    public static int Sideways(IReadOnlyList<KeySpot> row, int column, int step)
    {
        for (var i = column + step; i >= 0 && i < row.Count; i += step)
        {
            if (!row[i].IsEmpty)
            {
                return i;
            }
        }

        return column;
    }

    /// <summary>
    /// The key above (<paramref name="step"/> -1) or below (+1) nearest in position; the same key at the edge.
    /// Between two keys equally near (centred rows of 13 and 12 keys meet half a key apart) the one
    /// further right wins going up and the one further left going down, as if each row sat a little
    /// further right than the one above, like on a real keyboard: up from e is 3, down from 3 is e.
    /// </summary>
    public static (int Row, int Column) Vertical(IReadOnlyList<IReadOnlyList<KeySpot>> rows, int row, int column, int step)
    {
        var centre = rows[row][column].Centre - (step * TieBreak);
        for (var r = row + step; r >= 0 && r < rows.Count; r += step)
        {
            if (Nearest(rows[r], centre) is { } nearest)
            {
                return (r, nearest);
            }
        }

        return (row, column);
    }

    private static int? Nearest(IReadOnlyList<KeySpot> row, double centre)
    {
        int? best = null;
        for (var i = 0; i < row.Count; i++)
        {
            if (!row[i].IsEmpty && (best is not { } b || Math.Abs(row[i].Centre - centre) < Math.Abs(row[b].Centre - centre)))
            {
                best = i;
            }
        }

        return best;
    }
}
