using System.ComponentModel;
using Arcadia.App.Keyboard;
using Arcadia.App.Search;
using Arcadia.App.ViewModels;
using Arcadia.App.Views;
using Arcadia.Core.Library;
using Arcadia.Core.Search;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Arcadia.Platform;
using Avalonia.Threading;

namespace Arcadia.App.Services;

/// <summary>
/// Wires the mouse mode (#64) into Arcadia: it follows every poll of the controllers (also with
/// Arcadia in the background), shows its feedback over the screen, rumbles the controller that
/// switched, and runs the automation from the settings on Arcadia's focus changes. Y opens Arcadia's
/// on-screen keyboard over the window in front (#110, <see cref="OnScreenKeyboardViewModel"/>), or with the
/// search in front takes over the search's keyboard. Beside
/// it, View held brings Arcadia back (<see cref="ReturnWithView"/>), and Steam's desktop layout is
/// dropped where the settings ask for it (<see cref="IDesktopInputFilter"/>).
/// </summary>
public sealed class MouseModeHost : IDisposable
{
    /// <summary>While Arcadia switched the mode on by itself, this often it checks whether a game came to the front.</summary>
    private static readonly TimeSpan ForegroundCheckInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Arcadia must stay in the background this long before the mode switches on by itself, so a focus change of a
    /// moment (a dialog of Windows, a window flashing up) doesn't flash "Mausmodus an" and "aus".
    /// </summary>
    private static readonly TimeSpan FocusLossSettleTime = TimeSpan.FromMilliseconds(300);

    /// <summary>This often Arcadia checks whether it is in front and how much of Steam's desktop layout to drop.</summary>
    private static readonly TimeSpan DesktopFilterCheckInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>How the foreground log names Arcadia itself.</summary>
    private const string ArcadiaInFront = "Arcadia";

    private readonly IGamepadSource _gamepad;
    private readonly SettingsService _settings;
    private readonly MainWindow _window;
    private readonly MainViewModel _viewModel;
    private readonly MouseMode _mode;
    private readonly MouseModeAutomation _automation;
    private readonly ReturnWithView _returnWithView;
    private readonly ControllerActivity _activity;
    private readonly IDesktopInputFilter _desktopFilter;
    private readonly MouseModeOverlay _overlay = new();
    private readonly IMouseKeyboardOutput _output = MouseKeyboardOutput.CreateDefault();
    private readonly TimeProvider _time;

    // Made the first time Y opens it, so a session without it costs nothing.
    private OnScreenKeyboardViewModel? _keyboard;
    private MouseModeKeyboardWindow? _keyboardWindow;

    // The keyboard the buttons go to while it is open: the window's, or the search's with Arcadia in front.
    private OnScreenKeyboardViewModel? _openKeyboard;
    private SearchViewModel? _typingSearch;

    // A game was in front at the last check; one coming to the front closes the keyboard.
    private bool _gameWasInFront;
    private readonly DispatcherTimer _foregroundCheck;
    private readonly DispatcherTimer _focusLossSettle;
    private readonly DispatcherTimer _desktopFilterCheck;

    // Arcadia's window is active by its own events; decides "in front" only where Windows can't tell.
    private volatile bool _arcadiaActive;

    // The automation's view of the foreground (UI thread), and Arcadia's window once it has one (read on the poll thread).
    private bool _ownsForeground;
    private nint _windowHandle;
    private DesktopFilterScope _desktopFilterScope;
    private string? _loggedForeground;
    private bool? _loggedActive;
    private int? _lastForegroundId;
    private GameFolders? _lastForegroundFolders;
    private string? _lastForegroundGame;

    // Only the poll thread reads these two.
    private MouseModeSettings? _optionsFrom;
    private MouseModeOptions? _options;
    private ReturnWithViewSettings? _optionsReturnFrom;
    private ReturnWithViewSettings? _returnOptionsFrom;
    private ReturnWithViewOptions? _returnOptions;

    // Set on the poll thread after an unexpected error; the mode stays off until Arcadia restarts.
    private volatile bool _failed;

    // A game in front forbids the mouse mode (UpdateGameBlock, UI thread); read on the poll thread.
    private volatile bool _blockedByGame;
    private bool _optionsBlocked;

    // A web page opened by controller holds the mode on (#280), and opened the keyboard if it wasn't open yet.
    private bool _heldForWebPage;
    private bool _keyboardForWebPage;

