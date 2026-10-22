using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;
using System.ComponentModel;

namespace Arcadia.ControllerSupport;

/// <summary>How a status message is shown (owner, 2026-10-21); ordered, so the header shows the worst one.</summary>
public enum HealthSeverity
{
    /// <summary>Intended behaviour in a particular situation, e.g. SISR paused for a game: a blue i.</summary>
    Info,

    /// <summary>A feature works only partly and the user has to act, e.g. Steam not running: a yellow triangle.</summary>
    Warning,

    /// <summary>A lasting failure, e.g. SISR crashing again and again: a red X.</summary>
    Error,
}

/// <summary>What Arcadia can do about an issue, offered as a button next to it.</summary>
public enum HealthFix
{
    None,
    InstallSisr,
    InstallUsbip,
    StartSteam,
    EnableCefDebugging,
    RestartSteam,
    CreateMarker,
    StopExternalSisr,
    RestartSisr,
}

/// <summary>Whether the setting can be switched on, offered for installing, or not used at all.</summary>
public enum ControllerSupportSetup
{
    /// <summary>SISR, the usbip driver and Steam's CEF flag are in place: the switch is shown.</summary>
    Ready,

    /// <summary>Something is missing: "SISR installieren" is shown instead of the switch.</summary>
    NeedsInstall,

    /// <summary>SISR exists only for Windows.</summary>
    UnsupportedPlatform,

    /// <summary>SISR reads the controller through Steam Input, so it needs Steam.</summary>
    SteamMissing,
}

public sealed record HealthIssue(string Id, HealthSeverity Severity, string Title, string Detail, HealthFix Fix = HealthFix.None, string? FixLabel = null);

/// <summary>
/// Where Arcadia's SISR is in its life. Every change goes through one method, which knows the
/// allowed transitions; the UI reads only this state.
/// </summary>
public enum SisrState
{
    /// <summary>Steam Controller Support is switched off.</summary>
    Off,

    /// <summary>Switched on, but SISR doesn't run (not installed, crashed, about to start).</summary>
    Stopped,

    /// <summary>SISR runs but doesn't emulate a controller yet.</summary>
    Starting,

    /// <summary>SISR emulates a controller.</summary>
    Running,

    /// <summary>SISR is ready, but found no controller to emulate (e.g. the controller is off).</summary>
    NoController,

    Stopping,

    /// <summary>Stopped because a game runs that uses Steam Input itself.</summary>
    Paused,

    /// <summary>SISR crashed too often in a short time; Arcadia stopped restarting it.</summary>
    Failed,
}

