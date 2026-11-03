using Arcadia.App.Input;
using Arcadia.Core.Settings;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// Pan's own screens over everything: the firmware notice (#295) and, at start, the update page while the installed Pan
/// isn't this Arcadia's (#297, #326). One at a time: the update waits for an open firmware notice.
/// </summary>
public sealed partial class PanDialogsViewModel : ViewModelBase, IDisposable
{
    private readonly ControllerSupportService _controllerSupport;
    private readonly SettingsService _settings;
    private readonly ToastViewModel _toast;
    private readonly TimeProvider _time;
    private readonly Action _quit;
    private readonly Action _cancelHold;
    private IPanEmulationView _pan = new NoPanEmulation();

    // [A] and Pan's answer came while one of its dialogs was open: the update runs once that dialog is closed.
    private bool _panUpdateAfterDialog;

    /// <summary>Pan answered its first hello at start (#297): the update waits for that, a firmware hold shows first.</summary>
    private bool _panAnswered;

    /// <param name="quit">"Arcadia jetzt beenden" in one of the dialogs.</param>
    /// <param name="cancelHold">Drops every held button of the window, before a dialog takes the input.</param>
    public PanDialogsViewModel(
        ControllerSupportService controllerSupport, SettingsService settings, ToastViewModel toast, TimeProvider time, Action quit, Action cancelHold)
    {
        _controllerSupport = controllerSupport;
        _settings = settings;
        _toast = toast;
        _time = time;
        _quit = quit;
        _cancelHold = cancelHold;
    }

    /// <summary>Pan's firmware notice (#295): over everything, the first start and the settings included.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FirmwareAttempt), nameof(IsPanDialogOpen))]
    public partial ViewModelBase? PanDialog { get; private set; }

    public FirmwareAttemptViewModel? FirmwareAttempt => PanDialog as FirmwareAttemptViewModel;

    public bool IsPanDialogOpen => PanDialog is not null;

    /// <summary>A Pan dialog opened: the App brings Arcadia to the front, as it may be behind a game.</summary>
    public event EventHandler? PanDialogShown;

    /// <summary>
    /// The start page while the installed Pan isn't this Arcadia's and Pan is on (#297, #326): over everything but Pan's
    /// dialogs, until Pan is updated, or switched off and the old one has ended.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPanUpdateShown))]
    public partial PanUpdatePageViewModel? PanUpdate { get; private set; }

    public bool IsPanUpdateShown => PanUpdate is not null;

    /// <summary>One of Pan's screens is shown and takes every press.</summary>
    public bool IsOpen => IsPanDialogOpen || IsPanUpdateShown;

    /// <summary>Pan's emulation, once at the start: Steam's firmware attempts through it open the notice.</summary>
    public void ConnectPan(IPanEmulationView pan)
    {
        _pan = pan;
        pan.FirmwareUpdateTried += OnFirmwareUpdateTried;
    }

    /// <summary>A press goes to Pan's screen on top; false if none is shown.</summary>
    public bool Handle(UiAction action)
    {
        if (PanDialog is FirmwareAttemptViewModel firmware)
        {
            if (!firmware.Handle(action))
            {
                ClosePanDialog(firmware);
            }

            return true;
        }

        if (PanUpdate is { } panUpdate)
        {
            panUpdate.Handle(action);
            return true;
        }

        return false;
    }

    public void Release(UiAction action) => FirmwareAttempt?.Release(action);

    public void CancelHold() => FirmwareAttempt?.CancelHold();

    /// <summary>The pointer went down on a held button of the firmware notice; false if it isn't one.</summary>
    public bool PressHold(HoldButtonViewModel button)
    {
        if (PanDialog is not FirmwareAttemptViewModel firmware)
        {
            return false;
        }

        firmware.Press(button);
        return true;
    }

    /// <summary>
    /// At start, before the window shows (#297): compares the installed Pan with the bundled one and, if they differ and
    /// Pan is on, shows the notice of the update in place of the library (#326). Switched off, the settings offer it.
    /// </summary>
    public void ShowPanUpdateIfDue()
    {
        if (!_controllerSupport.CheckPanAtStart() || !_settings.Current.PanEnabled)
        {
            return;
        }

        PanUpdate = new PanUpdatePageViewModel(_controllerSupport, _time, StartPanUpdate, toast =>
        {
            PanUpdate = null;
            _toast.Show(toast);
        }, _quit);
    }

    /// <summary>Pan has answered once at start (or never will), so the update doesn't stop a teardown that still runs.</summary>
    public void PanAnsweredAtStart()
    {
        _panAnswered = true;
        StartPanUpdate();
    }

    /// <summary>
    /// The update (or, after [B], the switch-off) runs once the notice was answered and Pan answered, and after an open
    /// firmware notice, one dialog at a time (#297, decision 7; #326).
    /// </summary>
    private void StartPanUpdate()
    {
        if (PanUpdate is not { State: PanUpdateState.Waiting } panUpdate || !_panAnswered)
        {
            return;
        }

        if (IsPanDialogOpen)
        {
            _panUpdateAfterDialog = true;
            return;
        }

        _panUpdateAfterDialog = false;
        panUpdate.Run();
    }

    /// <summary>Steam tried a firmware update through the copy while no game was in front (#295): the notice says why it failed.</summary>
    public void ShowFirmwareAttempt()
    {
        if (FirmwareAttempt is not null)
        {
            return;
        }

        var dialog = new FirmwareAttemptViewModel(
            _pan.ReleaseForFirmwareAsync,
            _quit,
            _time,
            action => Avalonia.Threading.Dispatcher.UIThread.Post(action));
        dialog.Closed += () => ClosePanDialog(dialog);
        ShowPanDialog(dialog);
    }

    private void OnFirmwareUpdateTried() => Avalonia.Threading.Dispatcher.UIThread.Post(ShowFirmwareAttempt);

    private void ShowPanDialog(ViewModelBase dialog)
    {
        _cancelHold();
        (PanDialog as IDisposable)?.Dispose();
        PanDialog = dialog;
        PanDialogShown?.Invoke(this, EventArgs.Empty);
    }

    private void ClosePanDialog(ViewModelBase dialog)
    {
        if (PanDialog == dialog)
        {
            (dialog as IDisposable)?.Dispose();
            PanDialog = null;
            if (_panUpdateAfterDialog)
            {
                StartPanUpdate();
            }
        }
    }

    /// <summary>"OK" on the note after the release for a firmware update.</summary>
    [RelayCommand]
    private void CloseFirmwareNote()
    {
        if (FirmwareAttempt is { IsReleased: true } firmware)
        {
            ClosePanDialog(firmware);
        }
    }

    /// <summary>Lets go of Pan's emulation and the open notice's hold timers.</summary>
    public void Dispose()
    {
        _pan.FirmwareUpdateTried -= OnFirmwareUpdateTried;
        (PanDialog as IDisposable)?.Dispose();
    }
}
