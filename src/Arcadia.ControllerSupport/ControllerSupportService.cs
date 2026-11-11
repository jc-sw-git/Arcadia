using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Pan;
using Arcadia.Platform;
using System.ComponentModel;

namespace Arcadia.ControllerSupport;

/// <summary>How a status message is shown (owner, 2026-10-21); ordered, so the header shows the worst one.</summary>
public enum HealthSeverity
{
    /// <summary>Intended behaviour in a particular situation, e.g. a restart Windows still wants: a blue i.</summary>
    Info,

    /// <summary>A feature works only partly and the user has to act, e.g. a driver missing: a yellow triangle.</summary>
    Warning,

    /// <summary>A lasting failure, e.g. the emulation stopped after failing again and again: a red X.</summary>
    Error,
}

/// <summary>What Arcadia can do about an issue, offered as a button next to it.</summary>
public enum HealthFix
{
    None,
    InstallDrivers,

    /// <summary>The pinned HidHide over the installed one (#242): its driver doesn't answer a request Pan needs.</summary>
    ReinstallHidHide,

    /// <summary>"Windows neu starten" (#382): the settings ask first, then <see cref="ControllerSupportService.RestartWindowsAsync"/>.</summary>
    RestartWindows,
}

/// <param name="Detail">The whole explanation, for the info panel.</param>
/// <param name="Summary">
/// One short line below the title where <paramref name="Detail"/> is longer than two lines (owner, 2026-10-22); null:
/// the detail is short enough.
/// </param>
public sealed record HealthIssue(string Id, HealthSeverity Severity, string Title, string Detail, HealthFix Fix = HealthFix.None, string? FixLabel = null, string? Summary = null)
{
    /// <summary>The line below the title of the issue's settings row.</summary>
    public string ShortText => Summary ?? Detail;
}

public static class HealthIssues
{
    /// <summary>
    /// The worst level among the problems: a warning or an error, the symbol beside the settings category they belong to
    /// (#15). Null when there are none or only information, which is intended behaviour and marks nothing.
    /// </summary>
    public static HealthSeverity? WorstProblem(IEnumerable<HealthIssue> issues)
    {
        HealthSeverity? worst = null;
        foreach (var issue in issues)
        {
            if (issue.Severity > HealthSeverity.Info && (worst is null || issue.Severity > worst))
            {
                worst = issue.Severity;
            }
        }

        return worst;
    }
}

/// <summary>
/// The setup around the controller emulation: its parts (usbip-win2, VIIPER, Pan, HidHide) and their install, Pan's
/// update at start, "Controller wiederherstellen" and the per-game Steam Controller mode; what is left of SISR is
/// <see cref="Sisr"/>.
/// </summary>
/// <remarks>Every action runs under one gate with its busy text, so two never cut into each other.</remarks>
public sealed class ControllerSupportService
{
    /// <summary>How long Arcadia waits for Steam to come back after it closed it (owner, 2026-10-16).</summary>
    public static readonly TimeSpan SteamStartTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Downloading VIIPER again when it went missing.</summary>
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(7);

    /// <summary>
    /// The drivers' downloads, then Pan's elevated <c>--install</c>, which <see cref="PanSetup"/> already bounds by Pan's
    /// step budgets: this outer limit must never fire first (QE review of PR #264).
    /// </summary>
    private static readonly TimeSpan DriversInstallTimeout = PanTimeouts.Install + TimeSpan.FromMinutes(3);

    private readonly SettingsService _sharedSettings;
    private readonly HttpClient _http;
    private readonly TimeProvider _time;
    private readonly IControllerSupportPlatform _platform;
    private readonly ILauncherProgram _steam;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _ticking;
    private volatile bool _shuttingDown;

    /// <summary>Cancelled when Arcadia exits: ends the actions under the gate and the waits for it.</summary>
    private readonly CancellationTokenSource _shutdown = new();

    /// <param name="sisr">What the old Arcadia left of SISR (#214); null on other platforms and in tests without it.</param>
    /// <param name="steam">
    /// Steam the program, which holds the Steam Controller: the program of the launcher that owns Steam Input
    /// (<see cref="LauncherInfo.OwnsSteamInput"/>), closed for a driver install that restarts the puck; null: no Steam.
    /// </param>
    public ControllerSupportService(SettingsService sharedSettings, HttpClient http, TimeProvider time, IControllerSupportPlatform? platform = null,
        SisrInstallation? sisr = null, ILauncherProgram? steam = null)
    {
        _sharedSettings = sharedSettings;
        _http = http;
        _time = time;
        _platform = platform ?? new ControllerSupportPlatform();
        _steam = steam ?? NoSteam.Instance;
        Sisr = new SisrLeftovers(this, sisr);
    }

    public event EventHandler? Changed;

    public AppSettings Settings => _sharedSettings.Current;

    /// <summary>Whether a launcher's games use Steam Input themselves, by launcher id: <see cref="LauncherRegistry.GamesUseSteamInput"/>.</summary>
    public required Func<string, bool> GamesUseSteamInput { get; init; }

    /// <summary>The Steam Controller mode of the game with this key: true = "Arcadia", false = "Steam-Eingabe" (<see cref="AppSettings.UsesArcadiaPad"/>).</summary>
    public bool UsesArcadiaPad(string gameKey) => Settings.UsesArcadiaPad(gameKey, GamesUseSteamInput);

    public bool IsSupportedPlatform => _platform.IsSupported;

