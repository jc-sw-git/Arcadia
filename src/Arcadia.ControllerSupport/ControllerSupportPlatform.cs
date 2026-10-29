using System.Diagnostics;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Pan;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Steam;

namespace Arcadia.ControllerSupport;

/// <summary>What the controller emulation's setup (drivers, Pan, closing Steam for them) needs from Windows and Steam; replaced in tests.</summary>
public interface IControllerSupportPlatform
{
    /// <summary>The emulation exists only for Windows so far.</summary>
    bool IsSupported { get; }

    bool IsSteamInstalled { get; }

    bool IsSteamRunning { get; }

    /// <summary>
    /// Steam is back after Arcadia closed it: its process and its web helper run, a user is logged in and the client
    /// finished that login (it can hang at "loading user data" after the server accepted it).
    /// </summary>
    bool IsSteamReady { get; }

    /// <summary>usbip-win2 is installed in at least <see cref="UsbipDriver.MinimumVersion"/> and loaded.</summary>
    bool IsUsbipReady { get; }

    /// <summary>Pan, the guardian service, is installed (#243); without it Arcadia plugs no pad in.</summary>
    bool IsPanReady { get; }

    /// <summary>Whether the installed Pan is exactly the one Arcadia ships (#297); reads files, so off the UI thread or once at start.</summary>
    PanCompatibility PanCompatibility { get; }

    /// <summary>HidHide's driver version, or null if HidHide isn't installed (#243 part B2).</summary>
    Version? HidHideVersion { get; }

    /// <summary>
    /// <see cref="InstallDriversAsync"/> would run HidHide's installer now, which restarts the puck afterwards (#296):
    /// HidHide is missing or of an unsupported version, and no uninstall waits for a restart.
    /// </summary>
    bool InstallsHidHide => ControllerSupportPlatform.HidHideNeedsInstaller(HidHideVersion);

    /// <summary>The name of the Steam game running right now, or null.</summary>
    string? RunningSteamGame();

    /// <summary>The app id of the Steam game running right now, or null.</summary>
    int? RunningSteamAppId();

    /// <summary>Asks Steam to exit and waits for it; false if it is still running after the timeout.</summary>
    Task<bool> StopSteamAsync(TimeProvider time, CancellationToken cancellationToken);

    /// <summary>Starts Steam minimized to the tray.</summary>
    void StartSteam();

    /// <summary>
    /// Installs or updates what is missing of usbip-win2, HidHide and Pan, with one Windows prompt (#243); false if the
    /// user declined it.
    /// </summary>
    Task<bool> InstallDriversAsync(HttpClient http, CancellationToken cancellationToken);

    /// <summary>
    /// "Neu installieren" for a HidHide that doesn't answer as expected (#242): the pinned installer over the installed
    /// one, Pan with it, one Windows prompt; false if the user declined it.
    /// </summary>
    Task<bool> ReinstallHidHideAsync(HttpClient http, CancellationToken cancellationToken) => Task.FromResult(false);

    /// <summary>
    /// The update at start (#297): only Pan's <c>--install</c> with the bundled exe, no driver installer, so no download
    /// and no puck restart; false if the user declined the Windows prompt.
    /// </summary>
    Task<bool> UpdatePanAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    /// <summary>"Pan entfernen" after a failed update (#297): Pan's <c>--uninstall</c> without the drivers; false if declined.</summary>
    Task<bool> UninstallPanAsync(CancellationToken cancellationToken) => Task.FromResult(false);

    /// <summary>Pan's <c>--restore</c>: gives the real controllers back by hand; false if the user declined the Windows prompt.</summary>
    Task<bool> RestoreControllersAsync(CancellationToken cancellationToken);

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

    private bool IsSteamUp =>
        _isSteamRunning() && ProcessWatch.IsRunning("steamwebhelper")
        && _locate() is { ActiveUserId: not (null or 0) };

    public bool IsUsbipReady =>
        OperatingSystem.IsWindows() && UsbipDriver.InstalledVersion() is { } version && version >= UsbipDriver.MinimumVersion && UsbipDriver.IsRunning();

    /// <summary>Installed, and its last run didn't end with an error (then its install repairs it).</summary>
    public bool IsPanReady => OperatingSystem.IsWindows() && PanSetup.IsInstalled && !PanPipeTransport.LastRunFailed();

    public PanCompatibility PanCompatibility => OperatingSystem.IsWindows() ? PanSetup.Compatibility : PanCompatibility.NotInstalled;

    public Version? HidHideVersion => OperatingSystem.IsWindows() ? HidHideInstallation.InstalledVersion() : null;

    /// <summary>HidHide was uninstalled and goes at the next restart; its setup would fail until then.</summary>
    private static bool HidHideRemovalPending => OperatingSystem.IsWindows() && HidHideInstallation.IsRemovalPending();

    public bool InstallsHidHide => HidHideNeedsInstaller(HidHideVersion) && !HidHideRemovalPending;

    public string? RunningSteamGame() => SteamInstallation.RunningGameName();

    public int? RunningSteamAppId() => SteamInstallation.RunningAppId();

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
            hidHideInstaller = InstallsHidHide ? await HidHideInstallation.DownloadInstallerAsync(http, cancellationToken) : null;
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

    public Task<bool> UpdatePanAsync(CancellationToken cancellationToken) =>
        OperatingSystem.IsWindows() ? PanSetup.InstallAsync(null, null, cancellationToken) : Task.FromResult(false);

    public Task<bool> UninstallPanAsync(CancellationToken cancellationToken) =>
        OperatingSystem.IsWindows() ? PanSetup.UninstallAsync(removeDrivers: false, cancellationToken) : Task.FromResult(false);

    public async Task<bool> ReinstallHidHideAsync(HttpClient http, CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        // The service checked it too; the repair arguments over another version would be an up- or downgrade.
        if (HidHideVersion is not { } installed || !HidHideRelease.IsPinnedDriver(installed))
        {
            throw new InvalidOperationException($"HidHide {HidHideRelease.Version} ist nicht installiert; „Neu installieren“ geht nur darüber.");
        }

        var installer = await HidHideInstallation.DownloadInstallerAsync(http, cancellationToken);
        try
        {
            return await PanSetup.InstallAsync(null, installer, cancellationToken, repairHidHide: true);
        }
        finally
        {
            File.Delete(installer);
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
