using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>The right end of the header: clock, Steam Controller Support status and the Arcadia update symbol.</summary>
public partial class StatusBarViewModel : ViewModelBase
{
    private readonly ControllerSupportService _controllerSupport;
    private readonly ArcadiaUpdateService _updates;
    private readonly TimeProvider _time;

    public StatusBarViewModel(ControllerSupportService controllerSupport, ArcadiaUpdateService updates, TimeProvider time)
    {
        _controllerSupport = controllerSupport;
        _updates = updates;
        _time = time;
        _controllerSupport.Changed += (_, _) => Dispatcher.UIThread.Post(UpdateControllerSupportState);
        _updates.Changed += (_, _) => Dispatcher.UIThread.Post(UpdateArcadiaUpdateState);
        UpdateClock();
        UpdateControllerSupportState();
        UpdateArcadiaUpdateState();
    }

    /// <summary>Steam Controller Support is on but something is wrong; shown as a warning in the header.</summary>
    [ObservableProperty]
    public partial bool HasControllerWarning { get; private set; }

    [ObservableProperty]
    public partial string ControllerWarningText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSisrUpdate { get; private set; }

    /// <summary>A newer Arcadia is on GitHub; shown as an update symbol in the header.</summary>
    [ObservableProperty]
    public partial bool HasArcadiaUpdate { get; private set; }

    [ObservableProperty]
    public partial string ArcadiaUpdateText { get; private set; } = string.Empty;

    /// <summary>SISR is starting and the controller isn't emulated yet; shown as a spinner in the header.</summary>
    [ObservableProperty]
    public partial bool IsControllerStarting { get; private set; }

    [ObservableProperty]
    public partial string Clock { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Date { get; private set; } = string.Empty;

    public void UpdateClock()
    {
        var now = _time.GetLocalNow();
        Clock = Formatting.Clock(now);
        Date = Formatting.ShortDate(now);
    }

    private void UpdateArcadiaUpdateState()
    {
        HasArcadiaUpdate = _updates.AvailableVersion is not null && !_updates.IsRestartingForUpdate;
        ArcadiaUpdateText = _updates.AvailableVersion is { } version ? $"Arcadia {version} verfügbar" : string.Empty;
    }

    private void UpdateControllerSupportState()
    {
        var support = _controllerSupport;
        HasSisrUpdate = support.Enabled && support.AvailableUpdate is not null;
        IsControllerStarting = support.Enabled && support.IsStarting;
        HasControllerWarning = support.Enabled && support.Issues.Count > 0;
        ControllerWarningText = support.Issues.Count switch
        {
            0 => string.Empty,
            1 => $"Steam Controller Support: {support.Issues[0].Title}",
            var n => $"Steam Controller Support: {n} Probleme",
        };
    }
}
