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

    /// <summary>usbip-win2 is installed in at least <see cref="UsbipDriver.MinimumVersion"/> and loaded.</summary>
    bool IsUsbipReady { get; }

    /// <summary>Steam's CEF debug flag file exists (it takes effect on Steam's next start).</summary>
    bool IsCefDebuggingEnabled { get; }

    void EnableCefDebugging();

    /// <summary>The name of the Steam game running right now, or null.</summary>
    string? RunningSteamGame();

    /// <summary>Asks Steam to exit and waits for it; false if it is still running after the timeout.</summary>
    Task<bool> StopSteamAsync(TimeProvider time, CancellationToken cancellationToken);

    /// <summary>Starts Steam minimized to the tray.</summary>
    void StartSteam();

    /// <summary>Installs or updates usbip-win2; false if the user declined the Windows prompt.</summary>
    Task<bool> InstallUsbipAsync(HttpClient http, CancellationToken cancellationToken);

    bool IsSteamControllerConnected();

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

    public bool IsUsbipReady =>
        OperatingSystem.IsWindows() && UsbipDriver.InstalledVersion() is { } version && version >= UsbipDriver.MinimumVersion && UsbipDriver.IsRunning();

    public bool IsCefDebuggingEnabled => SteamInstallation.Find() is { } steam && File.Exists(steam.CefDebuggingFlagPath);

    public void EnableCefDebugging()
    {
        var steam = SteamInstallation.Find() ?? throw new InvalidOperationException("Steam wurde nicht gefunden.");
        File.WriteAllBytes(steam.CefDebuggingFlagPath, []);
    }

    public string? RunningSteamGame() => SteamInstallation.RunningGameName();

    public async Task<bool> StopSteamAsync(TimeProvider time, CancellationToken cancellationToken) =>
        await SteamInstallation.ShutdownAsync(SteamExitTimeout, time, cancellationToken);

    public void StartSteam() => SteamInstallation.Find()?.StartSilently();

    public Task<bool> InstallUsbipAsync(HttpClient http, CancellationToken cancellationToken) =>
        OperatingSystem.IsWindows() ? UsbipDriver.InstallAsync(http, cancellationToken) : Task.FromResult(false);

    public bool IsSteamControllerConnected()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            return SteamControllerDetector.IsConnected();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Without the device list, rather show the per-game setting than hide it.
            return true;
        }
    }

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
        else if (settings.UsbipInstalledAt is { } installedAt && installedAt > LastBootTime(now))
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

        return issues;
    }

    private static DateTimeOffset LastBootTime(DateTimeOffset now) => now - TimeSpan.FromMilliseconds(Environment.TickCount64);
}
