using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Arcadia.Launchers.Steam;
using Microsoft.Win32;

namespace Arcadia.ControllerSupport;

/// <summary>
/// Removes SISR and everything that belongs to it, when Arcadia is uninstalled and the user
/// agrees. Does what SISR's own <c>uninstall.ps1</c> does (install and data folders, shortcuts,
/// uninstall entry, VIIPER, usbip-win2) and also removes SISR's Steam marker shortcut and
/// Steam's CEF remote-debugging flag, which exist only for SISR.
/// </summary>
public static class ControllerSupportRemoval
{
    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static string SisrDirectory => Path.Combine(LocalAppData, "SISR");

    private static string ViiperExecutable => Path.Combine(LocalAppData, "VIIPER", "viiper.exe");

    /// <summary>The installed parts, worded for the question to the user; empty if there is nothing to remove.</summary>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<string> FindInstalledParts()
    {
        var parts = new List<string>();
        if (Directory.Exists(SisrDirectory))
        {
            parts.Add("SISR");
        }

        if (File.Exists(ViiperExecutable))
        {
            parts.Add("VIIPER");
        }

        if (UsbipDriver.UninstallCommand() is not null)
        {
            parts.Add("usbip-win2 (Treiber; Windows fragt nach Administratorrechten)");
        }

        return parts;
    }

    /// <summary>
    /// Removes what <see cref="FindInstalledParts"/> found. Best effort: a step that fails is
    /// logged and skipped. Returns within a few seconds; usbip-win2's uninstaller keeps running
    /// on its own.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void Remove(Action<string> log)
    {
        StopProcesses("SISR");

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
        else
        {
            // VIIPER bundled in SISR's folder.
            StopProcesses("viiper");
        }

        var sisrExe = Path.Combine(SisrDirectory, "SISR.exe");
        if (SteamInstallation.Find() is { } steam)
        {
            // SISR asks Steam for this flag to read the controller layout; nothing else needs it.
            Step(log, "Steam-CEF-Flag", () => File.Delete(Path.Combine(steam.RootPath, ".cef-enable-remote-debugging")));

            // Steam rewrites shortcuts.vdf on exit, so the marker can only go while Steam is closed.
            if (!SteamInstallation.IsRunning && steam.ShortcutsPath is { } shortcuts)
            {
                Step(log, "Steam-Eintrag", () => SteamShortcuts.RemoveByExecutable(shortcuts, sisrExe));
            }
        }

        Step(log, "SISR-Ordner", () => DeleteDirectory(SisrDirectory));
        Step(log, "SISR-Daten", () => DeleteDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SISR")));
        Step(log, "SISR-Verknüpfungen", () =>
        {
            var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SISR.lnk"));
            File.Delete(Path.Combine(programs, "SISR.lnk"));
            File.Delete(Path.Combine(programs, "SISR (No Steam).lnk"));
        });
        Step(log, "SISR-Deinstallationseintrag", () =>
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\SISR", throwOnMissingSubKey: false));

        if (UsbipDriver.UninstallCommand() is { } usbip)
        {
            Step(log, "usbip-win2", () =>
            {
                var (file, arguments) = SplitCommand(usbip);
                using var _ = Process.Start(new ProcessStartInfo(file, arguments) { UseShellExecute = true, Verb = "runas" });
            });
        }
    }

    /// <summary>Splits an uninstall command like <c>"C:\x\uninstall.exe" /S</c> into file and arguments.</summary>
    public static (string File, string Arguments) SplitCommand(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"') && command.IndexOf('"', 1) is var end and > 0)
        {
            return (command[1..end], command[(end + 1)..].Trim());
        }

        return (command, string.Empty);
    }

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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            log($"Nicht entfernt: {what} ({ex.Message})");
        }
    }
}
