using Arcadia.App.Settings;
using Arcadia.Core.Navigation;
using Arcadia.Launchers.Abstractions;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

public enum GameActionKind
{
    Play,
    Details,
    Favorite,
    Artwork,
    OpenInLauncher,
    ControllerMode,
    MouseMode,
    Uninstall,
}

/// <summary>What the actions of a game do; implemented by <see cref="GameActions"/>.</summary>
public interface IGameActionHost
{
    /// <summary>True when the Steam Controller mode matters (option on, Steam Controller connected).</summary>
    bool ShowControllerMode { get; }

    /// <summary>Pan's emulation is on but doesn't run (#242): the mode stays "Steam-Eingabe", greyed out with a triangle.</summary>
    bool ControllerModeBroken { get; }

    /// <summary>[A] on the greyed mode: the controller settings on the row that says why (#242).</summary>
    void ShowPanProblem();

    /// <summary>"Standard (…)", "Arcadia" or "Steam-Eingabe" (<see cref="ControllerModes.Of"/>).</summary>
    string ControllerModeOf(Game game);

    void Play(GameTileViewModel tile);

    void ShowDetails(GameTileViewModel tile);

    void OpenInLauncher(GameTileViewModel tile);

    /// <summary>Why the mode doesn't show now: no Steam Controller, or Pan off (#499); null while it shows.</summary>
    string? ControllerModeHiddenReason { get; }

    /// <summary>Standard → Arcadia → Steam-Eingabe → Standard (#178); a negative <paramref name="delta"/> the other way (#499).</summary>
    void StepControllerMode(GameTileViewModel tile, int delta);

    /// <summary>The game's mode after the next [A], named as <see cref="ControllerModeOf"/> names it.</summary>
    string NextControllerModeOf(Game game);

    /// <summary>"Standard" with what it means for the game's launcher, e.g. "Standard (Arcadia)", for the detail page's info box (#506).</summary>
    string DefaultControllerModeOf(Game game);

    /// <summary>True when the mouse mode is "Von Hand" with "Beenden des Mausmodus in Spielen erzwingen" on "Individuell".</summary>
    bool ShowGameMouseMode { get; }

    /// <summary>The game's "Individueller Mausmodus": allowed, forbidden, or null for "Standard".</summary>
    bool? GameMouseModeOf(Game game);

    /// <summary>What "Standard" means now: allowed or forbidden.</summary>
    bool DefaultGameMouseModeAllowed { get; }

    /// <summary>Standard → Erlaubt → Deaktiviert → Standard; a negative <paramref name="delta"/> the other way (#499).</summary>
    void StepGameMouseMode(GameTileViewModel tile, int delta);

    void Uninstall(GameTileViewModel tile);

    /// <summary>"Artwork ändern" (#490): the modal for the game's cover and background.</summary>
    void ChangeArtwork(GameTileViewModel tile);
}

/// <summary>
/// One action on a game. The context menu lists them, the detail page shows them as buttons;
/// both get them from <see cref="For"/>, so they always offer the same.
/// </summary>
public sealed partial class GameActionViewModel : ViewModelBase
{
    private static string NotYetAvailable => AppTexts.ActionNotYetAvailable;

    /// <summary>Why a game's setting that applies when it starts can't be changed while it runs (#95).</summary>
    public static string NextStartOnly => AppTexts.ActionNextStartOnly;

    private readonly Action _invoke;

    // ◀ ▶ on a value (#499): steps it back or on; null for an action.
    private Action<int>? _step;

    private GameActionViewModel(GameActionKind kind, string label, string? note, bool isEnabled, Action invoke, string? buttonLabel = null)
    {
        Kind = kind;
        Label = label;
        ButtonLabel = buttonLabel ?? label;
        Note = note;
        IsEnabled = isEnabled;
        _invoke = invoke;
        InvokeCommand = new RelayCommand(Invoke, () => IsEnabled);
    }

