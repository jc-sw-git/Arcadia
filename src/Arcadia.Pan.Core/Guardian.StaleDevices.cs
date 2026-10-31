namespace Arcadia.Pan;

/// <summary>
/// Removing the stale device entries Arcadia's emulated devices leave in Windows (#347): every plug through usbip-win2
/// creates new instance ids, and the unplugged ones stay as entries. Pan removes them when nothing of Arcadia's is
/// plugged in and nothing is left to undo: after an emulation ended and every real controller is shown again, and once
/// after Pan starts (after its journal recovery). Silent: one log line with the counts.
/// </summary>
public sealed partial class Guardian
{
    /// <summary>
    /// How long one cleanup may run before it stops between two entries; the rest goes next time. 136 entries took
    /// 1.8 s on the owner's PC. A new emulation waits for it with "Pan räumt noch auf", which the client counts as no
    /// pad loss (<c>PanClient.BeingShownWait</c>).
    /// </summary>
    public TimeSpan StaleCleanupBudget { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Windows' device entries; without it Pan doesn't clean up.</summary>
    public IStaleDeviceStore? StaleDevices { get; init; }

    /// <summary>Under the lock: the cleanup running or last run, for a new emulation to wait on and for tests.</summary>
    private Task? _staleCleanup;

    /// <summary>The cleanup running or last run; never faults.</summary>
    public Task? StaleCleanup
    {
        get { lock (_lock) return _staleCleanup; }
    }

    /// <summary>
    /// Starts a cleanup when it may run now, without waiting for it: at Pan's start right after the journal recovery, so
    /// Arcadia's connect isn't delayed (it gives up after 2 s). The cleanup never faults; <see cref="StopAsync"/> waits for
    /// it, <see cref="StaleCleanup"/> is it.
    /// </summary>
    public void CleanUpStaleDevices(string trigger)
    {
        lock (_lock)
        {
            ScheduleStaleCleanup(trigger);
        }
    }

    /// <summary>Under the lock.</summary>
    private void ScheduleStaleCleanup(string trigger)
    {
        if (StaleDevices is null || !StaleCleanupMayRun() || _staleCleanup is { IsCompleted: false })
        {
            return;
        }

        _staleCleanupYield = false;
        _staleCleanup = RunStaleCleanupAsync(StaleDevices, trigger);
    }

    /// <summary>
    /// Under the lock. Only while nothing of Arcadia's is plugged in (<c>CM_Uninstall_DevNode</c> removes present
    /// devnodes too) and the journal has nothing left to undo: no forwarding process or usbip port, no controller still
    /// hidden or waiting for its restart. Never while Pan stops.
    /// </summary>
    private bool StaleCleanupMayRun() => !_stopping && _phase != Phase.Emulating && !_journal.HasWork;

    /// <summary>Under the lock: a new emulation waits for the cleanup, which then stops after the entry it is on.</summary>
    private bool _staleCleanupYield;

    /// <summary>Under the lock: whether a new emulation must wait for a cleanup; asks it to stop at the next entry.</summary>
    private bool StaleCleanupRunning()
    {
        if (_staleCleanup is { IsCompleted: false })
        {
            _staleCleanupYield = true;
            return true;
        }

        return false;
    }

    private async Task RunStaleCleanupAsync(IStaleDeviceStore store, string trigger)
    {
        // Off the caller: the pipe reader, the teardown and the SCM must not wait on the PnP lock.
        await Task.Yield();
        try
        {
            if (_hiding is not null && await FlashingAsync())
            {
                // Steam flashes a controller (#295): no PnP work beside it.
                _log($"Stale device entries not removed ({trigger}): a Valve bootloader is connected");
                return;
            }

            bool MayContinue()
            {
                lock (_lock)
                {
                    return StaleCleanupMayRun() && !_staleCleanupYield;
                }
            }

            var result = await Task.Run(() => StaleDeviceCleanup.Run(store, MayContinue, StaleCleanupBudget, _log));
            if (result.Found > 0)
            {
                _log($"Stale device entries ({trigger}): {result.Found} found, {result.Removed} removed, {result.Failed} failed, {result.Kept} kept"
                    + (result.Stopped ? ", stopped; the rest goes next time" : string.Empty));
            }
        }
        catch (Exception ex)
        {
            _log($"Removing stale device entries failed: {ex}");
        }
    }
}
