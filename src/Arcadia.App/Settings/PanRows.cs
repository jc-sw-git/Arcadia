using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Copy;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Input;
using Arcadia.Pan;

namespace Arcadia.App.Settings;

/// <summary>What the settings, the header and the mode need of Pan's emulation (#242); the App's is <c>SteamControllerForGames</c>.</summary>
public interface IPanEmulationView
{
    PanEmulationStatus Status { get; }

    /// <summary><see cref="Status"/> changed; any thread.</summary>
    event Action? Changed;

    /// <summary>"Erneut versuchen": the failures so far no longer count, and the next tick tries again.</summary>
    void Retry();

    /// <summary>The attempt "Erneut versuchen" started hasn't ended yet; a toast tells how it ended (#308).</summary>
    bool Retrying { get; }

    /// <summary>Steam tried a firmware update through the copy while no game was in front (#295); any thread.</summary>
    event Action? FirmwareUpdateTried;

    /// <summary>
    /// "Controller freigeben" for a firmware update (#295): the real Steam Controller and puck are shown until Pan is
    /// switched off and on, or Arcadia restarts.
    /// </summary>
    Task ReleaseForFirmwareAsync();

    /// <summary>Connected controllers Pan couldn't hide, so games see them directly (#238 D7).</summary>
    IReadOnlyList<ControllerNotHidden> NotHidden => [];

    /// <summary>Connected controllers Pan waits with while their vendor's firmware tool runs (#238 D8).</summary>
    IReadOnlyList<ControllerWaitingForTool> WaitingForTool => [];

    /// <summary>The controller models connected now but the Steam Controller, for "Controller zuordnen" (#238 D3a).</summary>
    IReadOnlyList<ConnectedModel> ConnectedModels => [];

    /// <summary>The Steam Controller is connected: "Controller zuordnen" lists it, locked (#238 D3a).</summary>
    bool SteamControllerConnected => false;

    /// <summary>The controllers Arcadia saw before Pan hid them and no longer sees since (#396), by name.</summary>
    IReadOnlyList<string> NotVisibleToArcadia => [];
}

/// <summary>No emulation (tests that don't need one): always off.</summary>
public sealed class NoPanEmulation : IPanEmulationView
{
    public PanEmulationStatus Status => PanEmulationStatus.Off;

    public event Action? Changed
    {
        add { }
        remove { }
    }

    public void Retry()
    {
    }

    public bool Retrying => false;

    public event Action? FirmwareUpdateTried
    {
        add { }
        remove { }
    }

    public Task ReleaseForFirmwareAsync() => Task.CompletedTask;
}

/// <summary>"Controller-Wächter (Pan)": a switch, "Treiber installieren", or why neither is possible (#242).</summary>
public sealed class PanRowViewModel(Action<bool?> activate)
    : SetupSwitchRowViewModel(PanRows.MainKey, AppTexts.PanTitle, activate)
{
    public override string InfoTitle => AppTexts.PanInfoTitle;

    public override string ActivateText => IsInstall ? InstallText : AppTexts.Change;
}

/// <summary>
/// The group "PAN" of the controller settings (#242, artboards T242Settings, T242Hauptzeile, T242Warnzeilen): the switch
/// "Controller-Wächter (Pan)", below it one indented row per reason it doesn't run (only while it is on: off needs
/// nothing), and "Controller wiederherstellen" as soon as Pan is installed, also while it is off.
/// </summary>
public sealed class PanRows
{
    public const string MainKey = "pan";
    public const string RestoreKey = "pan-restore";
    public const string LayoutsKey = "pan-layouts";
    public const string Group = "PAN";

    private static string Admin => AppTexts.PanAdminOnce;

    public static string UpdateDueSubtitle => AppTexts.PanUpdateDueSubtitle;
    public static string UpdatingSubtitle => AppTexts.PanUpdatingSubtitle;
    public static string UpdateDueInfo => AppTexts.PanUpdateDueInfo;
    public static string ExcludedSubtitle => AppTexts.PanExcludedSubtitle;
    public static string NotUpdatedToast => AppTexts.PanNotUpdatedToast;

    private readonly ControllerSupportService _support;
    private readonly SettingsChanges _changes;
    private readonly IPanEmulationView _pan;
    private readonly Func<string?> _gameInTheWay;
    private readonly Action<HealthFix> _applyFix;
    private readonly Action<HealthSeverity, string> _notify;
    private readonly Dictionary<string, ActionRowViewModel> _rows = [];

