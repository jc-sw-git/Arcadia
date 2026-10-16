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

    bool IsSteamRunning { get; }

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

    public bool IsSteamRunning => SteamInstallation.IsRunning;

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
