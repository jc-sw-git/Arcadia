using Arcadia.Core.Settings;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>What <see cref="ControllerModeViewModel"/> needs from the window around it.</summary>
public interface IControllerModeHost
{
    /// <summary>The library's focused cover, whose mode the preview shows.</summary>
    GameTileViewModel? FocusedTile { get; }

    /// <summary>Whether the mode shows, or works, changed: the detail page's buttons name it.</summary>
    void ControllerModeChanged();

    /// <summary>Opens the settings on the category "Controller"; null if they didn't open.</summary>
    ControllerSettingsViewModel? OpenControllerSettings();
}

/// <summary>
/// The Steam Controller mode per game (#178, #213) and Pan's emulation behind it (#242): whether the mode shows, whether it
/// works, the focused game's mode, and the way to the controller settings from the header and from a broken mode.
/// </summary>
public sealed partial class ControllerModeViewModel : ViewModelBase, IDisposable
{
    private readonly ControllerSupportService _controllerSupport;
    private readonly SettingsService _settings;
    private readonly StatusBarViewModel _status;
    private readonly GameActions _actions;
    private readonly IControllerModeHost _host;
    private bool _steamControllerSeenBySdl;

    public ControllerModeViewModel(
        ControllerSupportService controllerSupport, SettingsService settings, StatusBarViewModel status, GameActions actions, IControllerModeHost host)
    {
        _controllerSupport = controllerSupport;
        _settings = settings;
        _status = status;
        _actions = actions;
        _host = host;
        _controllerSupport.Changed += OnControllerSupportChanged;
    }

    /// <summary>Pan's emulation (#242); none until <see cref="ConnectPan"/>.</summary>
    public IPanEmulationView Pan { get; private set; } = new NoPanEmulation();

    /// <summary>The per-game mode ("Arcadia" or "Steam-Eingabe") shows only with a Steam Controller connected (#213).</summary>
    [ObservableProperty]
    public partial bool ShowControllerSupport { get; private set; }

    /// <summary>
    /// Pan's emulation is on but doesn't run (#242): the mode is greyed out with a triangle and says "Steam-Eingabe", as
    /// that is what a game gets; [A] on it opens the controller settings on the row that says why.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ControllerModeWorks), nameof(ControllerModeUnavailableReason))]
    public partial bool ControllerModeBroken { get; private set; }

    /// <summary>"Arcadia" can be chosen and is what a game gets.</summary>
    public bool ControllerModeWorks => !ControllerModeBroken;

    /// <summary>The mode's tooltip while it doesn't work, else null.</summary>
    public string? ControllerModeUnavailableReason => ControllerModeBroken ? ControllerModes.PanNotRunning : null;

    /// <summary>The Steam Controller mode of the library's focused game: "Standard (…)", "Arcadia" or "Steam-Eingabe".</summary>
    [ObservableProperty]
    public partial string FocusedControllerSupportText { get; private set; } = string.Empty;

    /// <summary>Why the per-game mode doesn't show now (#499): Pan off or not available here, or no Steam Controller; null while it shows.</summary>
    public string? HiddenReason => ShowControllerSupport ? null
        : !IsPanSupported ? AppTexts.LauncherControllerPanUnsupported
        : !_settings.Current.PanEnabled ? AppTexts.LauncherControllerNeedsPan
        : AppTexts.DetailModeNoSteamController;

    /// <summary>Pan is switched on and can run here.</summary>
    public bool IsPanOn => _settings.Current.PanEnabled && IsPanSupported;

    public bool IsPanSupported => Pan.Status.State != PanEmulationState.Unsupported;

    /// <summary>Pan's emulation (#242), once at the start: the mode, the header and the controller settings follow it.</summary>
    public void ConnectPan(IPanEmulationView pan)
    {
        Pan = pan;
        pan.Changed += OnPanChanged;
        UpdatePanState();
    }

    /// <summary>SDL sees a Steam Controller or no longer does (#212); the per-game mode shows only while it does (#213).</summary>
    public void SetSteamControllerSeen(bool seen)
    {
        _steamControllerSeenBySdl = seen;
        UpdateControllerSupportState();
    }

    /// <summary>The library focused another game (or its first one at the start): its mode shows.</summary>
    public void FocusedGameChanged() => UpdateControllerSupportState();

    /// <summary>The settings opened or closed: Pan may have been switched on or off.</summary>
    public void SettingsChanged() => UpdatePanState();

    /// <summary>
    /// Switches the focused game on: Standard → Arcadia → Steam-Eingabe (#178); not while it runs (#95). While Pan's emulation
    /// doesn't run it opens the controller settings on the row that says why (#242).
    /// </summary>
    [RelayCommand]
    private void CycleControllerSupport()
    {
        if (ControllerModeBroken)
        {
            ShowPanProblem();
            return;
        }

        if (_host.FocusedTile is not { IsRunningOrStarting: false, Game: var game })
        {
            return;
        }

        _actions.StepControllerMode(game, 1);
    }

    /// <summary>The controller settings with the row focused that says why Pan's emulation doesn't run; B goes back.</summary>
    public void ShowPanProblem()
    {
        if (_host.OpenControllerSettings() is { } controller)
        {
            controller.FocusRow(controller.PanWarningKey ?? PanRows.MainKey);
        }
    }

    private void OnControllerSupportChanged(object? sender, EventArgs e) => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateControllerSupportState);

    private void OnPanChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePanState);

    private void UpdatePanState()
    {
        var status = Pan.Status;
        _status.ShowPan(status);
        var broken = _settings.Current.PanEnabled && status.HasProblem;
        if (ControllerModeBroken != broken)
        {
            ControllerModeBroken = broken;
            _host.ControllerModeChanged();
        }

        UpdateControllerSupportState();
    }

    private void UpdateControllerSupportState()
    {
        // The mode only matters with a Steam Controller; other pads are never emulated (#72, #213). With Pan off nothing
        // is emulated, so there is no mode either (#242).
        var show = _steamControllerSeenBySdl && IsPanOn;
        if (ShowControllerSupport != show)
        {
            ShowControllerSupport = show;
            _host.ControllerModeChanged();
        }

        if (_host.FocusedTile?.Game is { } game)
        {
            FocusedControllerSupportText = _actions.ControllerModeOf(game);
        }
    }

    /// <summary>Lets go of the services, which outlive the window in the tests.</summary>
    public void Dispose()
    {
        _controllerSupport.Changed -= OnControllerSupportChanged;
        Pan.Changed -= OnPanChanged;
    }
}