    public MouseModeHost(IGamepadSource gamepad, SettingsService settings, MainWindow window, MainViewModel viewModel, TimeProvider time)
    {
        _gamepad = gamepad;
        _settings = settings;
        _window = window;
        _viewModel = viewModel;
        _time = time;
        _mode = new MouseMode(_output, CurrentOptions, time);
        // "Automatisch" always ends when Arcadia comes to the front; "Von Hand" only with its option.
        _automation = new MouseModeAutomation(
            _mode,
            () => _settings.Current.MouseMode is { Activation: MouseModeActivation.Automatic } or { Activation: MouseModeActivation.Manual, EndWhenArcadiaInFront: true },
            () => _settings.Current.MouseMode.Activation == MouseModeActivation.Automatic);
        _returnWithView = new ReturnWithView(CurrentReturnOptions, () => IsArcadiaActive, time);
        _activity = new ControllerActivity(time);
        _desktopFilter = DesktopInputFilter.CreateDefault(_activity, gamepad, AppLog.Write);
        _arcadiaActive = window.IsActive;
        _ownsForeground = window.IsActive;
        _foregroundCheck = new DispatcherTimer(ForegroundCheckInterval, DispatcherPriority.Background, (_, _) => CheckForeground());
        _foregroundCheck.Stop();
        _focusLossSettle = new DispatcherTimer(FocusLossSettleTime, DispatcherPriority.Background, (_, _) => OnFocusLossSettled());
        _focusLossSettle.Stop();
        _desktopFilterCheck = new DispatcherTimer(DesktopFilterCheckInterval, DispatcherPriority.Background, (_, _) =>
        {
            LogForegroundChange();
            CheckArcadiaInFront();
            UpdateGameBlock();
            UpdateDesktopFilter();
            FollowForegroundWithKeyboard();
        });

        _mode.HoldStarted += fillTime => Dispatcher.UIThread.Post(() => _overlay.ShowHold(fillTime));
        _mode.HoldCancelled += () => Dispatcher.UIThread.Post(_overlay.CancelHold);
        _mode.Switched += OnSwitched;
        _mode.KeyboardOpened += () => Dispatcher.UIThread.Post(OpenKeyboard);
        _mode.KeyboardButtonChanged += (button, down) => Dispatcher.UIThread.Post(() => _openKeyboard?.Handle(button, down));
        // No rumble: over a game in exclusive full screen it would read as "switched".
        _mode.Refused += padId =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                AppLog.Write($"Back + Start an Controller #{padId}: Mausmodus in diesem Spiel deaktiviert");
                _overlay.ShowSwitched("Mausmodus ist in diesem Spiel deaktiviert", flash: false);
            });
        };
        _returnWithView.HoldStarted += fillTime => Dispatcher.UIThread.Post(() => _overlay.ShowReturnHold(fillTime));
        _returnWithView.HoldCancelled += () => Dispatcher.UIThread.Post(_overlay.CancelReturnHold);
        _returnWithView.Requested += () => Dispatcher.UIThread.Post(ReturnToArcadia);
        _gamepad.Polled += OnPolled;
        _gamepad.Lost += OnControllersLost;
        _window.Activated += OnArcadiaActivated;
        _window.Deactivated += OnArcadiaDeactivated;
        _viewModel.PropertyChanged += OnViewModelChanged;
    }

    /// <summary>Gamepad input is the mouse's now, not Arcadia's navigation. Readable from any thread.</summary>
    public bool IsOn => _mode.IsOn;

    /// <summary>A notice over the screen while Arcadia is in the background (#243, <see cref="Input.PanNotices"/>); UI thread.</summary>
    public void ShowNotice(string text) => _overlay.ShowNotice(text);

    /// <summary>
    /// Arcadia is in front and can be seen: its controller input and View's way back depend on it.
    /// Readable from any thread, unlike <c>Window.IsActive</c>. On Windows the foreground window
    /// decides, not the window's activation events: those got out of step while switching between a
    /// game and Arcadia (owner's PC, 2026-10-20), and Arcadia then took the game's controller input as
    /// its own and ignored View held. A game in borderless full screen can also stay above Arcadia
    /// while Arcadia has the foreground, so the window at Arcadia's centre must be Arcadia's too.
    /// </summary>
    public bool IsArcadiaActive => OwnsForeground() && (_windowHandle == 0 || ForegroundGuard.IsUncovered(_windowHandle));

    /// <summary>
    /// A Windows dialog Arcadia opened from the controller opens (true) or closed (#138): with "Bei System-Dialogfenstern
    /// einschalten" the mode comes on for it and goes off after it, if it came on for it. Nothing with "Mausmodus: Aus".
    /// </summary>
    /// <returns>Whether the mode was asked to come on, so the caller lets go of it after the dialog.</returns>
    public bool HoldForDialog(bool open)
    {
        if (open && _settings.Current.MouseMode is not { Activation: not MouseModeActivation.Off, ForDialogs: true })
        {
            return false;
        }

        AppLog.Write(open ? "Windows-Dialog offen: Mausmodus für ihn" : "Windows-Dialog zu");
        _mode.HoldForDialog(open);
        return true;
    }

    /// <summary>
    /// A launcher's web page opened (#280, e.g. Steam's sign-in): it is used with the mouse and keyboard, so when the
    /// controller opened it, the mode comes on for it, whatever the settings say, with its keyboard open; after the page
    /// the mode goes off again if it came on for it (owner, 2026-10-27).
    /// </summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsWebSignInOpen))
        {
            return;
        }

        if (_viewModel.IsWebSignInOpen && _viewModel.UsesController && !_heldForWebPage)
        {
            _heldForWebPage = true;
            _keyboardForWebPage = !_mode.IsKeyboardOpen;
            AppLog.Write("Webseite per Controller geöffnet: Mausmodus und Bildschirmtastatur für sie");
            _mode.HoldForDialog(true);
            if (_keyboardForWebPage)
            {
                _mode.OpenKeyboard();
            }
        }
        else if (!_viewModel.IsWebSignInOpen && _heldForWebPage)
        {
            _heldForWebPage = false;
            if (_keyboardForWebPage)
            {
                CloseKeyboard("Webseite zu");
            }

            _mode.HoldForDialog(false);
        }
    }

    /// <summary>Call after the gamepad source stopped polling: lets go of a held mouse button or key.</summary>
    public void Dispose()
    {
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _gamepad.Polled -= OnPolled;
        _gamepad.Lost -= OnControllersLost;
        _foregroundCheck.Stop();
        _focusLossSettle.Stop();
        _desktopFilterCheck.Stop();
        _mode.Stop();
        _desktopFilter.Dispose();
        _overlay.Close();
        _keyboard?.Dispose();
        _keyboardWindow?.Close();
    }

    private void OnPolled(IReadOnlyList<GamepadReading> pads)
    {
        if (_failed)
        {
            return;
        }

        try
        {
            _activity.Note(pads);
            _mode.Update(pads);
            _returnWithView.Update(pads);
        }
        catch (Exception ex)
        {
            // An error here would end the poll thread and with it every controller input; this way only the mouse mode goes.
            _failed = true;
            AppErrors.Report(ex, "Mausmodus");
            try
            {
                _mode.Stop();
            }
            catch (Exception stopError)
            {
                AppLog.Write($"Mausmodus ließ sich nach dem Fehler nicht sauber beenden: {stopError}");
            }
        }
    }

    /// <summary>
    /// No poll will come any more (SDL failed, #72): the mouse mode lets go of its mouse buttons and keys, View's hold
    /// ends, and with no controller left the desktop filter goes at its next check. On the poll thread, like OnPolled.
    /// </summary>
    private void OnControllersLost()
    {
        try
        {
            _mode.Stop();
        }
        finally
        {
            try
            {
                _returnWithView.Update([]);
            }
            finally
            {
                _activity.Note([]);
            }
        }
    }

    /// <summary>Read on every poll; rebuilt only when the settings changed.</summary>
    private MouseModeOptions CurrentOptions()
    {
        var settings = _settings.Current.MouseMode;
        var returnSettings = _settings.Current.ReturnWithView;
        var blocked = _blockedByGame;
        if (_options is null || !ReferenceEquals(settings, _optionsFrom) || !ReferenceEquals(returnSettings, _optionsReturnFrom) || blocked != _optionsBlocked)
        {
            _optionsFrom = settings;
            _optionsReturnFrom = returnSettings;
            _optionsBlocked = blocked;
            _options = new MouseModeOptions(
                settings.Activation != MouseModeActivation.Off,
                TimeSpan.FromSeconds(settings.HoldSeconds),
                settings.PointerSpeed,
                blocked,
                returnSettings.Enabled ? TimeSpan.FromSeconds(returnSettings.HoldSeconds) : null);
        }

        return _options;
    }

    /// <summary>Like <see cref="CurrentOptions"/> for "Zurück zu Arcadia".</summary>
    private ReturnWithViewOptions CurrentReturnOptions()
    {
        var settings = _settings.Current.ReturnWithView;
        if (_returnOptions is null || !ReferenceEquals(settings, _returnOptionsFrom))
        {
            _returnOptionsFrom = settings;
            _returnOptions = new ReturnWithViewOptions(settings.Enabled, TimeSpan.FromSeconds(settings.HoldSeconds));
        }

        return _returnOptions;
    }

    private void ReturnToArcadia()
    {
        CloseKeyboard("Arcadia kommt nach vorne");
        _overlay.EndReturnHold();
        AppLog.Write("View gehalten: Arcadia kommt nach vorne");
        ForegroundGuard.BringBack(_window);
        AppLog.Write($"Nach View gehalten: {DescribeForeground()}, Arcadia {(IsArcadiaActive ? "sichtbar vorne" : "nicht sichtbar vorne")}");
    }

    /// <summary>
    /// Logs every change of the foreground and of Avalonia's activation, so a log shows how Arcadia
    /// came to the front beside a game (Steam's Alt+Tab, a click in the mouse mode, another monitor).
    /// </summary>
    private void LogForegroundChange()
    {
        var foreground = DescribeForeground();
        var active = _window.IsActive;
        if (foreground == _loggedForeground && active == _loggedActive)
        {
            return;
        }

        _loggedForeground = foreground;
        _loggedActive = active;
        var mismatch = active != (foreground == ArcadiaInFront) ? " (widerspricht dem Vordergrund)" : string.Empty;
        AppLog.Write($"Vordergrund: {foreground}; Arcadia laut Fenster {(active ? "aktiv" : "inaktiv")}{mismatch}");
    }

    /// <summary>
    /// Only the kind of window in front, never the program: which programs someone uses is private, and the log only
    /// needs to show how Arcadia and a game took turns (owner, 2026-10-23).
    /// </summary>
    private string DescribeForeground()
    {
        if (ProcessWatch.ForegroundProcessId() is not { } id)
        {
            return "kein Fenster";
        }

        if (id == Environment.ProcessId)
        {
            return ArcadiaInFront;
        }

        return GameInFront() ? "Spiel" : "kein Spiel";
    }

    private void OnSwitched(MouseModeSwitch change)
    {
        if (change.PadId is { } padId)
        {
            _gamepad.Rumble(padId);
        }

        Dispatcher.UIThread.Post(() =>
        {
            var gameInFront = ForegroundGame.IsInFront(_viewModel.Session.GameFolders, _viewModel.Session.LaunchedGames);
            AppLog.Write($"Mausmodus {(change.IsOn ? "an" : "aus")} ({(change.PadId is { } s ? $"Back + Start an Controller #{s}" : "automatisch")}{(gameInFront ? ", Spiel vorne" : string.Empty)})");
            if (change.IsOn)
            {
                // A held A must not open the details while the controller is a mouse.
                _viewModel.CancelHold();
                if (gameInFront)
                {
                    ForegroundGuard.CentrePointerOnForeground();
                }
            }

            var text = (change.IsOn, change.IsAutomatic, gameInFront) switch
            {
                (true, _, _) when change.ForDialog => "Mausmodus an, solange das Windows-Fenster offen ist",
                (false, _, _) when change.ByGame => "Mausmodus aus – in diesem Spiel deaktiviert",
                (true, false, true) => "Mausmodus an – das Spiel sieht die Eingaben weiterhin",
                (true, _, _) => "Mausmodus an",
                _ => "Mausmodus aus",
            };
            _overlay.ShowSwitched(text, flash: !change.IsAutomatic);
            if (!change.IsOn)
            {
                CloseKeyboard("Mausmodus aus");
            }

            UpdateForegroundCheck();
            UpdateDesktopFilter();
        });
    }

    /// <summary>
    /// Y in the mouse mode: the keyboard opens over the window in front, at the edge chosen last unless the
    /// caret would lie under it there. With Arcadia's search in front, its own keyboard takes the buttons instead,
    /// so there are never two (owner, 2026-10-22). Should that fail, the buttons go back to the mouse at once.
    /// </summary>
    private void OpenKeyboard()
    {
        if (!_mode.IsKeyboardOpen)
        {
            return;
        }

        try
        {
            if (IsArcadiaActive && _viewModel is { IsSearchShown: true, IsDialogOpen: false, Search: { } search })
            {
                _typingSearch = search;
                search.KeyboardClosed += OnSearchKeyboardClosed;
                search.StartMouseModeTyping();
                _openKeyboard = search.Keyboard;
                AppLog.Write("Bildschirmtastatur der Suche übernimmt die Tasten");
                return;
            }

            var settings = _settings.Current;
            var layout = KeyboardLayout.Find(settings.KeyboardLayout);
            if (_keyboard is null || _keyboardWindow is null)
            {
                var target = new WindowInFrontTarget(_output, () => CloseKeyboard("B oder Ausblenden"), OnKeyboardDocked);
                _keyboard = new OnScreenKeyboardViewModel(layout, target, _time, action => Dispatcher.UIThread.Post(action), _activity.IsInUse);
                _keyboard.LayoutChosen += chosen => SaveKeyboardSetting(s => s with { KeyboardLayout = chosen.Id });
                _keyboardWindow = new MouseModeKeyboardWindow { DataContext = _keyboard };
            }

            var atTop = _keyboardWindow.Open(settings.MouseMode.KeyboardAtTop);
            _keyboard.Open(layout, atTop);
            _openKeyboard = _keyboard;
            _gameWasInFront = GameInFront();
            AppLog.Write($"Bildschirmtastatur offen ({(atTop ? "oben" : "unten")}, {layout.Code})");
        }
        catch (Exception ex)
        {
            AppErrors.Report(ex, "Bildschirmtastatur öffnen");
            CloseKeyboard("Fehler beim Öffnen");
        }
    }

    /// <summary>Closes the keyboard; the mouse mode goes on with the buttons as the mouse's.</summary>
    private void CloseKeyboard(string reason)
    {
        var wasOpen = _mode.IsKeyboardOpen || _keyboardWindow?.IsVisible == true || _typingSearch is not null;
        _mode.CloseKeyboard();
        _openKeyboard = null;
        if (_typingSearch is { } search)
        {
            _typingSearch = null;
            search.KeyboardClosed -= OnSearchKeyboardClosed;
            search.EndMouseModeTyping();
        }

        _keyboard?.Closed();
        _keyboardWindow?.Hide();
        if (wasOpen)
        {
            AppLog.Write($"Bildschirmtastatur zu ({reason})");
        }
    }

    /// <summary>B, the search closing or its keyboard put away by other means: the buttons are the mouse's again.</summary>
    private void OnSearchKeyboardClosed() => CloseKeyboard("Tastatur der Suche weg");

    /// <summary>
    /// A game coming to the front closes the keyboard; one that was in front when it opened doesn't, and any other
    /// window coming to the front puts the keyboard back on top. The search's keyboard lets go once Arcadia isn't
    /// in front, so the buttons don't type into it behind another window.
    /// </summary>
    private void FollowForegroundWithKeyboard()
    {
        if (_typingSearch is not null && !IsArcadiaActive)
        {
            CloseKeyboard("Arcadia nicht mehr vorne");
            return;
        }

        if (_keyboardWindow?.IsVisible != true)
        {
            return;
        }

        var gameInFront = GameInFront();
        if (gameInFront && !_gameWasInFront)
        {
            CloseKeyboard("Spiel vorne");
        }
        else
        {
            _keyboardWindow.StayOnTop();
        }

        _gameWasInFront = gameInFront;
    }

    /// <summary>⧉ tapped or the dock key: the other edge, kept for the next time.</summary>
    private void OnKeyboardDocked(bool atTop)
    {
        _keyboardWindow?.MoveTo(atTop);
        SaveKeyboardSetting(s => s with { MouseMode = s.MouseMode with { KeyboardAtTop = atTop } });
    }

    /// <summary>The change applies at once; if settings.json can't be written, only until Arcadia closes.</summary>
    private void SaveKeyboardSetting(Func<AppSettings, AppSettings> change)
    {
        try
        {
            _settings.Update(change);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Einstellung der Bildschirmtastatur nicht gespeichert: {ex.Message}");
        }
    }

    /// <summary>Arcadia's process has the foreground; on Windows by asking Windows, elsewhere by the window's activation.</summary>
    private bool OwnsForeground() => OperatingSystem.IsWindows() ? ProcessWatch.ForegroundProcessId() == Environment.ProcessId : _arcadiaActive;

    private void OnArcadiaActivated(object? sender, EventArgs e)
    {
        _arcadiaActive = true;
        CheckArcadiaInFront();
    }

    private void OnArcadiaDeactivated(object? sender, EventArgs e)
    {
        _arcadiaActive = false;
        CheckArcadiaInFront();
    }

    /// <summary>
    /// The automation follows the foreground as Windows reports it, checked on Avalonia's activation
    /// events and every <see cref="DesktopFilterCheckInterval"/>, because those events alone got out of step.
    /// </summary>
    private void CheckArcadiaInFront()
    {
        if (_windowHandle == 0 && _window.TryGetPlatformHandle() is { } handle)
        {
            _windowHandle = handle.Handle;
        }

        var inFront = OwnsForeground();
        if (inFront == _ownsForeground)
        {
            return;
        }

        _ownsForeground = inFront;
        _focusLossSettle.Stop();
        if (inFront)
        {
            _automation.ArcadiaActivated();
            UpdateForegroundCheck();
        }
        else
        {
            _focusLossSettle.Start();
        }
    }

    private void OnFocusLossSettled()
    {
        _focusLossSettle.Stop();
        if (!OwnsForeground())
        {
            _automation.ArcadiaDeactivated(ForegroundGame.IsInFront(_viewModel.Session.GameFolders, _viewModel.Session.LaunchedGames));
        }
    }

    private void CheckForeground()
    {
        _automation.ForegroundChecked(ForegroundGame.IsInFront(_viewModel.Session.GameFolders, _viewModel.Session.LaunchedGames));
        UpdateForegroundCheck();
    }

    /// <summary>
    /// Steam's desktop layout is dropped while the setting is on, whatever the mouse mode's setting:
    /// whenever the controller is in use outside games and in the mouse mode; beside a game of the library only keys and only while ⧉ + ≡
    /// are held, so the game's own Steam layout still reaches it. Without a controller the hooks are out.
    /// </summary>
    private void UpdateDesktopFilter()
    {
        // After an error in OnPolled the controller activity isn't noted any more, so the filter goes too.
        var scope = !_settings.Current.IgnoreSteamDesktopLayout || _failed || _activity.ConnectedPads == 0
            ? DesktopFilterScope.Off
            : _mode.IsOn || !GameInFront() ? DesktopFilterScope.ControllerInUse : DesktopFilterScope.ComboHeld;
        if (scope == _desktopFilterScope)
        {
            return;
        }

        _desktopFilterScope = scope;
        var dropped = _desktopFilter.SetScope(scope);
        AppLog.Write(scope switch
        {
            DesktopFilterScope.Off => $"Steams Desktop-Belegung wirkt wieder ({dropped ?? 0} Eingaben verworfen)",
            DesktopFilterScope.ComboHeld => "Steams Desktop-Belegung wird nur bei ⧉ + ≡ ignoriert (Spiel vorne)",
            _ => "Steams Desktop-Belegung wird ignoriert",
        });
    }

    private bool GameInFront() => ForegroundGameKey() is not null;

    /// <summary>The game in front; asks for the foreground process's path only when the foreground changed.</summary>
    private string? ForegroundGameKey()
    {
        var id = ProcessWatch.ForegroundProcessId();
        var folders = _viewModel.Session.GameFolders;
        if (id != _lastForegroundId || !ReferenceEquals(folders, _lastForegroundFolders))
        {
            _lastForegroundId = id;
            _lastForegroundFolders = folders;
            _lastForegroundGame = ForegroundGame.InFront(folders, _viewModel.Session.LaunchedGames);
        }

        return _lastForegroundGame;
    }

    /// <summary>
    /// "Beenden des Mausmodus in Spielen erzwingen": while a game that forbids it is in front, the mode
    /// ends and Back + Start doesn't switch it on. Checked with the foreground, every <see cref="DesktopFilterCheckInterval"/>.
    /// </summary>
    private void UpdateGameBlock()
    {
        var settings = _settings.Current.MouseMode;
        var blocked = settings.Activation == MouseModeActivation.Manual && settings.InGames != MouseModeInGames.Allowed
            && !settings.AllowedIn(ForegroundGameKey());
        if (blocked != _blockedByGame)
        {
            _blockedByGame = blocked;
            AppLog.Write(blocked ? "Mausmodus im Spiel vorne deaktiviert (Einstellung)" : "Mausmodus wieder erlaubt");
        }
    }

    /// <summary>The timer runs only while it has something to watch, so it costs nothing beside a game otherwise.</summary>
    private void UpdateForegroundCheck()
    {
        if (_automation.WatchesForeground)
        {
            _foregroundCheck.Start();
        }
        else
        {
            _foregroundCheck.Stop();
        }
    }
}