    /// <summary>The settings are open; the tick rechecks the parts (and SISR) only then.</summary>
    public bool WatchSetup { get; set; }

    /// <summary>
    /// Pan's answer to "could you hide the Steam Controller now?" (#243 part B2), asked while the settings show the
    /// drivers; null without Pan (tests, other platforms).
    /// </summary>
    public IHidingCheck? HidingCheck { get; set; }

    /// <summary>Arcadia's own <c>viiper.exe</c> (#242 part B); null in tests that don't look at it.</summary>
    public IViiperInstallation? Viiper { get; init; }

    /// <summary>What the old Arcadia (or the user) left of SISR (#214): the group "SISR" and "SISR entfernen".</summary>
    public SisrLeftovers Sisr { get; }

    /// <summary>Pan hides the puck or holds it for a firmware attempt now (#242): HidHide must not be reinstalled then.</summary>
    public Func<bool>? PanHoldsPuck { get; set; }

    /// <summary>
    /// Lets go of Pan once <see cref="WhyPanIsLeftAlone"/> names a reason: waits for Arcadia's request still under way
    /// (a hide), then closes the connection (#366). Null in tests without a Pan client.
    /// </summary>
    public Func<CancellationToken, Task>? LetGoOfPan { get; set; }

    /// <summary>
    /// Restarts Arcadia, with the reason for the log (#396): "Arcadia neu starten", and after a HidHide install. Set by the
    /// App; null in tests that don't look at it.
    /// </summary>
    public Action<string>? RestartArcadia { get; set; }

    public static string HidHideInstallingText => ControllerSupportTexts.HidHideInstalling;
    public static string RestartPendingText => ControllerSupportTexts.RestartPendingText;

    /// <summary>
    /// Why Pan's emulation may hide nothing now, or null (#396): HidHide's installer runs, or ran and Arcadia restarts.
    /// HidHide doesn't let a process that ran before its install see what it hides, Arcadia included.
    /// </summary>
    public string? HoldsEmulation => _restartPending ? RestartPendingText : _hidHideInstalling ? HidHideInstallingText : null;

    private volatile bool _hidHideInstalling;
    private volatile bool _restartPending;

    /// <summary>
    /// Whether a driver change waits for the Windows restart (#382, D1): checked with the parts and, cheap registry reads
    /// only, on every tick, so a removal outside Arcadia pauses Pan also while the settings are closed. A failed check
    /// counts as none and is logged.
    /// </summary>
    public PendingRestart PendingRestart
    {
        get => (PendingRestart)Volatile.Read(ref _pendingRestart);
        private set => Volatile.Write(ref _pendingRestart, (int)value);
    }

    private int _pendingRestart;

    /// <summary>
    /// A driver removal waits for the restart (D1): Pan's emulation pauses, nothing is emulated or hidden, while the
    /// switch keeps its state, so Pan runs again after the restart without the player.
    /// </summary>
    public bool PausesForWindowsRestart => PendingRestart == PendingRestart.Required;

    /// <summary>
    /// HidHide is installed in a version Pan can use, as of the last check of the parts (#382, S1): without it Pan's
    /// emulation doesn't ask Pan whether it can hide. True until the first check.
    /// </summary>
    public bool HidHideUsable { get; private set; } = true;

    /// <summary>Every part of the emulation is missing (a removal waiting for the restart counts), as of the last check.</summary>
    public bool AllPartsMissing { get; private set; }

    /// <summary>The rows as the platform and Pan found them, before the restart row takes their place (<see cref="EmulationParts.WithRestart"/>).</summary>
    private IReadOnlyList<HealthIssue> _foundIssues = [];

    /// <summary><paramref name="fix"/> runs HidHide's installer now: the settings say first that Arcadia restarts afterwards (#396).</summary>
    public bool InstallsHidHide(HealthFix fix) => RestartsPuck(fix);

    public static string HidHideReinstallWhileHiddenText =>
        ControllerSupportTexts.HidHideReinstallWhileHidden;

    /// <summary>An action that has to close Steam stops before it changes anything when Steam doesn't exit.</summary>
    public static string SteamWontCloseText => ControllerSupportTexts.SteamWontClose;

    /// <summary>Steam's client runs: it could update the Steam Controller's firmware (#242).</summary>
    public bool SteamRuns => _steam.IsRunning;

    public IReadOnlyList<HealthIssue> Issues { get; private set; } = [];

    public string? BusyText { get; private set; }

    /// <summary>An action runs; the settings take no input then, so a press can't cut into it.</summary>
    public bool IsSettling => BusyText is not null;

    /// <summary>The message of the last failed action, shown in the settings dialog.</summary>
    public string? LastError { get; private set; }

    /// <summary>Optional diagnostics sink (Arcadia's log file).</summary>
    public Action<string>? Log { get; set; }

    /// <summary>
    /// <paramref name="fix"/> closes Steam first and starts it again afterwards (#296): it runs HidHide's installer, which
    /// restarts the puck, and Steam must not be updating it then. The settings ask before (or name a running Steam game).
    /// </summary>
    public bool StopsSteamFor(HealthFix fix) => _steam.IsRunning && RestartsPuck(fix);

    /// <summary>The fix runs usbip-win2's installer, which may freeze USB devices for a moment (#413, D5).</summary>
    public bool InstallsUsbip(HealthFix fix) => fix == HealthFix.InstallDrivers && IsSupportedPlatform && !_platform.IsUsbipReady;

