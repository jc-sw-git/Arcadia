using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Arcadia.Launchers.Steam;
using Microsoft.Win32;

namespace Arcadia.ControllerSupport;

/// <summary>What of the controller emulation is installed; <see cref="Drivers"/> need Pan's elevated uninstall.</summary>
public sealed record InstalledParts(bool Sisr, bool Viiper, bool Usbip, bool HidHide)
{
    public bool Any => Sisr || Viiper || Usbip || HidHide;

    public bool Drivers => Usbip || HidHide;
}

/// <summary>
/// Removes the controller emulation's drivers and SISR, when Arcadia is uninstalled and the user agrees to the one
/// question "Treiber für die Controller-Emulation auch entfernen?" (#243 part B2). SISR and VIIPER go here as "SISR
/// entfernen" in the settings removes them (<see cref="SisrInstallation"/>, #214). usbip-win2 and HidHide go in Pan's
/// <c>--uninstall --remove-drivers</c>, inside its one UAC prompt and after Pan undid its own HidHide entries.
/// </summary>
public static class ControllerSupportRemoval
{
    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static string SisrDirectory => Path.Combine(LocalAppData, "SISR");

    private static string ViiperExecutable => Path.Combine(LocalAppData, "VIIPER", "viiper.exe");

    /// <summary>What is installed of the controller emulation, for the question at Arcadia's uninstall.</summary>
    [SupportedOSPlatform("windows")]
    public static InstalledParts FindInstalledParts() => new(
        Sisr: Directory.Exists(SisrDirectory),
        Viiper: File.Exists(ViiperExecutable),
        Usbip: UsbipDriver.UninstallCommand() is not null,
        HidHide: HidHideInstallation.InstalledVersion() is not null);

    /// <summary>
    /// The one question at Arcadia's uninstall (#243 part B2). A Windows message box can't offer a checklist, so it lists
    /// what is there and asks once; no answer or "Nein" keeps everything (other programs may use the drivers).
    /// </summary>
    public static string Question(InstalledParts parts, bool steamRunning, uint timeoutSeconds)
    {
        var lines = new List<string>();
        if (parts.Usbip)
        {
            lines.Add("usbip-win2 (Treiber)");
        }

        if (parts.HidHide)
        {
            lines.Add("HidHide (Treiber)");
        }

        if (parts.Sisr || parts.Viiper)
        {
            lines.Add(parts.Sisr && parts.Viiper ? "SISR und VIIPER" : parts.Sisr ? "SISR" : "VIIPER");
        }

        return "Arcadia wird deinstalliert.\n\n"
            + "Treiber für die Controller-Emulation auch entfernen?\n\n"
            + string.Concat(lines.Select(l => $"•  {l}\n"))
            + "\nAndere Programme, die sie nutzen (etwa DS4Windows mit HidHide), funktionieren danach nicht mehr. "
            + (parts.Drivers ? "Windows fragt dafür einmal nach Administratorrechten. " : string.Empty)
            + (parts.Sisr && steamRunning ? "Steam wird dafür kurz beendet und wieder gestartet. " : string.Empty)
            + $"Ohne Antwort innerhalb von {timeoutSeconds} Sekunden bleibt alles installiert; Arcadia wird trotzdem deinstalliert.";
    }

    /// <summary>
    /// Removes SISR and VIIPER (<see cref="FindInstalledParts"/>) as "SISR entfernen" does (<see cref="SisrInstallation"/>);
    /// the drivers go with Pan's uninstall. Best effort: what fails is logged and skipped. Returns within a few seconds.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void Remove(Action<string> log)
    {
        // Ordered by what matters most, because Velopack may end the uninstall hook before the slow steps are done.
        StopProcesses("SISR");
        StopProcesses("viiper");

        var steam = SteamInstallation.Find();
        // Arcadia goes too, so its own viiper.exe no longer needs VIIPER's key folder.
        var sisr = new SisrInstallation(SisrInstallation.DefaultPaths(arcadiaViiper: null), steam, new WindowsSisrSystem());
        var steamClosed = steam is not null && sisr.Find().HasMarker && CloseSteamForMarker(log);
        var result = sisr.Remove(steamRunning: SteamInstallation.IsRunning);
        log(result.Complete ? "Entfernt: SISR und VIIPER" : $"Nicht entfernt: {string.Join(", ", result.NotRemoved)} ({result.Cause})");
        if (steamClosed)
        {
            steam!.StartSilently();
        }
    }

    /// <summary>How long the uninstall waits for Steam to close; it usually takes 2 to 5 s.</summary>
    public static readonly TimeSpan SteamShutdownTimeout = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Steam rewrites <c>shortcuts.vdf</c> from memory when it exits, so a marker removed while it runs comes back:
    /// Steam is closed for the removal and started again afterwards (owner, 2026-10-24). True if Arcadia closed it.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static bool CloseSteamForMarker(Action<string> log)
    {
        if (!SteamInstallation.IsRunning)
        {
            return false;
        }

        if (!SteamInstallation.ShutdownAsync(SteamShutdownTimeout, TimeProvider.System).GetAwaiter().GetResult())
        {
            log($"Steam-Eintrag „SISR Marker“ nicht entfernt: Steam war nach {SteamShutdownTimeout.TotalSeconds:0} s noch nicht beendet");
            return false;
        }

        return true;
    }

    private static void StopProcesses(string name)
    {
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // Already gone or not ours to stop.
                }
            }
        }
    }
}
