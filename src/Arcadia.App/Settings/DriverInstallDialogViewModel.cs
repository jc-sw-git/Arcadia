using System.Globalization;
using Arcadia.App.Input;
using Arcadia.ControllerSupport;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Settings;

/// <summary>One step in the progress dialog: ✓, spinner, a muted circle or a red ✕ (artboard T413Dialog).</summary>
public sealed record DriverInstallStepViewModel(string Title, string? Detail, DriverInstallStepState State)
{
    public bool IsDone => State == DriverInstallStepState.Done;

    public bool IsRunning => State == DriverInstallStepState.Running;

    public bool IsPending => State == DriverInstallStepState.Pending;

    public bool IsFailed => State == DriverInstallStepState.Failed;
}

/// <summary>
/// The dialog "Treiber werden installiert" over the controller settings (#413, artboard T413Dialog): the bar, the run's
/// steps, the USB hint while usbip-win2 is part of the run. It can't be closed while the install runs (D2: B does
/// nothing, no button); after a failure the failed step is red, the error stands below and A, B or "Schließen" close it
/// (D6, D9). A success closes it by the settings, unless Arcadia restarts (the last step then runs).
/// </summary>
public sealed partial class DriverInstallDialogViewModel : ViewModels.ViewModelBase, IHasOwnHints
{
    public static string UsbHintTitle => AppTexts.DriversUsbHintTitle;
    public static string UsbHintText => AppTexts.DriversUsbHintText;
    public static string NoCancelText => AppTexts.DriversNoCancel;
    public static string RestartFooterText => AppTexts.DriversRestartFooter;
    public static string ClosesByItselfText => AppTexts.DriversClosesByItself;

    private readonly Action _close;

    public DriverInstallDialogViewModel(DriverInstallProgress progress, Action close)
    {
        Progress = progress;
        _close = close;
        CloseCommand = new RelayCommand(Close);
        Refresh();
    }

    public DriverInstallProgress Progress { get; }

    /// <summary>"Schließen" after a failure; a click beside the dialog does nothing.</summary>
    public IRelayCommand CloseCommand { get; }

    [ObservableProperty]
    public partial string Title { get; private set; } = DriverInstallProgress.RunningTitle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    public partial bool IsFailed { get; private set; }

    public bool IsRunning => !IsFailed;

    [ObservableProperty]
    public partial IReadOnlyList<DriverInstallStepViewModel> Steps { get; private set; } = [];

    /// <summary>0 to 100.</summary>
    [ObservableProperty]
    public partial double Value { get; private set; }

    [ObservableProperty]
    public partial string Percent { get; private set; } = "0 %";

    [ObservableProperty]
    public partial string StepCount { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? Error { get; private set; }

    public bool ShowsUsbHint => Progress.ShowsUsbHint;

    /// <summary>Only a run that installs HidHide restarts Arcadia; the others close the dialog themselves (D6).</summary>
    public string Footer => NoCancelText + " " + (Progress.Plan.Restarts ? RestartFooterText : ClosesByItselfText);

    /// <summary>Reads the progress again; on the UI thread.</summary>
    public void Refresh()
    {
        var steps = Progress.Steps;
        Steps = [.. steps.Select(step => new DriverInstallStepViewModel(step.Title, step.Detail, step.State))];
        // Red and closable once the run is over: Steam may still start after the failed step (D8).
        IsFailed = Progress.IsOver && Progress.Failed;
        Title = IsFailed ? DriverInstallProgress.FailedTitle : DriverInstallProgress.RunningTitle;
        Error = Progress.Error;
        var percent = Math.Round(Progress.Value * 100);
        Value = percent;
        Percent = percent.ToString("0", CultureInfo.InvariantCulture) + " %";
        StepCount = AppTexts.DriversStepOf(current: Progress.CurrentNumber, count: steps.Count);
    }

    /// <summary>While it runs nothing gets through (D2); after a failure A and B close it (D9).</summary>
    public void Handle(UiAction action)
    {
        if (action is UiAction.Accept or UiAction.Back)
        {
            Close();
        }
    }

    private void Close()
    {
        if (Progress.IsOver && Progress.Failed)
        {
            _close();
        }
    }
}
