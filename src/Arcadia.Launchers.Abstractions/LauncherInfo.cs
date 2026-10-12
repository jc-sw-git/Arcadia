namespace Arcadia.Launchers.Abstractions;

/// <summary>
/// A launcher Arcadia can connect to. Every connected launcher gets its own tab in the library.
/// </summary>
/// <param name="Id">Stable identifier, used as part of <see cref="Game.Key"/> and in settings.</param>
/// <param name="DisplayName">Name shown on the tab.</param>
/// <param name="SearchKeyword">Default keyword for the search prefix, e.g. <c>/epic</c>.</param>
public sealed record LauncherInfo(string Id, string DisplayName, string SearchKeyword);