    /// <param name="notify">Shows a toast, e.g. how switching on with a due update ended (#326).</param>
    public PanRows(ControllerSupportService support, SettingsChanges changes, IPanEmulationView pan, Func<string?> gameInTheWay, Action<HealthFix> applyFix,
        Action<HealthSeverity, string> notify)
    {
        _notify = notify;
        _support = support;
        _changes = changes;
        _pan = pan;
        _gameInTheWay = gameInTheWay;
        _applyFix = applyFix;
        Main = new PanRowViewModel(_ => Activate()) { Group = Group };
        Restore = new ActionRowViewModel(RestoreKey, AppTexts.ErrorContextRestoreControllers, () => _support.RestoreControllersAsync().Forget(AppTexts.ErrorContextRestoreControllers))
        {
            Group = Group,
            Subtitle = AppTexts.PanRestoreSubtitle,
            ActionText = AppTexts.Restore,
            Info =
            [
                new InfoText(AppTexts.PanRestoreInfo),
                new InfoText(AppTexts.PanRestoreInfoNormal),
                new InfoSection(AppTexts.PanIfThatFails,
                [
                    new InfoText(AppTexts.PanRestoreManual),
                ]),
            ],
        };
        Layouts = new ActionRowViewModel(LayoutsKey, ControllerLayoutsViewModel.Title, () => LayoutsRequested?.Invoke())
        {
            Group = Group,
            Indent = 1,
            Subtitle = AppTexts.PanLayoutsSubtitle,
            ActionText = AppTexts.Assign,
        };
    }

    public PanRowViewModel Main { get; }

    public ActionRowViewModel Restore { get; }

    /// <summary>"Controller zuordnen" (#238 D3a): [Zuordnen] opens the modal (<see cref="LayoutsRequested"/>).</summary>
    public ActionRowViewModel Layouts { get; }

    /// <summary>[Zuordnen] was pressed: the category opens "Controller zuordnen".</summary>
    public event Action? LayoutsRequested;

    /// <summary>The symbol beside the category: the worst row below the switch, or the emulation's problem; null while off.</summary>
    public HealthSeverity? Marker { get; private set; }

    /// <summary>The switch was flipped: the rows are due to be built again.</summary>
    public event Action? Switched;

    /// <summary>The first row that says why the emulation doesn't run, where [A] on a greyed mode leads; null: none.</summary>
    public string? FirstWarningKey { get; private set; }

    private bool IsOn => _changes.Current.PanEnabled;

    /// <summary>The installed Pan isn't this Arcadia's and Pan is off (#326): switching on updates it first.</summary>
    private bool IsUpdateDue => _support.IsPanUpdateDue && !IsOn;

    /// <summary>Switching on runs the update now; the Windows prompt is up.</summary>
    private bool _updating;

    private bool IsBusy => _support.BusyText is not null;

    /// <summary>A part of the emulation is missing, too old or waits for the restart (<see cref="EmulationParts.IsMissingPart"/>).</summary>
    private bool PartMissing => _support.Issues.Any(EmulationParts.IsMissingPart);

    private bool IsInstalling => _support.BusyText == ControllerSupportService.DriversBusyText;

