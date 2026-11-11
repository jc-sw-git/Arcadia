using Arcadia.App.Messages;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.App.Status;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>The right end of the header: clock, Steam Controller status and the Arcadia update symbol.</summary>
public partial class StatusBarViewModel : ViewModelBase, IDisposable
{
    private readonly ControllerSupportService _controllerSupport;
    private readonly ArcadiaUpdateService _updates;
    private readonly TimeProvider _time;
    private readonly StatusMessageChanges _history;

    // Kept so it isn't collected; it ticks until the window is disposed.
    private readonly ITimer _clock;

    private static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(1);

    /// <summary>The tooltip of the triangle while Pan's emulation is on but doesn't run (#242).</summary>
    public static string PanNotRunningText => AppTexts.PanNotRunning;

    private PanEmulationStatus _pan = PanEmulationStatus.Off;

    /// <summary>The status message of Pan's emulation, for "Letzte Meldungen" (#433).</summary>
    public const string PanStatusId = "pan-not-running";

    /// <summary>The line below "Pan läuft nicht" in the status modal (#166): the reason has several rows of its own.</summary>
    public static string PanShortText => AppTexts.PanWhyInSettings;

    /// <summary>The Arcadia update as an entry of the status modal (#166).</summary>
    public const string UpdateStatusId = "arcadia-update";

    public static string UpdateShortText => AppTexts.UpdateShortText;

    public static string UpdateFixLabel => AppTexts.UpdateNow;

    // When each shown message appeared, at its current level (#166); a message that goes is forgotten.
    private readonly Dictionary<string, (StatusLevel Level, DateTimeOffset Since)> _since = [];
    private IReadOnlyList<StatusEntry> _controllerEntries = [];

    /// <param name="history">Gets the status messages the header shows on every change (#433).</param>
    public StatusBarViewModel(ControllerSupportService controllerSupport, ArcadiaUpdateService updates, TimeProvider time, StatusMessageChanges history)
    {
        _history = history;
        _controllerSupport = controllerSupport;
        _updates = updates;
        _time = time;
        _controllerSupport.Changed += OnControllerSupportChanged;
        _updates.Changed += OnUpdatesChanged;
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

    /// <summary>
    /// Every message the header shows, for the status modal (#166): the Steam Controller's, "Pan läuft nicht" and the
    /// update; "Controller wird eingerichtet …" is none. Replaced as a whole on every change, on the UI thread.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntries))]
    public partial IReadOnlyList<StatusEntry> Entries { get; private set; } = [];

    /// <summary>The header shows a message or the update, so Y (V) or a click opens the status modal.</summary>
    public bool HasEntries => Entries.Count > 0;

    /// <summary>Something stands left of the search chip, so a divider separates it (owner, 2026-10-21).</summary>
    public bool HasAnyStatus => IsControllerStarting || HasControllerStatus || HasArcadiaUpdate;

    [ObservableProperty]
    public partial string Clock { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Date { get; private set; } = string.Empty;

    /// <summary>Stops the clock and lets go of the services, which outlive the window in the tests.</summary>
    public void Dispose()
    {
        _clock.Dispose();
        _controllerSupport.Changed -= OnControllerSupportChanged;
        _updates.Changed -= OnUpdatesChanged;
        GC.SuppressFinalize(this);
    }

    private void OnControllerSupportChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(UpdateControllerSupportState);

    private void OnUpdatesChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(UpdateArcadiaUpdateState);

    private void UpdateClock()
    {
        var now = _time.GetLocalNow();
        Clock = Formatting.Clock(now);
        Date = Formatting.ShortDate(now);
    }

    private void UpdateArcadiaUpdateState()
    {
        HasArcadiaUpdate = _updates.AvailableVersion is not null && !_updates.IsRestartingForUpdate;
        ArcadiaUpdateText = _updates.AvailableVersion is { } version ? AppTexts.UpdateAvailable(version) : string.Empty;
        ShowEntries();
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
        var panSuffix = panProblem ? AppTexts.StatusSuffix(PanNotRunningText) : string.Empty;
        ControllerStatusText = (issues.Count, panProblem) switch
        {
            (0, false) => string.Empty,
            (0, true) => PanNotRunningText,
            (1, _) => $"{owner}: {issues[0].Title}{panSuffix}",
            (var n, _) => $"{owner}: {n} Meldungen{panSuffix}",
        };

        // What the header stands for, message by message: their changes are entries of "Letzte Meldungen" (#433).
        var panLevel = _pan.State == PanEmulationState.Stopped ? HealthSeverity.Error : HealthSeverity.Warning;
        _history.Show(
        [
            .. issues.Select(issue => new StatusMessage(issue.Id, issue.Severity, issue.Title)),
            .. panProblem ? [new StatusMessage(PanStatusId, panLevel, PanNotRunningText)] : Array.Empty<StatusMessage>(),
        ]);

        _controllerEntries =
        [
            .. issues.Select(issue => new StatusEntry(issue.Id, LevelOf(issue.Severity), issue.Title, PanRows.SubtitleOf(issue), default, StatusTarget.ControllerIssue,
                issue.Fix == HealthFix.None ? null : issue.FixLabel ?? PanRows.DefaultFixLabel)),
            .. panProblem ? [new StatusEntry(PanStatusId, LevelOf(panLevel), PanNotRunningText, PanShortText, default, StatusTarget.PanProblem, null)] : Array.Empty<StatusEntry>(),
        ];
        ShowEntries();
    }

    private static StatusLevel LevelOf(HealthSeverity severity) => severity switch
    {
        HealthSeverity.Error => StatusLevel.Error,
        HealthSeverity.Warning => StatusLevel.Warning,
        _ => StatusLevel.Info,
    };

    /// <summary>While the update loads, its entry shows the progress and has no fix, so A can't start it twice.</summary>
    private StatusEntry UpdateEntry() => _updates is { Progress: { } percent, AvailableVersion: { } version }
        ? new StatusEntry(UpdateStatusId, StatusLevel.Update, ArcadiaUpdateText, AboutSettingsViewModel.LoadingText(version, percent), default, StatusTarget.ArcadiaUpdate, null)
        : new StatusEntry(UpdateStatusId, StatusLevel.Update, ArcadiaUpdateText, UpdateShortText, default, StatusTarget.ArcadiaUpdate, UpdateFixLabel);

    /// <summary>The header's messages as the modal's entries, each with the time it appeared at its level.</summary>
    private void ShowEntries()
    {
        IReadOnlyList<StatusEntry> shown =
        [
            .. _controllerEntries,
            .. HasArcadiaUpdate
                ? [UpdateEntry()]
                : Array.Empty<StatusEntry>(),
        ];

        var now = _time.GetLocalNow();
        foreach (var gone in _since.Keys.Where(id => shown.All(entry => entry.Id != id)).ToList())
        {
            _since.Remove(gone);
        }

        var stamped = new List<StatusEntry>(shown.Count);
        foreach (var entry in shown)
        {
            // A level change counts as new (owner).
            if (!_since.TryGetValue(entry.Id, out var seen) || seen.Level != entry.Level)
            {
                seen = (entry.Level, now);
                _since[entry.Id] = seen;
            }

            stamped.Add(entry with { Since = seen.Since });
        }

        if (!stamped.SequenceEqual(Entries))
        {
            Entries = stamped;
        }
    }
}
