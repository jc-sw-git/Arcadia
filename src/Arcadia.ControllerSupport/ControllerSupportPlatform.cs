using System.Diagnostics;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Pan;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Steam;

namespace Arcadia.ControllerSupport;

/// <summary>What Steam Controller Support needs from Windows and Steam; replaced in tests.</summary>
public interface IControllerSupportPlatform
{
    /// <summary>SISR exists only for Windows.</summary>
    bool IsSupported { get; }

    bool IsSteamInstalled { get; }

    bool IsSteamRunning { get; }

    /// <summary>
    /// Steam is up far enough for SISR: its process and its web helper (CEF, which SISR talks to) run,
    /// a user is logged in and the client finished that login (it can hang at "loading user data"
    /// after the server accepted it). SISR started before that shows its own full-screen "Steam is
    /// not running" window, or can't find its marker (it reads the logged-in user's shortcuts once
    /// at start) and asks to add a second one.
    /// </summary>
    bool IsSteamReady { get; }

    /// <summary>
    /// Steam runs with its web helper and a logged-in user, but may still be logging in. Losing this
    /// means Steam goes away (closed, shutting down, logged out): SISR is stopped then, and killed,
    /// because its cleanup would start Steam again. A login that hangs or repeats doesn't count, so
    /// SISR isn't killed mid-game over it.
    /// </summary>
    bool IsSteamUp { get; }

    /// <summary>
    /// Adds SISR's marker shortcut ("SISR Marker", <c>--marker</c>) to Steam unless it is there, as
    /// SISR's own setup would: for the given accounts, or without them for the logged-in one (else the
    /// only or the last one). Only while Steam is closed (Steam overwrites the file on exit);
    /// false if Steam runs or no Steam account could be found.
    /// </summary>
    bool EnsureSisrMarker(string sisrExecutable, IReadOnlyCollection<uint>? accountIds = null);

    /// <summary>The Steam accounts on this PC and whether each has SISR's marker, the last logged-in one first.</summary>
    IReadOnlyList<MarkerAccount> SisrMarkerAccounts(string sisrExecutable);

    /// <summary>
    /// SISR's marker shortcut is in Steam. SISR reads it only when it starts and asks to add one if
    /// it is missing, so SISR is not started without it. False if no Steam account is known; while
    /// Steam's file can't be read (it writes it), the last answer.
    /// </summary>
    bool HasSisrMarker(string sisrExecutable);

    /// <summary>usbip-win2 is installed in at least <see cref="UsbipDriver.MinimumVersion"/> and loaded.</summary>
    bool IsUsbipReady { get; }

    /// <summary>Pan, the guardian service, is installed (#243); without it Arcadia plugs no pad in.</summary>
    bool IsPanReady { get; }

    /// <summary>Arcadia ships a newer Pan than the installed one.</summary>
    bool IsPanUpdateAvailable { get; }

    /// <summary>HidHide's driver version, or null if HidHide isn't installed (#243 part B2).</summary>
    Version? HidHideVersion { get; }

    /// <summary>Steam's CEF debug flag file exists (it takes effect on Steam's next start).</summary>
    bool IsCefDebuggingEnabled { get; }

    void EnableCefDebugging();

    /// <summary>The name of the Steam game running right now, or null.</summary>
    string? RunningSteamGame();

    /// <summary>The app id of the Steam game running right now, or null.</summary>
    int? RunningSteamAppId();

    /// <summary>How Steam Input is set for the Steam game <paramref name="appId"/>, or null if Steam's settings can't be read.</summary>
    SteamInputSetting? SteamInputFor(int appId);

    /// <summary>Steam's Steam Input defaults outside any game, or null if Steam's settings can't be read. Cheap while the file is unchanged.</summary>
    SteamInputSetting? SteamInputDefaults();

    /// <summary>Asks Steam to exit and waits for it; false if it is still running after the timeout.</summary>
    Task<bool> StopSteamAsync(TimeProvider time, CancellationToken cancellationToken);

    /// <summary>Starts Steam minimized to the tray.</summary>
    void StartSteam();

    /// <summary>
    /// Installs or updates what is missing of usbip-win2, HidHide and Pan, with one Windows prompt (#243); false if the
    /// user declined it.
    /// </summary>
    Task<bool> InstallDriversAsync(HttpClient http, CancellationToken cancellationToken);

    /// <summary>Pan's <c>--restore</c>: gives the real controllers back by hand; false if the user declined the Windows prompt.</summary>
    Task<bool> RestoreControllersAsync(CancellationToken cancellationToken);

    /// <summary>Tells Steam to drop SISR's forced controller layout (applied on the next focus change).</summary>
    void ResetSteamInputLayout();

    /// <summary>True while a process from <paramref name="directory"/> runs, e.g. the game SISR is paused for.</summary>
    bool AnyRunningFrom(string directory);

