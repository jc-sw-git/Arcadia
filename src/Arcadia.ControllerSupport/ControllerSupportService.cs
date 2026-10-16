using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using System.ComponentModel;

namespace Arcadia.ControllerSupport;

public enum HealthSeverity
{
    Warning,
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
    SettingsStore store,
    ISisrHost sisr,
    HttpClient http,
    TimeProvider time,
    IControllerSupportPlatform? platform = null)
{
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupGrace = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan CrashWindow = TimeSpan.FromMinutes(5);
    public const int MaxRestartsPerWindow = 3;

    /// <summary>Asking GitHub for the latest release; a hanging request must not hold up the periodic check.</summary>
    private static readonly TimeSpan ReleaseLookupTimeout = TimeSpan.FromSeconds(20);

    /// <summary>Downloading and unpacking SISR or the usbip driver.</summary>
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(5);

    /// <summary>On exit, how long to wait for a running action before stopping SISR anyway.</summary>
    private static readonly TimeSpan ShutdownWait = TimeSpan.FromSeconds(2);

    private static readonly Dictionary<SisrState, SisrState[]> AllowedTransitions = new()
    {
        [SisrState.Off] = [SisrState.Stopped, SisrState.Starting, SisrState.Paused],
        // Stopped → Running/Stopping: a SISR that kept running across an update restart is adopted or stopped.
        [SisrState.Stopped] = [SisrState.Off, SisrState.Starting, SisrState.Running, SisrState.Stopping, SisrState.Paused, SisrState.Failed],
        [SisrState.Starting] = [SisrState.Running, SisrState.NoController, SisrState.Stopping, SisrState.Stopped],
        [SisrState.Running] = [SisrState.Stopping, SisrState.Stopped],
        [SisrState.NoController] = [SisrState.Running, SisrState.Stopping, SisrState.Stopped],
        [SisrState.Stopping] = [SisrState.Off, SisrState.Stopped, SisrState.Paused],
        [SisrState.Paused] = [SisrState.Off, SisrState.Stopped, SisrState.Starting],
        [SisrState.Failed] = [SisrState.Off, SisrState.Stopped, SisrState.Starting, SisrState.Stopping, SisrState.Paused],
    };

    private readonly IControllerSupportPlatform _platform = platform ?? new ControllerSupportPlatform();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _stateLock = new();
    private readonly List<DateTimeOffset> _restarts = [];
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _pausedDirectories = new(StringComparer.OrdinalIgnoreCase);
    private SisrState _state;
    private ControllerSupportSetup? _setup;
    private int _startGeneration;
    private int _ticking;
    private int _pausedGames;
    private volatile bool _shuttingDown;
    private DateTimeOffset _lastStart = DateTimeOffset.MinValue;
    private bool _checkedThisSession;
    private bool _initialized;

    public event EventHandler? Changed;

    public AppSettings Settings { get; private set; } = store.Load();

    public bool Enabled => Settings.SteamControllerSupport;

    public bool IsSupportedPlatform => _platform.IsSupported;

    /// <summary>What the settings offer; rechecked on every <see cref="RefreshAsync"/> and tick.</summary>
    public ControllerSupportSetup Setup => _setup ??= CheckSetup();

    /// <summary>A Steam Controller is plugged in (only checked while the setting is on).</summary>
    public bool SteamControllerConnected { get; private set; }

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

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        Log?.Invoke($"Steam Controller (SISR) {(enabled ? "ein" : "aus")}geschaltet");
        UpdateSettings(Settings with { SteamControllerSupport = enabled });
        if (enabled)
        {
            // Installing is "SISR installieren"'s job; the switch is only offered when SISR is there.
            await RunBusyAsync("SISR wird gestartet …", () =>
            {
                StartSisr();
                return Task.CompletedTask;
            });
        }
        else
        {
            // Through the same gate as starting, so a quick off → on waits for SISR to be gone.
            await RunBusyAsync("SISR wird beendet …", () => StopSisrAndReleaseControllerAsync(cancellationToken));
        }

        await RefreshAsync(cancellationToken);
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
    /// </summary>
    public Task InstallAsync(CancellationToken cancellationToken = default) =>
        RunBusyAsync("SISR wird eingerichtet …", async () =>
        {
            if (InstallBlocker() is { } blocker)
            {
                throw new InvalidOperationException(blocker);
            }

            var stoppedSteam = false;
            try
            {
                if (_platform.IsSteamRunning)
                {
                    SetBusyText("Steam wird beendet …");
                    if (!await _platform.StopSteamAsync(time, cancellationToken))
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
                    await InstallLatestSisrAsync(cancellationToken);
                }

                if (!_platform.IsUsbipReady)
                {
                    SetBusyText("usbip-Treiber wird installiert – bitte die Windows-Abfrage bestätigen …");
                    if (!await WithTimeoutAsync(token => _platform.InstallUsbipAsync(http, token), InstallTimeout, "Der Treiber-Download", cancellationToken))
                    {
                        throw new InvalidOperationException("Die Installation wurde in der Windows-Abfrage abgelehnt.");
                    }

                    UpdateSettings(Settings with { UsbipInstalledAt = time.GetUtcNow() });
                }
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
            UpdateSettings(Settings with { SteamControllerSupport = true });
            _restarts.Clear();
            StartSisr();
        });

    /// <summary>Stops SISR when Arcadia exits; nothing starts it again afterwards.</summary>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _shuttingDown = true;
        var entered = await _gate.WaitAsync(ShutdownWait, cancellationToken);
        try
        {
            await StopSisrAndReleaseControllerAsync(cancellationToken);
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
    public void SetModeFor(Game game, bool useSisr) => UpdateSettings(Settings.WithControllerSupportChoice(game, useSisr));

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
            var setup = Setup;
            UpdateSetup();
            if (Issues.Count > 0 || Setup != setup)
            {
                Issues = [];
                OnChanged();
            }

            return;
        }

        await KeepSisrRunningAsync(cancellationToken);

        // A controller switched on after SISR started gets picked up by SISR on its own.
        if (State == SisrState.NoController && sisr.IsRunning && await sisr.Api.GetEmulatedControllerCountAsync(cancellationToken) > 0)
        {
            TryMove(SisrState.NoController, SisrState.Running);
        }

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

            var now = time.GetUtcNow();
            _restarts.RemoveAll(t => now - t > CrashWindow);
            if (!sisr.IsRunning)
            {
                if (State is SisrState.Starting or SisrState.Running or SisrState.NoController)
                {
                    Log?.Invoke("SISR ist unerwartet beendet");
                    MoveTo(SisrState.Stopped);
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

        UpdateSettings(Settings with { LastSisrUpdateCheck = time.GetUtcNow() });
        AvailableUpdate = latest?.Version is { } version && sisr.InstalledVersion is { } installed && version > installed ? latest : null;
        OnChanged();
    }

    public Task InstallUpdateAsync(CancellationToken cancellationToken = default) =>
        RunBusyAsync("SISR wird aktualisiert …", async () =>
        {
            var release = AvailableUpdate
                ?? await WithTimeoutAsync(sisr.GetLatestReleaseAsync, ReleaseLookupTimeout, "GitHub", cancellationToken)
                ?? throw new InvalidOperationException("Die aktuelle SISR-Version konnte nicht ermittelt werden.");
            await StopSisrAsync(cancellationToken);
            await WithTimeoutAsync(token => sisr.InstallAsync(release, token), InstallTimeout, "Der SISR-Download", cancellationToken);
            AvailableUpdate = null;
            StartSisr();
        });

    public Task ApplyFixAsync(HealthFix fix, CancellationToken cancellationToken = default) => fix switch
    {
        HealthFix.InstallSisr => RunBusyAsync("SISR wird installiert …", async () =>
        {
            await InstallLatestSisrAsync(cancellationToken);
            StartSisr();
        }),
        HealthFix.InstallUsbip => RunBusyAsync("usbip-Treiber wird installiert – bitte die Windows-Abfrage bestätigen …", async () =>
        {
            if (!await WithTimeoutAsync(token => _platform.InstallUsbipAsync(http, token), InstallTimeout, "Der Treiber-Download", cancellationToken))
            {
                throw new InvalidOperationException("Die Installation wurde in der Windows-Abfrage abgelehnt.");
            }

            // The update replaces the emulated USB host controller under VIIPER; SISR (and with it
            // VIIPER) must reconnect, otherwise the virtual controller stays attached to the old one.
            UpdateSettings(Settings with { UsbipInstalledAt = time.GetUtcNow() });
            await StopSisrAsync(cancellationToken);
            _restarts.Clear();
            StartSisr();
        }),
        HealthFix.StartSteam => RunBusyAsync("Steam wird gestartet …", async () =>
        {
            _platform.StartSteam();
            await ProcessWatch.WaitUntilAsync(() => _platform.IsSteamRunning, TimeSpan.FromSeconds(30), time, cancellationToken);
        }),
        HealthFix.EnableCefDebugging => RunBusyAsync("Steam-Debug-Schnittstelle wird aktiviert …", async () =>
        {
            if (!await sisr.Api.EnableCefRemoteDebuggingAsync(cancellationToken))
            {
                throw new InvalidOperationException("SISR konnte die Steam-Debug-Schnittstelle nicht aktivieren.");
            }

            await sisr.Api.RestartSteamAsync(cancellationToken);
        }),
        HealthFix.RestartSteam => RunBusyAsync("Steam wird neu gestartet …", async () =>
        {
            await sisr.Api.RestartSteamAsync(cancellationToken);
        }),
        HealthFix.CreateMarker => RunBusyAsync("Steam-Eintrag für SISR wird angelegt …", async () =>
        {
            if (!await sisr.Api.CreateMarkerShortcutAsync(cancellationToken))
            {
                throw new InvalidOperationException("SISR konnte den Steam-Eintrag nicht anlegen.");
            }
        }),
        HealthFix.StopExternalSisr => RunBusyAsync("Andere SISR-Instanz wird beendet …", () =>
        {
            sisr.StopExternalInstances();
            return Task.CompletedTask;
        }),
        HealthFix.RestartSisr => RunBusyAsync("SISR wird neu gestartet …", async () =>
        {
            await StopSisrAsync(cancellationToken);
            _restarts.Clear();
            StartSisr();
        }),
        _ => Task.CompletedTask,
    };

    /// <summary>
    /// Called right before <paramref name="game"/> starts. If SISR must not run for it, SISR is
    /// stopped until the game has ended (watched through its install directory).
    /// </summary>
    public async Task BeginGameAsync(Game game, CancellationToken cancellationToken = default)
    {
        // Only pause for games that will actually start and whose process we can watch; otherwise
        // SISR would sit paused waiting for a process that never appears.
        if (!Enabled || Settings.ControllerSupportFor(game) || game.Launch is LaunchTarget.None
            || game.InstallDirectory is not { } directory || !Directory.Exists(directory))
        {
            return;
        }

        // Counted before waiting for the gate, so whatever holds it can't start SISR meanwhile.
        // Not cancellable: the watch below is what ends the pause again.
        Pause(directory);
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

    private void Pause(string directory)
    {
        Interlocked.Increment(ref _pausedGames);
        _pausedDirectories.AddOrUpdate(directory, 1, (_, n) => n + 1);
        OnChanged();
        _ = Task.Run(() => WatchPausedGameAsync(directory), CancellationToken.None);
    }

    private async Task WatchPausedGameAsync(string directory)
    {
        try
        {
            bool Running() => _platform.AnyRunningFrom(directory);
            if (await ProcessWatch.WaitUntilAsync(Running, TimeSpan.FromMinutes(2), time, CancellationToken.None))
            {
                while (Running())
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), time);
                }
            }
        }
        catch (Exception ex)
        {
            // Ending the pause below matters more than why watching failed.
            Log?.Invoke($"Spiel in {directory} konnte nicht beobachtet werden: {ex}");
        }

        try
        {
            await EndPauseAsync(directory);
        }
        catch (Exception ex)
        {
            Log?.Invoke($"SISR konnte nach dem Spiel nicht weiterlaufen: {ex}");
        }
    }

    private async Task EndPauseAsync(string directory)
    {
        await _gate.WaitAsync();
        try
        {
            if (_pausedDirectories.AddOrUpdate(directory, 0, (_, n) => n - 1) <= 0)
            {
                _pausedDirectories.TryRemove(directory, out _);
            }

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
        SteamControllerConnected = Enabled && IsSupportedPlatform && _platform.IsSteamControllerConnected();
    }

    private ControllerSupportSetup CheckSetup() =>
        !IsSupportedPlatform ? ControllerSupportSetup.UnsupportedPlatform
        : !_platform.IsSteamInstalled ? ControllerSupportSetup.SteamMissing
        : sisr.IsInstalled && _platform.IsUsbipReady && _platform.IsCefDebuggingEnabled ? ControllerSupportSetup.Ready
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
            issues.Add(new("sisr-missing", HealthSeverity.Error, "SISR ist nicht installiert", "Ohne SISR kommt der Steam Controller nicht als Gamepad an.", HealthFix.InstallSisr, "Installieren"));
        }

        issues.AddRange(_platform.CheckDependencies(Settings, time.GetUtcNow()));

        if (sisr.ExternalInstances().Count > 0)
        {
            issues.Add(new("sisr-external", HealthSeverity.Warning, "Ein SISR aus einem anderen Ordner läuft", "Zwei SISR-Instanzen erzeugen doppelte Controller. Die zweite sollte beendet werden.", HealthFix.StopExternalSisr, "Beenden"));
        }

        var state = State;
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
            issues.Add(new("cef-disabled", HealthSeverity.Error, "Steam-Debug-Schnittstelle ist aus", "SISR braucht Steams lokale Debug-Schnittstelle (CEF). Zum Aktivieren startet Steam einmal neu.", HealthFix.EnableCefDebugging, "Aktivieren"));
        }
        else if (steam.SteamRunning && !steam.CefDebugReachable)
        {
            issues.Add(new("cef-unreachable", HealthSeverity.Warning, "Steam muss neu gestartet werden", "Die Debug-Schnittstelle ist aktiviert, aber erst nach einem Neustart von Steam erreichbar.", HealthFix.RestartSteam, "Steam neu starten"));
        }

        if (steam.SteamRunning && steam.CefDebugReachable && !steam.MarkerShortcutPresent)
        {
            issues.Add(new("marker-missing", HealthSeverity.Warning, "Steam-Eintrag für SISR fehlt", "Über diesen Eintrag legt Steam fest, welche Controller-Belegung SISR verwendet.", HealthFix.CreateMarker, "Anlegen"));
        }

        var viiper = await sisr.Api.PingViiperAsync(cancellationToken);
        if (string.IsNullOrEmpty(viiper?.Status?.Version))
        {
            issues.Add(new("viiper-down", HealthSeverity.Error, "VIIPER antwortet nicht", "VIIPER erzeugt die virtuellen Controller für SISR. Ein Neustart von SISR startet es mit.", HealthFix.RestartSisr, "SISR neu starten"));
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
        try
        {
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

        _ = Task.Run(() => WaitUntilReadyAsync(generation));
    }

    /// <summary>
    /// Ready means: the API answers and SISR emulates at least one controller. SISR also loads
    /// Steam's overlay and has Steam force its layout right after its API comes up — that's when
    /// the foreground can jump, so "started" is only reported after that.
    /// </summary>
    private async Task WaitUntilReadyAsync(int generation)
    {
        try
        {
            var deadline = time.GetUtcNow() + ReadyTimeout;
            var controllers = 0;
            while (time.GetUtcNow() < deadline && sisr.IsRunning)
            {
                controllers = await sisr.Api.GetEmulatedControllerCountAsync() ?? 0;
                if (controllers > 0)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), time);
            }

            if (controllers > 0 && time.GetUtcNow() - _lastStart < MinimumSettleTime)
            {
                await Task.Delay(MinimumSettleTime - (time.GetUtcNow() - _lastStart), time);
            }

            Log?.Invoke($"SISR bereit: {controllers} Controller emuliert");

            // Only if nothing stopped or restarted SISR meanwhile.
            var next = !sisr.IsRunning ? SisrState.Stopped : controllers > 0 ? SisrState.Running : SisrState.NoController;
            TryMove(SisrState.Starting, next, generation);
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
            await sisr.StopAsync(cancellationToken);
        }
        finally
        {
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
        if (wasRunning && _platform.IsSteamRunning)
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
        }

        Log?.Invoke(AllowedTransitions[previous].Contains(next)
            ? $"SISR: {previous} → {next}"
            : $"SISR: unerwarteter Zustandswechsel {previous} → {next}");
        OnChanged();
        return true;
    }

    private async Task RunBusyAsync(string text, Func<Task> action)
    {
        await _gate.WaitAsync();
        BusyText = text;
        LastError = null;
        OnChanged();
        try
        {
            await action();
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
            await RefreshAsync();
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

    private void UpdateSettings(AppSettings settings)
    {
        Settings = settings;
        store.Save(settings);
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
