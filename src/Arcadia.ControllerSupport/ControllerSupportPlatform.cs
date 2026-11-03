using System.Diagnostics;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Pan;
using Arcadia.Core.Settings;
using Arcadia.Platform;

namespace Arcadia.ControllerSupport;

/// <summary>What the controller emulation's setup (drivers, Pan) needs from Windows; replaced in tests. Steam is a launcher program (<c>ILauncherProgram</c>).</summary>
public interface IControllerSupportPlatform
{
    /// <summary>The emulation exists only for Windows so far.</summary>
    bool IsSupported { get; }

    /// <summary>usbip-win2 is installed in at least <see cref="UsbipDriver.MinimumVersion"/> and loaded.</summary>
    bool IsUsbipReady { get; }

    /// <summary>Pan, the guardian service, is installed (#243); without it Arcadia plugs no pad in.</summary>
    bool IsPanReady { get; }

    /// <summary>The Pan service is in any state but stopped (#326); false if it isn't installed.</summary>
    bool IsPanRunning => false;

    /// <summary>Whether the installed Pan is exactly the one Arcadia ships (#297); reads files, so off the UI thread or once at start.</summary>
    PanCompatibility PanCompatibility { get; }

    /// <summary>HidHide's driver version, or null if HidHide isn't installed (#243 part B2).</summary>
    Version? HidHideVersion { get; }

    /// <summary>
    /// A driver install would run HidHide's installer now, which restarts the puck afterwards (#296):
    /// HidHide is missing or of an unsupported version, and no uninstall waits for a restart.
    /// </summary>
    bool InstallsHidHide => ControllerSupportPlatform.HidHideNeedsInstaller(HidHideVersion);

    /// <summary>
    /// The Windows prompt of a driver install (#413, D7): elevated Pan, waiting for the installers of
    /// <paramref name="plan"/>; null if the user declined the prompt. Nothing else happens before it.
    /// </summary>
    Task<IPanInstallRun?> StartPanInstallAsync(PanInstallPlan plan, CancellationToken cancellationToken);

    /// <summary>usbip-win2's latest installer into <paramref name="folder"/>, checked by GitHub's SHA-256; returns its path.</summary>
    Task<string> DownloadUsbipInstallerAsync(HttpClient http, string folder, IProgress<DownloadProgress> progress, CancellationToken cancellationToken);

    /// <summary>The pinned HidHide installer into <paramref name="folder"/>, checked by its pinned SHA-256; returns its path.</summary>
    Task<string> DownloadHidHideInstallerAsync(HttpClient http, string folder, IProgress<DownloadProgress> progress, CancellationToken cancellationToken);

    /// <summary>
    /// The update at start (#297): only Pan's <c>--install</c> with the bundled exe, no driver installer, so no download
    /// and no puck restart; false if the user declined the Windows prompt.
    /// </summary>
    Task<bool> UpdatePanAsync(CancellationToken cancellationToken) => Task.FromResult(false);


    /// <summary>Pan's <c>--restore</c>: gives the real controllers back by hand; false if the user declined the Windows prompt.</summary>
    Task<bool> RestoreControllersAsync(CancellationToken cancellationToken);

    /// <summary>Problems with what Arcadia's own pad depends on: the usbip driver and Pan (#243).</summary>
    IReadOnlyList<HealthIssue> CheckDrivers(AppSettings settings, DateTimeOffset now);

    /// <summary>
    /// A driver change waits for the PC's restart (#407): HidHide or usbip-win2 was uninstalled, or usbip-win2 was
    /// installed since the PC started. An install before the restart would meet a half removed or half set up driver.
    /// </summary>
    bool DriverChangePending(AppSettings settings, DateTimeOffset now) => false;

    /// <summary>
    /// A controller's firmware may be flashed now (#407): a Steam Controller or puck is in its bootloader, or a vendor's
    /// firmware tool runs (<see cref="ControllerList.FirmwareTools"/>). An install restarts the puck and controllers.
    /// </summary>
    bool FirmwareUpdateRuns => false;

    /// <summary>A Steam Controller (by cable) or its puck is plugged in (#407).</summary>
    bool SteamControllerPlugged => false;
}

