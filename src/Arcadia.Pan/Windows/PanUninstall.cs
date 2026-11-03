using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.Pan.Windows;

/// <summary>
/// Pan's way off the PC (#243 part B2, #270, #422): <c>--uninstall</c> in <see cref="RunAsync"/>, and the service
/// finishing a pending uninstall or removing itself when no Arcadia is left in <see cref="FinishInServiceAsync"/>. Both
/// go through <see cref="UninstallOrder"/>, which keeps the service until nothing is left for it to retry.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PanUninstall
{
    public static PendingUninstallFile Marker => new(Path.Combine(PanPaths.DataDirectory, PendingUninstallFile.FileName));

    public static RecordedInstallsFile RecordedInstalls => new(Path.Combine(PanPaths.DataDirectory, RecordedInstallsFile.FileName));

    /// <summary>
    /// Records an <c>Arcadia.exe</c> so the service finds it at its start even where Velopack's installer didn't put it
    /// (a build run from its folder): the one beside the bundled Pan at <c>--install</c>, and every client named so.
    /// Only ever evidence that Arcadia is there, so a client that only borrows the name keeps Pan installed, nothing
    /// more. Best effort: unrecorded, only Velopack's places count.
    /// </summary>
    public static void RecordArcadia(string? executable, Action<string> log)
    {
        if (executable is null || !string.Equals(Path.GetFileName(executable), ArcadiaInstallations.ExecutableName, StringComparison.OrdinalIgnoreCase)
            || RecordedInstalls.Load().Contains(executable, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            RecordedInstalls.Add(executable);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"{executable} not recorded: {ex.Message}");
        }
    }

    /// <summary>
    /// <c>--uninstall [--remove-drivers]</c>: a data folder that isn't ours is neither written nor deleted, and gets no
    /// marker, so nothing retries there; the service and Program Files go anyway.
    /// </summary>
    public static async Task<UninstallOutcome> RunAsync(DataFolder data, bool removeDrivers, Action stopFileLog, Action<string> log)
    {
        var outcome = await UninstallOrder.RunAsync(data, new PendingUninstall(removeDrivers, DriverAttempts: 0), Marker, new UninstallSteps
        {
            StopService = () => ServiceSetup.StopInstalledService(log),
            Restore = () => Restore.RunAsync(log),
            RemoveStaleDevices = () => Restore.RemoveStaleDevicesAsync(log),
            RemoveDrivers = () => DriverRemoval.RunAsync(log),
            RemoveService = () => ServiceSetup.DeleteService(log),
            StopFileLog = stopFileLog,
            DeleteData = () => DeleteDataFolders(log),
            Log = log,
        });
        if (outcome is UninstallOutcome.Done or UninstallOutcome.DriversLeft)
        {
            DeleteTree(PanPaths.InstallDirectory, log);
        }

        return outcome;
    }

    /// <summary>
    /// Whether the service, just started and its journal recovered, is to finish an uninstall instead of serving
    /// (<see cref="SelfRemoval"/>). Never throws: in doubt Pan stays.
    /// </summary>
    public static async Task<bool> ShouldFinishAsync(bool undoComplete, Func<bool> clientCame, Action<string> log)
    {
        try
        {
            return await SelfRemoval.ShouldFinishAsync(undoComplete, Marker, ArcadiaProbe.Look, () => Task.Delay(SelfRemoval.SecondLookDelay), clientCame, log);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"Pending uninstall not checked: {ex.Message}; Pan stays");
            return false;
        }
    }

    /// <summary>
    /// The service finishes the uninstall as the marker says (without one: without the drivers, as the player never said
    /// yes to them). It is the service, so there is nothing to stop and the journal is recovered already; it deletes its
    /// own service, which Windows removes once this process has stopped, and moves its running exe out of the way.
    /// Never throws: a failure here would make Windows' recovery restart Pan at once, up to three times, and use up the
    /// driver attempts meant for three Windows starts in one; the next start tries again instead.
    /// </summary>
    public static async Task FinishInServiceAsync(PanLog log)
    {
        try
        {
            await FinishAsync(log);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log.Write($"Finishing the uninstall failed: {ex}; the next Windows start tries again");
        }
    }

    private static async Task FinishAsync(PanLog log)
    {
        var marker = Marker;
        var pending = marker.Load() ?? new PendingUninstall(RemoveDrivers: false, DriverAttempts: 0);
        var outcome = await UninstallOrder.RunAsync(DataFolder.Ours, pending, marker, new UninstallSteps
        {
            StopService = () => { },
            Restore = () => Task.FromResult(true),
            // The service's start removed them already (Guardian.CleanUpStaleDevices).
            RemoveStaleDevices = null,
            RemoveDrivers = () => DriverRemoval.RunAsync(log.Write),
            RemoveService = () => ServiceSetup.DeleteService(log.Write),
            StopFileLog = log.StopFileLogging,
            DeleteData = () => DeleteDataFolders(log.Write),
            Log = log.Write,
        });
        if (outcome is UninstallOutcome.Done or UninstallOutcome.DriversLeft)
        {
            RetireInstallDirectory(log.Write);
        }
    }

    /// <summary>
    /// The running <c>Pan.exe</c> can't be deleted, but it can be renamed: it moves into <c>Pan-Uninstall</c>, which
    /// Windows deletes at the next boot. Scheduling <c>Pan\Pan.exe</c> itself would delete a Pan installed again before
    /// that boot. Only Program Files is touched, which only administrators can write.
    /// </summary>
    private static void RetireInstallDirectory(Action<string> log)
    {
        var running = PanPaths.InstalledExecutable;
        var retired = Path.Combine(PanPaths.HandOffDirectory, $"Pan-retired-{DateTime.UtcNow:yyyyMMddHHmmss}.exe");
        try
        {
            Directory.CreateDirectory(PanPaths.HandOffDirectory);
            File.Move(running, retired);
            ScheduleHandOffRemoval(retired, log);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"{running} could not be moved aside ({ex.Message})");
        }

        DeleteTree(PanPaths.InstallDirectory, log);
    }

    /// <summary>
    /// The hand-off copy's last step (#270): it can't delete itself, so Windows deletes it, its <c>Pan-Uninstall</c>
    /// folder and then <c>%ProgramFiles%\Arcadia</c> at the next boot, in that order. Safe there: only administrators
    /// can plant anything in Program Files. A folder that isn't empty by then stays (Windows deletes only empty ones),
    /// so an Arcadia or Pan installed again before the reboot is kept.
    /// </summary>
    public static void ScheduleHandOffRemoval(string ownPath, Action<string> log)
    {
        foreach (var path in new[] { ownPath, PanPaths.HandOffDirectory, PanPaths.ArcadiaProgramFiles })
        {
            if (!Native.MoveFileExW(path, null, Native.MoveFileDelayUntilReboot))
            {
                log($"{path} could not be scheduled for deletion (error {Marshal.GetLastPInvokeError()}); the next --install or --uninstall removes it");
            }
        }

        log($"{PanPaths.HandOffDirectory} is deleted at the next boot");
    }

    private static void DeleteDataFolders(Action<string> log)
    {
        DeleteTree(PanPaths.DataDirectory, log);
        DeleteIfEmpty(Path.GetDirectoryName(PanPaths.DataDirectory)!, log);
    }

    /// <summary>The parent folder Pan created for itself goes when nothing else is in it; never anything inside.</summary>
    private static void DeleteIfEmpty(string directory, Action<string> log)
    {
        try
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory, recursive: false);
                log($"Deleted {directory}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"{directory} not deleted: {ex.Message}");
        }
    }

    private static void DeleteTree(string directory, Action<string> log)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
            log($"Deleted {directory}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Pan.exe itself may be the one running this: Windows deletes it at the next boot.
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                Native.MoveFileExW(file, null, Native.MoveFileDelayUntilReboot);
            }

            Native.MoveFileExW(directory, null, Native.MoveFileDelayUntilReboot);
            log($"{directory} is deleted at the next boot ({ex.Message})");
        }
    }

}
