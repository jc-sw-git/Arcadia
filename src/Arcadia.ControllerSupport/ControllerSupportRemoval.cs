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
/// question "Treiber für die Controller-Emulation auch entfernen?" (#243 part B2). SISR and VIIPER go here, as SISR's
/// own <c>uninstall.ps1</c> would (install and data folders, shortcuts, uninstall entry), with SISR's Steam marker and
/// Steam's CEF flag, which exist only for SISR; until #214 removes SISR. usbip-win2 and HidHide go in Pan's
/// <c>--uninstall --remove-drivers</c>, inside its one UAC prompt and after Pan undid its own HidHide entries.
/// </summary>
public static class ControllerSupportRemoval
{
    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static string SisrDirectory => Path.Combine(LocalAppData, "SISR");

    private static string ViiperExecutable => Path.Combine(LocalAppData, "VIIPER", "viiper.exe");

    /// <summary>
    /// What SISR and VIIPER leave in the roaming profile: SISR's WebView2 data lands in a folder
    /// named after its exe ("SISR.exe"), VIIPER keeps its key file in "VIIPER" (seen on the
    /// owner's PC, 2026-10-16). "SISR" is what SISR's own uninstaller removes.
    /// </summary>
    public static IReadOnlyList<string> DataDirectories { get; } =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SISR"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SISR.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VIIPER"),
    ];

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
    /// Removes SISR and VIIPER (<see cref="FindInstalledParts"/>); the drivers go with Pan's uninstall. Best effort: a
    /// step that fails is logged and skipped. Returns within a few seconds.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void Remove(Action<string> log)
    {
        // Ordered by what matters most, because Velopack may end the uninstall hook before the slow steps are done.
        StopProcesses("SISR");
        StopProcesses("viiper");

        var sisrExe = Path.Combine(SisrDirectory, "SISR.exe");
        Step(log, "SISR-Ordner", () => DeleteDirectory(SisrDirectory));
        foreach (var data in DataDirectories)
        {
            Step(log, $"Daten in {Path.GetFileName(data)}", () => DeleteDirectory(data));
        }
        Step(log, "SISR-Verknüpfungen", () =>
        {
            var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SISR.lnk"));
            File.Delete(Path.Combine(programs, "SISR.lnk"));
            File.Delete(Path.Combine(programs, "SISR (No Steam).lnk"));
        });
        Step(log, "SISR-Deinstallationseintrag", () =>
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\SISR", throwOnMissingSubKey: false));

        if (SteamInstallation.Find() is { } steam)
        {
            // SISR asks Steam for this flag to read the controller layout; nothing else needs it.
            Step(log, "Steam-CEF-Flag", () => File.Delete(steam.CefDebuggingFlagPath));
            RemoveMarkersWithSteamClosed(steam, sisrExe, log);
        }

        if (File.Exists(ViiperExecutable))
        {
            Step(log, "VIIPER", () =>
            {
                using var uninstall = Process.Start(new ProcessStartInfo(ViiperExecutable, "uninstall --quiet") { CreateNoWindow = true });
                uninstall?.WaitForExit(TimeSpan.FromSeconds(5));
            });
            StopProcesses("viiper");
            Step(log, "VIIPER-Ordner", () => Directory.Delete(Path.GetDirectoryName(ViiperExecutable)!, recursive: true));
        }
    }

    /// <summary>How long the uninstall waits for Steam to close; it usually takes 2 to 5 s.</summary>
    public static readonly TimeSpan SteamShutdownTimeout = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Steam rewrites <c>shortcuts.vdf</c> from memory when it exits, so a marker removed while it runs comes back:
    /// Steam is closed for the removal and started again afterwards (owner, 2026-10-24).
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void RemoveMarkersWithSteamClosed(SteamInstallation steam, string sisrExe, Action<string> log)
    {
        var wasRunning = SteamInstallation.IsRunning;
        if (wasRunning && !SteamInstallation.ShutdownAsync(SteamShutdownTimeout, TimeProvider.System).GetAwaiter().GetResult())
        {
            log($"Steam-Eintrag „SISR Marker“ nicht entfernt: Steam war nach {SteamShutdownTimeout.TotalSeconds:0} s noch nicht beendet");
            return;
        }

        Step(log, "Steam-Eintrag „SISR Marker“", () => RemoveMarkers(steam, sisrExe));
        if (wasRunning)
        {
            steam.StartSilently();
        }
    }

    /// <summary>Removes the marker from every account that has one; returns how many had it.</summary>
    public static int RemoveMarkers(SteamInstallation steam, string sisrExe) =>
        steam.AllShortcutsPaths().Count(shortcuts => SteamShortcuts.RemoveByExecutable(shortcuts, sisrExe) > 0);

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
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

    private static void Step(Action<string> log, string what, Action action)
    {
        try
        {
            action();
            log($"Entfernt: {what}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException or FormatException)
        {
            log($"Nicht entfernt: {what} ({ex.Message})");
        }
    }
}