    /// <summary>The rows of the group, in order; call on every change of the support, the emulation or the settings.</summary>
    public IReadOnlyList<SettingsRowViewModel> Build()
    {
        var status = _pan.Status;
        var notHere = !_support.IsSupportedPlatform;
        var allMissing = !notHere && _support.AllPartsMissing;
        // #407: a firmware update keeps the install greyed out, with the reason as a row below; a removal waiting for
        // the Windows restart too, with the restart row below (#382, F02).
        var blockers = allMissing && !IsBusy ? _support.InstallBlockers : [];
        var game = _gameInTheWay();
        var restartRow = notHere ? null : WindowsRestartRow(game);
        var installBlocked = blockers.Count > 0 || (allMissing && _support.PausesForWindowsRestart);
        var installFailed = allMissing && _support.FailedFix == HealthFix.InstallDrivers && _support.LastError is not null;
        var warnings = !notHere && !allMissing && IsOn ? WarningRows(status) : [];
        var excluded = !notHere && !allMissing && IsOn && status.Problem == PanEmulationProblem.Excluded;

        var problem = warnings.Any(row => row.Severity > HealthSeverity.Info) || status.HasProblem;
        var updateDue = !notHere && !allMissing && IsUpdateDue;

        Main.InstallText = IsInstalling ? AppTexts.Installing : installFailed ? AppTexts.TryAgain : AppTexts.DriversInstall;
        Main.Update(
            notHere ? SetupRowMode.Unavailable : allMissing ? SetupRowMode.Install : SetupRowMode.Switch,
            !notHere && !allMissing && IsOn,
            isLocked: IsBusy || _updating || (game is not null && !allMissing) || installBlocked,
            unavailableText: AppTexts.LauncherControllerUnsupported,
            status: Subtitle(status, allMissing, installFailed, problem, game, excluded, updateDue));
        // A running game would lose its controller mid-game: greyed out like "Auf Werkseinstellungen zurücksetzen".
        Main.IsDimmed = notHere || (game is not null && !allMissing && !IsBusy) || installBlocked;
        Main.Severity = installFailed ? HealthSeverity.Error : updateDue && !_updating ? HealthSeverity.Info : null;
        Main.ShowLamp(notHere || allMissing || updateDue ? null
            : !IsOn ? RowLamp.Resting
            : status.State == PanEmulationState.Stopped ? RowLamp.Failure
            : problem ? RowLamp.Problem
            : status.State == PanEmulationState.Running ? RowLamp.Working
            : RowLamp.Resting);
        Main.Info = [.. installFailed ? [new InfoError(_support.LastError!)] : Array.Empty<InfoBlock>(), .. updateDue ? UpdateInfo() : [], .. MainInfo];

        // Not beside the update's Windows prompt: one prompt at a time.
        Restore.IsEnabled = !IsBusy && !_updating;
        // A fix that failed marks its row red, not the category: the problem itself is still the same (#243 B2).
        Marker = !IsOn || notHere ? null
            : status.State == PanEmulationState.Stopped ? HealthSeverity.Error
            : problem || allMissing ? HealthSeverity.Warning
            : null;
        var blockerRows = blockers.Select(blocker => IssueRow(blocker, failed: null)).ToList();
        FirstWarningKey = blockerRows.FirstOrDefault()?.Key ?? restartRow?.Key
            ?? warnings.FirstOrDefault(row => row.Severity > HealthSeverity.Info)?.Key ?? warnings.FirstOrDefault()?.Key;
        // Nothing to assign while a missing part keeps the emulation from running (#410); an old usbip-win2 or HidHide may
        // still let it run, and then the controllers can be assigned.
        var layouts = !notHere && !allMissing && IsOn && (!PartMissing || status.State == PanEmulationState.Running) && UpdateLayouts();
        return
        [
            Main,
            .. restartRow is null ? Array.Empty<SettingsRowViewModel>() : [restartRow],
            .. blockerRows,
            .. warnings,
            .. layouts ? [Layouts] : Array.Empty<SettingsRowViewModel>(),
            .. _support.PanInstalled ? [Restore] : Array.Empty<SettingsRowViewModel>(),
        ];
    }

    /// <summary>
    /// "Controller zuordnen" shows while a controller is connected (D3a); its info panel says what games see of each
    /// (artboard T238Settings). False: none is connected.
    /// </summary>
    private bool UpdateLayouts()
    {
        var models = _pan.ConnectedModels;
        var steamController = _pan.SteamControllerConnected;
        if (models.Count == 0 && !steamController)
        {
            return false;
        }

        var chosen = _changes.Current.ControllerLayouts;
        Layouts.IsEnabled = !IsBusy;
        Layouts.Info =
        [
            new InfoText(AppTexts.PanLayoutsInfo),
            new InfoText(AppTexts.PanLayoutsInfoWhen),
            new InfoText(AppTexts.PanLayoutsInfoNotApplied),
            new InfoFacts(AppTexts.PanGamesSee,
            [
                .. steamController ? [new InfoFact(ControllerLayoutsViewModel.SteamControllerTitle, PadKinds.DisplayName(PadKind.Xbox360))] : Array.Empty<InfoFact>(),
                .. models.Select(model => ControllerLayouts.Effective(ControllerLayouts.Find(chosen, model.Model), model.DefaultKind) is { IsTakenOver: true } layout
                    ? new InfoFact(model.Name, ControllerLayouts.DisplayName(layout))
                    : new InfoFact(model.Name, AppTexts.PanLayoutNotApplied, IsMuted: true)),
            ]),
        ];
        return true;
    }

