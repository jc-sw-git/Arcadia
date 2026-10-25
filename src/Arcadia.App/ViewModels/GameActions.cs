using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.ViewModels;

/// <summary>What <see cref="GameActions"/> needs from the window around it; it knows no pages (#155).</summary>
public interface IGameActionsOwner
{
    /// <summary>True when the Steam Controller mode matters (option on, Steam Controller connected).</summary>
    bool ShowControllerMode { get; }

    /// <summary>True when games show their "Individueller Mausmodus".</summary>
    bool ShowGameMouseMode { get; }

    /// <summary>The install folders of the library's games, where "Zurück ins Spiel" looks for the game's window.</summary>
    GameFolders GameFolders { get; }

    /// <summary>Opens the detail page of the game: held A, or "Details anzeigen".</summary>
    void ShowDetails(GameTileViewModel tile);

    /// <summary>Asks before something that can't be undone.</summary>
    void Confirm(ConfirmationViewModel confirmation);

    void CycleGameMouseMode(Game game);

    /// <summary>A game is starting or its start failed: every shown cover takes its run state again.</summary>
    void RunStatesChanged();
}

/// <summary>
/// What a game can do wherever it is shown (library, detail page, search): start, install, back to the running game, hand
/// over to its launcher, and the hold of A, ≡ or the mouse button on its cover (tapped starts, held opens the details).
/// Pages pass the game; the window around it is reached only through <see cref="IGameActionsOwner"/>.
/// </summary>
/// <remarks>
/// One per window, created and owned by <see cref="MainViewModel"/> and shared by every page: it holds the one press in
/// progress and its timers, so pages never create their own. UI thread only; the hold's timers post back to it. Main
/// drops the hold when the page on top changes or the window loses its focus (see <see cref="IPage"/>).
/// </remarks>
public sealed class GameActions : IGameActionHost
{
    /// <summary>A shorter press of A starts the game; the ring around the preview fills from here on.</summary>
    private static readonly TimeSpan HoldFeedbackDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>Holding A this long opens the detail page. The ring's animation in Theme.axaml lasts the difference.</summary>
    private static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(1);

    private readonly IGameActionsOwner _owner;
    private readonly GameLaunchService _launcher;
    private readonly ControllerSupportService _controllerSupport;
    private readonly InstallTracker _installs;
    private readonly RunningGamesTracker _running;
    private readonly LaunchedGames _launchedGames;
    private readonly SettingsService _settings;
    private readonly ToastViewModel _toast;
    private readonly TimeProvider _time;
    private readonly HoldGesture _hold;
    private GameTileViewModel? _holdTile;

    // A in the library, ≡ or Enter on a hit in the search: the button whose release ends the hold; null for the left mouse button.
    private UiAction? _holdAction = UiAction.Accept;

    public GameActions(
        IGameActionsOwner owner,
        GameLaunchService launcher,
        ControllerSupportService controllerSupport,
        InstallTracker installs,
        RunningGamesTracker running,
        LaunchedGames launchedGames,
        SettingsService settings,
        ToastViewModel toast,
        TimeProvider time)
    {
        _owner = owner;
        _launcher = launcher;
        _controllerSupport = controllerSupport;
        _installs = installs;
        _running = running;
        _launchedGames = launchedGames;
        _settings = settings;
        _toast = toast;
        _time = time;

        _hold = new HoldGesture(HoldFeedbackDelay, HoldTime, time, action => Avalonia.Threading.Dispatcher.UIThread.Post(action));
        // The tile has the focus already: the press started on it. A click only picks the cover; a double click starts it.
        _hold.Tapped += () => WithHoldTile(tile =>
        {
            if (_holdAction is not null)
            {
                Start(tile);
            }
        });
        _hold.Held += () => WithHoldTile(owner.ShowDetails);
        _hold.FeedbackChanged += holding => WithHoldTile(tile => tile.IsHolding = holding);
    }

    /// <summary>Raised when Arcadia should bring its window back to the front.</summary>
    public event EventHandler? ForegroundRequested;

    /// <summary>Tapped starts the game, held opens the details; both are decided by the release of <paramref name="button"/> or the timer.</summary>
    public void Press(GameTileViewModel tile, UiAction? button)
    {
        _holdTile = tile;
        _holdAction = button;
        _hold.Press();
    }

    /// <summary>Any other button pressed meanwhile drops the hold.</summary>
    public void Pressed(UiAction action)
    {
        if (action != _holdAction)
        {
            _hold.Cancel();
        }
    }

    public void Release(UiAction action)
    {
        if (action == _holdAction)
        {
            _hold.Release();
        }
    }

    /// <summary>A mouse button was let go anywhere.</summary>
    public void ReleasePointer()
    {
        if (_holdAction is null)
        {
            _hold.Release();
        }
    }

    /// <summary>The page changed under the press, or its release may never arrive: no late start, no details.</summary>
    public void CancelHold() => _hold.Cancel();