    public GameActionKind Kind { get; }

    [ObservableProperty]
    public partial string Label { get; private set; }

    /// <summary>The shorter text of the detail page's button.</summary>
    [ObservableProperty]
    public partial string ButtonLabel { get; private set; }

    /// <summary>A small heading above the button text, e.g. "STEAM CONTROLLER" above the mode.</summary>
    public string? Caption => Kind switch
    {
        GameActionKind.ControllerMode => AppTexts.SteamControllerGroup,
        GameActionKind.MouseMode => AppTexts.IndividualMouseModeCaption,
        _ => null,
    };

    public bool HasCaption => Caption is not null;

    public bool IsPrimary => Kind == GameActionKind.Play;

    /// <summary>A hint below the label, or why the action isn't available.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    public partial string? Note { get; private set; }

    public bool HasNote => !string.IsNullOrEmpty(Note);

    /// <summary>
    /// Why the action isn't available, for the detail page's tooltip. The focus skips such a button, so only the
    /// mouse can ask (#127); an available button explains itself.
    /// </summary>
    public string? DisabledReason => IsEnabled && !IsBroken ? null : Note;

    public bool IsEnabled { get; }

    /// <summary>The Steam Controller mode while Pan's emulation doesn't run (#242): greyed out with a triangle, but focusable.</summary>
    public bool IsBroken { get; private set; }

    public bool IsDanger => Kind == GameActionKind.Uninstall;

    /// <summary>The menu draws a line above it.</summary>
    public bool StartsGroup => Kind == GameActionKind.Uninstall;

    /// <summary>Switching a mode keeps the menu open, so the new mode can be seen.</summary>
    public bool KeepsMenuOpen => Kind is GameActionKind.ControllerMode or GameActionKind.MouseMode;

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    public IRelayCommand InvokeCommand { get; }

    /// <summary>Stroke icon in a 24×24 box.</summary>
    public Geometry Icon => StreamGeometry.Parse(Kind switch
    {
        GameActionKind.Play => "M7 4.5v15l13-7.5z",
        GameActionKind.Details => "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18M12 11v6M12 7.5v.5",
        GameActionKind.Favorite => "M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.8L12 16.8l-5.2 2.8 1-5.8-4.3-4.1 5.9-.8z",
        GameActionKind.Artwork => "M5.5 4.5h13a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2v-11a2 2 0 0 1 2-2M20.5 16l-5-5-8 8.5M9 8.2a1.8 1.8 0 1 0 0 3.6a1.8 1.8 0 1 0 0-3.6",
        GameActionKind.OpenInLauncher => "M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5",
        GameActionKind.ControllerMode => "M4 8h12M13 5l3 3-3 3M20 16H8M11 13l-3 3 3 3",
        GameActionKind.MouseMode => "M12 3.5a5.5 5.5 0 0 0-5.5 5.5v6a5.5 5.5 0 0 0 11 0V9A5.5 5.5 0 0 0 12 3.5M6.5 10h11M12 3.5V10",
        _ => "M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3",
    });

    public void Invoke()
    {
        if (IsEnabled)
        {
            _invoke();
        }
    }

    /// <summary>A value with ◀ ▶ (the game's modes), not an action.</summary>
    public bool IsChoice => _step is not null;

    /// <summary>◀ ▶ on the detail page (#499): the value one step back or on; nothing while it can't change.</summary>
    public void Step(int delta)
    {
        if (IsEnabled && !IsBroken)
        {
            _step?.Invoke(delta);
        }
    }

