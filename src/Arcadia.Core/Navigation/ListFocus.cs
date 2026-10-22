namespace Arcadia.Core.Navigation;

/// <summary>
/// The focus in a column of rows that the controller moves up and down, e.g. the settings page (#15). It stops at
/// both ends, as the library does; rows may come and go while it stands on one.
/// </summary>
public static class ListFocus
{
    /// <summary>The row <paramref name="delta"/> rows away, held at the first and the last one. -1 for an empty list.</summary>
    public static int Move(int index, int delta, int count) => count == 0 ? -1 : Math.Clamp(index + delta, 0, count - 1);

    /// <summary>
    /// Where the focus goes after the rows changed: to the row with the key it stood on, wherever that is now; if that
    /// row is gone, to the same place (clamped), so it doesn't jump to the top. -1 for an empty list.
    /// </summary>
    public static int Keep(string? focusedKey, int previousIndex, IReadOnlyList<string> keys)
    {
        if (focusedKey is not null)
        {
            for (var i = 0; i < keys.Count; i++)
            {
                if (keys[i] == focusedKey)
                {
                    return i;
                }
            }
        }

        return keys.Count == 0 ? -1 : Math.Clamp(previousIndex, 0, keys.Count - 1);
    }

    /// <summary>The next of <paramref name="count"/> tabs, round from the last to the first like the launcher tabs.</summary>
    public static int Cycle(int index, int delta, int count) => count == 0 ? -1 : (((index + delta) % count) + count) % count;
}
