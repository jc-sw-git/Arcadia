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

    /// <summary>The journal still lists something: the service stays and tries again at every Windows start.</summary>
    RestoreIncomplete,

    /// <summary>A driver stayed: the service stays and tries again at the next Windows start.</summary>
    DriversPending,

    /// <summary>Pan went, a driver stayed for good (no trusted data folder, or the attempts ran out); the log says why.</summary>
    DriversLeft,

    /// <summary><c>--install</c> ran meanwhile: the player wants Arcadia again, so nothing more is removed.</summary>
    Cancelled,
}

/// <summary>The steps of an uninstall, run by <see cref="UninstallOrder"/>; each may log, none may throw but the service's.</summary>
public sealed class UninstallSteps
{
    /// <summary>Stops the installed service (its stop is the teardown); nothing to do when the service itself uninstalls.</summary>
    public required Action StopService { get; init; }

    /// <summary>Undoes what the journal lists; true when nothing is left.</summary>
    public required Func<Task<bool>> Restore { get; init; }

    /// <summary>Removes the stale device entries of Arcadia's emulated devices (#347); null when done already.</summary>
    public Func<Task>? RemoveStaleDevices { get; init; }

    /// <summary>Uninstalls HidHide and usbip-win2; true when none is left.</summary>
    public required Func<Task<bool>> RemoveDrivers { get; init; }

    public required Action RemoveService { get; init; }

    public required Action StopFileLog { get; init; }

    public required Action DeleteData { get; init; }

    public required Action<string> Log { get; init; }
}

/// <summary>
/// The order of an uninstall (#243 part B2, #422): 0. the pending uninstall is recorded, so a crash, a power loss or a
/// step that fails is finished by the service at a later start; 1. the service stops; 2. what the journal lists is
/// undone; 3. the stale device entries go (#347), once nothing is forwarded and while usbip-win2's host still tells
/// them apart; 4. the drivers, only once that undo is confirmed, as removing HidHide shows the real Steam Controller;
/// 5. the service goes, only now, so it is still there to retry 2 and 4; 6. the data folder with the marker, with file
/// logging stopped first, as a deleted folder can be recreated by anyone and an elevated log write would follow it.
/// </summary>
public static class UninstallOrder
{
    /// <summary>Driver removals before Pan gives up on them: at the uninstall and at three Windows starts after it.</summary>
    public const int MaxDriverAttempts = 4;

    /// <param name="marker">Where <paramref name="pending"/> is kept; only used when <paramref name="data"/> is ours.</param>
    public static async Task<UninstallOutcome> RunAsync(DataFolder data, PendingUninstall pending, IPendingUninstallStore marker, UninstallSteps steps)
    {
        var log = steps.Log;
        var ours = data == DataFolder.Ours;
        if (ours)
        {
            marker.Save(pending);
        }

        steps.StopService();
        if (ours && !await steps.Restore())
        {
            log("Uninstall incomplete: what the journal lists stays; Pan tries again at every Windows start, the drivers stay until then");
            return UninstallOutcome.RestoreIncomplete;
        }

        if (data != DataFolder.NotOurs && steps.RemoveStaleDevices is { } removeStaleDevices)
        {
            await removeStaleDevices();
        }

        var driversLeft = false;
        if (pending.RemoveDrivers)
        {
            if (data == DataFolder.NotOurs)
            {
                log("Drivers kept: without a journal Pan can trust, it can't confirm that no copy of the Steam Controller is forwarded");
                driversLeft = true;
            }
            else if (Cancelled(ours, marker, log))
            {
                return UninstallOutcome.Cancelled;
            }
            else if (pending.DriverAttempts >= MaxDriverAttempts)
            {
                log($"Drivers kept: they could not be removed in {MaxDriverAttempts} attempts; remove them in Windows' Apps");
                driversLeft = true;
            }
            else
            {
                pending = pending with { DriverAttempts = pending.DriverAttempts + 1 };
                if (ours)
                {
                    // Counted before the uninstallers run, so one that crashes Pan counts too.
                    marker.Save(pending);
                }

                if (!await steps.RemoveDrivers())
                {
                    if (ours && pending.DriverAttempts < MaxDriverAttempts)
                    {
                        log($"Not every driver was removed (attempt {pending.DriverAttempts} of {MaxDriverAttempts}): Pan stays and tries again at the next Windows start");
                        return UninstallOutcome.DriversPending;
                    }

                    log("Not every driver was removed; Pan's data folder stays with this log");
                    driversLeft = true;
                }
            }
        }

        if (Cancelled(ours, marker, log))
        {
            return UninstallOutcome.Cancelled;
        }

        steps.RemoveService();
        if (driversLeft)
        {
            if (ours)
            {
                marker.Delete();
            }

            return UninstallOutcome.DriversLeft;
        }

        if (ours)
        {
            steps.StopFileLog();
            steps.DeleteData();
        }

        return UninstallOutcome.Done;
    }

    /// <summary>The marker is gone: a new <c>--install</c> deleted it while this uninstall ran.</summary>
    private static bool Cancelled(bool ours, IPendingUninstallStore marker, Action<string> log)
    {
        if (!ours || marker.Load() is not null)
        {
            return false;
        }

        log("Uninstall cancelled: Pan was installed again meanwhile");
        return true;
    }
}
