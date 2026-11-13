using Arcadia.App.Settings;
using Arcadia.Core.Settings;
using Avalonia.Media;

namespace Arcadia.App.ViewModels;

/// <summary>
/// What the detail page's areas hold for one game (#499): the rows of "Controller" and "Verwalten" built from the same
/// <see cref="GameActionViewModel.For"/> as the context menu, so both offer the same actions, and the value line of each
/// area. A row the game doesn't offer now is still built, greyed out with its reason, so an area keeps its rows (and the
/// focus its place) while the carousel turns or the game's state changes. "Übersicht" shows a quick selection of these rows
/// (#500), stored by <see cref="QuickIdOf"/>.
/// </summary>
public static class DetailAreas
{
    /// <summary>Line icons on a 24 × 24 grid, from the design (RD6Mischung).</summary>
    public const string OverviewIcon = "M4 4h7v7H4zM13 4h7v7h-7zM4 13h7v7H4zM13 13h7v7h-7z";

    public const string InfosIcon = "M5 6h14M5 11h14M5 16h9";

    public const string ControllerIcon = "M8.5 7h7a5.5 5.5 0 0 1 0 11h-7a5.5 5.5 0 0 1 0-11zM8 11v3M6.5 12.5h3M15.5 11.5h.01M17.5 13.5h.01";

    public const string ChangeTitleIcon = "M4 20h4L19 9l-4-4L4 16zM13.5 6.5l4 4";

    public const string ResetTitleIcon = "M4 9h11a5 5 0 0 1 0 10H8M8 5L4 9l4 4";

    public const string ManageIcon = "M4 7h9M17 7h3M4 17h3M11 17h9M15 5v4M9 15v4";

    /// <summary>
    /// The quick selection of a game never changed (#500): every entry there is today. An entry a later feature adds here
    /// reaches only such games, and only while no selection was applied to all games.
    /// </summary>
    public static IReadOnlyList<string> QuickDefaults { get; } =
        [QuickIdOf(GameActionKind.ControllerMode), QuickIdOf(GameActionKind.MouseMode), QuickIdOf(GameActionKind.Artwork), QuickIdOf(GameActionKind.Uninstall)];

    /// <summary>
    /// The id a row is stored by in <c>settings.json</c>: written out, not the enum's number, so a renamed or reordered kind
    /// keeps every player's selection.
    /// </summary>
    public static string QuickIdOf(GameActionKind kind) => kind switch
    {
        GameActionKind.ControllerMode => "controller-mode",
        GameActionKind.MouseMode => "mouse-mode",
        GameActionKind.Artwork => "artwork",
        GameActionKind.ChangeTitle => "change-title",
        GameActionKind.ResetTitle => "reset-title",
        GameActionKind.Uninstall => "uninstall",
        _ => kind.ToString(),
    };

    /// <summary>The icon beside a quick selection entry (#500), from the design; a kind without its own gets the sliders.</summary>
    public static Geometry QuickIconOf(GameActionKind kind) => QuickIcons.Value.GetValueOrDefault(kind) ?? Sliders.Value;

    private static readonly Lazy<IReadOnlyDictionary<GameActionKind, Geometry>> QuickIcons = new(() => new Dictionary<GameActionKind, Geometry>
    {
        [GameActionKind.ControllerMode] = StreamGeometry.Parse(ControllerIcon),
        [GameActionKind.MouseMode] = StreamGeometry.Parse("M6 3.5l12.5 7-5.5 1.8-1.8 5.7z"),
        [GameActionKind.Artwork] = StreamGeometry.Parse("M3.5 4.5h17v15h-17zM20.5 16l-5-5-8 8.5M9 10h.01"),
        [GameActionKind.ChangeTitle] = StreamGeometry.Parse(ChangeTitleIcon),
        [GameActionKind.ResetTitle] = StreamGeometry.Parse(ResetTitleIcon),
        [GameActionKind.Uninstall] = StreamGeometry.Parse("M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3"),
    });

    private static readonly Lazy<Geometry> Sliders = new(() => StreamGeometry.Parse(ManageIcon));

    /// <summary>
    /// The quick selection: the rows of the other areas whose ids are chosen, in the order of the areas whatever the order
    /// of ticking, at most five. An id no row has (an entry a later version removed) drops out silently.
    /// </summary>
    public static IReadOnlyList<DetailRowViewModel> QuickRows(IReadOnlyList<string> chosen, IEnumerable<DetailRowViewModel> rowsInAreaOrder) =>
        [.. rowsInAreaOrder.Where(row => chosen.Contains(row.QuickId, StringComparer.Ordinal)).Take(QuickSelectionSettings.MaxEntries)];

    public static IReadOnlyList<DetailRowViewModel> ControllerRows(
        GameTileViewModel tile, IReadOnlyList<GameActionViewModel> actions, IGameActionHost host, Action<DetailRowViewModel> point)
    {
        var game = tile.Game;
        var notInstalled = game.IsInstalled ? null : AppTexts.DetailSetAfterInstall;
        var defaultAllowed = host.DefaultGameMouseModeAllowed;
        return
        [
            new(GameActionKind.ControllerMode, AppTexts.DetailControllerModeTitle, AppTexts.DetailControllerModeSubtitle, AppTexts.DetailControllerModeInfo,
                Find(actions, GameActionKind.ControllerMode), notInstalled ?? host.ControllerModeHiddenReason, host.ControllerModeOf(game), point)
            {
                ValueBoxes =
                [
                    new(host.DefaultControllerModeOf(game), AppTexts.DetailControllerModeDefaultInfo),
                    new(ControllerModes.Arcadia, AppTexts.DetailControllerModeArcadiaInfo),
                    new(ControllerModes.Steam, AppTexts.DetailControllerModeSteamInfo),
                ],
            },
            new(GameActionKind.MouseMode, AppTexts.DetailMouseModeTitle, AppTexts.DetailMouseModeSubtitle, infoText: string.Empty,
                Find(actions, GameActionKind.MouseMode), notInstalled ?? AppTexts.DetailMouseModeNotPerGame,
                GameMouseModes.Of(host.GameMouseModeOf(game), defaultAllowed), point)
            {
                ValueBoxes =
                [
                    new(GameMouseModes.Of(null, defaultAllowed), AppTexts.DetailMouseModeDefaultInfo),
                    new(GameMouseModes.Allowed, AppTexts.DetailMouseModeAllowedInfo),
                    new(GameMouseModes.Blocked, AppTexts.DetailMouseModeBlockedInfo),
                ],
            },
        ];
    }

    public static IReadOnlyList<DetailRowViewModel> ManageRows(
        GameTileViewModel tile, IReadOnlyList<GameActionViewModel> actions, Action<DetailRowViewModel> point) =>
    [
        new(GameActionKind.Artwork, AppTexts.ActionChangeArtwork, AppTexts.DetailArtworkSubtitle, AppTexts.DetailArtworkInfo,
            Find(actions, GameActionKind.Artwork), AppTexts.DetailSetAfterInstall, value: null, point),
        new(GameActionKind.ChangeTitle, AppTexts.ActionChangeTitle, AppTexts.DetailChangeTitleSubtitle, AppTexts.DetailChangeTitleInfo,
            Find(actions, GameActionKind.ChangeTitle), greyedReason: null, value: null, point),
        new(GameActionKind.ResetTitle, AppTexts.ActionResetTitle, AppTexts.DetailResetTitleSubtitle, AppTexts.DetailResetTitleInfo,
            Find(actions, GameActionKind.ResetTitle), greyedReason: null, value: null, point),
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
