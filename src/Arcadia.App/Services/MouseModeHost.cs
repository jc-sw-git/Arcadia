using Arcadia.App.ViewModels;
using Arcadia.App.Views;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Avalonia.Threading;

namespace Arcadia.App.Services;

/// <summary>
/// Wires the mouse mode (#64) into Arcadia: it follows every poll of the controllers (also with
/// Arcadia in the background), shows its feedback over the screen, rumbles the controller that
/// switched, and runs the automation from the settings on Arcadia's focus changes.
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

    private readonly IGamepadSource _gamepad;
    private readonly SettingsService _settings;
    private readonly MainWindow _window;
    private readonly MainViewModel _viewModel;
    private readonly MouseMode _mode;
    private readonly MouseModeAutomation _automation;
    private readonly MouseModeOverlay _overlay = new();
    private readonly DispatcherTimer _foregroundCheck;
    private readonly DispatcherTimer _focusLossSettle;

    // Only the poll thread reads these two.
    private MouseModeSettings? _optionsFrom;
    private MouseModeOptions? _options;

    // Set on the poll thread after an unexpected error; the mode stays off until Arcadia restarts.
    private volatile bool _failed;

    public MouseModeHost(IGamepadSource gamepad, SettingsService settings, MainWindow window, MainViewModel viewModel, TimeProvider time)
    {
        _gamepad = gamepad;
        _settings = settings;
        _window = window;
        _viewModel = viewModel;
        _mode = new MouseMode(MouseKeyboardOutput.CreateDefault(), CurrentOptions, time);
        _automation = new MouseModeAutomation(
            _mode,
            () => _settings.Current.MouseMode.EndWhenArcadiaInFront,
            () => _settings.Current.MouseMode.StartWhenArcadiaLosesFocus);
        _foregroundCheck = new DispatcherTimer(ForegroundCheckInterval, DispatcherPriority.Background, (_, _) => CheckForeground());
        _foregroundCheck.Stop();
        _focusLossSettle = new DispatcherTimer(FocusLossSettleTime, DispatcherPriority.Background, (_, _) => OnFocusLossSettled());
        _focusLossSettle.Stop();

        _mode.HoldStarted += fillTime => Dispatcher.UIThread.Post(() => _overlay.ShowHold(fillTime));
        _mode.HoldCancelled += () => Dispatcher.UIThread.Post(_overlay.CancelHold);
        _mode.Switched += OnSwitched;
        _mode.ArcadiaRequested += () => Dispatcher.UIThread.Post(() => ForegroundGuard.BringBack(_window));
        _gamepad.Polled += OnPolled;
        _window.Activated += OnArcadiaActivated;
        _window.Deactivated += OnArcadiaDeactivated;
    }

    /// <summary>Gamepad input is the mouse's now, not Arcadia's navigation. Readable from any thread.</summary>
    public bool IsOn => _mode.IsOn;

    /// <summary>Call after the gamepad source stopped polling: lets go of a held mouse button or key.</summary>
    public void Dispose()
    {
        _gamepad.Polled -= OnPolled;
        _foregroundCheck.Stop();
        _focusLossSettle.Stop();
        _mode.Stop();
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
            _mode.Update(pads);
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
            _options = new MouseModeOptions(settings.ComboEnabled, TimeSpan.FromSeconds(settings.HoldSeconds), settings.PointerSpeed);
        }

        return _options;
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
        });
    }

    private void OnArcadiaActivated(object? sender, EventArgs e)
    {
        _focusLossSettle.Stop();
        _automation.ArcadiaActivated();
        UpdateForegroundCheck();
    }

    private void OnArcadiaDeactivated(object? sender, EventArgs e)
    {
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