    /// <summary>
    /// A rescan can rebuild the page or the hits while A is down, e.g. right after a game ended. The
    /// press moves to the same game's new tile; only a game that's no longer <paramref name="shown"/> drops it.
    /// </summary>
    public void KeepHoldOn(IEnumerable<GameTileViewModel> shown)
    {
        if (_holdTile is not { } held)
        {
            return;
        }

        _holdTile = shown.FirstOrDefault(t => t.Game.Key == held.Game.Key);
        if (_holdTile is null)
        {
            _hold.Cancel();
            return;
        }

        _holdTile.IsHolding = held.IsHolding;
    }

    private void WithHoldTile(Action<GameTileViewModel> action)
    {
        if (_holdTile is { } tile)
        {
            action(tile);
        }
    }

    /// <summary>
    /// Starts the game, or installs it, or brings it back to the front while it runs, or says why none of that is
    /// possible right now.
    /// </summary>
    public void Start(GameTileViewModel tile)
    {
        if (tile.InstallState is { } install)
        {
            _toast.Show(install.Phase == InstallPhase.AwaitingConfirmation
                ? $"Die Installation von „{tile.Title}“ wartet auf deine Bestätigung in {tile.LauncherName}."
                : install.Phase == InstallPhase.Uninstalling
                    ? $"„{tile.Title}“ wird gerade deinstalliert."
                    : $"„{tile.Title}“ wird bereits {(tile.IsInstalled ? "aktualisiert" : "installiert")} – Pausieren und Abbrechen in {tile.LauncherName}.");
            return;
        }

        switch (_running.StateOf(tile.Game.Key)?.Phase)
        {
            case RunPhase.Starting:
                // A second A must not start the game twice (#95).
                _toast.Show($"„{tile.Title}“ wird bereits gestartet …");
                return;
            case RunPhase.Running:
                ReturnToGameAsync(tile).Forget("Zurück ins Spiel");
                return;
        }

        if (!tile.IsInstalled)
        {
            Install(tile);
            return;
        }

        LaunchAsync(tile).Forget("Spielstart");
    }

    private async Task LaunchAsync(GameTileViewModel tile)
    {
        _running.Starting(tile.Game.Key);
        _owner.RunStatesChanged();
        var progress = new Progress<LaunchStage>(stage => OnLaunchStage(tile, stage));

        // Games that use Steam Input themselves would get every input twice while SISR runs.
        var result = await _launcher.TryStartAsync(tile.Game, async token =>
        {
            _launchedGames.Starting(tile.Game.Key, await Task.Run(ProcessWatch.RunningProcessIds, token));
            await _controllerSupport.BeginGameAsync(tile.Game, token);
        }, progress);
        if (result.Outcome != LaunchOutcome.Requested)
        {
            StartFailed(tile);
        }

        switch (result)
        {
            case { Outcome: LaunchOutcome.Failed, Error: { } error }:
                _toast.Show($"„{tile.Title}“ konnte nicht gestartet werden: {error.Message}");
                break;
            case { Outcome: LaunchOutcome.FailedUnexpectedly }:
                _toast.Show($"„{tile.Title}“ konnte wegen eines unerwarteten Fehlers nicht gestartet werden – Details stehen in arcadia.log.");
                break;
            case { Outcome: LaunchOutcome.AlreadyLaunching }:
                _toast.Show($"Ein anderes Spiel wird gerade gestartet – „{tile.Title}“ bitte gleich noch einmal starten.");
                break;
        }
    }

    /// <summary>The game shows "Spielen" again.</summary>
    private void StartFailed(GameTileViewModel tile)
    {
        _running.StartFailed(tile.Game.Key);
        _owner.RunStatesChanged();
    }

    /// <summary>
    /// "Zurück ins Spiel" (#95): the game's window comes to the front. Arcadia is in front itself, so Windows lets it hand
    /// the foreground on. A game without a window of its own (it is still loading, or runs in the tray) is named in a toast.
    /// </summary>
    private async Task ReturnToGameAsync(GameTileViewModel tile)
    {
        var key = tile.Game.Key;
        var folders = _owner.GameFolders;
        var window = await Task.Run(() => GameWindow.Find(key, folders, _launchedGames));
        if (window == 0)
        {
            AppLog.Write($"Zurück ins Spiel: kein Fenster von {key} gefunden");
            _toast.Show($"Arcadia findet kein Fenster von „{tile.Title}“ – vielleicht startet es noch oder läuft ohne Fenster.");
            return;
        }

        var shown = GameWindow.BringToFront(window);
        AppLog.Write($"Zurück ins Spiel: {key} {(shown ? "vorne" : "ließ sich nicht nach vorne holen")}");
        if (!shown)
        {
            _toast.Show($"„{tile.Title}“ ließ sich nicht nach vorne holen.");
        }
    }

