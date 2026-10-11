using Arcadia.Core.Models;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Steam;

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

public sealed record HealthIssue(string Id, HealthSeverity Severity, string Title, string Detail, HealthFix Fix = HealthFix.None, string? FixLabel = null);

/// <summary>
/// "Steam Controller Support": keeps Arcadia's SISR running while it is enabled, pauses it for
/// games that use Steam Input themselves, watches SISR and its dependencies, and checks for
/// SISR updates.
/// </summary>
public sealed class ControllerSupportService(SettingsStore store, SisrHost sisr, HttpClient http, TimeProvider time)
{
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupGrace = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CrashWindow = TimeSpan.FromMinutes(5);
    private const int MaxRestartsPerWindow = 3;

    private readonly List<DateTimeOffset> _restarts = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastStart = DateTimeOffset.MinValue;
    private int _pausedGames;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _pausedDirectories = new(StringComparer.OrdinalIgnoreCase);
    private bool _checkedThisSession;

    public event EventHandler? Changed;

    public AppSettings Settings { get; private set; } = store.Load();

    public bool Enabled => Settings.SteamControllerSupport;

    public bool IsSupportedPlatform => OperatingSystem.IsWindows();

    public SisrHost Sisr => sisr;

    public IReadOnlyList<HealthIssue> Issues { get; private set; } = [];

    /// <summary>A newer SISR release than the installed one, if any.</summary>
    public Release? AvailableUpdate { get; private set; }

    /// <summary>SISR is stopped because a game runs that uses Steam Input itself.</summary>
    public bool IsPaused => _pausedGames > 0;

    /// <summary>Install directories of the games SISR is paused for; an update restart hands them on.</summary>
    public IReadOnlyCollection<string> PausedFor => _pausedDirectories.Keys.ToList();

    public string? BusyText { get; private set; }

    /// <summary>Optional diagnostics sink (Arcadia's log file).</summary>
    public Action<string>? Log { get; set; }

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        Log?.Invoke($"Steam Controller Support {(enabled ? "ein" : "aus")}geschaltet");
        UpdateSettings(Settings with { SteamControllerSupport = enabled });
        if (enabled)
        {
            await RunBusyAsync("SISR wird eingerichtet …", async () =>
            {
                if (!sisr.IsInstalled)
                {
                    await InstallLatestSisrAsync(cancellationToken);
                }

                StartSisr();
            });
        }
        else
        {
            // Through the same gate as starting, so a quick off → on waits for SISR to be gone.
            await RunBusyAsync("SISR wird beendet …", () => StopSisrAndReleaseControllerAsync(cancellationToken));
        }