    /// <summary>The actions for <paramref name="tile"/> in menu order; actions that don't apply are left out.</summary>
    public static IReadOnlyList<GameActionViewModel> For(GameTileViewModel tile, IGameActionHost host)
    {
        var game = tile.Game;
        var launcher = tile.LauncherName;
        var busy = tile.InstallState is not null;
        var actions = new List<GameActionViewModel>();

        // While an installation, update or uninstall runs, its status takes the place of the primary action. A running
        // game is brought back to the front; one being started says so again instead of starting twice (#95).
        // A waiting game stays enabled: A opens the launcher's install dialog again, no launcher reports a cancelled
        // one (#343).
        actions.Add(tile.InstallState is { Phase: InstallPhase.AwaitingConfirmation }
            ? new(GameActionKind.Play, tile.InstallStatusText, null, true, () => host.Play(tile))
            : busy
            ? new(GameActionKind.Play, tile.InstallStatusText, tile.ShowLauncherControlsHint ? AppTexts.ActionPauseAndCancelIn(launcher) : null, false, () => { })
            : !game.IsInstalled
                ? game.InstallBlockedReason is { } blocked
                    ? new(GameActionKind.Play, AppTexts.Install, blocked, false, () => { })
                    : new(GameActionKind.Play, AppTexts.Install, AppTexts.ActionInstallOpensIn(launcher), true, () => host.Play(tile))
                : tile.IsRunningOrStarting
                    ? new(GameActionKind.Play, tile.PrimaryActionText, null, true, () => host.Play(tile))
                    : new(GameActionKind.Play, tile.PrimaryActionText, tile.UpdateAvailable ? AppTexts.ActionLauncherMayUpdate(launcher) : null, true, () => host.Play(tile)));

        actions.Add(new(GameActionKind.Details, AppTexts.ActionShowDetails, null, true, () => host.ShowDetails(tile)));
        actions.Add(new(GameActionKind.Favorite, AppTexts.ActionAddToFavorites, NotYetAvailable, false, () => { }, AppTexts.ActionToFavorites));
        if (game.IsInstalled)
        {
            actions.Add(new(GameActionKind.Artwork, AppTexts.ActionChangeArtworkMore, null, true, () => host.ChangeArtwork(tile), AppTexts.ActionChangeArtwork));
        }

        actions.Add(game.OpenInLauncher is null
            ? new(GameActionKind.OpenInLauncher, AppTexts.ActionOpenIn(launcher), AppTexts.ActionLauncherNotOpenable(launcher), false, () => { })
            : new(GameActionKind.OpenInLauncher, AppTexts.ActionOpenIn(launcher), null, true, () => host.OpenInLauncher(tile)));

        if (!game.IsInstalled)
        {
            return actions;
        }

        // The Steam Controller mode decides at the game's start how Pan hands it the controller, so a change applies to the next
        // start only and waits until the game ended (#95). The mouse mode's choice applies at once and stays open.
        if (host.ShowControllerMode)
        {
            GameActionViewModel? mode = null;
            void StepMode(int delta)
            {
                host.StepControllerMode(tile, delta);
                mode!.ShowControllerMode(host.ControllerModeOf(game), host.NextControllerModeOf(game));
            }

            // Broken, it stays focusable: [A] leads to the row that says why (#242).
            mode = new(GameActionKind.ControllerMode, string.Empty, null, host.ControllerModeBroken || !tile.IsRunningOrStarting, () =>
            {
                if (host.ControllerModeBroken)
                {
                    host.ShowPanProblem();
                    return;
                }

                StepMode(1);
            });
            mode._step = StepMode;
            mode.ShowControllerMode(host.ControllerModeOf(game), host.NextControllerModeOf(game));
            mode.IsBroken = host.ControllerModeBroken;
            if (host.ControllerModeBroken)
            {
                mode.Note = ControllerModes.PanNotRunning;
            }
            else if (tile.IsRunningOrStarting)
            {
                mode.Note = NextStartOnly;
            }

            actions.Add(mode);
        }

        if (host.ShowGameMouseMode)
        {
            GameActionViewModel? mouse = null;
            void StepMouse(int delta)
            {
                host.StepGameMouseMode(tile, delta);
                mouse!.ShowGameMouseMode(host.GameMouseModeOf(game), host.DefaultGameMouseModeAllowed);
            }

            mouse = new(GameActionKind.MouseMode, string.Empty, null, true, () => StepMouse(1));
            mouse._step = StepMouse;
            mouse.ShowGameMouseMode(host.GameMouseModeOf(game), host.DefaultGameMouseModeAllowed);
            actions.Add(mouse);
        }

        actions.Add(busy
            ? new(GameActionKind.Uninstall, AppTexts.ActionUninstallMore, AppTexts.ActionAfterInstall, false, () => { })
            : tile.IsRunningOrStarting
                ? new(GameActionKind.Uninstall, AppTexts.ActionUninstallMore, AppTexts.ActionAfterGameEnded, false, () => { })
                : game.Uninstall is not null
                    ? new(GameActionKind.Uninstall, AppTexts.ActionUninstallMore, null, true, () => host.Uninstall(tile))
                    : game.OpenInLauncher is not null
                        ? new(GameActionKind.Uninstall, AppTexts.ActionUninstallMore, AppTexts.ActionOnlyInLauncherOpens(launcher), true, () => host.Uninstall(tile))
                        : new(GameActionKind.Uninstall, AppTexts.ActionUninstallMore, AppTexts.ActionOnlyInLauncher(launcher), false, () => { }));

        return actions;
    }