    private void Install(GameTileViewModel tile)
    {
        try
        {
            if (_launcher.Install(tile.Game))
            {
                tile.InstallState = _installs.Started(tile.Game);
                _toast.Show($"Installation von „{tile.Title}“ wird in {tile.LauncherName} geöffnet …");
            }
            else
            {
                _toast.Show(tile.Game.Install is LaunchTarget.None
                    ? $"Demo-Spiel: Hier würde jetzt „{tile.Title}“ installiert."
                    : $"„{tile.Title}“ kann nur direkt in {tile.LauncherName} installiert werden.");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _toast.Show($"Installation von „{tile.Title}“ konnte nicht geöffnet werden: {ex.Message}");
        }
    }

    private void OnLaunchStage(GameTileViewModel tile, LaunchStage stage)
    {
        switch (stage)
        {
            case LaunchStage.StartingLauncher:
                _toast.Show($"{tile.LauncherName} wird im Hintergrund gestartet …", sticky: true);
                Task.Delay(TimeSpan.FromSeconds(2), _time).ContinueWith(
                    _ => ForegroundRequested?.Invoke(this, EventArgs.Empty),
                    TaskScheduler.FromCurrentSynchronizationContext()).Forget("Fenster nach vorn holen");
                break;
            case LaunchStage.LauncherNotReady:
                _toast.Show($"{tile.LauncherName} reagiert nicht – „{tile.Title}“ wird trotzdem angefragt …");
                ForegroundRequested?.Invoke(this, EventArgs.Empty);
                break;
            case LaunchStage.LauncherReady:
                // Starting a launcher can steal the foreground; take it back before the game opens.
                ForegroundRequested?.Invoke(this, EventArgs.Empty);
                break;
            case LaunchStage.Starting:
                _toast.Show(tile.UpdateAvailable ? $"„{tile.Title}“ wird gestartet – {tile.LauncherName} installiert ggf. vorher das Update …" : $"„{tile.Title}“ wird gestartet …");
                break;
            case LaunchStage.NotStartable:
                StartFailed(tile);
                _toast.Show($"Demo-Spiel: Hier würde jetzt „{tile.Title}“ gestartet.");
                break;
            case LaunchStage.NotConfirmed:
                StartFailed(tile);
                _toast.Show($"„{tile.Title}“ ist nicht gestartet – bitte in {tile.LauncherName} nachsehen.");
                break;
        }
    }

    /// <summary>Hands one of the game's launcher actions to its launcher and says so.</summary>
    private void HandOver(GameTileViewModel tile, LaunchTarget? target, string handedOver, string demo)
    {
        try
        {
            _toast.Show(_launcher.Open(target) ? handedOver
                : target is LaunchTarget.None ? demo
                : $"{tile.LauncherName} bietet das für „{tile.Title}“ nicht an.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _toast.Show($"{tile.LauncherName} konnte nicht geöffnet werden: {ex.Message}");
        }
    }

    public string ControllerModeOf(Game game) =>
        _controllerSupport.Settings.ControllerSupportFor(game) ? ControllerModes.Sisr : ControllerModes.Steam;

    bool IGameActionHost.ShowControllerMode => _owner.ShowControllerMode;

    void IGameActionHost.Play(GameTileViewModel tile) => Start(tile);

    void IGameActionHost.ShowDetails(GameTileViewModel tile) => _owner.ShowDetails(tile);

    void IGameActionHost.OpenInLauncher(GameTileViewModel tile) =>
        HandOver(tile, tile.Game.OpenInLauncher, $"{tile.LauncherName} wird geöffnet …", $"Demo-Spiel: Hier würde jetzt {tile.LauncherName} geöffnet.");

    void IGameActionHost.ToggleControllerMode(GameTileViewModel tile) =>
        _controllerSupport.SetModeFor(tile.Game, !_controllerSupport.Settings.ControllerSupportFor(tile.Game));

    void IGameActionHost.Uninstall(GameTileViewModel tile)
    {
        if (tile.Game.Uninstall is not { } uninstall)
        {
            // Only the launcher's own window can do it.
            HandOver(tile, tile.Game.OpenInLauncher, $"„{tile.Title}“ lässt sich nur in {tile.LauncherName} deinstallieren – {tile.LauncherName} wird geöffnet …", $"Demo-Spiel: Hier würde jetzt {tile.LauncherName} geöffnet.");
            return;
        }

        _owner.Confirm(new ConfirmationViewModel(
            $"„{tile.Title}“ deinstallieren?",
            $"{tile.LauncherName} entfernt das Spiel von diesem PC und fragt eventuell selbst noch einmal nach. Installieren lässt es sich danach jederzeit wieder.",
            "Deinstallieren",
            () => HandOver(tile, uninstall, $"Deinstallation von „{tile.Title}“ wird in {tile.LauncherName} geöffnet …", $"Demo-Spiel: Hier würde jetzt „{tile.Title}“ deinstalliert.")));
    }

    bool IGameActionHost.ShowGameMouseMode => _owner.ShowGameMouseMode;

    bool? IGameActionHost.GameMouseModeOf(Game game) => _settings.Current.MouseMode.GameChoiceFor(game.Key);

    bool IGameActionHost.DefaultGameMouseModeAllowed => _settings.Current.MouseMode.PerGameDefaultAllowed;

    void IGameActionHost.CycleGameMouseMode(GameTileViewModel tile) => _owner.CycleGameMouseMode(tile.Game);
}