    /// <summary>First in the panel, before everything else (artboard T326S01): why switching on asks Windows; why the last update failed.</summary>
    private InfoBlock[] UpdateInfo() =>
        _updating ? [new InfoNote(PanUpdatePageViewModel.PromptHint, HealthSeverity.Info)]
        : [new InfoNote(UpdateDueInfo, HealthSeverity.Info) { IsBold = true }, .. _support.PanUpdateError is { } error ? [new InfoError(error)] : Array.Empty<InfoBlock>()];

    private string Subtitle(PanEmulationStatus status, bool allMissing, bool installFailed, bool problem, string? game, bool excluded, bool updateDue) =>
        (allMissing, IsOn) switch
        {
            (true, _) when IsInstalling => AppTexts.PanAdminOnceSubtitle,
            (true, _) when installFailed => AppTexts.PanSubtitleInstallAborted,
            (true, _) => AppTexts.PanSubtitleDescription,
            _ when game is not null => AppTexts.PanSubtitleGameRunning(game),
            _ when updateDue => _updating ? UpdatingSubtitle : UpdateDueSubtitle,
            (_, false) => AppTexts.PanSubtitleOff,
            _ when _support.PausesForWindowsRestart => PanEmulation.WindowsRestartReason,
            _ when status.State == PanEmulationState.Stopped => AppTexts.PanSubtitlePaused,
            _ when problem => AppTexts.PanSubtitleProblem,
            _ when excluded => ExcludedSubtitle,
            _ => status.State switch
            {
                PanEmulationState.Running => AppTexts.PanSubtitleOn,
                PanEmulationState.Ready => AppTexts.PanSubtitleReadyNoController,
                _ => AppTexts.PanSubtitleSettingUp,
            },
        };

    /// <summary>A, ◀ ▶ or a click: installs everything while nothing is set up, else switches; nothing while locked.</summary>
    private void Activate()
    {
        if (Main.IsLocked || Main.Mode == SetupRowMode.Unavailable)
        {
            return;
        }

        if (Main.Mode == SetupRowMode.Install)
        {
            _applyFix(HealthFix.InstallDrivers);
            return;
        }

        var on = !IsOn;
        if (on && _support.IsPanUpdateDue)
        {
            UpdateAndSwitchOnAsync().Forget(AppTexts.ErrorContextUpdatePan);
            return;
        }

        _changes.Apply(settings => settings with { PanEnabled = on }, AppTexts.PanTitle);
        Services.AppLog.Write($"Controller-Wächter (Pan): {(on ? "an" : "aus")}");
        Switched?.Invoke();
    }

    /// <summary>An old Pan is never switched on (#326): it is updated first, and only a confirmed update switches Pan on.</summary>
    private async Task UpdateAndSwitchOnAsync()
    {
        _updating = true;
        Switched?.Invoke();
        bool updated;
        try
        {
            // Off the UI thread: the elevated start waits for the Windows prompt.
            updated = await Task.Run(() => _support.UpdatePanAsync(CancellationToken.None));
            if (!updated)
            {
                _support.SwitchPanOffForUpdate(null);
            }
        }
        catch (Exception ex)
        {
            // Pan's own failures come as these with their text, shown in the info panel; anything else is a bug,
            // logged with its stack, and the row still says that Pan stays off.
            if (ex is not (InvalidOperationException or TimeoutException))
            {
                AppErrors.Report(ex, AppTexts.ErrorContextUpdatePan);
            }

            _support.SwitchPanOffForUpdate(ex.Message);
            updated = false;
        }
        finally
        {
            _updating = false;
        }

        if (updated)
        {
            _changes.Apply(settings => settings with { PanEnabled = true }, AppTexts.PanTitle);
            Services.AppLog.Write("Controller-Wächter (Pan): aktualisiert und an");
        }

        _notify(updated ? HealthSeverity.Info : HealthSeverity.Warning, updated ? PanUpdatePageViewModel.UpdatedToast : NotUpdatedToast);
        Switched?.Invoke();
    }