/// <summary>
/// Steam Controller Support ("Steam Controller als Xbox 360 Gamepad verwenden"): keeps Arcadia's
/// SISR running while it is enabled, pauses it for games that use Steam Input themselves, watches
/// SISR and its dependencies, installs them, and checks for SISR updates.
/// </summary>
/// <remarks>
/// Everything that starts or stops SISR (the periodic check, game pauses, switching on and off,
/// updates, fixes) runs under one gate, and <see cref="StartSisr"/> checks there whether SISR may
/// run at all. So no path starts SISR while the support is off or a game has it paused.
/// </remarks>
public sealed class ControllerSupportService(
    SettingsService sharedSettings,
    ISisrHost sisr,
    HttpClient http,
    TimeProvider time,
    IControllerSupportPlatform? platform = null)
{
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupGrace = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Right after its start SISR may see a pad as Steam's virtual gamepad until it forces its marker's layout
    /// (about 2 s in sisr.log); only what it reports after that counts towards a pause for another controller.
    /// </summary>
    private static readonly TimeSpan OtherControllerGrace = TimeSpan.FromSeconds(5);

    /// <summary>How long Steam may take to log in before the settings say so; a normal start takes seconds.</summary>
    private static readonly TimeSpan SteamLoginGrace = TimeSpan.FromSeconds(30);

    /// <summary>How long switching on (or "Steam starten") waits for Steam before it gives up (owner, 2026-10-16).</summary>
    public static readonly TimeSpan SteamStartTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan CrashWindow = TimeSpan.FromMinutes(5);
    public const int MaxRestartsPerWindow = 3;

    /// <summary>Asking GitHub for the latest release; a hanging request must not hold up the periodic check.</summary>
    private static readonly TimeSpan ReleaseLookupTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Downloading and unpacking SISR or the usbip driver.</summary>
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// On exit, how long the polite way (wait for the running action, SISR's own quit) may take before SISR
    /// is killed. <c>App</c> waits for <see cref="ShutdownAsync"/> a little longer than this plus the kill.
    /// </summary>
    private static readonly TimeSpan ShutdownGraceful = TimeSpan.FromSeconds(3);

    private static readonly Dictionary<SisrState, SisrState[]> AllowedTransitions = new()
    {
        [SisrState.Off] = [SisrState.Stopped, SisrState.Starting, SisrState.Paused],
        // Stopped → Running/Stopping: a SISR that kept running across an update restart is adopted or stopped.
        [SisrState.Stopped] = [SisrState.Off, SisrState.Starting, SisrState.Running, SisrState.Stopping, SisrState.Paused, SisrState.Failed],
        [SisrState.Starting] = [SisrState.Running, SisrState.NoController, SisrState.Stopping, SisrState.Stopped],
        // Running ↔ NoController: the Steam Controller was switched off or on while SISR runs.
        [SisrState.Running] = [SisrState.NoController, SisrState.Stopping, SisrState.Stopped],
        [SisrState.NoController] = [SisrState.Running, SisrState.Stopping, SisrState.Stopped],
        [SisrState.Stopping] = [SisrState.Off, SisrState.Stopped, SisrState.Paused],
        [SisrState.Paused] = [SisrState.Off, SisrState.Stopped, SisrState.Starting],
        [SisrState.Failed] = [SisrState.Off, SisrState.Stopped, SisrState.Starting, SisrState.Stopping, SisrState.Paused],
    };

    private readonly IControllerSupportPlatform _platform = platform ?? new ControllerSupportPlatform();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _stateLock = new();
    private readonly List<DateTimeOffset> _restarts = [];

    /// <summary>SISR was stopped because Steam went away; starting it again with Steam is not a crash restart.</summary>
    private bool _heldForSteam;

    /// <summary>Since when Steam runs but hasn't finished logging in, or null.</summary>
    private DateTimeOffset? _steamLoginSince;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _pausedDirectories = new(StringComparer.OrdinalIgnoreCase);
    private SisrState _state;
    private ControllerSupportSetup? _setup;
    private int _startGeneration;
    private int _ticking;
    private int _watching;
    private int _pausedGames;
    private volatile bool _shuttingDown;

    /// <summary>Cancelled when Arcadia exits: ends the waits of an open start and of the actions under the gate.</summary>
    private readonly CancellationTokenSource _shutdown = new();
    private DateTimeOffset _lastStart = DateTimeOffset.MinValue;

    /// <summary>The Steam game SISR is paused for because Steam Input takes other controllers there too (#124).</summary>
    private volatile SteamInputPause? _steamInputPause;

    /// <summary>The running Steam app whose Steam Input setting was read and needs no pause; read once per game.</summary>
    private int? _steamInputCheckedFor;

    /// <summary>Other controllers SISR emulates while no Steam game runs, for the log and the settings.</summary>
    private volatile OtherControllers? _otherControllers;

    /// <summary>Checks in a row that saw SISR emulate another controller outside a game; one alone may be SISR starting up.</summary>
    private int _otherControllerSightings;

    /// <summary>The other controllers SISR is paused for outside a game (#143).</summary>
    private volatile OtherControllers? _otherControllerPause;

    /// <summary>"Erneut versuchen" ends the pause for another controller.</summary>
    private volatile bool _otherControllerRetry;

    /// <summary>Completed to have the watches of paused games look again now instead of after their next delay.</summary>
    private TaskCompletionSource _lookAtPausesNow = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _checkedThisSession;
    private bool _initialized;

    // Set by the state: Running saw a controller, NoController or Off saw none; the other states keep it.
    private volatile bool _controllerSeen;

    public event EventHandler? Changed;

    public AppSettings Settings => sharedSettings.Current;

    public bool Enabled => Settings.SteamControllerSupport;

    public bool IsSupportedPlatform => _platform.IsSupported;

    /// <summary>The settings are open; while off, the tick rechecks <see cref="Setup"/> only then.</summary>
    public bool WatchSetup { get; set; }

    /// <summary>What the settings offer; rechecked on every <see cref="RefreshAsync"/>, and by the tick while on or while the settings are open.</summary>
    public ControllerSupportSetup Setup => _setup ??= CheckSetup();

    /// <summary>
    /// A switched-on Steam Controller is there: SISR emulates it. Windows lists the wireless dongle
    /// as a Valve device even while the controller is off, so only SISR can tell (owner's PC, 2026-10-16).
    /// While SISR doesn't run (paused for a Steam Input game, starting again), the last thing SISR saw
    /// counts, so the mode choice doesn't vanish from the detail page during the game (owner, 2026-10-20).
    /// </summary>
    public bool SteamControllerConnected => Enabled && _controllerSeen;

    /// <summary>"SISR installieren" closes Steam first (after asking) and starts it again afterwards.</summary>
    public bool InstallStopsSteam => _platform.IsSteamRunning;

    public ISisrHost Sisr => sisr;

    public SisrState State
    {
        get
        {
            lock (_stateLock)
            {
                EnsureInitialized();
                return _state;
            }
        }
    }

    public IReadOnlyList<HealthIssue> Issues { get; private set; } = [];

    /// <summary>A newer SISR release than the installed one, if any.</summary>
    public Release? AvailableUpdate { get; private set; }

    /// <summary>Install directories of the games SISR is paused for; an update restart hands them on.</summary>
    public IReadOnlyCollection<string> PausedFor => _pausedDirectories.Keys.ToList();

    public string? BusyText { get; private set; }

    /// <summary>
    /// Steam, SISR or VIIPER are being started or stopped, by the user or in the background (the
    /// watchdog, Steam closing). The settings take no input then, so a switch can't cut into it.
    /// A Steam that runs but hangs before its login completes doesn't count: it can hang for good,
    /// and switching off must stay possible.
    /// </summary>
    public bool IsSettling => BusyText is not null || State is SisrState.Starting or SisrState.Stopping;

    /// <summary>The message of the last failed action, shown in the settings dialog.</summary>
    public string? LastError { get; private set; }

    /// <summary>Optional diagnostics sink (Arcadia's log file).</summary>
    public Action<string>? Log { get; set; }

    /// <summary>Raised right before SISR starts, so the UI can keep the foreground.</summary>
    public event EventHandler? SisrStarting;

    /// <summary>Raised once a freshly started SISR answers (or after a timeout); always follows <see cref="SisrStarting"/>.</summary>
    public event EventHandler? SisrStarted;

    /// <summary>
    /// Raised after SISR stopped and Steam was told to drop SISR's layout. Steam applies that only
    /// when the foreground window changes, so the UI should trigger one.
    /// </summary>
    public event EventHandler? ControllerReleased;

    private bool IsPaused => Volatile.Read(ref _pausedGames) > 0;

    /// <summary>
    /// Switching on gave up because Steam didn't start, and the settings haven't shown that yet
    /// (they were closed with B while Steam started). They show it the next time they open.
    /// </summary>
    public bool UnseenSteamStartFailure { get; private set; }

    public void AcknowledgeSteamStartFailure() => UnseenSteamStartFailure = false;

    /// <returns>False if switching on failed because Steam didn't start; the setting is off again then.</returns>
    public async Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        Log?.Invoke($"Steam Controller (SISR) {(enabled ? "ein" : "aus")}geschaltet");
        UpdateSettings(settings => settings with { SteamControllerSupport = enabled });
        var steamStarted = true;
        if (enabled)
        {
            // Installing is "SISR installieren"'s job; the switch is only offered when SISR is there.
            // SISR needs a running Steam, so switching on starts Steam first.
            await RunBusyAsync(_platform.IsSteamReady ? "SISR wird gestartet …" : "Steam wird gestartet …", async token =>
            {
                if (!await StartSteamAndWaitAsync(token))
                {
                    Log?.Invoke($"Steam ist nach {SteamStartTimeout.TotalSeconds:0} s nicht bereit; Steam Controller (SISR) wieder aus");
                    UpdateSettings(settings => settings with { SteamControllerSupport = false });
                    steamStarted = false;
                    UnseenSteamStartFailure = true;
                    return;
                }

                UnseenSteamStartFailure = false;
                SetBusyText("SISR wird gestartet …");
                _restarts.Clear();
                StartSisr();
            }, cancellationToken);
        }
        else
        {
            // Through the same gate as starting, so a quick off → on waits for SISR to be gone.
            await RunBusyAsync("SISR wird beendet …", StopSisrAndReleaseControllerAsync, cancellationToken);
        }

        await RefreshAsync(cancellationToken);
        return steamStarted;
    }

    /// <summary>Starts Steam unless it runs and waits until it is ready for SISR; false after <see cref="SteamStartTimeout"/>.</summary>
    private async Task<bool> StartSteamAndWaitAsync(CancellationToken cancellationToken)
    {
        if (_platform.IsSteamReady)
        {
            return true;
        }

        if (!_platform.IsSteamRunning)
        {
            _platform.StartSteam();
        }

        return await ProcessWatch.WaitUntilAsync(() => _platform.IsSteamReady, SteamStartTimeout, time, cancellationToken);
    }

    /// <summary>
    /// Why "SISR installieren" can't run right now, or null. Installing closes Steam, which would
    /// end a running Steam game.
    /// </summary>
    public string? InstallBlocker() =>
        SteamGameBlockingInstall() is { } game
            ? $"Ein laufendes Steam-Spiel ({game}) wurde erkannt. Beende es zuerst, da für die Installation Steam beendet und neu gestartet werden muss."
            : null;

    /// <summary>The name of the Steam game that keeps "SISR installieren" from closing Steam, or null.</summary>
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
        OnChanged();
    }

    /// <summary>
    /// "SISR installieren": closes Steam if it runs (the caller has asked the user), sets Steam's
    /// CEF flag, installs what is missing of SISR and the usbip driver, starts Steam again if it
    /// was closed here, and switches the setting on. On failure the setting stays off.
    /// <paramref name="markerAccounts"/>: the Steam accounts the user chose for SISR's marker
    /// (<see cref="MarkerAccountsToChoose"/>); null writes it for the logged-in or last account.
    /// </summary>
    public Task InstallAsync(IReadOnlyCollection<uint>? markerAccounts = null, CancellationToken cancellationToken = default) =>
        RunBusyAsync("SISR wird eingerichtet …", async token =>
        {
            if (InstallBlocker() is { } blocker)
            {
                throw new InvalidOperationException(blocker);
            }

            // Checked before Steam closes and the driver installs, not only when the marker is written.
            var accounts = _platform.SisrMarkerAccounts(sisr.ExecutablePath);
            if (accounts.Count == 0)
            {
                throw new InvalidOperationException(NoSteamAccountMessage);
            }

            // One account gets the marker without asking; with several that all have it, nothing is written.
            markerAccounts ??= accounts.Count > 1 && accounts.All(a => a.HasMarker) ? [] : null;

            var stoppedSteam = false;
            try
            {
                if (_platform.IsSteamRunning)
                {
                    // A SISR left running would lose Steam under it: its "Steam is not running"
                    // window, or Steam started again by its cleanup.
                    if (sisr.IsRunning)
                    {
                        SetBusyText("SISR wird beendet …");
                        await StopSisrAndReleaseControllerAsync(token);
                    }

                    SetBusyText("Steam wird beendet …");
                    if (!await _platform.StopSteamAsync(time, token))
                    {
                        throw new InvalidOperationException("Steam ließ sich nicht beenden. Beende Steam selbst und versuche es dann erneut.");
                    }

                    stoppedSteam = true;
                }

                // Steam reads the flag only when it starts, which is why it had to close.
                if (!_platform.IsCefDebuggingEnabled)
                {
                    _platform.EnableCefDebugging();
                }

                if (!sisr.IsInstalled)
                {
                    SetBusyText("SISR wird installiert …");
                    await InstallLatestSisrAsync(token);
                }

                if (!_platform.IsUsbipReady)
                {
                    SetBusyText("usbip-Treiber wird installiert – bitte die Windows-Abfrage bestätigen …");
                    if (!await WithTimeoutAsync(token => _platform.InstallUsbipAsync(http, token), InstallTimeout, "Der Treiber-Download", token))
                    {
                        throw new InvalidOperationException("Die Installation wurde in der Windows-Abfrage abgelehnt.");
                    }

                    UpdateSettings(settings => settings with { UsbipInstalledAt = time.GetUtcNow() });
                }

                // What SISR's own first-run setup would do, so SISR never shows a window. Steam is
                // closed here, so its shortcuts can be written.
                if (!EnsureSisrMarker(markerAccounts))
                {
                    throw new InvalidOperationException(_platform.IsSteamRunning
                        ? "Der Steam-Eintrag „SISR Marker“ konnte nicht angelegt werden, weil Steam während der Einrichtung wieder gestartet wurde. Beende Steam und versuche es erneut."
                        : NoSteamAccountMessage);
                }

                Log?.Invoke("Steam-Eintrag „SISR Marker“ vorhanden");
            }
            finally
            {
                // Arcadia closed Steam, so it brings it back, also after a failed install.
                if (stoppedSteam)
                {
                    _platform.StartSteam();
                }
            }

            Log?.Invoke("Steam Controller (SISR) eingerichtet");
            UpdateSettings(settings => settings with { SteamControllerSupport = true });
            _restarts.Clear();
            if (stoppedSteam)
            {
                // SISR may only start once the restarted Steam is up; if it takes longer, the tick starts it.
                SetBusyText("Steam wird gestartet …");
                await ProcessWatch.WaitUntilAsync(() => _platform.IsSteamReady, SteamStartTimeout, time, token);
            }

            StartSisr();
        }, cancellationToken);

    /// <summary>
    /// Restarts Steam itself, as "SISR installieren" does: SISR first (it would lose Steam under it),
    /// then Steam, optionally with the CEF flag that Steam reads only when it starts. SISR's own
    /// <c>steam/restart</c> isn't used: Arcadia would kill SISR as soon as Steam went away. Under the gate.
    /// </summary>
    private async Task RestartSteamAsync(bool enableCefDebugging, CancellationToken cancellationToken)
    {
        if (SteamGameBlockingInstall() is { } game)
        {
            throw new InvalidOperationException($"Ein laufendes Steam-Spiel ({game}) wurde erkannt. Beende es zuerst, da Steam dafür neu gestartet werden muss.");
        }

        await StopSisrAndReleaseControllerAsync(cancellationToken);
        if (_platform.IsSteamRunning && !await _platform.StopSteamAsync(time, cancellationToken))
        {
            throw new InvalidOperationException("Steam ließ sich nicht beenden. Beende Steam selbst und versuche es dann erneut.");
        }

        if (enableCefDebugging)
        {
            _platform.EnableCefDebugging();
        }

        SetBusyText("Steam wird gestartet …");
        _platform.StartSteam();
        if (!await ProcessWatch.WaitUntilAsync(() => _platform.IsSteamReady, SteamStartTimeout, time, cancellationToken))
        {
            throw new TimeoutException("Steam ist nach dem Neustart nicht innerhalb einer Minute bereit.");
        }

        _restarts.Clear();
        StartSisr();
    }

    /// <summary>How long Steam has been running without finishing its login; zero if it is ready or closed.</summary>
    private TimeSpan SteamLoginStuckFor()
    {
        if (!_platform.IsSteamRunning || _platform.IsSteamReady)
        {
            _steamLoginSince = null;
            return TimeSpan.Zero;
        }

        var now = time.GetUtcNow();
        _steamLoginSince ??= now;
        return now - _steamLoginSince.Value;
    }

    private const string NoSteamAccountMessage =
        "Der Steam-Eintrag „SISR Marker“ kann nicht angelegt werden: Es wurde kein Steam-Konto gefunden. Melde dich einmal in Steam an und versuche es dann erneut.";

    /// <summary>
    /// With several Steam accounts on this PC the user chooses which get SISR's marker (owner,
    /// 2026-10-16): it adds an entry to their Steam library. Empty with one account (it gets the marker
    /// without asking) or when every account has it.
    /// </summary>
    public IReadOnlyList<MarkerAccount> MarkerAccountsToChoose()
    {
        var accounts = _platform.SisrMarkerAccounts(sisr.ExecutablePath);
        return accounts.Count > 1 && accounts.Any(a => !a.HasMarker) ? accounts : [];
    }

    private bool EnsureSisrMarker(IReadOnlyCollection<uint>? accountIds)
    {
        try
        {
            return _platform.EnsureSisrMarker(sisr.ExecutablePath, accountIds);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Steams Liste der Nicht-Steam-Spiele (shortcuts.vdf) ist beschädigt; der Steam-Eintrag „SISR Marker“ konnte nicht angelegt werden.", ex);
        }
    }

    /// <summary>
    /// Stops SISR when Arcadia exits; nothing starts it again afterwards (#100). Whatever waits meanwhile
    /// (SISR coming up, Steam starting, a download) is cancelled, and SISR is asked to quit within
    /// <see cref="ShutdownGraceful"/>, then killed: a SISR that is still starting doesn't answer its API.
    /// </summary>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _shuttingDown = true;
        _shutdown.Cancel();

        using var graceful = new CancellationTokenSource(ShutdownGraceful, time);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, graceful.Token);
        var entered = false;
        try
        {
            try
            {
                await _gate.WaitAsync(limit.Token);
                entered = true;
                await StopSisrAndReleaseControllerAsync(limit.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Log?.Invoke($"SISR ließ sich nicht innerhalb von {ShutdownGraceful.TotalSeconds:0} Sekunden beenden");
            }

            if (sisr.IsRunning)
            {
                Log?.Invoke("SISR wird beim Beenden von Arcadia hart beendet");
                await Task.Run(sisr.Kill, CancellationToken.None);
                await sisr.CleanUpViiperAsync(CancellationToken.None);
                SettleAfterStop();
            }
        }
        finally
        {
            if (entered)
            {
                _gate.Release();
            }
        }
    }

    /// <summary>Per game: true = "Xbox 360 Controller (SISR)", false = "Steam Input".</summary>
    public void SetModeFor(Game game, bool useSisr) => UpdateSettings(settings => settings.WithControllerSupportChoice(game, useSisr));

    /// <summary>
    /// Called periodically: restarts a crashed SISR, rechecks health, checks for updates once a day.
    /// A call while the previous one still runs returns at once, so slow checks never pile up.
    /// </summary>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _ticking, 1) == 1)
        {
            return;
        }

        try
        {
            await TickOnceAsync(cancellationToken);
        }
        finally
        {
            Volatile.Write(ref _ticking, 0);
        }
    }

    private async Task TickOnceAsync(CancellationToken cancellationToken)
    {
        if (!Enabled || !IsSupportedPlatform)
        {
            // While off, only what the settings offer can change (e.g. SISR installed elsewhere).
            // That check reads the registry, driver list and Steam's shortcuts, so only while the
            // settings show it; they also get a fresh one when they open.
            var setup = Setup;
            if (WatchSetup)
            {
                UpdateSetup();
            }

            if (Issues.Count > 0 || Setup != setup)
            {
                Issues = [];
                OnChanged();
            }

            return;
        }

        await KeepSisrRunningAsync(cancellationToken);

        await UpdateControllerStateAsync(cancellationToken);

        // Once per start, then daily while Arcadia keeps running.
        if (!_checkedThisSession || Settings.LastSisrUpdateCheck is not { } last || time.GetUtcNow() - last > UpdateCheckInterval)
        {
            _checkedThisSession = true;
            await CheckForUpdateAsync(cancellationToken);
        }

        await RefreshAsync(cancellationToken);
    }

    /// <summary>Restarts a crashed SISR and takes over one that isn't Arcadia's, unless an action owns SISR right now.</summary>
    private async Task KeepSisrRunningAsync(CancellationToken cancellationToken)
    {
        if (!sisr.IsInstalled || !await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return;
        }

        try
        {
            // Checked again under the gate: the user may have switched off since the tick began.
            if (!Enabled || IsPaused || _shuttingDown)
            {
                return;
            }

            if (!_platform.IsSteamUp)
            {
                await HoldSisrWhileSteamIsDownAsync(cancellationToken);
                return;
            }

            var now = time.GetUtcNow();
            _restarts.RemoveAll(t => now - t > CrashWindow);
            if (!sisr.IsRunning)
            {
                if (State is SisrState.Starting or SisrState.Running or SisrState.NoController)
                {
                    Log?.Invoke("SISR ist unerwartet beendet");
                    MoveTo(SisrState.Stopped);
                }

                if (!_platform.IsSteamReady || !_platform.HasSisrMarker(sisr.ExecutablePath))
                {
                    // StartSisr wouldn't start it yet: Steam is still logging in, or the marker is
                    // missing (its own problem). Waiting for either is not a crash restart.
                    return;
                }

                if (_heldForSteam)
                {
                    // Stopped because Steam was gone, not crashed.
                    _heldForSteam = false;
                    StartSisr();
                    return;
                }

                if (_restarts.Count >= MaxRestartsPerWindow)
                {
                    MoveTo(SisrState.Failed);
                    return;
                }

                _restarts.Add(now);
                StartSisr();
            }
            else if (State == SisrState.Stopped)
            {
                // Kept running across an update restart: adopt it.
                MoveTo(SisrState.Running);
            }
            else if (now - _lastStart > StartupGrace && _restarts.Count < MaxRestartsPerWindow
                     && !await sisr.Api.IsReachableAsync(cancellationToken))
            {
                // Same SISR, but not started by Arcadia (e.g. from its tray shortcut), so its API
                // isn't on Arcadia's port. Take it over by restarting it with Arcadia's settings.
                _restarts.Add(now);
                await StopSisrAsync(cancellationToken);
                StartSisr();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Without Steam SISR may not run: it would show its own full-screen "Steam is not running" window
    /// and start Steam itself, also right after the user closed Steam. It starts again with Steam;
    /// exiting with Steam doesn't count as a crash. Call under the gate.
    /// </summary>
    private async Task HoldSisrWhileSteamIsDownAsync(CancellationToken cancellationToken)
    {
        if (sisr.IsRunning)
        {
            Log?.Invoke("Steam läuft nicht – SISR wird beendet, bis Steam wieder läuft");
            await StopSisrAsync(cancellationToken);
            _heldForSteam = true;
        }
        else if (State is SisrState.Starting or SisrState.Running or SisrState.NoController)
        {
            Log?.Invoke("SISR ist mit Steam beendet");
            MoveTo(SisrState.Stopped);
            _heldForSteam = true;
        }
    }

    /// <summary>
    /// The quick check (every 2 s): stops SISR as soon as Steam is gone and follows the Steam
    /// Controller being switched on or off, so the preview reacts fast. The rest waits for the tick.
    /// </summary>
    public async Task WatchAsync(CancellationToken cancellationToken = default)
    {
        // SISR's API can take up to its timeout to answer; a watch never overlaps the previous one.
        if (Interlocked.Exchange(ref _watching, 1) == 1)
        {
            return;
        }

        try
        {
            await WatchOnceAsync(cancellationToken);
        }
        finally
        {
            Volatile.Write(ref _watching, 0);
        }
    }

    private async Task WatchOnceAsync(CancellationToken cancellationToken)
    {
        if (!Enabled || !IsSupportedPlatform || !sisr.IsRunning)
        {
            return;
        }

        if (!_platform.IsSteamUp)
        {
            if (await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
            {
                try
                {
                    await HoldSisrWhileSteamIsDownAsync(cancellationToken);
                }
                finally
                {
                    _gate.Release();
                }

                await RefreshAsync(cancellationToken);
            }

            return;
        }

        await UpdateControllerStateAsync(cancellationToken);
    }

    private async Task UpdateControllerStateAsync(CancellationToken cancellationToken)
    {
        if (State is not (SisrState.NoController or SisrState.Running) || !sisr.IsRunning
            || await PauseForSteamInputGameAsync(cancellationToken))
        {
            return;
        }

        // SISR picks up a controller switched on (or off) after it started; the preview follows.
        if (await sisr.Api.GetEmulatedControllersAsync(cancellationToken) is not { } controllers)
        {
            return;
        }

        var others = OtherControllers.Of(controllers.Others);
        if (others is not null && _platform.RunningSteamAppId() is { } appId
            && await PauseForSteamGameAsync(appId, others.Names, $"SISR bildet einen anderen Controller als den Steam Controller nach ({others.ForLog})", cancellationToken))
        {
            return;
        }

        _otherControllerSightings = others is null ? 0
            : time.GetUtcNow() - _lastStart > OtherControllerGrace ? _otherControllerSightings + 1
            : _otherControllerSightings;
        if (others is not null && _otherControllerSightings >= 2 && await PauseForOtherControllerAsync(others, cancellationToken))
        {
            return;
        }

        if (_otherControllers != others)
        {
            _otherControllers = others;
            Log?.Invoke(others is null
                ? "SISR bildet nur noch Steam Controller nach"
                : $"SISR bildet einen anderen Controller als den Steam Controller nach, ohne dass ein Steam-Spiel läuft: {others.ForLog}");
        }

        if (controllers.SteamControllers > 0)
        {
            TryMove(SisrState.NoController, SisrState.Running);
        }
        else
        {
            TryMove(SisrState.Running, SisrState.NoController);
        }
    }

    /// <summary>
    /// A Steam game started outside Arcadia (or before SISR) whose Steam Input also takes other
    /// controllers: SISR pauses for it as <see cref="BeginGameAsync"/> would. Steam's setting is read
    /// once per game; false while no Steam game runs or the setting asks for no pause.
    /// </summary>
    private async Task<bool> PauseForSteamInputGameAsync(CancellationToken cancellationToken)
    {
        var appId = _platform.RunningSteamAppId();
        if (appId is null || appId == _steamInputCheckedFor)
        {
            _steamInputCheckedFor = appId;
            return false;
        }

        var setting = await Task.Run(() => _platform.SteamInputFor(appId.Value), cancellationToken);
        if (setting?.CoversOtherControllers != true)
        {
            Log?.Invoke($"Steam-Spiel {appId} läuft, Steam Input: {setting?.Description ?? "Einstellung nicht lesbar"}");
            _steamInputCheckedFor = appId;
            return false;
        }

        return await PauseForSteamGameAsync(appId.Value, controllers: null, $"Steam Input gilt auch für andere Controller ({setting.Description})", cancellationToken);
    }

    /// <summary>
    /// In a Steam game whose Steam Input takes other controllers than the Steam Controller (e.g. an Xbox pad),
    /// the game gets no input from them while SISR runs, whether or not SISR emulates them yet: on the owner's
    /// PC SISR took the pad late or never, and the game reacted to it only once SISR was gone (#124). SISR
    /// can't be told to leave one device alone, so it pauses until that game ends. In such a game Steam
    /// Input brings the Steam Controller too. False if an action holds the gate (the next check tries again).
    /// </summary>
    private async Task<bool> PauseForSteamGameAsync(int appId, string? controllers, string reason, CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return false;
        }

        try
        {
            // Checked again under the gate: something may have stopped SISR since the check began.
            if (!Enabled || IsPaused || _shuttingDown || !sisr.IsRunning)
            {
                return true;
            }

            var game = _platform.RunningSteamGame() ?? $"App {appId}";
            Log?.Invoke($"{reason}: SISR pausiert, bis „{game}“ endet");
            _otherControllers = null;
            PauseWhile(
                () => _platform.RunningSteamAppId() == appId,
                startTimeout: TimeSpan.Zero,
                $"Steam-Spiel {appId}",
                new SteamInputPause(game, controllers),
                ended: () => { });
            await StopSisrAndReleaseControllerAsync(cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Outside a game SISR emulates other controllers when Steam Input takes their whole type (Steam → Einstellungen →
    /// Controller → Erweiterte Controllereinstellungen). Steam then takes SISR's virtual pads again and SISR emulates
    /// those too: on the owner's PC (2026-10-22, #143) the Steam Controller worked only now and then, and pads with
    /// sticks stuck at an edge drove Arcadia for minutes. So SISR pauses until Steam's setting for Xbox and
    /// generic pads goes off (if it was on) or the user asks to try again. False if an action holds the gate.
    /// </summary>
    private async Task<bool> PauseForOtherControllerAsync(OtherControllers others, CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return false;
        }

        try
        {
            if (!Enabled || IsPaused || _shuttingDown || !sisr.IsRunning)
            {
                return true;
            }

            var defaults = await Task.Run(() => _platform.SteamInputDefaults(), cancellationToken);
            var coveredByDefault = defaults?.CoversOtherControllersByDefault == true;
            Log?.Invoke($"SISR bildet ohne Steam-Spiel einen anderen Controller nach ({others.ForLog}), Steam Input: {defaults?.Description ?? "Einstellung nicht lesbar"}. SISR pausiert, bis {(coveredByDefault ? "Steam Input für Xbox- und generische Controller aus ist oder " : string.Empty)}„Erneut versuchen“ gewählt wird");
            _otherControllers = null;
            _otherControllerSightings = 0;
            _otherControllerRetry = false;
            _otherControllerPause = others;
            PauseWhile(
                () => !_otherControllerRetry && !(coveredByDefault && _platform.SteamInputDefaults()?.CoversOtherControllersByDefault == false),
                startTimeout: TimeSpan.Zero,
                "anderer Controller",
                steamInput: null,
                ended: () =>
                {
                    _otherControllerPause = null;
                    Log?.Invoke(_otherControllerRetry ? "SISR wird erneut versucht" : "Steam Input für andere Controller ist aus: SISR läuft wieder");
                });
            await StopSisrAndReleaseControllerAsync(cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The owner's advice (2026-10-22) when another controller uses Steam Input outside a game.</summary>
    private const string SteamInputAdvice =
        "Es wird empfohlen, in Steam die Steam-Eingabe nur für den Steam Controller zu verwenden, wenn gleichzeitig die Steam Controller Kompatibilität in Arcadia genutzt wird. "
        + "Schalte sie daher für andere Gamepads in Steam unter Einstellungen → Controller → Erweiterte Controllereinstellungen aus. "
        + "Es kann sonst zu unvorhergesehenem Verhalten wie Phantomeingaben oder nicht erkannten Controllereingaben kommen.";

    /// <summary>Controllers other than the Steam Controller that SISR emulates: named for the settings, with USB ids for the log.</summary>
    private sealed record OtherControllers(string Names, int Count, string ForLog)
    {
        /// <summary>
        /// The pads by their own names; SISR's own virtual pads that Steam took again only carry Steam's or SDL's
        /// stand-in names (<see cref="SisrGamepad.HasStandInName"/>) and are left out unless nothing else is there.
        /// </summary>
        public static OtherControllers? Of(IReadOnlyList<SisrGamepad> pads)
        {
            if (pads.Count == 0)
            {
                return null;
            }

            var named = pads.Where(p => !p.HasStandInName).ToList();
            var shown = (named.Count > 0 ? named : pads).Select(p => p.DisplayName).Distinct().ToList();
            return new(string.Join(", ", shown), shown.Count, string.Join(", ", pads.Select(p => p.Description)));
        }

        /// <summary>The verb after <see cref="Names"/>.</summary>
        public string Use => Count == 1 ? "verwendet" : "verwenden";
    }

    /// <summary>What the settings say while SISR is paused for a Steam game with Steam Input for other controllers.</summary>
    private sealed record SteamInputPause(string Game, string? Controllers);

    private static int? SteamAppIdOf(Game game) =>
        game.LauncherId == KnownLaunchers.Steam.Id && int.TryParse(game.Id, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var appId)
            ? appId
            : null;

    public async Task CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        Release? latest;
        try
        {
            latest = await WithTimeoutAsync(sisr.GetLatestReleaseAsync, ReleaseLookupTimeout, "GitHub", cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or System.Text.Json.JsonException)
        {
            Log?.Invoke($"SISR-Update-Prüfung fehlgeschlagen: {ex.Message}");
            return;
        }

        UpdateSettings(settings => settings with { LastSisrUpdateCheck = time.GetUtcNow() });
        AvailableUpdate = latest?.Version is { } version && sisr.InstalledVersion is { } installed && version > installed ? latest : null;
        OnChanged();
    }

    public Task InstallUpdateAsync(CancellationToken cancellationToken = default) =>
        RunBusyAsync("SISR wird aktualisiert …", async token =>
        {
            var release = AvailableUpdate
                ?? await WithTimeoutAsync(sisr.GetLatestReleaseAsync, ReleaseLookupTimeout, "GitHub", token)
                ?? throw new InvalidOperationException("Die aktuelle SISR-Version konnte nicht ermittelt werden.");
            await StopSisrAsync(token);
            await WithTimeoutAsync(token => sisr.InstallAsync(release, token), InstallTimeout, "Der SISR-Download", token);
            AvailableUpdate = null;
            StartSisr();
        }, cancellationToken);

    public Task ApplyFixAsync(HealthFix fix, CancellationToken cancellationToken = default) => fix switch
    {
        HealthFix.InstallSisr => RunBusyAsync("SISR wird installiert …", async token =>
        {
            await InstallLatestSisrAsync(token);
            StartSisr();
        }, cancellationToken),
        HealthFix.InstallUsbip => RunBusyAsync("usbip-Treiber wird installiert – bitte die Windows-Abfrage bestätigen …", async token =>
        {
            if (!await WithTimeoutAsync(token => _platform.InstallUsbipAsync(http, token), InstallTimeout, "Der Treiber-Download", token))
            {
                throw new InvalidOperationException("Die Installation wurde in der Windows-Abfrage abgelehnt.");
            }

            // The update replaces the emulated USB host controller under VIIPER; SISR (and with it
            // VIIPER) must reconnect, otherwise the virtual controller stays attached to the old one.
            UpdateSettings(settings => settings with { UsbipInstalledAt = time.GetUtcNow() });
            await StopSisrAsync(token);
            _restarts.Clear();
            StartSisr();
        }, cancellationToken),
        HealthFix.StartSteam => RunBusyAsync("Steam wird gestartet …", async token =>
        {
            if (!await StartSteamAndWaitAsync(token))
            {
                throw new TimeoutException("Steam ließ sich nicht starten.");
            }

            SetBusyText("SISR wird gestartet …");
            _restarts.Clear();
            StartSisr();
        }, cancellationToken),
        HealthFix.EnableCefDebugging => RunBusyAsync("Steam-Debug-Schnittstelle wird aktiviert …", token => RestartSteamAsync(enableCefDebugging: true, token), cancellationToken),
        HealthFix.RestartSteam => RunBusyAsync("Steam wird neu gestartet …", token => RestartSteamAsync(enableCefDebugging: false, token), cancellationToken),
        // Closes Steam, so the settings ask first (or report a running Steam game) and call InstallAsync themselves.
        HealthFix.CreateMarker => throw new ArgumentException("Der Steam-Eintrag wird über InstallAsync angelegt, nachdem die Einstellungen gefragt haben.", nameof(fix)),
        HealthFix.StopExternalSisr => RunBusyAsync("Andere SISR-Instanz wird beendet …", token =>
        {
            sisr.StopExternalInstances();
            return Task.CompletedTask;
        }, cancellationToken),
        HealthFix.RestartSisr when _otherControllerPause is not null => RetryAfterOtherController(),
        HealthFix.RestartSisr => RunBusyAsync("SISR wird neu gestartet …", async token =>
        {
            await StopSisrAsync(token);
            _restarts.Clear();
            StartSisr();
        }, cancellationToken),
        _ => Task.CompletedTask,
    };

    /// <summary>
    /// Ends the pause for another controller at once and starts SISR. If Steam Input still takes another
    /// controller, SISR reports it within a few seconds, without any input on it, and the pause begins again.
    /// </summary>
    private Task RetryAfterOtherController()
    {
        _otherControllerRetry = true;
        Interlocked.Exchange(ref _lookAtPausesNow, new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called right before <paramref name="game"/> starts. If SISR must not run for it, SISR is
    /// stopped until the game has ended (watched through its install directory): on "Steam Input",
    /// and on "Xbox 360 Controller (SISR)" for a Steam game whose Steam Input takes other controllers
    /// too (see <see cref="PauseForSteamGameAsync"/>).
    /// </summary>
    public async Task BeginGameAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Only pause for games that will actually start and whose process we can watch; otherwise
        // SISR would sit paused waiting for a process that never appears.
        if (!Enabled || game.Launch is LaunchTarget.None || game.InstallDirectory is not { } directory || !Directory.Exists(directory))
        {
            return;
        }

        SteamInputPause? steamInput = null;
        if (Settings.ControllerSupportFor(game))
        {
            if (SteamAppIdOf(game) is not { } appId)
            {
                return;
            }

            // Steam's settings are a large file; the game start shouldn't wait on the UI thread for it.
            var setting = await Task.Run(() => _platform.SteamInputFor(appId), CancellationToken.None);
            Log?.Invoke($"„{game.Title}“ startet auf SISR, Steam Input: {setting?.Description ?? "Einstellung nicht lesbar"}");
            if (setting?.CoversOtherControllers != true)
            {
                return;
            }

            Log?.Invoke($"Steam Input gilt in „{game.Title}“ auch für andere Controller: SISR pausiert, bis das Spiel endet");
            steamInput = new SteamInputPause(game.Title, Controllers: null);
        }

        // Counted before waiting for the gate, so whatever holds it can't start SISR meanwhile.
        // Not cancellable: the watch below is what ends the pause again.
        Pause(directory, steamInput);
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            await StopSisrAndReleaseControllerAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Takes over a pause from before an update restart: SISR stays off while a process from
    /// <paramref name="directory"/> runs. Call before the first <see cref="TickAsync"/>.
    /// </summary>
    public void ResumePause(string directory)
    {
        if (!Directory.Exists(directory) || !_platform.AnyRunningFrom(directory))
        {
            return;
        }

        Log?.Invoke($"SISR bleibt pausiert für {directory}");
        Pause(directory);
        SettleRestingState();
    }

    private void Pause(string directory, SteamInputPause? steamInput = null)
    {
        _pausedDirectories.AddOrUpdate(directory, 1, (_, n) => n + 1);
        PauseWhile(() => _platform.AnyRunningFrom(directory), startTimeout: TimeSpan.FromMinutes(2), $"Spiel in {directory}", steamInput, ended: () =>
        {
            if (_pausedDirectories.AddOrUpdate(directory, 0, (_, n) => n - 1) <= 0)
            {
                _pausedDirectories.TryRemove(directory, out _);
            }
        });
    }

    /// <summary>
    /// Keeps SISR paused while <paramref name="running"/> holds, after waiting up to
    /// <paramref name="startTimeout"/> for it to begin; <paramref name="ended"/> runs under the gate when it is over.
    /// <paramref name="steamInput"/> is shown in the settings meanwhile.
    /// </summary>
    private void PauseWhile(Func<bool> running, TimeSpan startTimeout, string what, SteamInputPause? steamInput, Action ended)
    {
        if (steamInput is not null)
        {
            _steamInputPause = steamInput;
        }

        Interlocked.Increment(ref _pausedGames);
        OnChanged();
        _ = Task.Run(() => WatchPausedGameAsync(running, startTimeout, what, () =>
        {
            ended();
            if (steamInput is not null)
            {
                Interlocked.CompareExchange(ref _steamInputPause, null, steamInput);
            }
        }), CancellationToken.None);
    }

    private async Task WatchPausedGameAsync(Func<bool> running, TimeSpan startTimeout, string what, Action ended)
    {
        try
        {
            if (await ProcessWatch.WaitUntilAsync(running, startTimeout, time, CancellationToken.None))
            {
                while (running())
                {
                    await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(3), time), Volatile.Read(ref _lookAtPausesNow).Task);
                }
            }
        }
        catch (Exception ex)
        {
            // Ending the pause below matters more than why watching failed.
            Log?.Invoke($"{what} konnte nicht beobachtet werden: {ex}");
        }

        try
        {
            await EndPauseAsync(ended);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"SISR konnte nach dem Spiel nicht weiterlaufen: {ex}");
        }
    }

    private async Task EndPauseAsync(Action ended)
    {
        await _gate.WaitAsync();
        try
        {
            ended();
            if (Interlocked.Decrement(ref _pausedGames) == 0)
            {
                SettleRestingState();
                StartSisr();
            }
        }
        finally
        {
            _gate.Release();
            OnChanged();
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Issues = Enabled ? await CheckHealthAsync(cancellationToken) : [];
        UpdateSetup();
        OnChanged();
    }

    private void UpdateSetup()
    {
        _setup = CheckSetup();
    }

    private ControllerSupportSetup CheckSetup() =>
        !IsSupportedPlatform ? ControllerSupportSetup.UnsupportedPlatform
        : !_platform.IsSteamInstalled ? ControllerSupportSetup.SteamMissing
        : sisr.IsInstalled && _platform.IsUsbipReady && _platform.IsCefDebuggingEnabled && _platform.HasSisrMarker(sisr.ExecutablePath) ? ControllerSupportSetup.Ready
        : ControllerSupportSetup.NeedsInstall;

    private async Task<IReadOnlyList<HealthIssue>> CheckHealthAsync(CancellationToken cancellationToken)
    {
        if (!IsSupportedPlatform)
        {
            return [new("platform", HealthSeverity.Error, "Nur unter Windows verfügbar", "SISR gibt es bisher nur für Windows.")];
        }

        var issues = new List<HealthIssue>();
        if (!sisr.IsInstalled)
        {
            issues.Add(new("sisr-missing", HealthSeverity.Warning, "SISR ist nicht installiert", "Ohne SISR kommt der Steam Controller nicht als Gamepad an.", HealthFix.InstallSisr, "Installieren"));
        }

        issues.AddRange(_platform.CheckDependencies(Settings, time.GetUtcNow()));

        if (SteamLoginStuckFor() > SteamLoginGrace)
        {
            // Seen on the owner's PC: Steam hung at "loading user data" until Windows restarted.
            issues.Add(new("steam-login", HealthSeverity.Warning, "Steam meldet sich noch an", "SISR startet, sobald Steam angemeldet ist. Hängt Steam bei „Nutzerdaten werden geladen“, hilft oft ein Neustart von Steam.", HealthFix.RestartSteam, "Steam neu starten"));
        }

        if (sisr.IsInstalled && !_platform.HasSisrMarker(sisr.ExecutablePath))
        {
            // Read from Steam's shortcuts, not asked from SISR: SISR doesn't start without it.
            issues.Add(new("marker-missing", HealthSeverity.Warning, "Steam-Eintrag für SISR fehlt", "Über den Eintrag „SISR Marker“ legt Steam fest, welche Controller-Belegung SISR verwendet. Zum Anlegen wird Steam kurz beendet.", HealthFix.CreateMarker, "Anlegen"));
        }

        if (sisr.ExternalInstances().Count > 0)
        {
            issues.Add(new("sisr-external", HealthSeverity.Warning, "Ein SISR aus einem anderen Ordner läuft", "Zwei SISR-Instanzen erzeugen doppelte Controller. Die zweite sollte beendet werden.", HealthFix.StopExternalSisr, "Beenden"));
        }

        if (_steamInputPause is { } pause)
        {
            var controllers = pause.Controllers is { } names ? $" ({names})" : string.Empty;
            issues.Add(new("sisr-paused-other-controller", HealthSeverity.Info, $"SISR pausiert für „{pause.Game}“",
                $"In diesem Spiel gilt Steam Input auch für andere Controller als den Steam Controller{controllers}. Solange SISR läuft, bekäme das Spiel von ihnen keine Eingaben. Bis es endet, bekommt es alle Controller über Steam Input, auch den Steam Controller. Soll der Steam Controller als Xbox 360 Controller ankommen, schalte in Steam in den Controller-Einstellungen des Spiels Steam Input für die anderen Controller aus."));
        }

        if (_otherControllerPause is { } blocking)
        {
            issues.Add(new("sisr-paused-other-controller-type", HealthSeverity.Warning, "SISR pausiert wegen eines anderen Controllers",
                $"SISR wurde pausiert, weil {blocking.Names} in Steam ebenfalls Steam-Eingabe als Steuerungsmethode {blocking.Use}. {SteamInputAdvice}",
                HealthFix.RestartSisr, "Erneut versuchen"));
        }

        var state = State;
        if (state is SisrState.Running or SisrState.NoController && _otherControllers is { } others)
        {
            // Seen on the owner's PC (2026-10-22, #143): Steam Input on for a whole controller type in Steam's
            // settings hands that pad to SISR outside any game, and the Steam Controller stops working.
            issues.Add(new("sisr-other-controller", HealthSeverity.Warning, "Anderer Controller verwendet Steam-Eingabe",
                $"{others.Names} {others.Use} in Steam ebenfalls Steam-Eingabe als Steuerungsmethode. {SteamInputAdvice}"));
        }

        if (state == SisrState.Failed)
        {
            issues.Add(new("sisr-crashing", HealthSeverity.Error, "SISR stürzt wiederholt ab", "Arcadia hat SISR mehrfach neu gestartet. Details stehen in logs\\sisr.log.", HealthFix.RestartSisr, "Erneut starten"));
        }
        else if (state is SisrState.Running or SisrState.NoController && BusyText is null && time.GetUtcNow() - _lastStart > StartupGrace)
        {
            await CheckRunningSisrAsync(issues, cancellationToken);
        }

        return issues;
    }

    private async Task CheckRunningSisrAsync(List<HealthIssue> issues, CancellationToken cancellationToken)
    {
        var steam = await sisr.Api.GetSteamStatusAsync(cancellationToken);
        if (steam is null)
        {
            issues.Add(new("sisr-unresponsive", HealthSeverity.Warning, "SISR reagiert nicht", "SISR läuft, antwortet aber nicht auf Anfragen von Arcadia.", HealthFix.RestartSisr, "Neu starten"));
            return;
        }

        if (steam.SteamRunning && !steam.CefDebugEnabled)
        {
            issues.Add(new("cef-disabled", HealthSeverity.Warning, "Steam-Debug-Schnittstelle ist aus", "SISR braucht Steams lokale Debug-Schnittstelle (CEF). Zum Aktivieren startet Steam einmal neu.", HealthFix.EnableCefDebugging, "Aktivieren"));
        }
        else if (steam.SteamRunning && !steam.CefDebugReachable)
        {
            issues.Add(new("cef-unreachable", HealthSeverity.Warning, "Steam muss neu gestartet werden", "Die Debug-Schnittstelle ist aktiviert, aber erst nach einem Neustart von Steam erreichbar.", HealthFix.RestartSteam, "Steam neu starten"));
        }

        var viiper = await sisr.Api.PingViiperAsync(cancellationToken);
        if (string.IsNullOrEmpty(viiper?.Status?.Version))
        {
            issues.Add(new("viiper-down", HealthSeverity.Warning, "VIIPER antwortet nicht", "VIIPER erzeugt die virtuellen Controller für SISR. Ein Neustart von SISR startet es mit.", HealthFix.RestartSisr, "SISR neu starten"));
        }
    }

    private async Task InstallLatestSisrAsync(CancellationToken cancellationToken)
    {
        var release = await WithTimeoutAsync(sisr.GetLatestReleaseAsync, ReleaseLookupTimeout, "GitHub", cancellationToken)
            ?? throw new InvalidOperationException("Die aktuelle SISR-Version konnte nicht ermittelt werden.");
        await WithTimeoutAsync(token => sisr.InstallAsync(release, token), InstallTimeout, "Der SISR-Download", cancellationToken);
        AvailableUpdate = null;
    }

    /// <summary>
    /// Starts SISR if it may run: support on, no game paused, Arcadia not exiting. The only place
    /// that starts SISR; call it under the gate.
    /// </summary>
    private void StartSisr()
    {
        if (!Enabled || IsPaused || _shuttingDown || !IsSupportedPlatform)
        {
            return;
        }

        if (!_platform.IsSteamReady)
        {
            // See HoldSisrWhileSteamIsDownAsync; the tick starts SISR once Steam is up.
            Log?.Invoke("SISR-Start übersprungen: Steam läuft nicht");
            return;
        }

        if (!_platform.HasSisrMarker(sisr.ExecutablePath))
        {
            // SISR would ask to add a marker in its own window; the settings offer "Anlegen" instead.
            Log?.Invoke("SISR-Start übersprungen: Steam-Eintrag „SISR Marker“ fehlt");
            return;
        }

        if (sisr.IsRunning || !sisr.IsInstalled)
        {
            Log?.Invoke($"SISR-Start übersprungen (läuft: {sisr.IsRunning}, installiert: {sisr.IsInstalled})");
            return;
        }

        Log?.Invoke("SISR wird gestartet");
        _lastStart = time.GetUtcNow();
        int generation;
        lock (_stateLock)
        {
            generation = ++_startGeneration;
        }

        MoveTo(SisrState.Starting);
        SisrStarting?.Invoke(this, EventArgs.Empty);
        if (_shuttingDown)
        {
            // Arcadia began exiting while the UI thread prepared for the start; a SISR started now
            // would outlive Arcadia.
            MoveTo(SisrState.Stopped);
            return;
        }

        try
        {
            sisr.MarkInitialSetupDone();
            sisr.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Log?.Invoke($"SISR konnte nicht gestartet werden: {ex}");
            LastError = $"SISR konnte nicht gestartet werden: {ex.Message}";
            MoveTo(SisrState.Stopped);
            SisrStarted?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_shuttingDown)
        {
            // Arcadia began exiting while the process started; its shutdown may have looked before it existed.
            sisr.Kill();
            MoveTo(SisrState.Stopped);
            SisrStarted?.Invoke(this, EventArgs.Empty);
            return;
        }

        _ = Task.Run(() => WaitUntilReadyAsync(generation, _shutdown.Token));
    }

    /// <summary>
    /// Ready means: the API answers and SISR emulates at least one controller. SISR also loads
    /// Steam's overlay and has Steam force its layout right after its API comes up — that's when
    /// the foreground can jump, so "started" is only reported after that.
    /// </summary>
    private async Task WaitUntilReadyAsync(int generation, CancellationToken cancellationToken)
    {
        try
        {
            var deadline = time.GetUtcNow() + ReadyTimeout;
            SisrControllers? controllers = null;
            while (time.GetUtcNow() < deadline && sisr.IsRunning)
            {
                // Not cancelled itself: the API call has its own short timeout, and cancelling a request in flight
                // throws inside HttpClient, which shows up as an exception in the debugger on every exit (owner, 2026-10-22).
                controllers = await sisr.Api.GetEmulatedControllersAsync();
                if (controllers?.Total > 0)
                {
                    break;
                }

                if (!await DelayUnlessCancelledAsync(TimeSpan.FromMilliseconds(500), cancellationToken))
                {
                    break;
                }
            }

            if (controllers?.Total > 0 && time.GetUtcNow() - _lastStart < MinimumSettleTime)
            {
                await DelayUnlessCancelledAsync(MinimumSettleTime - (time.GetUtcNow() - _lastStart), cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                // Arcadia exits; its shutdown stops SISR.
                Log?.Invoke("Warten auf SISR abgebrochen: Arcadia wird beendet");
                return;
            }

            var steamControllers = controllers?.SteamControllers ?? 0;
            Log?.Invoke($"SISR bereit: {steamControllers} Steam Controller und {controllers?.Others.Count ?? 0} andere Controller über Steam Input");

            // Only if nothing stopped or restarted SISR meanwhile. Other controllers are the quick check's to pause for.
            var next = !sisr.IsRunning ? SisrState.Stopped : steamControllers > 0 ? SisrState.Running : SisrState.NoController;
            TryMove(SisrState.Starting, next, generation);
        }
        catch (OperationCanceledException)
        {
            // Arcadia exits; its shutdown stops SISR.
            Log?.Invoke("Warten auf SISR abgebrochen: Arcadia wird beendet");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"Warten auf SISR fehlgeschlagen: {ex}");
        }
        finally
        {
            SisrStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Waits, and returns false when cancelled first. Doesn't throw for that: exiting is no error.</summary>
    private async Task<bool> DelayUnlessCancelledAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var cancelled = new TaskCompletionSource();
        await using var registration = cancellationToken.Register(() => cancelled.TrySetResult());
        var elapsed = Task.Delay(delay, time, stop.Token);
        var first = await Task.WhenAny(elapsed, cancelled.Task);
        await stop.CancelAsync();
        return first == elapsed;
    }

    /// <summary>How long to wait for SISR to emulate a controller before giving up waiting.</summary>
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(10);

    /// <summary>SISR forces Steam's layout about two seconds after it starts.</summary>
    private static readonly TimeSpan MinimumSettleTime = TimeSpan.FromSeconds(3);

    /// <summary>Stops SISR without handing the controller back, e.g. to restart or update it. Call under the gate.</summary>
    private async Task StopSisrAsync(CancellationToken cancellationToken)
    {
        if (!sisr.IsRunning)
        {
            SettleAfterStop();
            return;
        }

        MoveTo(SisrState.Stopping);
        try
        {
            if (_platform.IsSteamUp)
            {
                await sisr.StopAsync(cancellationToken);
            }
            else
            {
                // SISR's own cleanup on quit opens a steam:// URL, which starts Steam again. That
                // includes a Steam that is still shutting down: steam.exe outlives the logout and
                // its web helper by a few seconds (owner's log, 2026-10-16). Kill waits for the
                // process, so it runs off the UI thread.
                Log?.Invoke("Steam läuft nicht (mehr) – SISR wird ohne Aufräumen beendet");
                await Task.Run(sisr.Kill, CancellationToken.None);
            }
        }
        finally
        {
            // A virtual controller left attached without VIIPER behind it is suspected of making
            // Steam hang at "loading user data" until Windows restarts (owner's PC, 2026-10-16).
            if (await sisr.CleanUpViiperAsync(CancellationToken.None) is > 0 and var leftovers)
            {
                Log?.Invoke($"Nach dem Beenden von SISR {leftovers} übrig gebliebene VIIPER-Prozesse oder -Geräte aufgeräumt");
            }

            SettleAfterStop();
        }
    }

    /// <summary>
    /// Stops SISR and hands the controller back to Steam's normal handling. SISR makes Steam force
    /// its marker's layout; Steam keeps that until the next focus change unless it is reset.
    /// Call under the gate.
    /// </summary>
    private async Task StopSisrAndReleaseControllerAsync(CancellationToken cancellationToken)
    {
        var wasRunning = sisr.IsRunning;
        Log?.Invoke($"SISR wird beendet (lief: {wasRunning})");
        if (wasRunning)
        {
            await sisr.Api.SetForceConfigAsync(false, cancellationToken);
        }

        await StopSisrAsync(cancellationToken);
        Log?.Invoke($"SISR beendet (läuft noch: {sisr.IsRunning})");
        // steam:// would start a closed Steam, so only a running one is told.
        if (wasRunning && _platform.IsSteamUp)
        {
            _platform.ResetSteamInputLayout();

            // Steam only applies the change on the next focus change.
            ControllerReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SettleAfterStop()
    {
        if (State is SisrState.Stopping or SisrState.Starting or SisrState.Running or SisrState.NoController)
        {
            MoveTo(SisrState.Stopping);
            MoveTo(RestingState);
        }
        else
        {
            SettleRestingState();
        }
    }

    /// <summary>Where SISR rests when it doesn't run: off, paused for a game, or waiting to start.</summary>
    private SisrState RestingState => !Enabled ? SisrState.Off : IsPaused ? SisrState.Paused : SisrState.Stopped;

    /// <summary>After the setting or a pause changed while SISR wasn't running.</summary>
    private void SettleRestingState()
    {
        if (State is SisrState.Off or SisrState.Stopped or SisrState.Paused || (State == SisrState.Failed && !Enabled))
        {
            MoveTo(RestingState);
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            _state = Enabled ? SisrState.Stopped : SisrState.Off;
            _initialized = true;
        }
    }

    private void MoveTo(SisrState next) => TryMove(from: null, next);

    /// <summary>
    /// The one place the state changes. With <paramref name="from"/> (and <paramref name="generation"/>)
    /// it changes only if nothing else changed it (or started SISR again) in the meantime.
    /// </summary>
    private bool TryMove(SisrState? from, SisrState next, int? generation = null)
    {
        SisrState previous;
        lock (_stateLock)
        {
            EnsureInitialized();
            previous = _state;
            if ((from is { } expected && previous != expected) || (generation is { } g && g != _startGeneration) || previous == next)
            {
                return false;
            }

            _state = next;
            _controllerSeen = next switch
            {
                SisrState.Running => true,
                SisrState.NoController or SisrState.Off => false,
                _ => _controllerSeen,
            };
        }

        Log?.Invoke(AllowedTransitions[previous].Contains(next)
            ? $"SISR: {previous} → {next}"
            : $"SISR: unerwarteter Zustandswechsel {previous} → {next}");
        OnChanged();
        return true;
    }

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
        }
        finally
        {
            SettleRestingState();
            BusyText = null;
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
        sharedSettings.Update(change);
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
