using Arcadia.App.Settings;

namespace Arcadia.App.ViewModels;

/// <summary>
/// What the detail page's areas hold for one game (#499): the rows of "Controller" and "Verwalten" built from the same
/// <see cref="GameActionViewModel.For"/> as the context menu, so both offer the same actions, and the value line of each
/// area. A row the game doesn't offer now is still built, greyed out with its reason, so an area keeps its rows (and the
/// focus its place) while the carousel turns or the game's state changes.
/// </summary>
public static class DetailAreas
{
    /// <summary>Line icons on a 24 × 24 grid, from the design (RD6Mischung).</summary>
    public const string OverviewIcon = "M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z";

    public const string ControllerIcon = "M8.5 7h7a5.5 5.5 0 0 1 0 11h-7a5.5 5.5 0 0 1 0-11zM8 11v3M6.5 12.5h3M15.5 11.5h.01M17.5 13.5h.01";

    public const string ManageIcon = "M4 7h9M17 7h3M4 17h3M11 17h9M15 5v4M9 15v4";

    public static IReadOnlyList<DetailRowViewModel> ControllerRows(
        GameTileViewModel tile, IReadOnlyList<GameActionViewModel> actions, IGameActionHost host, Action<DetailRowViewModel> point)
    {
        var game = tile.Game;
        var notInstalled = game.IsInstalled ? null : AppTexts.DetailSetAfterInstall;
        return
        [
            new(GameActionKind.ControllerMode, AppTexts.DetailControllerModeTitle, AppTexts.DetailControllerModeSubtitle, AppTexts.DetailControllerModeInfo,
                Find(actions, GameActionKind.ControllerMode), notInstalled ?? host.ControllerModeHiddenReason, host.ControllerModeOf(game), point),
            new(GameActionKind.MouseMode, AppTexts.DetailMouseModeTitle, AppTexts.DetailMouseModeSubtitle, AppTexts.DetailMouseModeInfo,
                Find(actions, GameActionKind.MouseMode), notInstalled ?? AppTexts.DetailMouseModeNotPerGame,
                GameMouseModes.Of(host.GameMouseModeOf(game), host.DefaultGameMouseModeAllowed), point),
        ];
    }

    public static IReadOnlyList<DetailRowViewModel> ManageRows(
        GameTileViewModel tile, IReadOnlyList<GameActionViewModel> actions, Action<DetailRowViewModel> point) =>
    [
        new(GameActionKind.Artwork, AppTexts.ActionChangeArtwork, AppTexts.DetailArtworkSubtitle, AppTexts.DetailArtworkInfo,
            Find(actions, GameActionKind.Artwork), AppTexts.ActionNotYetAvailable, value: null, point),
        new(GameActionKind.Uninstall, AppTexts.ActionUninstallMore, AppTexts.DetailUninstallSubtitle(tile.LauncherName), AppTexts.DetailUninstallInfo(tile.LauncherName),
            Find(actions, GameActionKind.Uninstall), AppTexts.NotInstalled, value: null, point),
    ];

    /// <summary>"Gestern · 38 Std.", or only when it was last played while the launcher reports no playtime.</summary>
    public static string OverviewLine(GameTileViewModel tile) =>
        tile.Game.Playtime is null ? tile.LastPlayedText : AppTexts.DetailOverviewLine(lastPlayed: tile.LastPlayedText, playtime: tile.PlaytimeText);

    /// <summary>"Modus „Arcadia“": the Steam Controller mode, the area's main value.</summary>
    public static string ControllerLine(IReadOnlyList<DetailRowViewModel> rows) =>
        AppTexts.DetailControllerLine(rows.First(row => row.Kind == GameActionKind.ControllerMode).ValueText);

    private static GameActionViewModel? Find(IReadOnlyList<GameActionViewModel> actions, GameActionKind kind) =>
        actions.FirstOrDefault(action => action.Kind == kind);
}