    /// <summary>Problems with what SISR depends on: the drivers and Steam.</summary>
    IReadOnlyList<HealthIssue> CheckDependencies(AppSettings settings, DateTimeOffset now);

    /// <summary>Problems with what Arcadia's own pad depends on: the usbip driver and Pan (#243).</summary>
    IReadOnlyList<HealthIssue> CheckDrivers(AppSettings settings, DateTimeOffset now);
}

/// <summary>The real Windows and Steam.</summary>
/// <param name="locate">Finds Steam; defaults to <see cref="SteamInstallation.Find"/>. The acceptance tests point it at a Steam folder of their own.</param>
/// <param name="isSteamRunning">Whether Steam runs; defaults to <see cref="SteamInstallation.IsRunning"/>.</param>
public sealed class ControllerSupportPlatform(Func<SteamInstallation?>? locate = null, Func<bool>? isSteamRunning = null) : IControllerSupportPlatform
{
    private readonly Func<SteamInstallation?> _locate = locate ?? SteamInstallation.Find;
    private readonly Func<bool> _isSteamRunning = isSteamRunning ?? (() => SteamInstallation.IsRunning);

    public bool IsSupported => OperatingSystem.IsWindows();

    private static readonly TimeSpan SteamExitTimeout = TimeSpan.FromSeconds(30);

    public bool IsSteamInstalled => _locate() is not null;

    public bool IsSteamRunning => _isSteamRunning();

    public bool IsSteamReady =>
        IsSteamUp && _locate() is { } steam
        && SteamLoginLog.IsLatestLoginComplete(steam.LoginLogPath) is not false;

    public bool IsSteamUp =>
        _isSteamRunning() && ProcessWatch.IsRunning("steamwebhelper")
        && _locate() is { ActiveUserId: not (null or 0) };

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _lastMarkerAnswer = new(StringComparer.OrdinalIgnoreCase);

    public bool EnsureSisrMarker(string sisrExecutable, IReadOnlyCollection<uint>? accountIds = null)
    {
        if (_isSteamRunning() || _locate() is not { } steam)
        {
            return false;
        }

        IEnumerable<string> userDataDirectories = accountIds is null
            ? steam.UserDataDirectory is { } current ? [current] : []
            : steam.Accounts().Where(a => accountIds.Contains(a.Id)).Select(a => steam.UserDataDirectoryOf(a.Id)).ToList();
        if (accountIds is null && !userDataDirectories.Any())
        {
            return false;
        }

        foreach (var userData in userDataDirectories)
        {
            var shortcuts = ShortcutsPathIn(userData);
            if (!ContainsMarker(shortcuts, sisrExecutable))
            {
                // A new account may have no config folder yet.
                Directory.CreateDirectory(Path.GetDirectoryName(shortcuts)!);
                SteamShortcuts.Add(shortcuts, "SISR Marker", sisrExecutable, "--marker");
            }
        }

        return true;
    }

    public IReadOnlyList<MarkerAccount> SisrMarkerAccounts(string sisrExecutable)
    {
        if (_locate() is not { } steam)
        {
            return [];
        }

        return steam.Accounts()
            .Select(a => new MarkerAccount(a.Id, a.Name, HasMarkerIn(ShortcutsPathIn(steam.UserDataDirectoryOf(a.Id)), sisrExecutable)))
            .ToList();
    }

    private static string ShortcutsPathIn(string userDataDirectory) => Path.Combine(userDataDirectory, "config", "shortcuts.vdf");

    public bool HasSisrMarker(string sisrExecutable) =>
        _locate()?.ShortcutsPath is { } shortcuts && HasMarkerIn(shortcuts, sisrExecutable);

    private bool HasMarkerIn(string shortcuts, string sisrExecutable)
    {
        try
        {
            return _lastMarkerAnswer[shortcuts] = ContainsMarker(shortcuts, sisrExecutable);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or InvalidDataException)
        {
            // Steam is writing the file, or it is broken; Steam rewrites it on its next exit.
            return _lastMarkerAnswer.GetValueOrDefault(shortcuts, true);
        }
    }

    /// <summary>SISR finds its marker by its own exe path plus "--marker" in the launch options.</summary>
    private static bool ContainsMarker(string shortcuts, string sisrExecutable) =>
        File.Exists(shortcuts)
        && SteamShortcuts.Read(shortcuts).Any(s => s.LaunchOptions.Contains("--marker", StringComparison.OrdinalIgnoreCase)
                                                   && SteamShortcuts.FindByExecutable([s], sisrExecutable) is not null);

    public bool IsUsbipReady =>
        OperatingSystem.IsWindows() && UsbipDriver.InstalledVersion() is { } version && version >= UsbipDriver.MinimumVersion && UsbipDriver.IsRunning();