    /// <summary>One row per reason, the parts first (as the support lists them), then the emulation's own.</summary>
    private List<ActionRowViewModel> WarningRows(PanEmulationStatus status)
    {
        var rows = new List<ActionRowViewModel>();
        var ids = new HashSet<string>();
        // Several parts share "Installieren": its error shows once, at the first of them.
        var errorShown = false;
        foreach (var issue in _support.Issues.Where(issue => issue.Id != EmulationParts.WindowsRestartId))
        {
            var failed = !errorShown && issue.Fix != HealthFix.None && _support.FailedFix == issue.Fix ? _support.LastError : null;
            errorShown |= failed is not null;
            rows.Add(IssueRow(issue, failed));
            ids.Add(issue.Id);
        }

        // A part is missing: the emulation can't run, and its part rows say what to do. What the emulation reports then
        // only follows from it and could contradict them, e.g. "HidHide ist nicht aktiv" beside "HidHide fehlt" (#410).
        foreach (var (id, row) in PartMissing ? [] : EmulationRows(status))
        {
            if (ids.Add(id))
            {
                rows.Add(row);
            }
        }

        if (_support.HoldsEmulation == ControllerSupportService.RestartPendingText && _support.RestartArcadia is { } restart && ids.Add("pan:restart-pending"))
        {
            // Normally gone at once with the restart; it stays only when that failed (its toast), so the player can try again.
            rows.Add(Row("pan:restart-pending", RestartPendingTitle, RestartPendingSubtitle, HealthSeverity.Info, RestartPendingInfo,
                RestartText, () => restart(AppTexts.PanRestartAfterHidHide)));
        }

        // Only while the emulation runs: otherwise every controller reaches games directly anyway, and the rows above say why.
        if (status.State == PanEmulationState.Running)
        {
            rows.AddRange(FirmwareToolRows());
            if (NotHiddenRow() is { } notHidden)
            {
                rows.Add(notHidden);
            }
        }

        if (status.State == PanEmulationState.Unavailable && !rows.Any(row => row.Severity > HealthSeverity.Info))
        {
            // Pan or VIIPER doesn't answer, and no part row says why.
            rows.Add(Row("pan:start-failed", AppTexts.PanEmulationNotStarting, status.Reason ?? AppTexts.PanNotAnswering, HealthSeverity.Warning,
                StartFailedText, RetryText, _pan.Retry));
        }

        if (_pan.Retrying && _retried is { } retried && rows.All(row => row.Key != retried) && _rows.TryGetValue(retried, out var kept))
        {
            // The attempt sets up anew for a moment, so its problem is gone meanwhile: the pressed row stays, showing
            // that it runs, until the attempt ends (owner, 2026-10-29); focus doesn't jump either.
            ShowRetrying(kept);
            rows.Add(kept);
        }

        foreach (var gone in _rows.Keys.Where(key => rows.All(row => row.Key != key)).ToList())
        {
            _rows.Remove(gone);
        }

        return rows;
    }

    private IEnumerable<(string Id, ActionRowViewModel Row)> EmulationRows(PanEmulationStatus status)
    {
        switch (status.Problem)
        {
            case PanEmulationProblem.Hiding when EmulationParts.FromHidingCheck(status.Hiding) is { } hiding:
                // HidHide's own state (its configuration open, the list inverted, ...): the same row the support shows.
                yield return (hiding.Id, IssueRow(hiding, null));
                break;
            case PanEmulationProblem.Hiding:
                yield return ("pan:hide-failed", Row("pan:hide-failed", AppTexts.PanSteamControllerNotHiddenTitle, AppTexts.PanRetriesSoon, HealthSeverity.Warning,
                    AppTexts.PanSteamControllerNotHiddenInfo,
                    RetryText, _pan.Retry));
                break;
            case PanEmulationProblem.CopyFailed:
                yield return ("pan:start-failed", Row("pan:start-failed", AppTexts.PanEmulationNotStarting, AppTexts.ArcadiaRetriesSoon, HealthSeverity.Warning,
                    StartFailedText, RetryText, _pan.Retry));
                break;
            case PanEmulationProblem.TooManyFailures:
                yield return ("pan:stopped", Row("pan:stopped", AppTexts.PanEmulationStoppedTitle, AppTexts.PanEmulationGaveUpSubtitle, HealthSeverity.Error,
                    AppTexts.PanEmulationGaveUpInfo,
                    RetryText, _pan.Retry));
                break;
            case PanEmulationProblem.NotVisibleToArcadia:
                yield return ("pan:not-visible", NotVisibleRow());
                break;
            case PanEmulationProblem.CleaningUp:
                yield return ("pan:cleaning-up", Row("pan:cleaning-up", AppTexts.PanCleaningUp, AppTexts.PanCleaningUpSubtitle, HealthSeverity.Info,
                    AppTexts.PanCleaningUpInfo,
                    null, null));
                break;
        }
    }

