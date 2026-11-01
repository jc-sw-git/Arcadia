namespace Arcadia.Pan;

/// <summary>Pan's data folder at <c>--uninstall</c>: checked as its own, absent (Pan never ran here), or someone else's.</summary>
public enum DataFolder
{
    Ours,
    Missing,
    NotOurs,
}

public enum UninstallOutcome
{
    Done,

    /// <summary>The journal still lists something: Pan.exe, the journal and the drivers stay for a later <c>--restore</c>.</summary>
    RestoreIncomplete,

    /// <summary>Pan went, a driver stayed; the data folder keeps the log that says why.</summary>
    DriversLeft,
}

/// <summary>
/// The order of <c>--uninstall [--remove-drivers]</c> after the service is gone (#243 part B2, overall review):
/// 1. undo what the journal lists; 2. remove the stale device entries Arcadia's emulated devices left (#347), once
/// nothing is forwarded any more and while usbip-win2's host still tells them apart; 3. the drivers, only once that undo
/// is confirmed, as removing HidHide shows the real Steam Controller, which must wait until no copy of it is forwarded;
/// 4. the data folder, with file logging stopped first, as a deleted folder can be recreated by anyone and an elevated
/// log write would follow it.
/// </summary>
public static class UninstallOrder
{
    /// <param name="removeStaleDevices">Never throws. Skipped when the restore is incomplete or the data folder isn't Pan's:
    /// then Pan can't confirm that nothing of Arcadia's is still plugged in.</param>
    public static async Task<UninstallOutcome> RunAsync(
        DataFolder data, Func<Task<bool>> restore, Func<Task<bool>>? removeDrivers, Action stopFileLog, Action deleteData, Action<string> log,
        Func<Task>? removeStaleDevices = null)
    {
        if (data == DataFolder.Ours && !await restore())
        {
            log("Uninstall incomplete: the journal and Pan.exe stay for a later `Pan.exe --restore`; the drivers stay too");
            return UninstallOutcome.RestoreIncomplete;
        }

        if (data != DataFolder.NotOurs && removeStaleDevices is not null)
        {
            await removeStaleDevices();
        }

        if (removeDrivers is not null)
        {
            if (data == DataFolder.NotOurs)
            {
                log("Drivers kept: without a journal Pan can trust, it can't confirm that no copy of the Steam Controller is forwarded");
                return UninstallOutcome.DriversLeft;
            }

            if (!await removeDrivers())
            {
                log("Not every driver was removed; Pan's data folder stays with this log");
                return UninstallOutcome.DriversLeft;
            }
        }

        if (data == DataFolder.Ours)
        {
            stopFileLog();
            deleteData();
        }

        return UninstallOutcome.Done;
    }
}
