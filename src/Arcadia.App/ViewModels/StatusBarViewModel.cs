using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>The right end of the header: clock, Steam Controller status and the Arcadia update symbol.</summary>
public partial class StatusBarViewModel : ViewModelBase
{
    private readonly ControllerSupportService _controllerSupport;
    private readonly ArcadiaUpdateService _updates;
    private readonly TimeProvider _time;

    // Kept so it isn't collected; it ticks for as long as Arcadia runs.
    private readonly ITimer _clock;

    private static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(1);

    /// <summary>The tooltip of the triangle while Pan's emulation is on but doesn't run (#242).</summary>
    public const string PanNotRunningText = "Pan läuft nicht";

    private PanEmulationStatus _pan = PanEmulationStatus.Off;

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

    /// <summary>The Steam Controller's emulation has status messages; the header shows the worst one's symbol.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyStatus))]
    public partial bool HasControllerStatus { get; private set; }

    /// <summary>All of them are only information : the blue i.</summary>
    [ObservableProperty]
    public partial bool IsControllerStatusInfo { get; private set; }

    /// <summary>One of them is a lasting failure: the red X. Neither this nor <see cref="IsControllerStatusInfo"/>: the warning triangle.</summary>
    [ObservableProperty]
    public partial bool IsControllerStatusError { get; private set; }

    [ObservableProperty]
    public partial string ControllerStatusText { get; private set; } = string.Empty;

    /// <summary>A newer Arcadia is on GitHub; shown as an update symbol in the header.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyStatus))]
    public partial bool HasArcadiaUpdate { get; private set; }

    [ObservableProperty]
    public partial string ArcadiaUpdateText { get; private set; } = string.Empty;

    /// <summary>Pan sets up the emulation (#242), and the controller isn't emulated yet; a spinner in the header.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyStatus))]
    public partial bool IsControllerStarting { get; private set; }

    /// <summary>Something stands left of the search chip, so a divider separates it (owner, 2026-10-21).</summary>
    public bool HasAnyStatus => IsControllerStarting || HasControllerStatus || HasArcadiaUpdate;

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

    /// <summary>Pan's emulation changed (#242); UI thread.</summary>
    public void ShowPan(PanEmulationStatus status)
    {
        _pan = status;
        UpdateControllerSupportState();
    }

    private void UpdateControllerSupportState()
    {
        var support = _controllerSupport;
        // With the switch "Controller-Wächter (Pan)" off nothing is needed, so a missing part is no problem then (#242).
        var panOff = !support.Settings.PanEnabled;
        IsControllerStarting = !panOff && _pan.State == PanEmulationState.SettingUp;
        IReadOnlyList<HealthIssue> issues = panOff ? [] : support.Issues;
        var panProblem = !panOff && _pan.HasProblem;
        var worst = issues.Count > 0 ? issues.Max(i => i.Severity) : HealthSeverity.Info;
        // Also while the option is off: a missing driver or Pan is worth the triangle before the first switch-on (#243).
        HasControllerStatus = issues.Count > 0 || panProblem;
        IsControllerStatusInfo = !panProblem && worst == HealthSeverity.Info;
        IsControllerStatusError = worst == HealthSeverity.Error || _pan.State == PanEmulationState.Stopped && panProblem;
        const string owner = "Steam Controller";
        var panSuffix = panProblem ? $" – {PanNotRunningText}" : string.Empty;
        ControllerStatusText = (issues.Count, panProblem) switch
        {
            (0, false) => string.Empty,
            (0, true) => PanNotRunningText,
            (1, _) => $"{owner}: {issues[0].Title}{panSuffix}",
            (var n, _) => $"{owner}: {n} Meldungen{panSuffix}",
        };

    }
}