    /// <summary>
    /// "Windows-Neustart ausstehend" (#382, D2, artboards T382F01–F04): one row for every pending restart, also while Pan
    /// is off or nothing is installed, so the player always has the button. Greyed out while a game runs or starts.
    /// </summary>
    private ActionRowViewModel? WindowsRestartRow(string? game)
    {
        if (_support.Issues.FirstOrDefault(issue => issue.Id == EmulationParts.WindowsRestartId) is not { } issue)
        {
            return null;
        }

        var row = IssueRow(issue, failed: null);
        var paused = issue == EmulationParts.WindowsRestartRequired;
        row.Info =
        [
            new InfoText(paused ? EmulationParts.PausedText : EmulationParts.RecommendedText),
            new InfoText(paused ? EmulationParts.PausedMore : EmulationParts.RecommendedMore),
            new InfoSection(paused ? EmulationParts.PausedBoxTitle : EmulationParts.RecommendedBoxTitle,
                [new InfoText(paused ? EmulationParts.PausedBoxText : EmulationParts.RecommendedBoxText)]),
        ];
        if (game is not null)
        {
            row.Subtitle = AppTexts.PanRestartGameRunning(game);
            row.IsEnabled = false;
        }

        return row;
    }

    /// <summary>One warning per firmware tool that runs (#238 D8, artboards T238F06–F07); it goes once the tool has ended.</summary>
    private IEnumerable<ActionRowViewModel> FirmwareToolRows()
    {
        foreach (var vendor in _pan.WaitingForTool.GroupBy(waiting => waiting.Model.Vendor))
        {
            var (tool, controller, it) = vendor.Key == ControllerList.Sony
                ? ("PlayStation Accessories", AppTexts.FirmwareToolTheDualSense, AppTexts.FirmwareToolItMasculine)
                : (AppTexts.FirmwareToolXboxAccessories, AppTexts.FirmwareToolTheXboxController, AppTexts.FirmwareToolItNeuter);
            var row = Row($"pan:firmware-tool:{vendor.Key:X4}", AppTexts.FirmwareToolOpen(tool), AppTexts.FirmwareToolWaiting(controller: controller, it: it), HealthSeverity.Warning,
                vendor.Key == ControllerList.Sony ? AppTexts.FirmwareToolInfoDualSense(tool) : AppTexts.FirmwareToolInfoXbox(tool),
                null, null);
            row.Info = [.. row.Info, new InfoSection(AppTexts.FirmwareToolGroup, [.. vendor.Select(waiting => new InfoText(waiting.Name))])];
            yield return row;
        }
    }

    /// <summary>
    /// The controllers that reach games directly as Pan couldn't hide them (#238 D7, artboard T238F05): one row with the
    /// blue i, since there is usually nothing to do.
    /// </summary>
    private ActionRowViewModel? NotHiddenRow()
    {
        var notHidden = _pan.NotHidden;
        if (notHidden.Count == 0)
        {
            return null;
        }

        var title = notHidden.Count == 1 ? AppTexts.PanNotHiddenOne(notHidden[0].Name) : AppTexts.PanNotHiddenMany(notHidden.Count);
        var row = Row("pan:not-hidden", title, AppTexts.PanNotHiddenSubtitle, HealthSeverity.Info,
            AppTexts.PanNotHiddenInfo,
            null, null);
        row.Info =
        [
            .. row.Info,
            new InfoText(AppTexts.PanNotHiddenInArcadia),
            new InfoSection(AppTexts.AffectedHeading, [.. notHidden.Select(controller => new InfoCard(controller.Name, NotHiddenReason(controller.Reason), string.Empty))]),
            new InfoText(AppTexts.PanNotHiddenReplug),
        ];
        return row;
    }

