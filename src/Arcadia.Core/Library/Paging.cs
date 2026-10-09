namespace Arcadia.Core.Library;

/// <summary>The selectable "games per page" values and the rule for which of them make sense.</summary>
public static class PageSizes
{
    public const int Default = 25;

    public static IReadOnlyList<int> Options { get; } = [10, 25, 50, 100];

    /// <summary>
    /// The smallest option is always allowed. Any larger option is allowed only if the library has
    /// more games than the next smaller option — otherwise the smaller one already shows everything.
    /// </summary>
    public static bool IsAllowed(int size, int gameCount)
    {
        var index = IndexOf(size);
        return index == 0 || (index > 0 && gameCount > Options[index - 1]);
    }

    public static IReadOnlyList<int> Allowed(int gameCount) =>
        Options.Where(size => IsAllowed(size, gameCount)).ToList();

    /// <summary>
    /// The page size to actually use: the preferred one if allowed, otherwise the largest allowed one.
    /// The preference itself is kept, so switching to a bigger library restores it.
    /// </summary>
    public static int Effective(int preferred, int gameCount) =>
        IsAllowed(preferred, gameCount) ? preferred : Allowed(gameCount)[^1];

    private static int IndexOf(int size)
    {
        for (var i = 0; i < Options.Count; i++)
        {
            if (Options[i] == size)
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>One page of a paginated list.</summary>
/// <param name="PageIndex">Zero-based, already clamped to the valid range.</param>
/// <param name="PageCount">At least 1, even for an empty list.</param>
/// <param name="Start">Index of the first item on the page.</param>
/// <param name="Length">Number of items on the page.</param>
public readonly record struct PageWindow(int PageIndex, int PageCount, int Start, int Length)
{
    public static PageWindow For(int itemCount, int pageSize, int requestedPage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

        var pageCount = Math.Max(1, (itemCount + pageSize - 1) / pageSize);
        var pageIndex = Math.Clamp(requestedPage, 0, pageCount - 1);
        var start = pageIndex * pageSize;
        var length = Math.Max(0, Math.Min(pageSize, itemCount - start));
        return new PageWindow(pageIndex, pageCount, start, length);
    }
}
