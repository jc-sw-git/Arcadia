namespace Arcadia.Core.Launchers;

/// <summary>
/// Which launchers are active and in which order (#136). An active launcher is scanned, watched and shown as a tab; an
/// inactive one is not touched at all. The user's choice is a list of the active launchers in tab order; without one
/// (before the first choice) every launcher found on this PC is active, in the order they were registered.
/// </summary>
public static class LauncherSelection
{
    /// <param name="registered">Every registered launcher, in the order they were registered.</param>
    /// <param name="chosen">The user's active launchers in tab order, or null if the user never chose.</param>
    /// <param name="isFound">Whether the launcher is installed on this PC.</param>
    /// <returns>
    /// The active launchers in tab order. A chosen launcher that isn't found any more stays active, so its tab can say
    /// so; a chosen launcher that isn't registered (e.g. a plugin that is gone) is left out.
    /// </returns>
    public static IReadOnlyList<string> Active(IReadOnlyList<string> registered, IReadOnlyList<string>? chosen, Func<string, bool> isFound) =>
        chosen is null
            ? registered.Where(isFound).ToList()
            : chosen.Where(registered.Contains).Distinct(StringComparer.Ordinal).ToList();

    /// <returns>The launchers that aren't active: first those found on this PC, then the others, each in registered order.</returns>
    public static IReadOnlyList<string> Inactive(IReadOnlyList<string> registered, IReadOnlyList<string> active, Func<string, bool> isFound)
    {
        var inactive = registered.Where(id => !active.Contains(id)).ToList();
        return [.. inactive.Where(isFound), .. inactive.Where(id => !isFound(id))];
    }

    /// <summary>Puts <paramref name="id"/> at <paramref name="index"/> of the active launchers, or takes it out of them.</summary>
    /// <param name="index">The place among the active launchers, counted without <paramref name="id"/>; null makes it inactive.</param>
    public static IReadOnlyList<string> Placed(IReadOnlyList<string> active, string id, int? index)
    {
        var placed = active.Where(other => other != id).ToList();
        if (index is { } at)
        {
            placed.Insert(Math.Clamp(at, 0, placed.Count), id);
        }

        return placed;
    }
}
