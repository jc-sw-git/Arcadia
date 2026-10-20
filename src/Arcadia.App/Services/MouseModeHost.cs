using Arcadia.App.ViewModels;
using Arcadia.App.Views;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Arcadia.Launchers;
using Avalonia.Threading;

namespace Arcadia.App.Services;

/// <summary>
/// Wires the mouse mode (#64) into Arcadia: it follows every poll of the controllers (also with
/// Arcadia in the background), shows its feedback over the screen, rumbles the controller that
/// switched, and runs the automation from the settings on Arcadia's focus changes. Beside it, View
/// held brings Arcadia back (<see cref="ReturnWithView"/>), and Steam's desktop layout is dropped
/// where the settings ask for it (<see cref="IDesktopInputFilter"/>).
/// </summary>
public sealed class MouseModeHost : IDisposable
{
    /// <summary>While Arcadia switched the mode on by itself, this often it checks whether a game came to the front.</summary>
    private static readonly TimeSpan ForegroundCheckInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Arcadia must stay in the background this long before the mode switches on by itself. Arcadia
    /// moves the focus away for a moment on purpose (to the desktop and back after SISR stops,
    /// <see cref="ForegroundGuard.BounceFocusAsync"/>); that must not flash "Mausmodus an" and "aus".
    /// </summary>
    private static readonly TimeSpan FocusLossSettleTime = TimeSpan.FromMilliseconds(300);

    /// <summary>This often Arcadia checks whether Steam's desktop layout must be dropped: not while a game is in front.</summary>
    private static readonly TimeSpan DesktopFilterCheckInterval = TimeSpan.FromMilliseconds(250);

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
    private readonly DispatcherTimer _foregroundCheck;
    private readonly DispatcherTimer _focusLossSettle;
    private readonly DispatcherTimer _desktopFilterCheck;

    // Arcadia's window is active by its own events; decides "in front" only where Windows can't tell.
    private volatile bool _arcadiaActive;
    private DesktopFilterScope _desktopFilterScope;
    private int? _lastForegroundId;
    private GameFolders? _lastForegroundFolders;
    private bool _lastForegroundIsGame;

    // Only the poll thread reads these two.
    private MouseModeSettings? _optionsFrom;
    private MouseModeOptions? _options;
    private ReturnWithViewSettings? _returnOptionsFrom;
    private ReturnWithViewOptions? _returnOptions;

    // Set on the poll thread after an unexpected error; the mode stays off until Arcadia restarts.
    private volatile bool _failed;

    public MouseModeHost(IGamepadSource gamepad, SettingsService settings, MainWindow window, MainViewModel viewModel, TimeProvider time)
    {
        _gamepad = gamepad;
        _settings = settings;
        _window = window;
        _viewModel = viewModel;
        _mode = new MouseMode(MouseKeyboardOutput.CreateDefault(), CurrentOptions, time);
        // "Automatisch" always ends when Arcadia comes to the front; "Von Hand" only with its option.
        _automation = new MouseModeAutomation(
            _mode,
            () => _settings.Current.MouseMode is { Activation: MouseModeActivation.Automatic } or { Activation: MouseModeActivation.Manual, EndWhenArcadiaInFront: true },
            () => _settings.Current.MouseMode.Activation == MouseModeActivation.Automatic);
        _returnWithView = new ReturnWithView(CurrentReturnOptions, () => IsArcadiaActive, time);
        _activity = new ControllerActivity(time);
        _desktopFilter = DesktopInputFilter.CreateDefault(_activity);
        _arcadiaActive = window.IsActive;
        _foregroundCheck = new DispatcherTimer(ForegroundCheckInterval, DispatcherPriority.Background, (_, _) => CheckForeground());
        _foregroundCheck.Stop();
        _focusLossSettle = new DispatcherTimer(FocusLossSettleTime, DispatcherPriority.Background, (_, _) => OnFocusLossSettled());
        _focusLossSettle.Stop();
        _desktopFilterCheck = new DispatcherTimer(DesktopFilterCheckInterval, DispatcherPriority.Background, (_, _) => UpdateDesktopFilter());

        _mode.HoldStarted += fillTime => Dispatcher.UIThread.Post(() => _overlay.ShowHold(fillTime));
        _mode.HoldCancelled += () => Dispatcher.UIThread.Post(_overlay.CancelHold);
        _mode.Switched += OnSwitched;
        _returnWithView.HoldStarted += fillTime => Dispatcher.UIThread.Post(() => _overlay.ShowReturnHold(fillTime));
        _returnWithView.HoldCancelled += () => Dispatcher.UIThread.Post(_overlay.CancelReturnHold);
        _returnWithView.Requested += () => Dispatcher.UIThread.Post(ReturnToArcadia);
        _gamepad.Polled += OnPolled;
        _window.Activated += OnArcadiaActivated;
        _window.Deactivated += OnArcadiaDeactivated;
    }

