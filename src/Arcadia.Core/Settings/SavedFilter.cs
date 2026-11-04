using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Settings;

/// <summary>
/// A tab's filter as <c>settings.json</c> holds it (#44). By name, not by number: a value this version doesn't know (a
/// newer Arcadia, a hand-edited file) drops out on its own instead of breaking the file or meaning something else.
/// </summary>
/// <param name="Installed">An <see cref="InstalledChoice"/>; null for "Alle".</param>
/// <param name="Types">The chosen <see cref="GameType"/>s; null or empty for "Alle Typen".</param>
public sealed record SavedFilter(string? Installed = null, IReadOnlyList<string>? Types = null)
{
    public GameFilter ToFilter() => GameFilter.Of(
        Parse<InstalledChoice>(Installed) ?? InstalledChoice.All,
        (Types ?? []).Select(Parse<GameType>).Where(type => type is not null).Select(type => type!.Value));

    /// <summary>Null for no filter: the tab's entry can go.</summary>
    public static SavedFilter? From(GameFilter filter) => filter.IsActive
        ? new SavedFilter(
            filter.Installed == InstalledChoice.All ? null : filter.Installed.ToString(),
            filter.Types.Count == 0 ? null : filter.Types.Select(type => type.ToString()).ToList())
        : null;

    // Only a member's own name: Enum.TryParse would also take "7" or "Game, Tool".
    private static T? Parse<T>(string? name)
        where T : struct, Enum =>
        name is not null && Enum.TryParse<T>(name, ignoreCase: false, out var value) && Enum.IsDefined(value) && value.ToString() == name ? value : null;
}