    private void ShowGameMouseMode(bool? choice, bool defaultAllowed)
    {
        var mode = GameMouseModes.Of(choice, defaultAllowed);
        Label = AppTexts.ActionMouseMode(mode);
        ButtonLabel = mode;
        Note = AppTexts.ActionSwitchesTo(GameMouseModes.Of(GameMouseModes.Next(choice), defaultAllowed));
    }

    private void ShowControllerMode(string mode, string next)
    {
        Label = AppTexts.ActionSteamControllerMode(mode);
        ButtonLabel = mode;
        Note = AppTexts.ActionSwitchesTo(next);
    }
}

/// <summary>The names of the two Steam Controller modes (#92, renamed with #213).</summary>
public static class ControllerModes
{
    /// <summary>Arcadia's Xbox pad; Steam gets nothing from the Steam Controller.</summary>
    public const string Arcadia = "Arcadia";

    /// <summary>No pad of Arcadia's; the game's own Steam configuration applies.</summary>
    public static string Steam => AppTexts.ControllerModeSteam;

    /// <summary>Why the mode is greyed out while Pan's emulation is on but doesn't run (#242); [A] leads to the reason.</summary>
    public static string PanNotRunning => AppTexts.ActionPanNotRunning;

    public static string Of(bool useArcadiaPad) => useArcadiaPad ? Arcadia : Steam;

    /// <summary>A game's mode; "Standard" names its launcher's default, e.g. "Standard (Arcadia)" (#178, like the mouse mode's).</summary>
    public static string Of(bool? choice, bool launcherUsesArcadiaPad) => choice is { } own ? Of(own) : AppTexts.ControllerModeDefault(Of(launcherUsesArcadiaPad));

    /// <summary>Standard → Arcadia → Steam-Eingabe → Standard.</summary>
    public static bool? Next(bool? choice) => ThreeWayChoice.Step(choice, 1);
}

/// <summary>
/// A game's own choice out of three, Standard (null) → true → false (the Steam Controller mode, "Individueller Mausmodus"):
/// ◀ ▶ step through it and go round at the ends like every value (#499).
/// </summary>
public static class ThreeWayChoice
{
    private static readonly bool?[] Order = [null, true, false];

    /// <param name="held">A held ◀ or ▶ repeating (#476): it stops at the end, only a fresh press goes round.</param>
    public static bool? Step(bool? choice, int delta, bool held = false) =>
        Order[ValueCycle.Step(Array.IndexOf(Order, choice), delta, 0, Order.Length - 1, held)];
}
