namespace Arcadia.Launchers.Abstractions;

/// <summary>
/// A launcher Arcadia can connect to. Every connected launcher gets its own tab in the library.
/// </summary>
/// <param name="Id">Stable identifier, used as part of <see cref="Game.Key"/> and in settings.</param>
/// <param name="DisplayName">Name shown on the tab; the user may give it another (#137).</param>
/// <param name="SearchKeyword">Default keyword for the search prefix, e.g. <c>/epic</c>; the user may choose another (#137).</param>
/// <param name="Logo">
/// The launcher's real logo as one-colour SVG path data in a 24 × 24 box (#138). Arcadia draws it in the text colour of
/// where it stands, dark on a light tab and light on a dark one, so it needs no variants; null shows the name's first
/// letter instead. Use the maker's logo unchanged and as its brand guidelines allow.
/// </param>
/// <param name="GamesUseSteamInput">
/// The launcher's games read the Steam Controller through Steam Input themselves, as Steam's do (#156). Their Steam
/// Controller mode then starts on "Steam-Eingabe" instead of "Arcadia"; Arcadia's pad would double their input.
/// </param>
/// <param name="OwnsSteamInput">
/// The launcher's program is Steam Input itself and holds the Steam Controller, as Steam does: Arcadia closes it
/// (<see cref="ILauncherIntegration.Program"/>) for a driver install that restarts the controller, drops its desktop
/// layout and shows its settings on this launcher's page. At most one launcher should; Arcadia uses the first.
/// </param>
public sealed record LauncherInfo(
    string Id, string DisplayName, string SearchKeyword, string? Logo = null, bool GamesUseSteamInput = false, bool OwnsSteamInput = false);