    /// <summary>
    /// Controllers Arcadia saw before Pan hid them and no longer sees since (#396): Pan shows them again, and only a
    /// restart of Arcadia brings the emulation back. A warning, as the player can act on it (owner, 2026-11-01).
    /// </summary>
    private ActionRowViewModel NotVisibleRow()
    {
        var restart = _support.RestartArcadia;
        var row = Row("pan:not-visible", NotVisibleTitle, NotVisibleSubtitle, HealthSeverity.Warning, NotVisibleText,
            restart is null ? null : RestartText, restart is null ? null : () => restart(RestartReason));
        row.Info =
        [
            .. row.Info,
            new InfoSection(AppTexts.PanNotVisibleGroup, [.. _pan.NotVisibleToArcadia.Select(name => new InfoText(name))]),
            new InfoText(NotVisibleAgainText),
        ];
        return row;
    }

    public static string NotVisibleTitle => AppTexts.PanNotVisibleTitle;
    public static string NotVisibleSubtitle => AppTexts.PanNotVisibleSubtitle;
    public static string NotVisibleText => AppTexts.PanNotVisibleInfo;
    public static string NotVisibleAgainText => AppTexts.PanNotVisibleAgain;
    public static string RestartText => AppTexts.RestartArcadia;
    public static string RestartPendingTitle => AppTexts.PanRestartPendingTitle;
    public static string RestartPendingSubtitle => AppTexts.PanRestartPendingSubtitle;
    public static string RestartPendingInfo => AppTexts.PanRestartPendingInfo;
    private static string RestartReason => AppTexts.PanRestartReason;

    /// <summary>Why Pan didn't hide a controller, in the player's words (#335's findings A1–A3).</summary>
    private static string NotHiddenReason(PanReason reason) => reason switch
    {
        PanReason.ControllerNeedsReplug => AppTexts.PanNotHiddenReasonReconnect,
        PanReason.ControllerNotHideable => AppTexts.PanNotHiddenReasonNothing,
        PanReason.ControllerNotConnected => AppTexts.PanNotHiddenReasonNotFound,
        // HidingFailed has several causes (a failed restart, an unknown image path, HidHide's errors): no guess at which.
        _ => AppTexts.PanNotHiddenReasonFailed,
    };

    private static string StartFailedText => AppTexts.PanStartFailedInfo;

    private ActionRowViewModel IssueRow(HealthIssue issue, string? failed)
    {
        var (subtitle, text, severity) = Texts(issue);
        var fix = issue.Fix;
        var row = Row(IssueKey(issue.Id), issue.Title, subtitle, failed is null ? severity : HealthSeverity.Error, text,
            fix == HealthFix.None ? null : issue.FixLabel ?? DefaultFixLabel, fix == HealthFix.None ? null : () => _applyFix(fix));
        row.ShowProblem(failed);
        if (failed is not null)
        {
            row.Info = [new InfoText(text), new InfoError(failed)];
        }

        return row;
    }

    private ActionRowViewModel Row(string key, string title, string subtitle, HealthSeverity severity, string text, string? action, Action? run)
    {
        if (!_rows.TryGetValue(key, out var row))
        {
            row = new ActionRowViewModel(key, title, () => _run.GetValueOrDefault(key)?.Invoke()) { Group = Group, Indent = 1 };
            _rows[key] = row;
        }

        _run[key] = action == RetryText ? () => RetryFrom(key) : run;
        row.Title = title;
        row.Subtitle = subtitle;
        row.Severity = severity;
        row.ActionText = action ?? string.Empty;
        row.IsEnabled = !IsBusy;
        row.IsWorking = false;
        row.ShowProblem(null);
        row.Info = [new InfoText(text)];
        if (action == RetryText && _pan.Retrying && _retried == key)
        {
            ShowRetrying(row);
        }

        return row;
    }

    private static string RetryText => AppTexts.TryAgain;

    /// <summary>The row whose "Erneut versuchen" was pressed last; it shows the attempt while it runs.</summary>
    private string? _retried;

    private void RetryFrom(string key)
    {
        _retried = key;
        _pan.Retry();
    }

    private static void ShowRetrying(ActionRowViewModel row)
    {
        row.ActionText = AppTexts.Retrying;
        row.IsWorking = true;
        row.IsEnabled = false;
    }

    private readonly Dictionary<string, Action?> _run = [];

    /// <summary>The row of a Steam Controller message, by its <see cref="HealthIssue.Id"/>; the status modal leads there (#166).</summary>
    public static string IssueKey(string issueId) => $"issue:{issueId}";

    /// <summary>The button of a fix that names none of its own.</summary>
    public static string DefaultFixLabel => AppTexts.Fix;