    public bool IsPanReady => OperatingSystem.IsWindows() && PanSetup.IsInstalled;

    public bool IsPanUpdateAvailable => OperatingSystem.IsWindows() && PanSetup.UpdateAvailable;

    public Version? HidHideVersion => OperatingSystem.IsWindows() ? HidHideInstallation.InstalledVersion() : null;

    /// <summary>HidHide was uninstalled and goes at the next restart; its setup would fail until then.</summary>
    private static bool HidHideRemovalPending => OperatingSystem.IsWindows() && HidHideInstallation.IsRemovalPending();

    public bool IsCefDebuggingEnabled => _locate() is { } steam && File.Exists(steam.CefDebuggingFlagPath);

    public void EnableCefDebugging()
    {
        var steam = _locate() ?? throw new InvalidOperationException("Steam wurde nicht gefunden.");
        File.WriteAllBytes(steam.CefDebuggingFlagPath, []);
    }

    public string? RunningSteamGame() => SteamInstallation.RunningGameName();

    public int? RunningSteamAppId() => SteamInstallation.RunningAppId();

    public SteamInputSetting? SteamInputFor(int appId)
    {
        if (_locate()?.LocalConfigPath is not { } path || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return SteamInputSetting.Read(File.ReadAllText(path), appId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Steam is writing the file; the check from SISR's devices still catches the game.
            return null;
        }
    }

    public SteamInputSetting? SteamInputDefaults()
    {
        if (_locate()?.LocalConfigPath is not { } path || !File.Exists(path))
        {
            return null;
        }

        try
        {
            // Asked every few seconds while SISR is paused for another controller; the file is about 1 MB.
            var written = File.GetLastWriteTimeUtc(path);
            if (_defaults is { } cached && cached.Path == path && cached.Written == written)
            {
                return cached.Setting;
            }

            var setting = SteamInputSetting.Read(File.ReadAllText(path), appId: 0);
            _defaults = new(path, written, setting);
            return setting;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // A reference, so a reader on another thread sees a whole entry or none.
    private volatile CachedDefaults? _defaults;

    private sealed record CachedDefaults(string Path, DateTime Written, SteamInputSetting? Setting);

    public async Task<bool> StopSteamAsync(TimeProvider time, CancellationToken cancellationToken) =>
        await SteamInstallation.ShutdownAsync(SteamExitTimeout, time, cancellationToken);

    public void StartSteam() => _locate()?.StartSilently();

    public async Task<bool> InstallDriversAsync(HttpClient http, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        // The driver only when it is missing or too old: its installer replaces the emulated host controller and
        // then wants a reboot. HidHide likewise: one of a supported version stays (e.g. DS4Windows's), with its lists.
        string? usbipInstaller = null;
        string? hidHideInstaller = null;
        try
        {
            usbipInstaller = IsUsbipReady ? null : await UsbipDriver.DownloadInstallerAsync(http, cancellationToken);
            hidHideInstaller = HidHideNeedsInstaller(HidHideVersion) && !HidHideRemovalPending ? await HidHideInstallation.DownloadInstallerAsync(http, cancellationToken) : null;
            return await PanSetup.InstallAsync(usbipInstaller, hidHideInstaller, cancellationToken);
        }
        finally
        {
            foreach (var installer in new[] { usbipInstaller, hidHideInstaller })
            {
                if (installer is not null)
                {
                    File.Delete(installer);
                }
            }
        }
    }

    /// <summary>
    /// Missing or older than Pan supports. A newer major one is left alone: its installer would be a downgrade, and
    /// only an Arcadia update can bring support for it.
    /// </summary>
    public static bool HidHideNeedsInstaller(Version? installed) =>
        installed is null || (installed.Major <= SteamControllerHiding.SupportedMajorVersion && !HidHideRelease.IsSupported(installed));

    public Task<bool> RestoreControllersAsync(CancellationToken cancellationToken) =>
        OperatingSystem.IsWindows() ? PanSetup.RestoreAsync(cancellationToken) : Task.FromResult(false);

    public void ResetSteamInputLayout() =>
        Process.Start(new ProcessStartInfo("steam://forceinputappid/0") { UseShellExecute = true })?.Dispose();

    public bool AnyRunningFrom(string directory) => ProcessWatch.AnyRunningFrom(directory);

    public IReadOnlyList<HealthIssue> CheckDependencies(AppSettings settings, DateTimeOffset now)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var issues = new List<HealthIssue>(CheckDrivers(settings, now));
        if (_locate() is null)
        {
            issues.Add(new("steam-missing", HealthSeverity.Warning, "Steam wurde nicht gefunden", "SISR liest den Controller über Steam Input; Steam muss installiert sein."));
        }
        else if (!_isSteamRunning())
        {
            issues.Add(new("steam-stopped", HealthSeverity.Warning, "Steam läuft nicht", "Steam muss im Hintergrund laufen, damit SISR den Controller weiterreichen kann.", HealthFix.StartSteam, "Steam starten"));
        }

        return issues;
    }

    public IReadOnlyList<HealthIssue> CheckDrivers(AppSettings settings, DateTimeOffset now)
    {
        var issues = new List<HealthIssue>();
        if (!OperatingSystem.IsWindows())
        {
            return issues;
        }

        var usbip = UsbipDriver.InstalledVersion();
        if (usbip is null)
        {
            issues.Add(new("usbip-missing", HealthSeverity.Warning, "usbip-Treiber fehlt", "Arcadia erzeugt den virtuellen Controller über diesen Treiber. Die Installation fragt einmalig nach Administratorrechten.", HealthFix.InstallDrivers, "Installieren"));
        }
        else if (usbip < UsbipDriver.MinimumVersion)
        {
            issues.Add(new("usbip-outdated", HealthSeverity.Warning, "usbip-Treiber ist veraltet", $"Installiert ist {usbip}, benötigt wird mindestens {UsbipDriver.MinimumVersion}.", HealthFix.InstallDrivers, "Aktualisieren"));
        }
        else if (!UsbipDriver.IsRunning())
        {
            issues.Add(new("usbip-stopped", HealthSeverity.Warning, "usbip-Treiber ist nicht aktiv", "Der Treiber ist installiert, aber nicht geladen. Meist hilft ein Neustart des PCs, sonst eine Neuinstallation.", HealthFix.InstallDrivers, "Neu installieren"));
        }
        else if (settings.UsbipInstalledAt is { } installedAt && installedAt > LastBootTime(now))
        {
            issues.Add(new("reboot-pending", HealthSeverity.Warning, "Windows-Neustart ausstehend", "Der usbip-Treiber wurde aktualisiert. Bis zum Neustart kann der Controller aussetzen; dann hilft es, SISR neu zu starten.", HealthFix.RestartSisr, "SISR neu starten"));
        }

        if (CheckHidHide(HidHideVersion, HidHideRemovalPending) is { } hidHide)
        {
            issues.Add(hidHide);
        }

        if (!PanSetup.IsInstalled)
        {
            issues.Add(new("pan-missing", HealthSeverity.Warning, "Wächterdienst Pan fehlt", "Pan gibt die echten Controller zurück, wenn Arcadia abstürzt oder hängt. Ohne Pan bringt Arcadia den Steam Controller nicht als Xbox Controller in Spiele. Die Installation fragt einmalig nach Administratorrechten.", HealthFix.InstallDrivers, "Installieren"));
        }

        return issues;
    }

    /// <summary>The row for HidHide's installation (#243 part B2), or null when a supported version is installed.</summary>
    public static HealthIssue? CheckHidHide(Version? installed, bool removalPending = false) => installed switch
    {
        _ when removalPending => EmulationParts.HidHideRemovalPending,
        null => new("hidhide-missing", HealthSeverity.Warning, "HidHide fehlt", HidHideMissingText, HealthFix.InstallDrivers, "Installieren"),
        _ when HidHideRelease.IsSupported(installed) => null,
        _ when HidHideNeedsInstaller(installed) => new("hidhide-unsupported", HealthSeverity.Warning, "HidHide-Version wird nicht unterstützt",
            $"Installiert ist ein älteres HidHide (Treiber {installed.ToString(3)}), Arcadia braucht HidHide {HidHideRelease.Version} oder neuer. „Aktualisieren“ installiert HidHide {HidHideRelease.Version}; was in HidHide eingestellt ist, bleibt erhalten. Die Installation fragt einmalig nach Administratorrechten.",
            HealthFix.InstallDrivers, "Aktualisieren"),
        _ => new("hidhide-unsupported", HealthSeverity.Warning, "HidHide-Version wird nicht unterstützt",
            $"Installiert ist HidHide {installed.ToString(3)}, Arcadia kennt bisher nur HidHide {SteamControllerHiding.SupportedMajorVersion}.x. Ein Arcadia-Update bringt die Unterstützung."),
    };

    private const string HidHideMissingText =
        "Mit HidHide versteckt Arcadia den echten Steam Controller vor Steam und anderen Programmen, solange es ihn selbst weiterreicht. Die Installation fragt einmalig nach Administratorrechten.";

    private static DateTimeOffset LastBootTime(DateTimeOffset now) => now - TimeSpan.FromMilliseconds(Environment.TickCount64);
}

/// <summary>A Steam account on this PC and whether SISR's marker shortcut is in its Steam library.</summary>
public sealed record MarkerAccount(uint Id, string Name, bool HasMarker);