        await RefreshAsync(cancellationToken);
    }

    /// <summary>Raised right before SISR starts, so the UI can keep the foreground.</summary>
    public event EventHandler? SisrStarting;

    /// <summary>Raised once a freshly started SISR answers (or after a timeout).</summary>
    public event EventHandler? SisrStarted;

    /// <summary>Stops SISR when Arcadia exits.</summary>
    public Task ShutdownAsync(CancellationToken cancellationToken = default) => StopSisrAndReleaseControllerAsync(cancellationToken);

    /// <summary>
    /// Stops SISR and hands the controller back to Steam's normal handling. SISR makes Steam force
    /// its marker's layout; Steam keeps that until the next focus change unless it is reset.
    /// </summary>
    private async Task StopSisrAndReleaseControllerAsync(CancellationToken cancellationToken)
    {
        var wasRunning = sisr.IsRunning;
        IsStarting = false;
        NoControllerFound = false;
        Log?.Invoke($"SISR wird beendet (lief: {wasRunning})");
        if (wasRunning)
        {
            await sisr.Api.SetForceConfigAsync(false, cancellationToken);
        }

        await sisr.StopAsync(cancellationToken);
        Log?.Invoke($"SISR beendet (läuft noch: {sisr.IsRunning})");
        if (wasRunning && SteamInstallation.IsRunning)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("steam://forceinputappid/0") { UseShellExecute = true })?.Dispose();

            // Steam only applies the change on the next focus change.
            ControllerReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Raised after SISR stopped and Steam was told to drop SISR's layout. Steam applies that only
    /// when the foreground window changes, so the UI should trigger one.
    /// </summary>
    public event EventHandler? ControllerReleased;

    public void SetOverride(Game game, bool? enabled) => UpdateSettings(Settings.WithControllerSupportOverride(game, enabled));

    /// <summary>Called periodically: restarts a crashed SISR, rechecks health, checks for updates once a day.</summary>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (!Enabled || !IsSupportedPlatform)
        {
            if (Issues.Count > 0)
            {
                Issues = [];
                OnChanged();
            }

            return;
        }

        if (!IsPaused && BusyText is null && sisr.IsInstalled)
        {
            _restarts.RemoveAll(t => time.GetUtcNow() - t > CrashWindow);
            if (!sisr.IsRunning)
            {
                if (_restarts.Count < MaxRestartsPerWindow)
                {
                    _restarts.Add(time.GetUtcNow());
                    StartSisr();
                }
            }
            else if (time.GetUtcNow() - _lastStart > StartupGrace && !await sisr.Api.IsReachableAsync(cancellationToken)
                     && _restarts.Count < MaxRestartsPerWindow)
            {
                // Same SISR, but not started by Arcadia (e.g. from its tray shortcut), so its API
                // isn't on Arcadia's port. Take it over by restarting it with Arcadia's settings.
                _restarts.Add(time.GetUtcNow());
                await sisr.StopAsync(cancellationToken);
                StartSisr();
            }
        }

        // A controller switched on after SISR started gets picked up by SISR on its own.
        if (NoControllerFound && sisr.IsRunning && await sisr.Api.GetEmulatedControllerCountAsync(cancellationToken) > 0)
        {
            NoControllerFound = false;
            OnChanged();
        }

        // Once per start, then daily while Arcadia keeps running.
        if (!_checkedThisSession || Settings.LastSisrUpdateCheck is not { } last || time.GetUtcNow() - last > UpdateCheckInterval)
        {
            _checkedThisSession = true;
            await CheckForUpdateAsync(cancellationToken);
        }

        await RefreshAsync(cancellationToken);
    }

    public async Task CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        Release? latest;
        try
        {
            latest = await sisr.GetLatestReleaseAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return;
        }

        UpdateSettings(Settings with { LastSisrUpdateCheck = time.GetUtcNow() });
        AvailableUpdate = latest?.Version is { } version && sisr.InstalledVersion is { } installed && version > installed ? latest : null;
        OnChanged();
    }

    public Task InstallUpdateAsync(CancellationToken cancellationToken = default) =>
        RunBusyAsync("SISR wird aktualisiert …", async () =>
        {
            var release = AvailableUpdate ?? await sisr.GetLatestReleaseAsync(cancellationToken)
                ?? throw new InvalidOperationException("Die aktuelle SISR-Version konnte nicht ermittelt werden.");
            await sisr.InstallAsync(release, cancellationToken);
            AvailableUpdate = null;
            if (Enabled && !IsPaused)
            {
                StartSisr();
            }
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
            if (OperatingSystem.IsWindows() && !await UsbipDriver.InstallAsync(http, cancellationToken))
            {
                throw new InvalidOperationException("Die Installation wurde in der Windows-Abfrage abgelehnt.");
            }

            // The update replaces the emulated USB host controller under VIIPER; SISR (and with it
            // VIIPER) must reconnect, otherwise the virtual controller stays attached to the old one.
            UpdateSettings(Settings with { UsbipInstalledAt = time.GetUtcNow() });
            await sisr.StopAsync(cancellationToken);
            _restarts.Clear();
            StartSisr();
        }),
        HealthFix.StartSteam => RunBusyAsync("Steam wird gestartet …", async () =>
        {
            SteamInstallation.Find()?.StartSilently();
            await ProcessWatch.WaitUntilAsync(() => SteamInstallation.IsRunning, TimeSpan.FromSeconds(30), time, cancellationToken);
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
            await sisr.StopAsync(cancellationToken);
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

        Interlocked.Increment(ref _pausedGames);
        await StopSisrAndReleaseControllerAsync(cancellationToken);
        OnChanged();
        WatchPausedGame(directory);
    }

    /// <summary>
    /// Takes over a pause from before an update restart: SISR stays off while a process from
    /// <paramref name="directory"/> runs. Call before the first <see cref="TickAsync"/>.
    /// </summary>
    public void ResumePause(string directory)
    {
        if (!Directory.Exists(directory) || !ProcessWatch.AnyRunningFrom(directory))
        {
            return;
        }

        Log?.Invoke($"SISR bleibt pausiert für {directory}");
        Interlocked.Increment(ref _pausedGames);
        OnChanged();
        WatchPausedGame(directory);
    }

    private void WatchPausedGame(string directory)
    {
        _pausedDirectories.AddOrUpdate(directory, 1, (_, n) => n + 1);
        _ = Task.Run(async () =>
        {
            try
            {
                bool Running() => ProcessWatch.AnyRunningFrom(directory);
                if (await ProcessWatch.WaitUntilAsync(Running, TimeSpan.FromMinutes(2), time, CancellationToken.None))
                {
                    while (Running())
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), time);
                    }
                }
            }
            finally
            {
                if (_pausedDirectories.AddOrUpdate(directory, 0, (_, n) => n - 1) <= 0)
                {
                    _pausedDirectories.TryRemove(directory, out _);
                }

                if (Interlocked.Decrement(ref _pausedGames) == 0 && Enabled)
                {
                    StartSisr();
                }

                OnChanged();
            }
        }, CancellationToken.None);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Issues = Enabled ? await CheckHealthAsync(cancellationToken) : [];
        OnChanged();
    }

    private async Task<IReadOnlyList<HealthIssue>> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var issues = new List<HealthIssue>();
        if (!OperatingSystem.IsWindows())
        {
            issues.Add(new("platform", HealthSeverity.Error, "Nur unter Windows verfügbar", "Steam Controller Support über SISR ist bisher nur für Windows umgesetzt."));
            return issues;
        }

        if (!sisr.IsInstalled)
        {
            issues.Add(new("sisr-missing", HealthSeverity.Error, "SISR ist nicht installiert", "Ohne SISR kommt der Steam Controller nicht als Gamepad an.", HealthFix.InstallSisr, "Installieren"));
        }

        var usbip = UsbipDriver.InstalledVersion();
        if (usbip is null)
        {
            issues.Add(new("usbip-missing", HealthSeverity.Error, "usbip-Treiber fehlt", "SISR erzeugt den virtuellen Controller über diesen Treiber. Die Installation fragt einmalig nach Administratorrechten.", HealthFix.InstallUsbip, "Installieren"));
        }
        else if (usbip < UsbipDriver.MinimumVersion)
        {
            issues.Add(new("usbip-outdated", HealthSeverity.Warning, "usbip-Treiber ist veraltet", $"Installiert ist {usbip}, benötigt wird mindestens {UsbipDriver.MinimumVersion}.", HealthFix.InstallUsbip, "Aktualisieren"));
        }
        else if (!UsbipDriver.IsRunning())
        {
            issues.Add(new("usbip-stopped", HealthSeverity.Error, "usbip-Treiber ist nicht aktiv", "Der Treiber ist installiert, aber nicht geladen. Meist hilft ein Neustart des PCs, sonst eine Neuinstallation.", HealthFix.InstallUsbip, "Neu installieren"));
        }
        else if (Settings.UsbipInstalledAt is { } installedAt && installedAt > LastBootTime())
        {
            issues.Add(new("reboot-pending", HealthSeverity.Warning, "Windows-Neustart ausstehend", "Der usbip-Treiber wurde aktualisiert. Bis zum Neustart kann der Controller aussetzen; dann hilft es, SISR neu zu starten.", HealthFix.RestartSisr, "SISR neu starten"));
        }

        if (SteamInstallation.Find() is null)
        {
            issues.Add(new("steam-missing", HealthSeverity.Error, "Steam wurde nicht gefunden", "SISR liest den Controller über Steam Input; Steam muss installiert sein."));
        }
        else if (!SteamInstallation.IsRunning)
        {
            issues.Add(new("steam-stopped", HealthSeverity.Warning, "Steam läuft nicht", "Steam muss im Hintergrund laufen, damit SISR den Controller weiterreichen kann.", HealthFix.StartSteam, "Steam starten"));
        }

        if (sisr.ExternalInstances().Count > 0)
        {
            issues.Add(new("sisr-external", HealthSeverity.Warning, "Ein SISR aus einem anderen Ordner läuft", "Zwei SISR-Instanzen erzeugen doppelte Controller. Die zweite sollte beendet werden.", HealthFix.StopExternalSisr, "Beenden"));
        }

        if (sisr.IsInstalled && !IsPaused && BusyText is null)
        {
            if (!sisr.IsRunning)
            {
                if (_restarts.Count >= MaxRestartsPerWindow)
                {
                    issues.Add(new("sisr-crashing", HealthSeverity.Error, "SISR stürzt wiederholt ab", "Arcadia hat SISR mehrfach neu gestartet. Details stehen in logs\\sisr.log.", HealthFix.RestartSisr, "Erneut starten"));
                }
            }
            else if (time.GetUtcNow() - _lastStart > StartupGrace)
            {
                await CheckRunningSisrAsync(issues, cancellationToken);
            }
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

    private DateTimeOffset LastBootTime() => time.GetUtcNow() - TimeSpan.FromMilliseconds(Environment.TickCount64);

    private async Task InstallLatestSisrAsync(CancellationToken cancellationToken)
    {
        var release = await sisr.GetLatestReleaseAsync(cancellationToken)
            ?? throw new InvalidOperationException("Die aktuelle SISR-Version konnte nicht ermittelt werden.");
        await sisr.InstallAsync(release, cancellationToken);
        AvailableUpdate = null;
    }

    private void StartSisr()
    {
        if (!IsSupportedPlatform)
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
        IsStarting = true;
        NoControllerFound = false;
        SisrStarting?.Invoke(this, EventArgs.Empty);
        OnChanged();
        sisr.Start();

        _ = Task.Run(async () =>
        {
            // Ready means: the API answers and SISR emulates at least one controller. SISR also
            // loads Steam's overlay and has Steam force its layout right after its API comes up —
            // that's when the foreground can jump, so "started" is only reported after that.
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

            IsStarting = false;
            NoControllerFound = sisr.IsRunning && controllers == 0;
            Log?.Invoke($"SISR bereit: {controllers} Controller emuliert");
            SisrStarted?.Invoke(this, EventArgs.Empty);
            OnChanged();
        });
    }

    /// <summary>How long to wait for SISR to emulate a controller before giving up waiting.</summary>
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(10);

    /// <summary>SISR forces Steam's layout about two seconds after it starts.</summary>
    private static readonly TimeSpan MinimumSettleTime = TimeSpan.FromSeconds(3);

    /// <summary>SISR runs but doesn't emulate a controller yet.</summary>
    public bool IsStarting { get; private set; }

    /// <summary>SISR is ready, but found no controller to emulate (e.g. the controller is off).</summary>
    public bool NoControllerFound { get; private set; }

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
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or IOException or UnauthorizedAccessException)
        {
            LastError = ex.Message;
        }
        finally
        {
            BusyText = null;
            _gate.Release();
            await RefreshAsync();
        }
    }

    /// <summary>The message of the last failed action, shown in the settings dialog.</summary>
    public string? LastError { get; private set; }

    private void UpdateSettings(AppSettings settings)
    {
        Settings = settings;
        store.Save(settings);
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