    /// <summary>Gamepad input is the mouse's now, not Arcadia's navigation. Readable from any thread.</summary>
    public bool IsOn => _mode.IsOn;

    /// <summary>
    /// Arcadia is in front. Readable from any thread, unlike <c>Window.IsActive</c>. On Windows the
    /// foreground window decides, not the window's activation events: those got out of step while
    /// switching between a game and Arcadia (owner's PC, 2026-10-20), and Arcadia then took the game's
    /// controller input as its own and ignored View held for the way back.
    /// </summary>
    public bool IsArcadiaActive => OperatingSystem.IsWindows() ? ProcessWatch.ForegroundProcessId() == Environment.ProcessId : _arcadiaActive;

    /// <summary>Call after the gamepad source stopped polling: lets go of a held mouse button or key.</summary>
    public void Dispose()
    {
        _gamepad.Polled -= OnPolled;
        _foregroundCheck.Stop();
        _focusLossSettle.Stop();
        _desktopFilterCheck.Stop();
        _mode.Stop();
        _desktopFilter.Dispose();
        _overlay.Close();
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

    /// <summary>Read on every poll; rebuilt only when the settings changed.</summary>
    private MouseModeOptions CurrentOptions()
    {
        var settings = _settings.Current.MouseMode;
        if (_options is null || !ReferenceEquals(settings, _optionsFrom))
        {
            _optionsFrom = settings;
            _options = new MouseModeOptions(settings.Activation != MouseModeActivation.Off, TimeSpan.FromSeconds(settings.HoldSeconds), settings.PointerSpeed);
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
        _overlay.EndReturnHold();
        AppLog.Write("View gehalten: Arcadia kommt nach vorne");
        ForegroundGuard.BringBack(_window);
    }

    private void OnSwitched(MouseModeSwitch change)
    {
        if (change.Slot is { } slot)
        {
            _gamepad.Rumble(slot);
        }

        Dispatcher.UIThread.Post(() =>
        {
            var gameInFront = ForegroundGame.IsInFront(_viewModel.GameFolders);
            AppLog.Write($"Mausmodus {(change.IsOn ? "an" : "aus")} ({(change.Slot is { } s ? $"Back + Start an Controller {s + 1}" : "automatisch")}{(gameInFront ? ", Spiel vorne" : string.Empty)})");
            if (change.IsOn)
            {
                // A held A must not open the details while the controller is a mouse.
                _viewModel.CancelHold();
            }

            var text = (change.IsOn, change.IsAutomatic, gameInFront) switch
            {
                (true, false, true) => "Mausmodus an – das Spiel sieht die Eingaben weiterhin",
                (true, _, _) => "Mausmodus an",
                _ => "Mausmodus aus",
            };
            _overlay.ShowSwitched(text, flash: !change.IsAutomatic);
            UpdateForegroundCheck();
            UpdateDesktopFilter();
        });
    }

    private void OnArcadiaActivated(object? sender, EventArgs e)
    {
        _arcadiaActive = true;
        _focusLossSettle.Stop();
        _automation.ArcadiaActivated();
        UpdateForegroundCheck();
    }

    private void OnArcadiaDeactivated(object? sender, EventArgs e)
    {
        _arcadiaActive = false;
        _focusLossSettle.Stop();
        _focusLossSettle.Start();
    }

    private void OnFocusLossSettled()
    {
        _focusLossSettle.Stop();
        if (!_window.IsActive)
        {
            _automation.ArcadiaDeactivated(ForegroundGame.IsInFront(_viewModel.GameFolders));
        }
    }

    private void CheckForeground()
    {
        _automation.ForegroundChecked(ForegroundGame.IsInFront(_viewModel.GameFolders));
        UpdateForegroundCheck();
    }

    /// <summary>
    /// Steam's desktop layout is dropped while the setting is on: whenever the controller is in use
    /// outside games and in the mouse mode; beside a game of the library only while ⧉ + ≡ are held,
    /// so the game's own Steam layout still reaches it.
    /// </summary>
    private void UpdateDesktopFilter()
    {
        var settings = _settings.Current.MouseMode;
        var scope = settings.Activation == MouseModeActivation.Off || !settings.IgnoreSteamDesktopLayout || _failed
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

    /// <summary>Asks for the foreground process's path only when the foreground changed.</summary>
    private bool GameInFront()
    {
        var id = ProcessWatch.ForegroundProcessId();
        var folders = _viewModel.GameFolders;
        if (id != _lastForegroundId || !ReferenceEquals(folders, _lastForegroundFolders))
        {
            _lastForegroundId = id;
            _lastForegroundFolders = folders;
            _lastForegroundIsGame = ForegroundGame.IsInFront(folders);
        }

        return _lastForegroundIsGame;
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
