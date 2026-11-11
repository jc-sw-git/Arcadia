namespace Arcadia.Core.Search;

/// <summary>A key of the on-screen keyboard as the focus sees it, measured in keys (a row is one key high).</summary>
/// <param name="Centre">Horizontal centre, measured from the same point in every row.</param>
/// <param name="IsEmpty">Types nothing on the shown level, or is greyed out or left out; the focus passes over it.</param>
/// <param name="Key">
/// Names a key that stands in two rows (the tall ISO Enter) with the same number in both, so the focus treats
/// both parts as one key; -1 for a key of one row.
/// </param>
/// <param name="Width">The key's width in this row, including the gap to its neighbour.</param>
public readonly record struct KeySpot(double Centre, bool IsEmpty, int Key = -1, double Width = 1)
{
    public double LeftEdge => Centre - (Width / 2);

    public double RightEdge => Centre + (Width / 2);
}

/// <summary>
/// Moving the focus over the on-screen keyboard's rows of differing length. Keys that are empty on the shown
/// level (AltGr: most of them), greyed out or left out are passed over, like on Steam's keyboard (owner,
/// 2026-10-21). Left and right go to the next key in the row; when the row has none in that direction, to the
/// nearest key wholly on that side in another row (from ² on AltGr left to ↹, owner 2026-10-22). Up and down
/// go to the nearest key above or below, counting rows and keys alike, so a far key in the next row doesn't
/// win over one right below in the row after (from ↹ on AltGr down to ⇧, not to ↵). Where nothing is left
/// in that direction a fresh press goes round (#476): left/right to the other end of the row, up/down to the key
/// nearest in position in the bottom or top row; a held one stays.
/// </summary>
public static class KeyGrid
{
    private const double TieBreak = 0.01;

    // Adjacent keys share an edge; sums of quarter keys are exact, this only guards against rounding.
    private const double EdgeSlack = 1e-6;

    /// <summary>
    /// The next key to the left (<paramref name="step"/> -1) or right (+1); at the edge the key at the row's other
    /// end, or with <paramref name="held"/> the same key.
    /// </summary>
    public static (int Row, int Column) Sideways(IReadOnlyList<IReadOnlyList<KeySpot>> rows, int row, int column, int step, bool held = false)
    {
        if (NextInRow(rows[row], column, step) is { } next)
        {
            return (row, next);
        }

        // The tall Enter goes on from its other part too, whichever row the focus came to it in.
        var (first, last) = RowsOf(rows, row, column);
        var spot = rows[row][column];
        for (var r = first; r <= last; r++)
        {
            if (r != row && NextInRow(rows[r], ColumnOf(rows[r], spot.Key), step) is { } other)
            {
                return (r, other);
            }
        }

        (int Row, int Column)? best = null;
        var bestDistance = double.MaxValue;
        for (var r = 0; r < rows.Count; r++)
        {
            if (r >= first && r <= last)
            {
                continue;
            }

            var rowsAway = r < first ? first - r : r - last;
            for (var c = 0; c < rows[r].Count; c++)
            {
                var candidate = rows[r][c];
                var beside = step < 0 ? candidate.RightEdge <= spot.LeftEdge + EdgeSlack : candidate.LeftEdge >= spot.RightEdge - EdgeSlack;
                var distance = Distance(rowsAway, candidate.Centre - spot.Centre);
                if (!candidate.IsEmpty && beside && distance < bestDistance)
                {
                    (best, bestDistance) = ((r, c), distance);
                }
            }
        }

        if (best is null && !held && NextInRow(rows[row], step < 0 ? rows[row].Count : -1, step) is { } wrapped)
        {
            best = (row, wrapped);
        }

        return best ?? (row, column);
    }

    /// <summary>
    /// The key above (<paramref name="step"/> -1) or below (+1) nearest in position; the same key at the edge.
    /// Between two keys equally near (centred rows of 13 and 12 keys meet half a key apart) the one
    /// further right wins going up and the one further left going down, as if each row sat a little
    /// further right than the one above, like on a real keyboard: up from e is 3, down from 3 is e. At the edge
    /// the nearest key counted from beyond the other edge, or with <paramref name="held"/> the same key.
    /// </summary>
    public static (int Row, int Column) Vertical(IReadOnlyList<IReadOnlyList<KeySpot>> rows, int row, int column, int step, bool held = false)
    {
        var centre = rows[row][column].Centre - (step * TieBreak);
        var (first, last) = RowsOf(rows, row, column);
        var best = Nearest(rows, step < 0 ? first : last, step, centre);
        if (best is null && !held)
        {
            // Round: as if the focus stood in a row just beyond the other edge.
            best = Nearest(rows, step < 0 ? rows.Count : -1, step, centre);
        }

        return best ?? (row, column);
    }

    /// <summary>The non-empty key nearest to <paramref name="centre"/> in the rows past <paramref name="from"/> in the direction of <paramref name="step"/>.</summary>
    private static (int Row, int Column)? Nearest(IReadOnlyList<IReadOnlyList<KeySpot>> rows, int from, int step, double centre)
    {
        (int Row, int Column)? best = null;
        var bestDistance = double.MaxValue;
        for (var r = from + step; r >= 0 && r < rows.Count; r += step)
        {
            for (var c = 0; c < rows[r].Count; c++)
            {
                var distance = Distance(r - from, rows[r][c].Centre - centre);
                if (!rows[r][c].IsEmpty && distance < bestDistance)
                {
                    (best, bestDistance) = ((r, c), distance);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// The key the focus may rest on nearest to <paramref name="centre"/> in <paramref name="row"/>, in any row:
    /// where the focus goes when its key empties (AltGr switched on) or the layout changes; null if there is none.
    /// </summary>
    public static (int Row, int Column)? Closest(IReadOnlyList<IReadOnlyList<KeySpot>> rows, int row, double centre)
    {
        (int Row, int Column)? best = null;
        var bestDistance = double.MaxValue;
        for (var r = 0; r < rows.Count; r++)
        {
            for (var c = 0; c < rows[r].Count; c++)
            {
                var distance = Distance(r - row, rows[r][c].Centre - centre);
                if (!rows[r][c].IsEmpty && distance < bestDistance)
                {
                    (best, bestDistance) = ((r, c), distance);
                }
            }
        }

        return best;
    }

    private static int? NextInRow(IReadOnlyList<KeySpot> row, int column, int step)
    {
        for (var i = column + step; i >= 0 && i < row.Count; i += step)
        {
            if (!row[i].IsEmpty)
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>The first and last row the key at <paramref name="row"/>, <paramref name="column"/> stands in.</summary>
    private static (int First, int Last) RowsOf(IReadOnlyList<IReadOnlyList<KeySpot>> rows, int row, int column)
    {
        var key = rows[row][column].Key;
        if (key < 0)
        {
            return (row, row);
        }

        var first = row;
        while (first > 0 && ColumnOf(rows[first - 1], key) >= 0)
        {
            first--;
        }

        var last = row;
        while (last < rows.Count - 1 && ColumnOf(rows[last + 1], key) >= 0)
        {
            last++;
        }

        return (first, last);
    }

    private static int ColumnOf(IReadOnlyList<KeySpot> row, int key)
    {
        for (var i = 0; i < row.Count; i++)
        {
            if (row[i].Key == key)
            {
                return i;
            }
        }

        return -1;
    }

    // Squared: only compared, never shown.
    private static double Distance(double rows, double keys) => (rows * rows) + (keys * keys);
}