    /// <summary>The line below a message's title, the same in its row and in the status modal (#166).</summary>
    public static string SubtitleOf(HealthIssue issue) => Texts(issue).Subtitle;

    /// <summary>The designs' line and explanation for a part's row (T242Warnzeilen); a row they don't name keeps its own.</summary>
    private static (string Subtitle, string Text, HealthSeverity Severity) Texts(HealthIssue issue) => issue.Id switch
    {
        "usbip-missing" => (AppTexts.PanUsbipSubtitle,
            AppTexts.PanUsbipInfo(Admin), issue.Severity),
        "usbip-outdated" => (issue.Detail.TrimEnd('.'),
            AppTexts.PanUsbipOutdatedInfo(minimumVersion: UsbipRelease.MinimumVersion, admin: Admin), issue.Severity),
        "usbip-stopped" => (AppTexts.PanRestartPcHelps,
            AppTexts.PanUsbipNotLoadedInfo(Admin), issue.Severity),
        "viiper-missing" => (AppTexts.PanViiperSubtitle, issue.Detail, issue.Severity),
        "pan-missing" => (AppTexts.PanServiceSubtitle,
            AppTexts.PanServiceInfo(Admin), issue.Severity),
        "pan-failing" => (AppTexts.PanServiceReinstallHelps,
            AppTexts.PanServiceEndsInfo(logPath: PanPaths.LogPath, admin: Admin), issue.Severity),
        "hidhide-missing" => (AppTexts.PanHidHideSubtitle,
            AppTexts.PanHidHideInfo(Admin), issue.Severity),
        "hidhide-unsupported" => (AppTexts.PanHidHideNeedsVersion(HidHideRelease.Version), $"{issue.Detail} {Admin}", issue.Severity),
        "hidhide-inactive" => (AppTexts.PanRestartPcHelps,
            AppTexts.PanHidHideNotRunningInfo, issue.Severity),
        "hidhide-interface" => (AppTexts.PanHidHideReinstallHelps(HidHideRelease.Version),
            AppTexts.PanHidHideUnknownRequestInfo(version: HidHideRelease.Version, url: HidHideRelease.ManualInstallUrl), issue.Severity),
        "hidhide-busy" => (AppTexts.PanHidHideCloseIt,
            AppTexts.PanHidHideConfigOpenInfo, issue.Severity),
        "hidhide-inverse" => (AppTexts.PanHidHideInverseSubtitle,
            AppTexts.PanHidHideInverseInfo, issue.Severity),
        "hidhide-cloaking-off" => (AppTexts.PanHidHideHidingOffSubtitle,
            AppTexts.PanHidHideHidingOffInfo, issue.Severity),
        _ => (issue.ShortText, issue.Detail, issue.Severity),
    };

    /// <summary>What each Steam Controller mode brings, here and on every launcher's page (#178).</summary>
    public static IReadOnlyList<InfoBlock> ModeCards { get; } =
    [
        new InfoCard(ControllerModes.Arcadia, AppTexts.PanModeArcadiaCard, string.Empty),
        new InfoCard(ControllerModes.Steam, AppTexts.PanModeSteamCard, string.Empty),
    ];

    /// <summary>The switch's info panel (T242Settings).</summary>
    private static readonly InfoBlock[] MainInfo =
    [
        new InfoText(AppTexts.PanMainInfo1),
        new InfoText(AppTexts.PanMainInfo2),
        new InfoText(AppTexts.PanMainInfo3),
        new InfoSection(AppTexts.SteamControllerGroup,
        [
            new InfoText(AppTexts.PanMainInfo4),
            .. ModeCards,
            new InfoNote(AppTexts.PanMainInfo5, HealthSeverity.Info) { Title = AppTexts.PanFirmwareUpdatesHeading },
        ]),
        new InfoSection(AppTexts.PanWhatItNeeds,
        [
            new InfoText(AppTexts.PanMainInfoDrivers),
            new InfoCard("usbip-win2", AppTexts.PanUsbipSubtitle, "github.com/vadimgrn/usbip-win2"),
            new InfoCard("VIIPER", AppTexts.PanViiperSubtitle, "github.com/Alia5/VIIPER"),
            new InfoCard("HidHide", AppTexts.PanHidHideMainSubtitle, "github.com/nefarius/HidHide"),
            new InfoText(AppTexts.PanMainInfoAdmin),
        ]),
    ];
}
