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
    /// How long one cleanup may run before it stops between two entries; the rest goes next time. 132 entries took
    /// 1.8 s on the owner's PC. A new emulation waits for it with "Pan räumt noch auf", which the client counts as no
    /// pad loss (<c>PanClient.BeingShownWait</c>).
    /// </summary>
    public TimeSpan StaleCleanupBudget { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a cleanup after an emulation waits before it lists: Windows unplugs the detached devices a moment after
    /// usbip's detach returns, and an entry still present then is skipped until the next cleanup (owner's PC: a hard end
    /// whose teardown took 0.1 s found nothing). A new emulation meanwhile starts at once and the cleanup is dropped.
    /// </summary>
    public TimeSpan StaleCleanupSettle { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long a stop waits for a removal still running: listing and removing may wait on Windows' PnP lock (a driver
    /// install, shutdown), and the service stop must not hang on it. Left running, it changes nothing journaled.
    /// </summary>
    public TimeSpan StaleCleanupStopWait { get; init; } = TimeSpan.FromSeconds(10);

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
            ScheduleStaleCleanup(trigger, settle: false);
        }
    }

    /// <summary>Under the lock. <paramref name="settle"/>: after an emulation, whose devices Windows is still unplugging.</summary>
    private void ScheduleStaleCleanup(string trigger, bool settle = true)
    {
        if (StaleDevices is null || !StaleCleanupMayRun() || _staleCleanup is { IsCompleted: false })
        {
            return;
        }

        _staleCleanupYield = false;
        _staleCleanup = RunStaleCleanupAsync(StaleDevices, trigger, settle ? StaleCleanupSettle : TimeSpan.Zero);
    }

    /// <summary>
    /// Under the lock. Only while nothing of Arcadia's is plugged in (<c>CM_Uninstall_DevNode</c> removes present
    /// devnodes too) and the journal has nothing left to undo: no forwarding process or usbip port, no controller still
    /// hidden or waiting for its restart. Never while Pan stops.
    /// </summary>
    private bool StaleCleanupMayRun() => !_stopping && _phase != Phase.Emulating && !_journal.HasWork;

    /// <summary>Under the lock: a new emulation waits for the cleanup, which then stops after the entry it is on.</summary>
    private bool _staleCleanupYield;

    /// <summary>Under the lock: the cleanup lists or removes now (not while it waits to settle or checks for a bootloader).</summary>
    private bool _staleCleanupRemoving;

    /// <summary>Cancelled by a stop: a cleanup still waiting to settle ends at once.</summary>
    private readonly CancellationTokenSource _staleCleanupStop = new();

    /// <summary>
    /// Under the lock: whether a new emulation must wait for a cleanup, which then stops at the next entry. One that hasn't
    /// started removing yet is dropped instead, and the emulation starts at once.
    /// </summary>
    private bool StaleCleanupRunning()
    {
        if (_staleCleanup is { IsCompleted: false })
        {
            _staleCleanupYield = true;
            return _staleCleanupRemoving;
        }

        return false;
    }

    private async Task RunStaleCleanupAsync(IStaleDeviceStore store, string trigger, TimeSpan settle)
    {
        // Off the caller: the pipe reader, the teardown and the SCM must not wait on the PnP lock.
        await Task.Yield();
        try
        {
            if (settle > TimeSpan.Zero)
            {
                await Task.Delay(settle, _staleCleanupStop.Token);
            }

            if (_hiding is not null && await FlashingAsync() != PanReason.None)
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

            lock (_lock)
            {
                if (!StaleCleanupMayRun() || _staleCleanupYield)
                {
                    // An emulation started meanwhile: its end cleans up.
                    return;
                }

                _staleCleanupRemoving = true;
            }

            var result = await Task.Run(() => StaleDeviceCleanup.Run(store, MayContinue, StaleCleanupBudget, _log));
            if (result.Found > 0)
            {
                _log($"Stale device entries ({trigger}): {result.Found} found, {result.Removed} removed, {result.Failed} failed, {result.Kept} kept"
                    + (result.Stopped ? ", stopped; the rest goes next time" : string.Empty));
            }
        }
        catch (OperationCanceledException) when (_staleCleanupStop.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _log($"Removing stale device entries failed: {ex}");
        }
        finally
        {
            lock (_lock)
            {
                _staleCleanupRemoving = false;
            }
        }
    }

    /// <summary>For <see cref="StopAsync"/>: ends a cleanup waiting to settle and waits, bounded, for a removal still running.</summary>
    private async Task StopStaleCleanupAsync(Task? cleanup)
    {
        await _staleCleanupStop.CancelAsync();
        if (cleanup is null)
        {
            return;
        }

        try
        {
            // Stops after the entry it is on (_stopping): a removal can't be cancelled.
            await cleanup.WaitAsync(StaleCleanupStopWait);
        }
        catch (TimeoutException)
        {
            _log($"Removing stale device entries still waits on Windows after {StaleCleanupStopWait.TotalSeconds:0} s; stopping without it");
        }
    }
}