    private bool RestartsPuck(HealthFix fix) => fix switch
    {
        HealthFix.ReinstallHidHide => true,
        HealthFix.InstallDrivers => _platform.InstallsHidHide,
        _ => false,
    };

    /// <summary>The name of the Steam game that keeps an action from closing Steam, or null.</summary>
    /// <remarks>Read fresh on every call, so the settings notice the game ending.</remarks>
    public string? SteamGameBlockingInstall() => _steam.IsRunning ? _steam.RunningGame()?.Name : null;

    /// <summary>
    /// Forgets the message of the last failed action, e.g. when the settings close; it describes
    /// a moment that is over. Kept while an action runs, which sets its own.
    /// </summary>
    public void ClearError()
    {
        if (LastError is null || BusyText is not null)
        {
            return;
        }

        LastError = null;
        FailedFix = null;
        OnChanged();
    }

    /// <summary>Ends what waits under the gate when Arcadia exits; nothing starts afterwards.</summary>
    public void Shutdown()
    {
        _shuttingDown = true;
        _shutdown.Cancel();
    }

    /// <summary>The game's own mode: true = "Arcadia", false = "Steam-Eingabe", null = "Standard", its launcher's default (#178).</summary>
    public bool? ChoiceFor(Game game) => Settings.ControllerModeChoiceFor(game.Key);

    /// <summary>The mode games of this launcher on "Standard" get (<see cref="AppSettings.LauncherUsesArcadiaPad"/>).</summary>
    public bool LauncherUsesArcadiaPad(string launcherId) => Settings.LauncherUsesArcadiaPad(launcherId, GamesUseSteamInput);

    /// <summary>Sets the game's own mode; null puts it on "Standard".</summary>
    public void SetChoiceFor(Game game, bool? useArcadiaPad) => UpdateSettings(settings => settings.WithControllerModeChoice(game.Key, useArcadiaPad));

    /// <summary>
    /// Called periodically: the parts are checked once at start and after every action, then only while the settings
    /// show them (registry, service list, Pan's pipe); otherwise what the last check found stands. A call while the
    /// previous one still runs returns at once, so slow checks never pile up.
    /// </summary>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _ticking, 1) == 1)
        {
            return;
        }

