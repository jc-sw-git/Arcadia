using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Arcadia.Pan.Windows;

/// <summary>
/// <c>--uninstall --remove-drivers</c> (#243 part B2): after Pan undid its own entries, HidHide and usbip-win2 go
/// too, silently and inside the same UAC prompt. Runs only when the user said yes at Arcadia's uninstall (default no:
/// other programs may use them). Best effort: a step that fails is logged, the next one still runs.
/// </summary>
[SupportedOSPlatform("windows")]
public static class DriverRemoval
{
    private static TimeSpan UninstallerWait => PanTimeouts.DriverUninstaller;

    private static readonly string[] UninstallRoots =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ];

    /// <returns>False if a driver that is installed could not be removed.</returns>
    public static async Task<bool> RunAsync(Action<string> log)
    {
        var entries = Entries();
        var complete = true;
        // HidHide first: once its filter is gone, nothing can stay hidden, whatever the journal still lists.
        foreach (var (what, listedAs, command) in new[]
                 {
                     ("HidHide", "HidHide", DriverUninstall.ForHidHide(entries, Environment.SystemDirectory)),
                     ("usbip-win2", "USBip", DriverUninstall.ForUsbip(entries, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))),
                 })
        {
            if (command is null)
            {
                var listed = entries.Any(e => e.DisplayName?.StartsWith(listedAs, StringComparison.OrdinalIgnoreCase) == true);
                log(listed ? $"{what}: no uninstaller Pan may run; remove it in Windows' Apps" : $"{what}: not installed");
                complete &= !listed;
                continue;
            }

            complete &= await RunUninstallerAsync(command, log);
        }

        return complete;
    }

    private static async Task<bool> RunUninstallerAsync(UninstallCommand command, Action<string> log)
    {
        var start = new ProcessStartInfo(command.File) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in command.Arguments)
        {
            start.ArgumentList.Add(argument);
        }

        log($"Removing {command.What}: {command.File} {string.Join(' ', command.Arguments)}");
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("not started");
            using var timeout = new CancellationTokenSource(UninstallerWait);
            await process.WaitForExitAsync(timeout.Token);
            // 3010: removed, Windows wants a restart to unload the driver.
            if (process.ExitCode is 0 or 3010)
            {
                log($"{command.What} removed{(process.ExitCode == 3010 ? "; Windows wants a restart" : string.Empty)}");
                return true;
            }

            log($"{command.What} not removed: its uninstaller reported {process.ExitCode}");
            return false;
        }
        catch (OperationCanceledException)
        {
            log($"{command.What}: its uninstaller still runs after {UninstallerWait.TotalMinutes:0} min; left running");
            return false;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            log($"{command.What} not removed: {ex.Message}");
            return false;
        }
    }

    private static List<UninstallEntry> Entries()
    {
        var entries = new List<UninstallEntry>();
        foreach (var root in UninstallRoots)
        {
            using var uninstall = Registry.LocalMachine.OpenSubKey(root);
            if (uninstall is null)
            {
                continue;
            }

            foreach (var name in uninstall.GetSubKeyNames())
            {
                using var entry = uninstall.OpenSubKey(name);
                if (entry is not null)
                {
                    entries.Add(new UninstallEntry(
                        name,
                        entry.GetValue("DisplayName") as string,
                        entry.GetValue("Publisher") as string,
                        entry.GetValue("UninstallString") as string,
                        entry.GetValue("WindowsInstaller") is int and 1));
                }
            }
        }

        return entries;
    }
}
