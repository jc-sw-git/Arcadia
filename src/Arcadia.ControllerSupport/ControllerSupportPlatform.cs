using System.Diagnostics;
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

    /// <summary>Installs or updates usbip-win2; false if the user declined the Windows prompt.</summary>
    Task<bool> InstallUsbipAsync(HttpClient http, CancellationToken cancellationToken);

    /// <summary>Tells Steam to drop SISR's forced controller layout (applied on the next focus change).</summary>
    void ResetSteamInputLayout();

    /// <summary>True while a process from <paramref name="directory"/> runs, e.g. the game SISR is paused for.</summary>
    bool AnyRunningFrom(string directory);

    /// <summary>Problems with what SISR depends on: the usbip driver and Steam.</summary>
    IReadOnlyList<HealthIssue> CheckDependencies(AppSettings settings, DateTimeOffset now);
}

/// <summary>The real Windows and Steam.</summary>
public sealed class ControllerSupportPlatform : IControllerSupportPlatform
{
    public bool IsSupported => OperatingSystem.IsWindows();

    private static readonly TimeSpan SteamExitTimeout = TimeSpan.FromSeconds(30);

    public bool IsSteamInstalled => SteamInstallation.Find() is not null;

    public bool IsSteamRunning => SteamInstallation.IsRunning;

    public bool IsSteamReady =>
        IsSteamUp && SteamInstallation.Find() is { } steam
        && SteamLoginLog.IsLatestLoginComplete(steam.LoginLogPath) is not false;

    public bool IsSteamUp =>
        SteamInstallation.IsRunning && ProcessWatch.IsRunning("steamwebhelper")
        && SteamInstallation.Find() is { ActiveUserId: not (null or 0) };

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _lastMarkerAnswer = new(StringComparer.OrdinalIgnoreCase);

    public bool EnsureSisrMarker(string sisrExecutable, IReadOnlyCollection<uint>? accountIds = null)
    {
        if (SteamInstallation.IsRunning || SteamInstallation.Find() is not { } steam)
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
        if (SteamInstallation.Find() is not { } steam)
        {
            return [];
        }

        return steam.Accounts()
            .Select(a => new MarkerAccount(a.Id, a.Name, HasMarkerIn(ShortcutsPathIn(steam.UserDataDirectoryOf(a.Id)), sisrExecutable)))
            .ToList();
    }

    private static string ShortcutsPathIn(string userDataDirectory) => Path.Combine(userDataDirectory, "config", "shortcuts.vdf");

    public bool HasSisrMarker(string sisrExecutable) =>
        SteamInstallation.Find()?.ShortcutsPath is { } shortcuts && HasMarkerIn(shortcuts, sisrExecutable);

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

    public bool IsCefDebuggingEnabled => SteamInstallation.Find() is { } steam && File.Exists(steam.CefDebuggingFlagPath);

    public void EnableCefDebugging()
    {
        var steam = SteamInstallation.Find() ?? throw new InvalidOperationException("Steam wurde nicht gefunden.");
        File.WriteAllBytes(steam.CefDebuggingFlagPath, []);
    }

    public string? RunningSteamGame() => SteamInstallation.RunningGameName();

    public int? RunningSteamAppId() => SteamInstallation.RunningAppId();

    public SteamInputSetting? SteamInputFor(int appId)
    {
        if (SteamInstallation.Find()?.LocalConfigPath is not { } path || !File.Exists(path))
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
        if (SteamInstallation.Find()?.LocalConfigPath is not { } path || !File.Exists(path))
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

    public void StartSteam() => SteamInstallation.Find()?.StartSilently();

    public Task<bool> InstallUsbipAsync(HttpClient http, CancellationToken cancellationToken) =>
        OperatingSystem.IsWindows() ? UsbipDriver.InstallAsync(http, cancellationToken) : Task.FromResult(false);

    public void ResetSteamInputLayout() =>
        Process.Start(new ProcessStartInfo("steam://forceinputappid/0") { UseShellExecute = true })?.Dispose();

    public bool AnyRunningFrom(string directory) => ProcessWatch.AnyRunningFrom(directory);

    public IReadOnlyList<HealthIssue> CheckDependencies(AppSettings settings, DateTimeOffset now)
    {
        var issues = new List<HealthIssue>();
        if (!OperatingSystem.IsWindows())
        {
            return issues;
        }

        var usbip = UsbipDriver.InstalledVersion();
        if (usbip is null)
        {
            issues.Add(new("usbip-missing", HealthSeverity.Warning, "usbip-Treiber fehlt", "SISR erzeugt den virtuellen Controller über diesen Treiber. Die Installation fragt einmalig nach Administratorrechten.", HealthFix.InstallUsbip, "Installieren"));
        }
        else if (usbip < UsbipDriver.MinimumVersion)
        {
            issues.Add(new("usbip-outdated", HealthSeverity.Warning, "usbip-Treiber ist veraltet", $"Installiert ist {usbip}, benötigt wird mindestens {UsbipDriver.MinimumVersion}.", HealthFix.InstallUsbip, "Aktualisieren"));
        }
        else if (!UsbipDriver.IsRunning())
        {
            issues.Add(new("usbip-stopped", HealthSeverity.Warning, "usbip-Treiber ist nicht aktiv", "Der Treiber ist installiert, aber nicht geladen. Meist hilft ein Neustart des PCs, sonst eine Neuinstallation.", HealthFix.InstallUsbip, "Neu installieren"));
        }
        else if (settings.UsbipInstalledAt is { } installedAt && installedAt > LastBootTime(now))
        {
            issues.Add(new("reboot-pending", HealthSeverity.Warning, "Windows-Neustart ausstehend", "Der usbip-Treiber wurde aktualisiert. Bis zum Neustart kann der Controller aussetzen; dann hilft es, SISR neu zu starten.", HealthFix.RestartSisr, "SISR neu starten"));
        }

        if (SteamInstallation.Find() is null)
        {
            issues.Add(new("steam-missing", HealthSeverity.Warning, "Steam wurde nicht gefunden", "SISR liest den Controller über Steam Input; Steam muss installiert sein."));
        }
        else if (!SteamInstallation.IsRunning)
        {
            issues.Add(new("steam-stopped", HealthSeverity.Warning, "Steam läuft nicht", "Steam muss im Hintergrund laufen, damit SISR den Controller weiterreichen kann.", HealthFix.StartSteam, "Steam starten"));
        }

        return issues;
    }

    private static DateTimeOffset LastBootTime(DateTimeOffset now) => now - TimeSpan.FromMilliseconds(Environment.TickCount64);
}

/// <summary>A Steam account on this PC and whether SISR's marker shortcut is in its Steam library.</summary>
public sealed record MarkerAccount(uint Id, string Name, bool HasMarker);