        try
        {
            if (!IsSupportedPlatform)
            {
                return;
            }

            var sisrChanged = WatchSetup && Sisr.Refresh(supportedPlatform: true);

            // Pan's HidHide states are asked only while the settings are open, so they go with them: "Bitte schließe
            // sie" must not stay in the header after the user closed HidHide's configuration (QE review of PR #264).
            var checkParts = WatchSetup || !_driversChecked;
            IReadOnlyList<HealthIssue> issues;
            if (checkParts)
            {
                issues = await CheckEmulationPartsAsync(cancellationToken);
            }
            else
            {
                _foundIssues = _foundIssues.Where(issue => !EmulationParts.IsSettingsOnly(issue)).ToList();
                issues = EmulationParts.WithRestart(_foundIssues, CheckPendingRestart());
            }

            _driversChecked = true;
            // Process list, device list and HID enumeration: only when the parts are checked, not beside a game.
            var installStateChanged = checkParts && UpdateInstallState();
            if (!issues.SequenceEqual(Issues) || sisrChanged || installStateChanged)
            {
                Issues = issues;
                OnChanged();
            }
        }
        finally
        {
            Volatile.Write(ref _ticking, 0);
        }
    }

    public static string FirmwareUpdateBlocksInstallText =>
        ControllerSupportTexts.FirmwareUpdateBlocksInstall;

    public static string DriversBusyText => ControllerSupportTexts.DriversBusy;

    private async Task InstallDriversAndSwitchPanOnAsync(CancellationToken cancellationToken)
    {
        await InstallDriversAsync(cancellationToken);
        // Pan is off on a fresh install (#402): whoever set up its parts wants it, so it runs without another click.
        if (!Settings.PanEnabled)
        {
            UpdateSettings(settings => settings with { PanEnabled = true });
        }
    }

    /// <summary>
    /// What is missing of the controller emulation (#243, #413): VIIPER (no prompt), and the usbip driver, HidHide and Pan
    /// with one Windows prompt, only if one of them is missing; the prompt comes first (D7). Inside a busy action; throws
    /// when the prompt was declined or a step failed, so the action fails visibly; <see cref="Installation"/> shows each step.
    /// </summary>
    private async Task InstallDriversAsync(CancellationToken cancellationToken)
    {
        // Also behind the rows' "Installieren" (#407): an install restarts the puck and controllers, which could cut a
        // firmware update off. Checked fresh; a failed check doesn't block, Pan refuses to hide during a flash anyway.
        if (FirmwareBlocksInstall() is { } firmware)
        {
            throw new InvalidOperationException(firmware);
        }

        // Checked fresh (D1): a setup would meet the half removed driver; the row offers the restart instead.
        if (CheckPendingRestart() == PendingRestart.Required)
        {
            throw new InvalidOperationException(WindowsRestartFirstText);
        }

        var panWasReady = _platform.IsPanReady;
        var needsPan = !(_platform.IsUsbipReady && panWasReady && !ControllerSupportPlatform.HidHideNeedsInstaller(_platform.HidHideVersion));
        var installsHidHide = needsPan && _platform.InstallsHidHide;
        var plan = new DriverInstallPlan(
            Prompt: needsPan,
            Viiper: Viiper is { IsInstalled: false },
            SteamCloses: installsHidHide && _steam.IsRunning,
            Usbip: needsPan && !_platform.IsUsbipReady,
            HidHide: installsHidHide,
            HidHideRepair: false,
            Restarts: installsHidHide);
        if (!plan.Prompt && !plan.Viiper)
        {
            return;
        }

        // A Steam game blocks before the prompt, as before #413: no prompt appears.
        if (plan.SteamCloses && SteamGameBlockingInstall() is { } game)
        {
            throw new InvalidOperationException(DriverInstallBlocker(game));
        }

        var usbipWasReady = _platform.IsUsbipReady;
        try
        {
            await RunInstallationAsync(plan, ControllerSupportTexts.DriversInstallDeclined, cancellationToken);
        }
        finally
        {
            // Also when a later step failed (e.g. HidHide, Pan's exit code 6): the driver came, and with it the restart it wants.
            if (!usbipWasReady && _platform.IsUsbipReady)
            {
                UpdateSettings(settings => settings with { UsbipInstalledAt = _time.GetUtcNow() });
            }
        }

        if (plan.Prompt)
        {
            Log?.Invoke($"Treiber eingerichtet ({InstalledParts(plan, panWasReady)})");
        }
    }

    public static string WindowsRestartFirstText => ControllerSupportTexts.WindowsRestartFirst;

    /// <summary>
    /// What a successful run set up, for the log (#382, S7): the drivers the plan installed, and Pan when it wasn't ready
    /// before; an unchanged Pan keeps its exe and service.
    /// </summary>
    internal static string InstalledParts(DriverInstallPlan plan, bool panWasReady)
    {
        string[] parts =
        [
            .. plan.Usbip ? ["usbip-win2"] : Array.Empty<string>(),
            .. plan.HidHide ? ["HidHide"] : Array.Empty<string>(),
            .. panWasReady ? Array.Empty<string>() : ["Pan"],
        ];
        return parts.Length switch
        {
            0 => "nichts Neues",
            1 => parts[0],
            _ => $"{string.Join(", ", parts[..^1])} und {parts[^1]}", // not a UI text (log)
        };
    }

    /// <summary>"Neu installieren" for a HidHide that answers unexpectedly (#242), with the progress dialog (#413, D3).</summary>
    private async Task ReinstallHidHideAsync(CancellationToken cancellationToken)
    {
        // Pan runs HidHide's installer before it replaces itself: a filter reinstall while the puck is hidden or held
        // for a firmware attempt could show Steam the real puck mid-attempt (decision 8).
        if (PanHoldsPuck?.Invoke() == true)
        {
            throw new InvalidOperationException(HidHideReinstallWhileHiddenText);
        }

        // Checked before the prompt: a reinstall that can't run must not ask or close Steam for nothing.
        if (_platform.HidHideVersion is not { } installed || !HidHideRelease.IsPinnedDriver(installed))
        {
            throw new InvalidOperationException(ControllerSupportTexts.HidHideReinstallWrongVersion(HidHideRelease.Version));
        }

        if (_steam.IsRunning && SteamGameBlockingInstall() is { } game)
        {
            throw new InvalidOperationException(DriverInstallBlocker(game));
        }

        // HidHide answering unexpectedly means Pan can't hide: the puck is Steam's, and the installer restarts it.
        var plan = new DriverInstallPlan(Prompt: true, Viiper: false, SteamCloses: _steam.IsRunning, Usbip: false, HidHide: true, HidHideRepair: true, Restarts: true);
        await RunInstallationAsync(plan, ControllerSupportTexts.HidHideReinstallDeclined, cancellationToken);
        Log?.Invoke("HidHide neu installiert");
    }

    /// <summary>
    /// The install a settings row started (#413): its steps for the progress dialog, from the prompt to Steam's start.
    /// Stays after the run until the settings dismiss it, so a failure can be read; a new Arcadia starts without it.
    /// </summary>
    public DriverInstallProgress? Installation { get; private set; }

    /// <summary>The dialog was closed (after a failure) or closed by itself (success): nothing to show any more.</summary>
    public void DismissInstallation()
    {
        if (Installation is { IsOver: true })
        {
            Installation = null;
            OnChanged();
        }
    }

    private async Task RunInstallationAsync(DriverInstallPlan plan, string declinedText, CancellationToken cancellationToken)
    {
        // The settings follow the steps themselves: a download reports too often to refresh every row each time.
        var progress = new DriverInstallProgress(plan);
        Installation = progress;
        OnChanged();
        var installation = new DriverInstallation(progress, _platform, _steam, _http, _time, Viiper)
        {
            BlockerBeforeChanges = () => FirmwareBlocksInstall() ?? (SteamGameBlockingInstall() is { } game && progress.Plan.SteamCloses ? DriverInstallBlocker(game) : null),
            DeclinedText = declinedText,
            Log = Log,
        };
        SetBusyText(plan.Prompt ? DriversBusyText : ControllerSupportTexts.ViiperInstalling);
        await installation.RunAsync(cancellationToken);
    }

    private string? FirmwareBlocksInstall() =>
        SafeCheck(() => _platform.FirmwareUpdateRuns, "ob ein Firmware-Update läuft") ? FirmwareUpdateBlocksInstallText : null; // not a UI text (log)

    /// <summary>
    /// Runs <paramref name="work"/>, with Steam closed when <paramref name="needsSteamClosed"/> and Steam runs: closed
    /// first (the caller has asked the user) and started again afterwards, also after a failed, declined or cancelled
    /// <paramref name="work"/>. A Steam game in the way (<paramref name="blocker"/>), or a Steam that won't close, stops
    /// it before anything changes. Waits for the restarted Steam when <paramref name="waitForSteam"/> says so, but
    /// doesn't need it to come back in time.
    /// </summary>
    internal async Task<T> WithSteamClosedAsync<T>(bool needsSteamClosed, string reason, Func<string, string> blocker,
        Func<CancellationToken, Task<T>> work, Func<T, bool> waitForSteam, CancellationToken cancellationToken)
    {
        if (!needsSteamClosed || !_steam.IsRunning)
        {
            return await work(cancellationToken);
        }

        if (SteamGameBlockingInstall() is { } game)
        {
            throw new InvalidOperationException(blocker(game));
        }

        var busy = BusyText;
        SetBusyText(ControllerSupportTexts.SteamStopping);
        if (!await _steam.CloseAsync(_time, cancellationToken))
        {
            throw new InvalidOperationException(SteamWontCloseText);
        }

        Log?.Invoke($"Steam beendet für {reason}");
        T result;
        try
        {
            SetBusyText(busy ?? ControllerSupportTexts.PleaseWait);
            result = await work(cancellationToken);
        }
        finally
        {
            // Arcadia closed Steam, so it brings it back.
            _steam.Start();
        }

        if (waitForSteam(result))
        {
            SetBusyText(ControllerSupportTexts.SteamStarting);
            await ProcessWatch.WaitUntilAsync(() => _steam.IsReady, SteamStartTimeout, _time, cancellationToken);
        }

        return result;
    }

    /// <summary>Why a driver install that closes Steam can't run now: closing Steam would end <paramref name="game"/>.</summary>
    public static string DriverInstallBlocker(string game) =>
        ControllerSupportTexts.DriverInstallBlockerSteamGame(game);

    /// <summary>
    /// Set at start while the installed Pan isn't the bundled one (#297) until it is replaced or removed: Pan's emulation
    /// waits meanwhile, as the update ends whatever the old Pan runs. Declined, it stays set while Pan is off (#326).
    /// </summary>
    public bool IsPanUpdateDue
    {
        get => Volatile.Read(ref _panUpdateDue);
        private set => Volatile.Write(ref _panUpdateDue, value);
    }

    private bool _panUpdateDue;

    /// <summary>
    /// Pan's <c>--install</c> runs, or ran and failed and Pan isn't switched off yet: <c>--install</c> stops the old
    /// service and waits for it to end, so a client that reconnects would start it again and fail the update.
    /// </summary>
    public bool IsPanUpdating
    {
        get => Volatile.Read(ref _panUpdating);
        private set => Volatile.Write(ref _panUpdating, value);
    }

    private bool _panUpdating;

    /// <summary>
    /// "Controller wiederherstellen" runs (#366): <c>--restore</c> undoes the journal in a second process beside the
    /// service, so Arcadia lets go of Pan first. Without a client nobody makes Pan hide anything, so the restore can't
    /// lose a hide the service journals at the same moment; Pan's teardown of the emulation only shows and removes, as
    /// the restore does.
    /// </summary>
    public bool IsRestoringControllers
    {
        get => Volatile.Read(ref _restoringControllers);
        private set => Volatile.Write(ref _restoringControllers, value);
    }

    private bool _restoringControllers;

    public static string PanUpdatingReason => ControllerSupportTexts.PanUpdatingReason;
    public static string RestoringReason => ControllerSupportTexts.RestoringReason;
    public static string OldPanOffReason => ControllerSupportTexts.OldPanOffReason;

    /// <summary>
    /// Why Arcadia doesn't talk to the installed Pan, null if it may. Not while the update runs (see
    /// <see cref="IsPanUpdating"/>) or the controllers are restored (<see cref="IsRestoringControllers"/>), and not to an old one the player didn't update (#326): left without a client, the
    /// old Pan undoes what it holds with its own code, whatever protocol it speaks, and ends itself.
    /// </summary>
    public string? WhyPanIsLeftAlone =>
        IsPanUpdating ? PanUpdatingReason
        : IsRestoringControllers ? RestoringReason
        : IsPanUpdateDue && !Settings.PanEnabled ? OldPanOffReason
        : null;

    public bool MayTalkToPan => WhyPanIsLeftAlone is null;

    /// <summary>Why the last update of Pan failed, for the switch's info panel while it is due (#326); null after a decline.</summary>
    public string? PanUpdateError { get; private set; }

    /// <summary>The Pan service still runs; an old Pan left alone ends itself unless it couldn't give the controllers back.</summary>
    public bool IsPanRunning => IsSupportedPlatform && _platform.IsPanRunning;

    /// <summary>
    /// Once at start, before the library shows (#297): reads the bundled and the installed Pan.exe. Not installed: no
    /// update; installed and not exactly the bundled one: <see cref="IsPanUpdateDue"/>, also while Pan is switched off,
    /// whose settings then offer the update (#326).
    /// </summary>
    public bool CheckPanAtStart()
    {
        var started = _time.GetTimestamp();
        var compatibility = IsSupportedPlatform ? _platform.PanCompatibility : PanCompatibility.NotInstalled;
        if (compatibility is not PanCompatibility.NotInstalled)
        {
            // On the start path before the window: the time it takes is part of the start budget.
            Log?.Invoke($"Pan geprüft in {_time.GetElapsedTime(started).TotalMilliseconds:0} ms: {compatibility}");
        }

        if (compatibility is PanCompatibility.UpdateRequired)
        {
            Log?.Invoke("Pan: die installierte Version ist nicht die mitgelieferte – fällig ist ein Update");
        }
        else if (compatibility is PanCompatibility.NotBundled)
        {
            Log?.Invoke("Pan: diese Arcadia-Version bringt kein Pan.exe mit – keine Prüfung");
        }

        IsPanUpdateDue = compatibility is PanCompatibility.UpdateRequired;
        return IsPanUpdateDue;
    }

    /// <summary>
    /// The update of Pan (#297, #326): only Pan, with one Windows prompt; no driver, no download, Steam may run.
    /// False if the prompt was declined; throws when Pan failed or didn't answer in time (its log says why). Arcadia
    /// doesn't talk to Pan from the start of the update until it succeeded or, declined or failed, until
    /// <see cref="SwitchPanOffForUpdate"/>: in between a reconnecting client would start the old Pan again.
    /// </summary>
    public async Task<bool> UpdatePanAsync(CancellationToken cancellationToken = default)
    {
        IsPanUpdating = true;
        OnChanged();
        if (!await WithTimeoutAsync(_platform.UpdatePanAsync, DriversInstallTimeout, ControllerSupportTexts.TimeoutWhatPanUpdate, cancellationToken))
        {
            Log?.Invoke("Pan-Aktualisierung in der Windows-Abfrage abgelehnt");
            return false;
        }

        IsPanUpdateDue = false;
        IsPanUpdating = false;
        PanUpdateError = null;
        _driversChecked = false;
        Log?.Invoke("Pan aktualisiert");
        OnChanged();
        return true;
    }

    /// <summary>
    /// The update was declined or failed (#326): Pan goes off, so Arcadia leaves the old Pan alone, and the switch offers
    /// the update again. <paramref name="error"/> is why it failed, null if the player said no.
    /// </summary>
    public void SwitchPanOffForUpdate(string? error)
    {
        PanUpdateError = error;
        UpdateSettings(settings => settings with { PanEnabled = false });
        // Only now: with Pan off, the old Pan stays left alone (MayTalkToPan) once the update no longer holds it.
        IsPanUpdating = false;
        Log?.Invoke(error is null ? "Pan nicht aktualisiert, Pan ist ausgeschaltet" : $"Pan-Aktualisierung fehlgeschlagen, Pan ist ausgeschaltet: {error}");
        OnChanged();
    }

    /// <summary>
    /// "Controller wiederherstellen" when the old Pan didn't end after the update was declined (#326): the bundled Pan's
    /// <c>--restore</c>, one prompt. False if the prompt was declined; throws when it failed.
    /// </summary>
    public async Task<bool> RestoreAfterOldPanAsync(CancellationToken cancellationToken = default)
    {
        if (!await _platform.RestoreControllersAsync(_ => Task.CompletedTask, cancellationToken))
        {
            Log?.Invoke("Wiederherstellung nach dem alten Pan in der Windows-Abfrage abgelehnt");
            return false;
        }

        Log?.Invoke("Controller nach dem alten Pan wiederhergestellt");
        return true;
    }

    /// <summary>
    /// "Controller wiederherstellen": Pan's <c>--restore</c>, elevated, for when the service hangs (#243) or a model
    /// stays hidden. Only once the Windows prompt was accepted Arcadia's emulation ends and its client lets go, as for an
    /// update, and comes back afterwards (#366, #471, see <see cref="IsRestoringControllers"/>); declined, nothing changes.
    /// </summary>
    public Task RestoreControllersAsync(CancellationToken cancellationToken = default) =>
        RunBusyAsync(ControllerSupportTexts.RestoringBusy, async token =>
        {
            try
            {
                if (!await _platform.RestoreControllersAsync(LetGoForRestoreAsync, token))
                {
                    throw new InvalidOperationException(ControllerSupportTexts.RestoreDeclined);
                }
            }
            finally
            {
                // Declined or failed too: the emulation comes back with the next connection.
                IsRestoringControllers = false;
            }

            Log?.Invoke("Controller über Pan wiederhergestellt");
            ControllersRestored?.Invoke(this, EventArgs.Empty);
        }, cancellationToken);

    private async Task LetGoForRestoreAsync(CancellationToken cancellationToken)
    {
        IsRestoringControllers = true;
        OnChanged();
        if (LetGoOfPan is { } letGo)
        {
            await letGo(cancellationToken);
        }
    }

    /// <summary>"Controller wiederherstellen" succeeded: Pan ended the emulation on purpose, so the pad may count afresh.</summary>
    public event EventHandler? ControllersRestored;

    /// <summary>The fix whose last run failed with <see cref="LastError"/>, so the settings show the error at its row (owner's test, 2026-10-26).</summary>
    public HealthFix? FailedFix { get; private set; }

    private HealthFix? _applyingFix;

    /// <summary>False until the next tick looks at the drivers (at start, and after an action may have installed them).</summary>
    private bool _driversChecked;

    public async Task ApplyFixAsync(HealthFix fix, CancellationToken cancellationToken = default)
    {
        _applyingFix = fix;
        var runsHidHide = InstallsHidHide(fix);
        _hidHideInstalling = runsHidHide;
        var hidHideBefore = _platform.HidHideVersion;
        try
        {
            await RunFixAsync(fix, cancellationToken);
        }
        finally
        {
            _applyingFix = null;
            // A HidHide that came with the fix, newly or again, doesn't let this process see what Pan hides (#396); also
            // when a later step failed, as long as its version changed. Pending before the install's hold ends, so no
            // tick in between finds nothing holding the emulation back (QE review of #397).
            if (runsHidHide && !_shuttingDown && _platform.HidHideVersion is { } now && HidHideRelease.IsSupported(now)
                && (LastError is null || now != hidHideBefore))
            {
                _restartPending = true;
                Log?.Invoke("HidHide installiert: Arcadia startet neu, bevor Pan etwas versteckt");
            }

            _hidHideInstalling = false;
            if (Installation is { } installation && !installation.IsOver)
            {
                if (_restartPending && !_shuttingDown && LastError is null)
                {
                    installation.Start(DriverInstallStepKind.ArcadiaRestart);
                }
                else if (LastError is { } error && !installation.Failed)
                {
                    // Failed before any step began (e.g. Pan's bundle missing): shown at the first step.
                    installation.Fail(installation.Steps[0].Kind, error);
                }

                installation.End();
            }

            OnChanged();
        }

        if (_restartPending && RestartArcadia is { } restart)
        {
            restart("nach der HidHide-Installation"); // not a UI text (log)
        }
    }

    private Task RunFixAsync(HealthFix fix, CancellationToken cancellationToken) => fix switch
    {
        HealthFix.InstallDrivers => RunBusyAsync(DriversBusyText, InstallDriversAndSwitchPanOnAsync, cancellationToken),
        HealthFix.ReinstallHidHide => RunBusyAsync(DriversBusyText, ReinstallHidHideAsync, cancellationToken),
        _ => Task.CompletedTask,
    };

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Issues = IsSupportedPlatform ? await CheckEmulationPartsAsync(cancellationToken) : [];
        UpdateInstallState();
        _panInstalled = CheckPanInstalled();
        Sisr.Refresh(IsSupportedPlatform);
        OnChanged();
    }

    /// <summary>
    /// The rows under Pan (#243 part B2), in the order usbip-win2, VIIPER, Pan, HidHide: the platform's driver checks,
    /// VIIPER, while the settings show them why Pan couldn't hide the Steam Controller now (HidHide's driver not
    /// running, inverse mode, ..., its configuration open), and an SISR running beside Pan (#214).
    /// </summary>
    private async Task<IReadOnlyList<HealthIssue>> CheckEmulationPartsAsync(CancellationToken cancellationToken)
    {
        var restart = CheckPendingRestart();
        var issues = _platform.CheckDrivers(Settings, _time.GetUtcNow()).ToList();
        if (Viiper is { IsInstalled: false })
        {
            issues.Add(EmulationParts.ViiperMissing);
        }

        HidHideUsable = EmulationParts.HidHideUsable(issues);
        AllPartsMissing = EmulationParts.AllMissing(issues);

        // Only while the settings are open: asking Pan costs a pipe round trip and HidHide's lock. Not while a removal
        // waits for the restart: Pan pauses then, and its answer would only follow from the half removed driver.
        if (WatchSetup && HidingCheck is { } check && _platform.IsPanReady && restart != PendingRestart.Required
            && _platform.HidHideVersion is { } hidHide && HidHideRelease.IsSupported(hidHide)
            && !issues.Contains(EmulationParts.HidHideRemovalPending)
            && EmulationParts.FromHidingCheck(await check.CheckHidingAsync(cancellationToken), hidHide) is { } hiding)
        {
            issues.Add(hiding);
        }

        // Also while the group "SISR" is hidden ("SISR behalten"): two programs on the Steam Controller break both. With
        // Pan off nothing of Arcadia's takes the controller, so SISR is welcome then.
        if (WatchSetup && Settings.PanEnabled && Sisr.IsAnyRunning)
        {
            issues.Add(EmulationParts.SisrRunning);
        }

        _foundIssues = EmulationParts.InRowOrder(issues);
        return EmulationParts.WithRestart(_foundIssues, restart);
    }

    /// <summary>Reads the pending restart fresh and keeps it (<see cref="PendingRestart"/>); a failed check counts as none (failure case).</summary>
    private PendingRestart CheckPendingRestart()
    {
        PendingRestart restart;
        try
        {
            restart = _platform.CheckPendingRestart(Settings, _time.GetUtcNow());
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log?.Invoke($"Nicht geprüft, ob ein Windows-Neustart aussteht: {ex}");
            restart = PendingRestart.None;
        }

        if (restart != PendingRestart)
        {
            Log?.Invoke(restart switch
            {
                PendingRestart.Required => "Windows-Neustart ausstehend: ein Treiber wurde deinstalliert, Pan pausiert bis dahin",
                PendingRestart.Recommended => "Windows-Neustart empfohlen: usbip-win2 wurde seit dem Start von Windows installiert",
                _ => "Kein Windows-Neustart mehr ausstehend",
            });
            PendingRestart = restart;
        }

        return restart;
    }

    public static string RestartRefusedTitle => ControllerSupportTexts.RestartRefusedTitle;
    public static string RestartRefusedText => ControllerSupportTexts.RestartRefusedText;

    private int _restartingWindows;

    /// <summary>
    /// "Windows neu starten" after the question (#382, S4): asks Windows to restart now. False when Windows refused or
    /// didn't answer (logged; the settings show a toast and keep the row); true when the restart is under way, also for a
    /// second press meanwhile, which asks nothing more. Not while an action runs (its row is greyed out then).
    /// </summary>
    public async Task<bool> RestartWindowsAsync(CancellationToken cancellationToken = default)
    {
        if (IsSettling || !IsSupportedPlatform)
        {
            return false;
        }

        if (Interlocked.Exchange(ref _restartingWindows, 1) == 1)
        {
            return true;
        }

        try
        {
            Log?.Invoke("Windows-Neustart angefordert");
            await _platform.RestartWindowsAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or TimeoutException or IOException
                                       or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Log?.Invoke($"Windows-Neustart abgelehnt: {ex.Message}");
            Volatile.Write(ref _restartingWindows, 0);
            return false;
        }
    }

    /// <summary>
    /// What keeps "Treiber installieren" from running now (#407), as rows: a controller's firmware update (a removal
    /// waiting for the restart is the restart row, #382). Checked while every part is missing (only then is the button
    /// there), else empty.
    /// </summary>
    public IReadOnlyList<HealthIssue> InstallBlockers { get; private set; } = [];

    /// <summary>A Steam Controller or its puck is plugged in; checked like <see cref="InstallBlockers"/> (#407).</summary>
    public bool SteamControllerPlugged { get; private set; }

    /// <summary>True if <see cref="InstallBlockers"/> or <see cref="SteamControllerPlugged"/> changed.</summary>
    private bool UpdateInstallState()
    {
        var allMissing = IsSupportedPlatform && AllPartsMissing;
        var blockers = allMissing ? CheckInstallBlockers() : [];
        var plugged = allMissing && SafeCheck(() => _platform.SteamControllerPlugged, "ob ein Steam Controller angeschlossen ist"); // not a UI text (log)
        var changed = !blockers.SequenceEqual(InstallBlockers) || plugged != SteamControllerPlugged;
        InstallBlockers = blockers;
        SteamControllerPlugged = plugged;
        return changed;
    }

    private List<HealthIssue> CheckInstallBlockers() =>
        SafeCheck(() => _platform.FirmwareUpdateRuns, "ob ein Firmware-Update läuft") ? [EmulationParts.FirmwareUpdateRuns] : []; // not a UI text (log)

    /// <summary>A check that fails counts as false and is logged: the install itself checks the firmware again.</summary>
    private bool SafeCheck(Func<bool> check, string what)
    {
        try
        {
            return check();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            Log?.Invoke($"Nicht geprüft, {what}: {ex.Message}");
            return false;
        }
    }

    /// <summary>Pan, the guardian service, is installed (#243); rechecked with every refresh. "Controller wiederherstellen" needs it.</summary>
    public bool PanInstalled => _panInstalled ??= CheckPanInstalled();

    private bool? _panInstalled;

    private bool CheckPanInstalled() => IsSupportedPlatform && _platform.IsPanReady;

    /// <summary>
    /// Runs an action under the gate with the busy text shown. Exiting Arcadia cancels the token the
    /// action gets (and the wait for the gate), so the shutdown isn't held up by a download or by Steam starting.
    /// </summary>
    internal async Task RunBusyAsync(string text, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        try
        {
            await _gate.WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (_shuttingDown)
        {
            return;
        }

        BusyText = text;
        LastError = null;
        FailedFix = null;
        OnChanged();
        try
        {
            await action(linked.Token);
        }
        catch (OperationCanceledException) when (_shuttingDown)
        {
            Log?.Invoke($"{text} abgebrochen: Arcadia wird beendet");
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or IOException
                                       or UnauthorizedAccessException or Win32Exception or TimeoutException)
        {
            Log?.Invoke($"{text} fehlgeschlagen: {ex.Message}");
            LastError = ex.Message;
            FailedFix = _applyingFix;
        }
        finally
        {
            BusyText = null;
            _driversChecked = false;
            _gate.Release();
            if (!_shuttingDown)
            {
                await RefreshAsync();
            }
        }
    }

    /// <summary>Runs a network call with its own time limit; running out of time throws <see cref="TimeoutException"/>.</summary>
    private Task WithTimeoutAsync(Func<CancellationToken, Task> call, TimeSpan timeout, string what, CancellationToken cancellationToken) =>
        WithTimeoutAsync(async token =>
        {
            await call(token);
            return true;
        }, timeout, what, cancellationToken);

    private async Task<T> WithTimeoutAsync<T>(Func<CancellationToken, Task<T>> call, TimeSpan timeout, string what, CancellationToken cancellationToken)
    {
        using var timer = new CancellationTokenSource(timeout, _time);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        try
        {
            return await call(limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(ControllerSupportTexts.TimeoutNoAnswer(what: what, seconds: timeout.TotalSeconds));
        }
    }

    internal void SetBusyText(string text)
    {
        BusyText = text;
        OnChanged();
    }

    private void UpdateSettings(Func<AppSettings, AppSettings> change)
    {
        try
        {
            _sharedSettings.Update(change);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The change holds for this session; a save that failed must not stop the action.
            Log?.Invoke($"Controller-Einstellung nicht gespeichert: {ex.Message}");
        }

        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>No Steam on this PC (or in a test without one): nothing runs, nothing to close.</summary>
file sealed class NoSteam : ILauncherProgram
{
    public static readonly NoSteam Instance = new();

    public string? Folder => null;

    public bool IsRunning => false;

    public bool IsReady => false;

    public RunningLauncherGame? RunningGame() => null;

    public Task<bool> CloseAsync(TimeProvider time, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public void Start()
    {
    }
}