/// <summary>The real Windows.</summary>
public sealed class ControllerSupportPlatform : IControllerSupportPlatform
{
    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsUsbipReady =>
        OperatingSystem.IsWindows() && UsbipDriver.InstalledVersion() is { } version && version >= UsbipDriver.MinimumVersion && UsbipDriver.IsRunning();

    /// <summary>Installed, and its last run didn't end with an error (then its install repairs it).</summary>
    public bool IsPanReady => OperatingSystem.IsWindows() && PanSetup.IsInstalled && !PanPipeTransport.LastRunFailed();

    public bool IsPanRunning => OperatingSystem.IsWindows() && PanPipeTransport.IsServiceRunning();

    public PanCompatibility PanCompatibility => OperatingSystem.IsWindows() ? PanSetup.Compatibility : PanCompatibility.NotInstalled;

    public Version? HidHideVersion => OperatingSystem.IsWindows() ? HidHideInstallation.InstalledVersion() : null;

    /// <summary>HidHide was uninstalled and goes at the next restart; its setup would fail until then.</summary>
    private static bool HidHideRemovalPending => OperatingSystem.IsWindows() && HidHideInstallation.IsRemovalPending();

    public bool InstallsHidHide => HidHideNeedsInstaller(HidHideVersion) && !HidHideRemovalPending;

    public Task<IPanInstallRun?> StartPanInstallAsync(PanInstallPlan plan, CancellationToken cancellationToken) =>
        OperatingSystem.IsWindows() ? PanSetup.StartInstallAsync(plan, cancellationToken) : Task.FromResult<IPanInstallRun?>(null);

    public Task<string> DownloadUsbipInstallerAsync(HttpClient http, string folder, IProgress<DownloadProgress> progress, CancellationToken cancellationToken) =>
        OperatingSystem.IsWindows() ? UsbipDriver.DownloadInstallerAsync(http, folder, progress, cancellationToken) : throw new PlatformNotSupportedException();

    public Task<string> DownloadHidHideInstallerAsync(HttpClient http, string folder, IProgress<DownloadProgress> progress, CancellationToken cancellationToken) =>
        OperatingSystem.IsWindows() ? HidHideInstallation.DownloadInstallerAsync(http, folder, progress, cancellationToken) : throw new PlatformNotSupportedException();

    public Task<bool> UpdatePanAsync(CancellationToken cancellationToken) =>
        OperatingSystem.IsWindows() ? PanSetup.UpdateAsync(cancellationToken) : Task.FromResult(false);

    /// <summary>
    /// Missing or older than Pan supports. A newer major one is left alone: its installer would be a downgrade, and
    /// only an Arcadia update can bring support for it.
    /// </summary>
    public static bool HidHideNeedsInstaller(Version? installed) =>
        installed is null || (installed.Major <= SteamControllerHiding.SupportedMajorVersion && !HidHideRelease.IsSupported(installed));

    public bool DriverChangePending(AppSettings settings, DateTimeOffset now) =>
        OperatingSystem.IsWindows()
        && (HidHideRemovalPending || UsbipDriver.IsRemovalPending() || settings.UsbipInstalledAt is { } installedAt && installedAt > LastBootTime(now));

    public bool FirmwareUpdateRuns =>
        OperatingSystem.IsWindows()
        && (FirmwareToolRuns() || ValveBootloaders.Present());

    /// <summary>One snapshot of the process list for all the tools' names, not one per name.</summary>
    private static bool FirmwareToolRuns()
    {
        var processes = Process.GetProcesses();
        try
        {
            var running = processes.Select(process => process.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return ControllerList.FirmwareTools.Any(tool => tool.ProcessNames.Any(running.Contains));
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    public bool SteamControllerPlugged => OperatingSystem.IsWindows() && Copy.WindowsPuck.Present() is not null;

    public Task<bool> RestoreControllersAsync(CancellationToken cancellationToken) =>
        OperatingSystem.IsWindows() ? PanSetup.RestoreAsync(cancellationToken) : Task.FromResult(false);

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
            issues.Add(EmulationParts.RebootPending);
        }

        if (CheckHidHide(HidHideVersion, HidHideRemovalPending) is { } hidHide)
        {
            issues.Add(hidHide);
        }

        if (PanSetup.IsInstalled)
        {
            if (PanPipeTransport.LastRunFailed())
            {
                issues.Add(EmulationParts.PanFailing);
            }
        }
        else
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
