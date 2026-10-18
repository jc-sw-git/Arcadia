namespace Arcadia.Core.Search;

/// <summary>
/// The hits on screen: <see cref="Size"/> of them from <see cref="Start"/>, laid out in rows of
/// <see cref="RowLength"/>. Single steps move through the hits and stop at the ends; LB/RB turn
/// a whole window and wrap around, so after the last window comes the first. In a grid the
/// window starts on a row, so the columns stay where they are while it moves.
/// </summary>
/// <param name="Start">The first hit shown.</param>
/// <param name="Size">Hits shown at once; a multiple of <paramref name="RowLength"/>.</param>
/// <param name="RowLength">Hits per row; one row is the whole window in the single-row layout.</param>
/// <param name="Count">All hits.</param>
public readonly record struct HitWindow(int Start, int Size, int RowLength, int Count)
{
    /// <summary>The first window over <paramref name="count"/> hits.</summary>
    public static HitWindow Of(int count, int rows, int rowLength) => new(0, rows * rowLength, rowLength, count);

    /// <summary>More hits than fit: LB/RB turn, and the neighbours beyond the edges show faded.</summary>
    public bool CanTurn => Count > Size;

    /// <summary>
    /// The start of the last window. In a single row it shows the last hits in full; in a grid it
    /// is the row that brings the last hit into view, so its last row may be partly empty.
    /// </summary>
    public int LastStart => Count <= Size ? 0 : ((Count - Size + Step - 1) / Step) * Step;

    /// <summary>The hit just before the window, wrapping round from the first to the last; null when everything fits.</summary>
    public int? Before => CanTurn ? (Start > 0 ? Start - 1 : Count - 1) : null;

    /// <summary>The hit just after the window, wrapping round from the last to the first; null when everything fits.</summary>
    public int? After => CanTurn ? (Start + Size < Count ? Start + Size : 0) : null;

    /// <summary>
    /// How far a step past the window's edge moves it: one hit in a single row, a row in a grid,
    /// so the columns stay put.
    /// </summary>
    private int Step => Size == RowLength ? 1 : RowLength;

    /// <summary>The same window over <paramref name="count"/> hits (a new search, a rescan), kept in range.</summary>
    public HitWindow WithCount(int count)
    {
        var resized = this with { Count = count };
        return resized with { Start = Math.Clamp(Start, 0, resized.LastStart) };
    }

    /// <summary>The same position with a different layout, e.g. when the on-screen keyboard shows or hides.</summary>
    public HitWindow WithLayout(int rows, int rowLength, int focus)
    {
        var relaid = new HitWindow(0, rows * rowLength, rowLength, Count);
        return relaid.Showing(focus);
    }

    /// <summary>
    /// The next (<paramref name="direction"/> 1) or previous (-1) window, wrapping at the ends.
    /// <paramref name="focus"/> keeps its place in the window where the new one has a hit there.
    /// </summary>
    public (HitWindow Window, int Focus) Turn(int direction, int focus)
    {
        if (!CanTurn)
        {
            return (this, focus);
        }

        var start = direction > 0
            ? (Start >= LastStart ? 0 : Math.Min(Start + Size, LastStart))
            : (Start == 0 ? LastStart : Math.Max(0, Start - Size));
        var turned = this with { Start = start };
        return (turned, Math.Min(start + Math.Max(0, focus - Start), Count - 1));
    }

    /// <summary>
    /// The focus after a step left/right (<paramref name="columns"/>) or up/down (<paramref name="rows"/>),
    /// and the window moved just far enough to show it. Steps stop at the first and last hit; up
    /// and down only move where there is a row.
    /// </summary>
    public (HitWindow Window, int Focus) Move(int focus, int columns, int rows)
    {
        if (Count == 0)
        {
            return (this, 0);
        }

        var target = Math.Clamp(focus + columns, 0, Count - 1);
        if (rows != 0)
        {
            var row = (focus / RowLength) + rows;
            var lastRow = (Count - 1) / RowLength;
            target = row < 0 || row > lastRow ? focus : Math.Min(focus + (rows * RowLength), Count - 1);
        }

        return (Showing(target), target);
    }

    /// <summary>The mouse wheel: the window moves by <paramref name="steps"/> without wrapping.</summary>
    public HitWindow Scroll(int steps) => this with { Start = Math.Clamp(Start + (steps * Step), 0, LastStart) };

    /// <summary>True if hit <paramref name="index"/> is on screen.</summary>
    public bool Contains(int index) => index >= Start && index < Start + Size && index < Count;

    /// <summary>The window moved by whole steps until hit <paramref name="index"/> is in it.</summary>
    public HitWindow Showing(int index)
    {
        var start = Start;
        if (index < start)
        {
            start -= (start - index + Step - 1) / Step * Step;
        }
        else if (index >= start + Size)
        {
            start += (index - (start + Size) + Step) / Step * Step;
        }

        return this with { Start = Math.Clamp(start, 0, LastStart) };
    }
}
