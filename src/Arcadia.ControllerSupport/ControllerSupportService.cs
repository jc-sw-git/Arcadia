using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Pan;
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
/// update at start, "Controller wiederherstellen", the per-game Steam Controller mode, and what the old Arcadia left of
/// SISR (#214): found, removed on request, never started.
/// </summary>
/// <remarks>Every action runs under one gate with its busy text, so two never cut into each other.</remarks>
public sealed class ControllerSupportService(
    SettingsService sharedSettings,
    HttpClient http,
    TimeProvider time,
    IControllerSupportPlatform? platform = null)
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

    private readonly IControllerSupportPlatform _platform = platform ?? new ControllerSupportPlatform();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _ticking;
    private volatile bool _shuttingDown;

    /// <summary>Cancelled when Arcadia exits: ends the actions under the gate and the waits for it.</summary>
    private readonly CancellationTokenSource _shutdown = new();

    public event EventHandler? Changed;

    public AppSettings Settings => sharedSettings.Current;

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

    /// <summary>What the old Arcadia (or the user) left of SISR (#214); null on other platforms and in tests without it.</summary>
    public SisrInstallation? Sisr { get; init; }

    /// <summary>Pan hides the puck or holds it for a firmware attempt now (#242): HidHide must not be reinstalled then.</summary>
    public Func<bool>? PanHoldsPuck { get; set; }

    public const string HidHideReinstallWhileHiddenText =
        "Solange Pan den Steam Controller versteckt, lässt sich HidHide nicht neu installieren. Schalte den Controller-Wächter (Pan) aus und versuche es dann noch einmal.";

    /// <summary>An action that has to close Steam stops before it changes anything when Steam doesn't exit.</summary>
    public const string SteamWontCloseText = "Steam ließ sich nicht beenden. Beende Steam selbst und versuche es dann erneut.";

    /// <summary>Steam's client runs: it could update the Steam Controller's firmware (#242).</summary>
    public bool SteamRuns => _platform.IsSteamRunning;

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
    public bool StopsSteamFor(HealthFix fix) => _platform.IsSteamRunning && RestartsPuck(fix);

    private bool RestartsPuck(HealthFix fix) => fix switch
    {
        HealthFix.ReinstallHidHide => true,
        HealthFix.InstallDrivers => _platform.InstallsHidHide,
        _ => false,
    };

    /// <summary>The name of the Steam game that keeps an action from closing Steam, or null.</summary>
    /// <remarks>Read fresh on every call, so the settings notice the game ending.</remarks>
    public string? SteamGameBlockingInstall() => _platform.IsSteamRunning ? _platform.RunningSteamGame() : null;

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

    /// <summary>Per game: true = "Arcadia", false = "Steam-Eingabe".</summary>
    public void SetModeFor(Game game, bool useArcadiaPad) => UpdateSettings(settings => settings.WithControllerSupportChoice(game, useArcadiaPad, GamesUseSteamInput));

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

            var sisr = SisrFound;
            if (WatchSetup)
            {
                SisrFound = FindSisr();
            }

            // Pan's HidHide states are asked only while the settings are open, so they go with them: "Bitte schließe
            // sie" must not stay in the header after the user closed HidHide's configuration (QE review of PR #264).
            var issues = WatchSetup || !_driversChecked
                ? await CheckEmulationPartsAsync(cancellationToken)
                : Issues.Where(issue => !EmulationParts.IsSettingsOnly(issue)).ToList();
            _driversChecked = true;
            if (!issues.SequenceEqual(Issues) || SisrFound != sisr)
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

    public const string DriversBusyText = "Treiber werden installiert – bitte die Windows-Abfrage bestätigen …";

    /// <summary>
    /// What is missing of the controller emulation (#243): VIIPER (no prompt), then the usbip driver, HidHide and Pan in
    /// one Windows prompt, only if one of them is missing.
    /// Inside a busy action; throws when the prompt was declined, so the action fails visibly.
    /// </summary>
    private async Task InstallDriversAsync(CancellationToken cancellationToken)
    {
        if (Viiper is { IsInstalled: false } own)
        {
            SetBusyText("VIIPER wird installiert …");
            await WithTimeoutAsync(token => own.InstallAsync(http, token), InstallTimeout, "Der VIIPER-Download", cancellationToken);
            Log?.Invoke("VIIPER eingerichtet");
        }

        if (_platform.IsUsbipReady && _platform.IsPanReady && !ControllerSupportPlatform.HidHideNeedsInstaller(_platform.HidHideVersion))
        {
            return;
        }

        var usbipWasReady = _platform.IsUsbipReady;
        try
        {
            SetBusyText(DriversBusyText);
            var installed = await WithSteamClosedAsync(_platform.InstallsHidHide, "die Treiber-Installation (der Puck startet dabei neu)", DriverInstallBlocker,
                token => WithTimeoutAsync(t => _platform.InstallDriversAsync(http, t), DriversInstallTimeout, "Die Treiber-Installation", token),
                waitForSteam: installed => installed, cancellationToken);
            if (!installed)
            {
                throw new InvalidOperationException("Die Installation wurde in der Windows-Abfrage abgelehnt – noch einmal „Installieren“ wählen und die Abfrage bestätigen.");
            }
        }
        finally
        {
            // Also when a later step failed (e.g. HidHide, Pan's exit code 6): the driver came, and with it the restart it wants.
            if (!usbipWasReady && _platform.IsUsbipReady)
            {
                UpdateSettings(settings => settings with { UsbipInstalledAt = time.GetUtcNow() });
            }
        }

        Log?.Invoke("Treiber eingerichtet (usbip-win2, HidHide und Pan)");
    }

    /// <summary>
    /// Runs <paramref name="work"/>, with Steam closed when <paramref name="needsSteamClosed"/> and Steam runs: closed
    /// first (the caller has asked the user) and started again afterwards, also after a failed, declined or cancelled
    /// <paramref name="work"/>. A Steam game in the way (<paramref name="blocker"/>), or a Steam that won't close, stops
    /// it before anything changes. Waits for the restarted Steam when <paramref name="waitForSteam"/> says so, but
    /// doesn't need it to come back in time.
    /// </summary>
    private async Task<T> WithSteamClosedAsync<T>(bool needsSteamClosed, string reason, Func<string, string> blocker,
        Func<CancellationToken, Task<T>> work, Func<T, bool> waitForSteam, CancellationToken cancellationToken)
    {
        if (!needsSteamClosed || !_platform.IsSteamRunning)
        {
            return await work(cancellationToken);
        }

        if (SteamGameBlockingInstall() is { } game)
        {
            throw new InvalidOperationException(blocker(game));
        }

        var busy = BusyText;
        SetBusyText("Steam wird beendet …");
        if (!await _platform.StopSteamAsync(time, cancellationToken))
        {
            throw new InvalidOperationException(SteamWontCloseText);
        }

        Log?.Invoke($"Steam beendet für {reason}");
        T result;
        try
        {
            SetBusyText(busy ?? "Bitte warten …");
            result = await work(cancellationToken);
        }
        finally
        {
            // Arcadia closed Steam, so it brings it back.
            _platform.StartSteam();
        }

        if (waitForSteam(result))
        {
            SetBusyText("Steam wird gestartet …");
            await ProcessWatch.WaitUntilAsync(() => _platform.IsSteamReady, SteamStartTimeout, time, cancellationToken);
        }

        return result;
    }

    /// <summary>Why a driver install that closes Steam can't run now: closing Steam would end <paramref name="game"/>.</summary>
    public static string DriverInstallBlocker(string game) =>
        $"Ein laufendes Steam-Spiel ({game}) wurde erkannt. Beende es zuerst, da Steam für die Installation kurz beendet werden muss.";

    /// <summary>What of SISR is on the PC (#214), as of the last check; null until checked or without <see cref="Sisr"/>.</summary>
    public SisrFound? SisrFound { get; private set; }

    /// <summary>The group "SISR" shows: SISR or its marker is there and the player didn't choose "SISR behalten".</summary>
    public bool ShowsSisrGroup => SisrFound is { ShowsGroup: true } && !Settings.KeepSisr;

    /// <summary>"SISR entfernen" has to close Steam first: the marker is there and Steam runs (it writes it back on exit).</summary>
    public bool SisrRemovalStopsSteam => SisrFound is { HasMarker: true } && _platform.IsSteamRunning;

    /// <summary>Why the last "SISR entfernen" left something, until it runs again; the group row names it.</summary>
    public string? SisrRemovalCause { get; private set; }

    /// <summary>"SISR entfernen" removed everything: the settings show the toast and move the focus.</summary>
    public event EventHandler? SisrRemoved;

    public const string SisrRemovingText = "SISR wird entfernt …";

    /// <summary>Why "SISR entfernen" can't close Steam now: it would end <paramref name="game"/>.</summary>
    public static string SisrRemovalBlocker(string game) =>
        $"„{game}“ läuft gerade über Steam. Beende das Spiel zuerst, dann kann Arcadia SISR entfernen.";

    /// <summary>
    /// "SISR entfernen" (#214): stops SISR, closes Steam for the marker if it runs (the settings asked), removes what is
    /// there and starts Steam again, also after a failure. What is left stays in the group with its cause.
    /// </summary>
    public Task RemoveSisrAsync(CancellationToken cancellationToken = default) =>
        RunBusyAsync(SisrRemovingText, async token =>
        {
            if (Sisr is not { } sisr)
            {
                return;
            }

            // Before Steam closes: an SISR that won't stop keeps its files, and Steam needn't close for nothing.
            if (!await Task.Run(sisr.StopForRemoval, token))
            {
                SisrRemovalCause = SisrInstallation.StopFailedCause;
                throw new InvalidOperationException(SisrInstallation.StopFailedCause);
            }

            var result = await WithSteamClosedAsync(sisr.Find().HasMarker, "„SISR entfernen“", SisrRemovalBlocker,
                async t =>
                {
                    SetBusyText(SisrRemovingText);
                    return await Task.Run(() => sisr.Remove(_platform.IsSteamRunning), t);
                },
                waitForSteam: _ => true, token);
            SisrFound = sisr.Find();
            SisrRemovalCause = result.Complete && !SisrFound.ShowsGroup ? null : result.Cause ?? SisrInstallation.SteamRunningCause;
            Log?.Invoke(result.Complete ? "SISR entfernt" : $"SISR nicht ganz entfernt: {string.Join(", ", result.NotRemoved)} ({result.Cause})");
            if (SisrRemovalCause is null)
            {
                SisrRemoved?.Invoke(this, EventArgs.Empty);
            }
        }, cancellationToken);

    /// <summary>
    /// Once at start (#214 decision 6): the old Arcadia kept SISR running across its update, so an SISR it started goes;
    /// one the user started stays. Never throws.
    /// </summary>
    public async Task StopSisrStartedByOldArcadiaAsync(CancellationToken cancellationToken = default)
    {
        if (Sisr is not { } sisr)
        {
            return;
        }

        try
        {
            if (await Task.Run(sisr.StopStartedByOldArcadia, cancellationToken) is > 0 and var stopped)
            {
                Log?.Invoke($"SISR beendet, das die frühere Arcadia-Version gestartet hatte ({stopped})");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log?.Invoke($"SISR der früheren Arcadia-Version ließ sich nicht beenden: {ex}");
        }
    }

    private SisrFound? FindSisr()
    {
        try
        {
            return Sisr?.Find();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Steam or SISR is writing; the last answer stands.
            return SisrFound;
        }
    }

    /// <summary>
    /// Set at start while the installed Pan isn't the bundled one (#297) until it is replaced or removed: Pan's emulation
    /// waits meanwhile, as the update ends whatever the old Pan runs.
    /// </summary>
    public bool IsPanUpdateDue
    {
        get => Volatile.Read(ref _panUpdateDue);
        private set => Volatile.Write(ref _panUpdateDue, value);
    }

    private bool _panUpdateDue;

    /// <summary>
    /// Once at start, before the library shows (#297): reads the bundled and the installed Pan.exe. Not installed: no
    /// update; installed (also switched off in Arcadia) and not exactly the bundled one: <see cref="IsPanUpdateDue"/>.
    /// </summary>
    public bool CheckPanAtStart()
    {
        var started = time.GetTimestamp();
        var compatibility = IsSupportedPlatform ? _platform.PanCompatibility : PanCompatibility.NotInstalled;
        if (compatibility is not PanCompatibility.NotInstalled)
        {
            // On the start path before the window: the time it takes is part of the start budget.
            Log?.Invoke($"Pan geprüft in {time.GetElapsedTime(started).TotalMilliseconds:0} ms: {compatibility}");
        }

        if (compatibility is PanCompatibility.UpdateRequired)
        {
            Log?.Invoke("Pan: die installierte Version ist nicht die mitgelieferte – Arcadia ersetzt sie vor dem Start");
        }
        else if (compatibility is PanCompatibility.NotBundled)
        {
            Log?.Invoke("Pan: diese Arcadia-Version bringt kein Pan.exe mit – keine Prüfung");
        }

        IsPanUpdateDue = compatibility is PanCompatibility.UpdateRequired;
        return IsPanUpdateDue;
    }

    /// <summary>
    /// The mandatory update at start (#297): only Pan, with one Windows prompt; no driver, no download, Steam may run.
    /// False if the prompt was declined; throws when Pan failed or didn't answer in time (its log says why).
    /// </summary>
    public async Task<bool> UpdatePanAsync(CancellationToken cancellationToken = default)
    {
        if (!await WithTimeoutAsync(_platform.UpdatePanAsync, DriversInstallTimeout, "Die Aktualisierung von Pan", cancellationToken))
        {
            Log?.Invoke("Pan-Aktualisierung in der Windows-Abfrage abgelehnt");
            return false;
        }

        IsPanUpdateDue = false;
        _driversChecked = false;
        Log?.Invoke("Pan aktualisiert");
        OnChanged();
        return true;
    }

    /// <summary>
    /// "Pan entfernen" when the update at start fails (#297): Pan's <c>--uninstall</c> without the drivers, one prompt;
    /// Arcadia then runs without Pan, switched off. False if the prompt was declined.
    /// </summary>
    public async Task<bool> RemovePanAsync(CancellationToken cancellationToken = default)
    {
        if (!await WithTimeoutAsync(_platform.UninstallPanAsync, DriversInstallTimeout, "Das Entfernen von Pan", cancellationToken))
        {
            Log?.Invoke("Entfernen von Pan in der Windows-Abfrage abgelehnt");
            return false;
        }

        IsPanUpdateDue = false;
        _driversChecked = false;
        UpdateSettings(settings => settings with { PanEnabled = false });
        Log?.Invoke("Pan entfernt, Pan ist ausgeschaltet");
        OnChanged();
        return true;
    }

    /// <summary>"Controller wiederherstellen": Pan's <c>--restore</c>, elevated, for when the service itself hangs (#243).</summary>
    public Task RestoreControllersAsync(CancellationToken cancellationToken = default) =>
        RunBusyAsync("Controller werden wiederhergestellt – bitte die Windows-Abfrage bestätigen …", async token =>
        {
            if (!await _platform.RestoreControllersAsync(token))
            {
                throw new InvalidOperationException("Die Wiederherstellung wurde in der Windows-Abfrage abgelehnt.");
            }

            Log?.Invoke("Controller über Pan wiederhergestellt");
            ControllersRestored?.Invoke(this, EventArgs.Empty);
        }, cancellationToken);

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
        try
        {
            await RunFixAsync(fix, cancellationToken);
        }
        finally
        {
            _applyingFix = null;
        }
    }

    private Task RunFixAsync(HealthFix fix, CancellationToken cancellationToken) => fix switch
    {
        HealthFix.InstallDrivers => RunBusyAsync(DriversBusyText, InstallDriversAsync, cancellationToken),
        HealthFix.ReinstallHidHide => RunBusyAsync(DriversBusyText, async token =>
        {
            // Pan runs HidHide's installer before it replaces itself: a filter reinstall while the puck is hidden or held
            // for a firmware attempt could show Steam the real puck mid-attempt (decision 8).
            if (PanHoldsPuck?.Invoke() == true)
            {
                throw new InvalidOperationException(HidHideReinstallWhileHiddenText);
            }

            // Checked before Steam closes: a reinstall that can't run must not close it for nothing.
            if (_platform.HidHideVersion is not { } installed || !HidHideRelease.IsPinnedDriver(installed))
            {
                throw new InvalidOperationException($"„Neu installieren“ geht nur über HidHide {HidHideRelease.Version}; installiert ist eine andere Version.");
            }

            // HidHide answering unexpectedly means Pan can't hide: the puck is Steam's, and the installer restarts it.
            var reinstalled = await WithSteamClosedAsync(needsSteamClosed: true, "die HidHide-Installation (der Puck startet dabei neu)", DriverInstallBlocker,
                ct => WithTimeoutAsync(t => _platform.ReinstallHidHideAsync(http, t), DriversInstallTimeout, "Die HidHide-Installation", ct),
                waitForSteam: done => done, token);
            if (!reinstalled)
            {
                throw new InvalidOperationException("Die Installation wurde in der Windows-Abfrage abgelehnt – noch einmal „Neu installieren“ wählen und die Abfrage bestätigen.");
            }

            Log?.Invoke("HidHide neu installiert");
        }, cancellationToken),
        _ => Task.CompletedTask,
    };

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Issues = IsSupportedPlatform ? await CheckEmulationPartsAsync(cancellationToken) : [];
        _panInstalled = CheckPanInstalled();
        SisrFound = IsSupportedPlatform ? FindSisr() : null;
        OnChanged();
    }

    /// <summary>
    /// The rows under Pan (#243 part B2), in the order usbip-win2, VIIPER, Pan, HidHide: the platform's driver checks,
    /// VIIPER, while the settings show them why Pan couldn't hide the Steam Controller now (HidHide's driver not
    /// running, inverse mode, ..., its configuration open), and an SISR running beside Pan (#214).
    /// </summary>
    private async Task<IReadOnlyList<HealthIssue>> CheckEmulationPartsAsync(CancellationToken cancellationToken)
    {
        var issues = _platform.CheckDrivers(Settings, time.GetUtcNow()).ToList();
        if (Viiper is { IsInstalled: false })
        {
            issues.Add(EmulationParts.ViiperMissing);
        }

        // Only while the settings are open: asking Pan costs a pipe round trip and HidHide's lock.
        if (WatchSetup && HidingCheck is { } check && _platform.IsPanReady
            && _platform.HidHideVersion is { } hidHide && HidHideRelease.IsSupported(hidHide)
            && !issues.Contains(EmulationParts.HidHideRemovalPending)
            && EmulationParts.FromHidingCheck(await check.CheckHidingAsync(cancellationToken), hidHide) is { } hiding)
        {
            issues.Add(hiding);
        }

        // Also while the group "SISR" is hidden ("SISR behalten"): two programs on the Steam Controller break both.
        if (WatchSetup && Sisr?.IsAnyRunning == true)
        {
            issues.Add(EmulationParts.SisrRunning);
        }

        return EmulationParts.InRowOrder(issues);
    }

    /// <summary>Pan, the guardian service, is installed (#243); rechecked with every refresh. "Controller wiederherstellen" needs it.</summary>
    public bool PanInstalled => _panInstalled ??= CheckPanInstalled();

    private bool? _panInstalled;

    private bool CheckPanInstalled() => IsSupportedPlatform && _platform.IsPanReady;

    /// <summary>
    /// Runs an action under the gate with the busy text shown. Exiting Arcadia cancels the token the
    /// action gets (and the wait for the gate), so the shutdown isn't held up by a download or by Steam starting.
    /// </summary>
    private async Task RunBusyAsync(string text, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
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
        using var timer = new CancellationTokenSource(timeout, time);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        try
        {
            return await call(limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"{what} hat nicht innerhalb von {timeout.TotalSeconds:0} Sekunden geantwortet.");
        }
    }

    private void SetBusyText(string text)
    {
        BusyText = text;
        OnChanged();
    }

    private void UpdateSettings(Func<AppSettings, AppSettings> change)
    {
        try
        {
            sharedSettings.Update(change);
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
