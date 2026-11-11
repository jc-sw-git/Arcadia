namespace Arcadia.Core.Navigation;

/// <summary>
/// The focus in a grid of items cut into pages, e.g. the library's pages or the search's hits (one page there). A step
/// past the last item goes round to the first and back; up/down past a page's edge go to the neighbouring page's row
/// in the same column, from the last page round to the first (#476). A held step stops at the end of all items; only
/// a fresh press goes round.
/// </summary>
public static class GridFocus
{
    /// <summary>The item a step from <paramref name="index"/> lands on; <paramref name="index"/> if it goes nowhere.</summary>
    /// <param name="index">The focused item, counted over all pages.</param>
    /// <param name="count">All items.</param>
    /// <param name="pageSize">Items per page, pages start at multiples of it; a multiple of <paramref name="columns"/> unless there is one page.</param>
    /// <param name="columns">Items per row.</param>
    /// <param name="columnStep">-1 left, 1 right, 0 for an up/down step.</param>
    /// <param name="rowStep">-1 up, 1 down, 0 for a left/right step.</param>
    /// <param name="held">The step repeats a held button or key.</param>
    public static int Step(int index, int count, int pageSize, int columns, int columnStep, int rowStep, bool held)
    {
        if (count == 0 || index < 0 || index >= count)
        {
            return index;
        }

        if (columnStep != 0)
        {
            var target = index + columnStep;
            return target >= 0 && target < count ? target
                : held ? index
                : target < 0 ? count - 1 : 0;
        }

        if (rowStep == 0)
        {
            return index;
        }

        var page = index / pageSize;
        var pageCount = ((count - 1) / pageSize) + 1;
        var onPage = index - (page * pageSize);
        var lastRow = (Length(page, count, pageSize) - 1) / columns;
        var row = onPage / columns;
        var column = onPage % columns;

        // A single row on a single page has nowhere to go up or down.
        if (pageCount == 1 && lastRow == 0)
        {
            return index;
        }

        if (rowStep > 0 && row < lastRow)
        {
            // Down onto a partial last row where the column is empty: the last item.
            return Math.Min(index + columns, (page * pageSize) + Length(page, count, pageSize) - 1);
        }

        if (rowStep < 0 && row > 0)
        {
            return index - columns;
        }

        var next = page + Math.Sign(rowStep);
        if (next < 0 || next >= pageCount)
        {
            if (held)
            {
                return index;
            }

            next = next < 0 ? pageCount - 1 : 0;
        }

        // Down enters the next page in its top row, up the previous one in its bottom row; same column, or the last
        // item if that row is shorter.
        var length = Length(next, count, pageSize);
        var targetRow = rowStep > 0 ? 0 : (length - 1) / columns;
        return (next * pageSize) + Math.Min((targetRow * columns) + column, length - 1);
    }

    private static int Length(int page, int count, int pageSize) => Math.Min(pageSize, count - (page * pageSize));
}
