using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>The right end of the header: clock, Steam Controller (SISR) status and the Arcadia update symbol.</summary>
public partial class StatusBarViewModel : ViewModelBase
{
    private readonly ControllerSupportService _controllerSupport;
    private readonly ArcadiaUpdateService _updates;
    private readonly TimeProvider _time;

    // Kept so it isn't collected; it ticks for as long as Arcadia runs.
    private readonly ITimer _clock;

    private static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(1);

    public StatusBarViewModel(ControllerSupportService controllerSupport, ArcadiaUpdateService updates, TimeProvider time)
    {
        _controllerSupport = controllerSupport;
        _updates = updates;
        _time = time;
        _controllerSupport.Changed += (_, _) => Dispatcher.UIThread.Post(UpdateControllerSupportState);
        _updates.Changed += (_, _) => Dispatcher.UIThread.Post(UpdateArcadiaUpdateState);
        UpdateClock();
        _clock = time.CreateTimer(_ => Dispatcher.UIThread.Post(UpdateClock, DispatcherPriority.Background), null, ClockInterval, ClockInterval);
        UpdateControllerSupportState();
        UpdateArcadiaUpdateState();
    }

    /// <summary>Steam Controller (SISR) is on and has status messages; the header shows the worst one's symbol.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyStatus))]
    public partial bool HasControllerStatus { get; private set; }

    /// <summary>All of them are only information (e.g. SISR paused for a game): the blue i.</summary>
    [ObservableProperty]
    public partial bool IsControllerStatusInfo { get; private set; }

    /// <summary>One of them is a lasting failure: the red X. Neither this nor <see cref="IsControllerStatusInfo"/>: the warning triangle.</summary>
    [ObservableProperty]
    public partial bool IsControllerStatusError { get; private set; }

    [ObservableProperty]
    public partial string ControllerStatusText { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyStatus))]
    public partial bool HasSisrUpdate { get; private set; }

    /// <summary>A newer Arcadia is on GitHub; shown as an update symbol in the header.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyStatus))]
    public partial bool HasArcadiaUpdate { get; private set; }

    [ObservableProperty]
    public partial string ArcadiaUpdateText { get; private set; } = string.Empty;

    /// <summary>SISR is starting and the controller isn't emulated yet; shown as a spinner in the header.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyStatus))]
    public partial bool IsControllerStarting { get; private set; }

    /// <summary>Something stands left of the search chip, so a divider separates it (owner, 2026-10-21).</summary>
    public bool HasAnyStatus => IsControllerStarting || HasControllerStatus || HasSisrUpdate || HasArcadiaUpdate;

    [ObservableProperty]
    public partial string Clock { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Date { get; private set; } = string.Empty;

    private void UpdateClock()
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
        IsControllerStarting = support.Enabled && support.State == SisrState.Starting;
        var issues = support.Issues;
        var worst = issues.Count > 0 ? issues.Max(i => i.Severity) : HealthSeverity.Info;
        // Also while the option is off: a missing driver or Pan is worth the triangle before the first switch-on (#243).
        HasControllerStatus = issues.Count > 0;
        IsControllerStatusInfo = worst == HealthSeverity.Info;
        IsControllerStatusError = worst == HealthSeverity.Error;
        ControllerStatusText = issues.Count switch
        {
            0 => string.Empty,
            1 => $"{(support.Enabled ? "Steam Controller (SISR)" : "Steam Controller")}: {issues[0].Title}",
            var n => $"{(support.Enabled ? "Steam Controller (SISR)" : "Steam Controller")}: {n} Meldungen",
        };
    }
}
