namespace Arcadia.Core.Navigation;

/// <summary>
/// The focus in a list the controller steps through, e.g. the settings page's rows (#15), a menu or the launcher tabs.
/// A step past the end goes round to the other end (#476); rows may come and go while it stands on one.
/// </summary>
public static class ListFocus
{
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

    /// <summary>
    /// The item <paramref name="delta"/> away of <paramref name="count"/>, round from the last to the first and back.
    /// Without a focus (<paramref name="index"/> -1) a step down lands on the first item and a step up on the last.
    /// -1 for an empty list.
    /// </summary>
    /// <param name="held">
    /// The step repeats a held button or key (#476): it stops at the last or first one, and only a fresh press goes round.
    /// </param>
    public static int Cycle(int index, int delta, int count, bool held = false) =>
        count == 0 ? -1
        : index < 0 ? (delta < 0 ? count - 1 : 0)
        : held && (index + delta < 0 || index + delta >= count) ? Math.Clamp(index, 0, count - 1)
        : (((index + delta) % count) + count) % count;

    /// <summary>
    /// Like <see cref="Cycle"/> one step (<paramref name="delta"/> ±1) at a time, passing over the items that can't take
    /// the focus (e.g. a greyed-out menu entry). Stays at <paramref name="index"/> when no other item can take it, or when
    /// a held step would have to go round.
    /// </summary>
    public static int Next(int index, int delta, int count, Func<int, bool> canFocus, bool held = false)
    {
        var at = index;
        for (var tried = 0; tried < count; tried++)
        {
            var next = Cycle(at, delta, count, held);
            if (next == at || next == index)
            {
                return index;
            }

            if (canFocus(next))
            {
                return next;
            }

            at = next;
        }

        return index;
    }
}
