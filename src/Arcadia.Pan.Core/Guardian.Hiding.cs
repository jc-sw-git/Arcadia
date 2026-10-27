namespace Arcadia.Pan;

/// <summary>
/// Part B of #243: hiding the real Steam Controller from every program but the client (HidHide), and showing it again
/// in every teardown. Every HidHide change is journaled before it is made and only ever adds or removes Pan's own
/// entries. HidHide is changed by one operation at a time (<see cref="_hidingGate"/>): the client's hide or unhide, a
/// device arrival, a teardown's show; its IOCTLs run inline (they don't wait on Windows' PnP lock), listing and
/// restarting the devices run with a timeout and may be left behind, as they change nothing Pan journals.
/// </summary>
public sealed partial class Guardian
{
    /// <summary>How long listing or restarting the Steam Controller's devices may take before Pan gives up on it.</summary>
    public static readonly TimeSpan DefaultDeviceStepTimeout = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _deviceStepTimeout;

    private readonly SemaphoreSlim _hidingGate = new(1, 1);

    /// <summary>The client's hide or unhide is running; under the lock. Its heartbeats wait behind it on the pipe.</summary>
    private int _hidingBusy;

    /// <summary>A device arrival is queued or running, so a burst of arrivals (one per HID collection) runs once.</summary>
    private int _arrivalQueued;

    /// <summary>
    /// A HID device of the puck arrived (Windows' notification, or a test). While Pan hides, the new instances are
    /// hidden too and the puck restarted, so no program keeps a handle it opened in between. Never throws.
    /// </summary>
    public async Task DevicesArrivedAsync()
    {
        lock (_lock)
        {
            if (_hiding is null || _journal.CloakingWasOn is null)
            {
                return;
            }
        }

        if (Interlocked.Exchange(ref _arrivalQueued, 1) == 1)
        {
            return;
        }

        try
        {
            await _hidingGate.WaitAsync();
            try
            {
                Interlocked.Exchange(ref _arrivalQueued, 0);
                bool hiding;
                lock (_lock)
                {
                    hiding = _journal.CloakingWasOn is not null;
                }

                if (hiding)
                {
                    // Every HID collection of the puck arrives on its own: only a burst that changed something is logged.
                    var (reason, _) = await HideCoreAsync(imagePath: null);
                    if (reason != PanReason.None)
                    {
                        _log($"Steam Controller device arrived while hiding, not hidden: {reason}");
                    }
                }
            }
            finally
            {
                _hidingGate.Release();
            }
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _arrivalQueued, 0);
            _log($"Hiding an arrived device failed: {ex}");
        }
    }

    private async Task<PanMessage> HideAsync(PanMessage message)
    {
        string? imagePath;
        lock (_lock)
        {
            if (RefuseHiding(message) is { } refused)
            {
                return refused;
            }

            imagePath = _client!.ImagePath;
            _hidingBusy++;
        }

        try
        {
            if (imagePath is null)
            {
                _log($"Hide refused: the client's image path is unknown, so it can't be allow-listed");
                return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.HidingFailed);
            }

            await _hidingGate.WaitAsync();
            try
            {
                lock (_lock)
                {
                    // A teardown may have begun while this waited: it shows the controller, so don't hide it again.
                    if (RefuseHiding(message) is { } refused)
                    {
                        return refused;
                    }
                }

                var (reason, count) = await HideCoreAsync(imagePath);
                _log(reason == PanReason.None
                    ? $"Steam Controller hidden for generation {message.Generation}: {count} instance(s)"
                    : $"Steam Controller not hidden for generation {message.Generation}: {reason}");
                return reason == PanReason.None
                    ? new PanMessage((byte)PanReply.Hidden, PanMessage.ProtocolVersion, message.Generation, (uint)count)
                    : PanMessage.Of(PanReply.Rejected, message.Generation, reason);
            }
            finally
            {
                _hidingGate.Release();
            }
        }
        catch (Exception ex)
        {
            _log($"Hide failed: {ex}");
            return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.HidingFailed);
        }
        finally
        {
            HidingDone();
        }
    }

    private async Task<PanMessage> UnhideAsync(PanMessage message)
    {
        lock (_lock)
        {
            if (RefuseHiding(message) is { } refused)
            {
                return refused;
            }

            _hidingBusy++;
        }

        try
        {
            await _hidingGate.WaitAsync();
            try
            {
                lock (_lock)
                {
                    if (RefuseHiding(message) is { } refused)
                    {
                        return refused;
                    }
                }

                // The client sends this once it removed its copy of the controller (#242), so nothing forwards to it.
                var reason = ShowCore();
                if (reason == PanReason.None && !await RestartIfPendingAsync(CancellationToken.None))
                {
                    reason = PanReason.HidingFailed;
                }

                _log(reason == PanReason.None
                    ? $"Steam Controller shown for generation {message.Generation}"
                    : $"Steam Controller not shown for generation {message.Generation}: {reason}; a teardown tries again");
                return reason == PanReason.None
                    ? PanMessage.Of(PanReply.Shown, message.Generation)
                    : PanMessage.Of(PanReply.Rejected, message.Generation, reason);
            }
            finally
            {
                _hidingGate.Release();
            }
        }
        catch (Exception ex)
        {
            _log($"Unhide failed: {ex}");
            return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.HidingFailed);
        }
        finally
        {
            HidingDone();
        }
    }

    private async Task<PanMessage> CheckHidingAsync(PanMessage message)
    {
        List<string> ours;
        lock (_lock)
        {
            if (RefuseHiding(message) is { } refused)
            {
                return refused;
            }

            ours = [.. _journal.HiddenDevices];
            // Behind an arrival's hide this may wait long; the client's heartbeats wait behind it on the pipe.
            _hidingBusy++;
        }

        // HidHide lets one program at a time open it: queue behind a running change instead of reading "busy".
        await _hidingGate.WaitAsync();
        try
        {
            var (reason, _, detail) = _hiding!.Check(ours);
            if (detail is not null)
            {
                _log($"Hiding check: {reason} ({detail})");
            }

            return PanMessage.Of(PanReply.HidingState, message.Generation, reason);
        }
        catch (Exception ex)
        {
            _log($"Hiding check failed: {ex}");
            return PanMessage.Of(PanReply.HidingState, message.Generation, PanReason.HidingFailed);
        }
        finally
        {
            _hidingGate.Release();
            HidingDone();
        }
    }

    /// <summary>Under the lock: why the client may not hide, unhide or check now, or null if it may.</summary>
    private PanMessage? RefuseHiding(PanMessage message)
    {
        if (_client is null || !_helloDone)
        {
            return PanMessage.Of(PanReply.Rejected, reason: PanReason.NotRegistered);
        }

        if (_stopping)
        {
            return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.ServiceStopping);
        }

        if (_hiding is null)
        {
            return PanMessage.Of(PanReply.Unsupported, message.Generation);
        }

        if (message.Command == PanCommand.CheckHiding)
        {
            return null;
        }

        // Hiding belongs to an emulation of the current generation, so every teardown of it undoes the hiding too.
        if (_phase == Phase.Idle)
        {
            return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.NotEmulating);
        }

        if (message.Generation != _generation)
        {
            return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.WrongGeneration);
        }

        return _phase == Phase.TornDown ? PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.TeardownNotAcknowledged) : null;
    }

    private void HidingDone()
    {
        lock (_lock)
        {
            _hidingBusy--;
            // The heartbeats queued behind the change come now; the time it took is no silence of the client's.
            _lastHeartbeatMs = _clock.UnbiasedMilliseconds;
        }
    }

    /// <summary>
    /// Inside the gate. Adds the listed devices that are connected and not hidden yet, the client's allow-list entry
    /// (unless <paramref name="imagePath"/> is null: an arrival) and turns cloaking on, each journaled first; then
    /// restarts the puck so programs that have it open lose it. Returns why not, and how many instances are hidden.
    /// </summary>
    private async Task<(PanReason Reason, int Count)> HideCoreAsync(string? imagePath)
    {
        // Listed first: it may wait on Windows' PnP lock, and HidHide's lists must be read right before they are written
        // back, so a change the user makes meanwhile isn't lost.
        IReadOnlyList<string> present;
        try
        {
            present = await Task.Run(_hiding!.Devices.PresentHidInstances).WaitAsync(_deviceStepTimeout);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log($"Steam Controller devices not listed: {ex.Message}");
            return (PanReason.HidingFailed, 0);
        }

        List<string> ours;
        lock (_lock)
        {
            ours = [.. _journal.HiddenDevices];
        }

        var (reason, state, detail) = _hiding.Check(ours);
        if (reason != PanReason.None)
        {
            _log($"Not hiding: {detail}");
            return (reason, 0);
        }

        // Only listed devices, also if the device list returned something else.
        var listed = present.Where(HidingList.IsListedHid).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var toHide = listed.Where(id => !SteamControllerHiding.Contains(state!.Hidden, id)).ToList();
        var allow = imagePath is not null && !SteamControllerHiding.Contains(state!.Allowed, imagePath) ? imagePath : null;
        var turnOn = !state!.Cloaking;
        if (toHide.Count == 0 && allow is null && !turnOn)
        {
            // Hidden already (a second hide, or the puck's own restart announcing it again).
            return (await RestartIfPendingAsync(CancellationToken.None) ? PanReason.None : PanReason.HidingFailed, listed.Count);
        }

        bool restart;
        lock (_lock)
        {
            var before = (Hidden: _journal.HiddenDevices.ToList(), Allowed: _journal.AllowListed.ToList(), _journal.CloakingWasOn, _journal.PuckRestartPending);
            _journal.HiddenDevices.AddRange(toHide);
            if (allow is not null)
            {
                _journal.AllowListed.Add(allow);
            }

            _journal.CloakingWasOn ??= state.Cloaking;
            _journal.PuckRestartPending |= toHide.Count > 0 || turnOn;
            restart = _journal.PuckRestartPending;
            if (!TrySaveJournal())
            {
                (_journal.HiddenDevices, _journal.AllowListed, _journal.CloakingWasOn, _journal.PuckRestartPending) = before;
                return (PanReason.HidingFailed, 0);
            }
        }

        try
        {
            // Allow-listed first, so the client never loses the controller it reads.
            if (allow is not null)
            {
                _hiding.HidHide.SetAllowedApplications([.. state.Allowed, allow]);
            }

            if (toHide.Count > 0)
            {
                _hiding.HidHide.SetHiddenDevices([.. state.Hidden, .. toHide]);
            }

            if (turnOn)
            {
                _hiding.HidHide.SetCloaking(true);
            }
        }
        catch (HidHideException ex)
        {
            // What was journaled is undone by the next show or teardown; removing an entry that never got in is harmless.
            _log($"HidHide not changed: {ex.Message}");
            return (ex.Reason, 0);
        }

        _log($"HidHide: {toHide.Count} device(s) hidden, {(allow is null ? "allow list unchanged" : "client allow-listed")}, cloaking {(turnOn ? "turned on" : "was on")}");
        if (restart && !await RestartIfPendingAsync(CancellationToken.None))
        {
            return (PanReason.HidingFailed, 0);
        }

        return (PanReason.None, listed.Count);
    }

    /// <summary>
    /// Inside the gate. Removes exactly the journaled entries from HidHide (the user's stay) and turns cloaking off if
    /// it was off before, each change journaled once made, with the restart journaled as pending first.
    /// </summary>
    private PanReason ShowCore()
    {
        JournalState journal;
        lock (_lock)
        {
            journal = _journal.Clone();
        }

        if (journal.HiddenDevices.Count == 0 && journal.AllowListed.Count == 0 && journal.CloakingWasOn is null)
        {
            return PanReason.None;
        }

        if (_hiding is null)
        {
            _log("HidHide entries are journaled, but this Pan has no access to HidHide");
            return PanReason.HidingFailed;
        }

        var hidHide = _hiding.HidHide;
        try
        {
            // Undone through the driver whatever its version reads as: the lists hide the puck as long as the driver runs.
            lock (_lock)
            {
                _journal.PuckRestartPending = true;
                SaveJournal();
            }

            if (journal.HiddenDevices.Count > 0)
            {
                var hidden = hidHide.GetHiddenDevices();
                var kept = SteamControllerHiding.Without(hidden, journal.HiddenDevices);
                if (kept.Count != hidden.Count)
                {
                    hidHide.SetHiddenDevices(kept);
                }

                lock (_lock)
                {
                    _journal.HiddenDevices = SteamControllerHiding.Without(_journal.HiddenDevices, journal.HiddenDevices);
                    SaveJournal();
                }
            }

            if (journal.AllowListed.Count > 0)
            {
                var allowed = hidHide.GetAllowedApplications();
                var kept = SteamControllerHiding.Without(allowed, journal.AllowListed);
                if (kept.Count != allowed.Count)
                {
                    hidHide.SetAllowedApplications(kept);
                }

                lock (_lock)
                {
                    _journal.AllowListed = SteamControllerHiding.Without(_journal.AllowListed, journal.AllowListed);
                    SaveJournal();
                }
            }

            if (journal.CloakingWasOn is { } wasOn)
            {
                if (!wasOn)
                {
                    hidHide.SetCloaking(false);
                }

                lock (_lock)
                {
                    _journal.CloakingWasOn = null;
                    SaveJournal();
                }
            }

            _log($"HidHide: {journal.HiddenDevices.Count} device(s) shown, {journal.AllowListed.Count} allow-list entr(ies) removed, cloaking {(journal.CloakingWasOn == false ? "turned off" : "left on")}");
            return PanReason.None;
        }
        catch (HidHideException ex) when (ex.Reason == PanReason.HidHideMissing && !hidHide.IsServiceRegistered())
        {
            // Uninstalled with its lists (no service, no control device): nothing of Pan's is hidden any more.
            _log("HidHide is gone: nothing of Pan's is hidden any more");
            ForgetHiding(restart: true);
            return PanReason.None;
        }
        catch (HidHideException ex)
        {
            _log($"HidHide not restored: {ex.Message}; the rest stays journaled");
            return ex.Reason;
        }
    }

    private void ForgetHiding(bool restart)
    {
        lock (_lock)
        {
            _journal.HiddenDevices = [];
            _journal.AllowListed = [];
            _journal.CloakingWasOn = null;
            _journal.PuckRestartPending |= restart;
            SaveJournal();
        }
    }

    /// <summary>Inside the gate: restarts the puck if the journal says so, with a timeout; true when nothing is left pending.</summary>
    private async Task<bool> RestartIfPendingAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (!_journal.PuckRestartPending)
            {
                return true;
            }
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_deviceStepTimeout);
        try
        {
            await Task.Run(() => _hiding!.Devices.RestartAsync(timeout.Token), CancellationToken.None).WaitAsync(_deviceStepTimeout, cancellationToken);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Left behind after a timeout, the restart changes nothing journaled; the next show or teardown restarts again.
            // The timeout shows either as WaitAsync's or as the restart seeing its token cancelled, whichever came first.
            var timedOut = ex is TimeoutException || timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
            await timeout.CancelAsync();
            _log($"Steam Controller not restarted: {(timedOut ? $"no answer within {_deviceStepTimeout.TotalSeconds:0} s" : ex.Message)}");
            return false;
        }

        lock (_lock)
        {
            _journal.PuckRestartPending = false;
            SaveJournal();
        }

        _log("Steam Controller restarted");
        return true;
    }

    /// <summary>
    /// The teardown's steps 3 and 4: show the Steam Controller and restart it, only once nothing forwards to it any
    /// more (<paramref name="forwardingGone"/>: the usbip ports are detached and the held processes ended), so no
    /// firmware path through the copy can exist while it is visible. Otherwise it stays hidden for the next trigger.
    /// </summary>
    /// <param name="retry">A retry outside a teardown: it gives up as soon as an emulation runs, whose own teardown shows the controller.</param>
    private async Task<IReadOnlyList<StepResult>> ShowAfterTeardownAsync(bool forwardingGone, bool retry = false)
    {
        bool work;
        lock (_lock)
        {
            work = _journal.HidingHasWork;
        }

        if (!work)
        {
            return [];
        }

        if (!forwardingGone)
        {
            var skipped = new StepResult("show the Steam Controller", StepOutcome.Failed, "skipped: the forwarding may still run; it stays hidden until the next teardown");
            _log($"Teardown: {skipped}");
            return [skipped];
        }

        return await TeardownRunner.RunAsync(
        [
            // The show may first wait for a running hide (its listing and restart are bounded by the device timeout).
            new TeardownStep("show the Steam Controller", token => ShowStepAsync(token, retry), TeardownPlan.StepTimeout + 2 * _deviceStepTimeout),
            new TeardownStep("restart the Steam Controller", token => RestartStepAsync(token, retry), TeardownPlan.StepTimeout + _deviceStepTimeout),
        ], _log);
    }

    private async Task ShowStepAsync(CancellationToken cancellationToken, bool retry)
    {
        // A cancelled wait (step timed out behind a stuck hide) changes nothing.
        await _hidingGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfRetryIsMoot(retry);
            var reason = ShowCore();
            if (reason != PanReason.None)
            {
                throw new InvalidOperationException($"HidHide not restored ({reason})");
            }
        }
        finally
        {
            _hidingGate.Release();
        }
    }

    private async Task RestartStepAsync(CancellationToken cancellationToken, bool retry)
    {
        await _hidingGate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfRetryIsMoot(retry);
            if (!await RestartIfPendingAsync(cancellationToken))
            {
                throw new InvalidOperationException("the Steam Controller was not restarted");
            }
        }
        finally
        {
            _hidingGate.Release();
        }
    }

    /// <summary>
    /// When a teardown could show the controller only partly (HidHide busy, its driver restarting, a restart that failed)
    /// after the forwarding was gone, Pan tries again after these pauses (the last one repeating) while nothing emulates,
    /// instead of leaving the puck hidden until the next trigger, which after a crash may be hours away.
    /// </summary>
    public IReadOnlyList<TimeSpan> ShowRetryDelays { get; init; } = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];

    /// <summary>The retry running, under the lock; cancelled by a stop, which tears down itself.</summary>
    private CancellationTokenSource? _showRetry;

    /// <summary>Under the lock.</summary>
    private void ScheduleShowRetry()
    {
        if (_showRetry is not null || _stopping || ShowRetryDelays.Count == 0)
        {
            return;
        }

        _showRetry = new CancellationTokenSource();
        ShowRetry = RetryShowAsync(_showRetry.Token);
    }

    /// <summary>The retry of a partial show, for tests to wait on; never faults.</summary>
    public Task? ShowRetry { get; private set; }

    /// <summary>A retry still has to show the Steam Controller: Pan stays up for it without a client (<see cref="IdleStop"/>).</summary>
    public bool ShowRetryPending
    {
        get
        {
            lock (_lock)
            {
                return _showRetry is not null;
            }
        }
    }

    /// <summary>
    /// Under the lock: a retry is changing HidHide or restarting the puck. A new emulation waits for it, because the
    /// retry checked that nothing emulates only before it started (review of #252, P2 2).
    /// </summary>
    private bool _retryShowing;

    private async Task RetryShowAsync(CancellationToken cancellationToken)
    {
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                var pause = ShowRetryDelays[Math.Min(attempt, ShowRetryDelays.Count - 1)];
                await Task.Delay(pause, cancellationToken);
                lock (_lock)
                {
                    if (!_journal.HidingHasWork || _phase == Phase.Emulating || _stopping)
                    {
                        // Done elsewhere, or an emulation runs: its teardown shows the controller.
                        return;
                    }

                    _retryShowing = true;
                }

                IReadOnlyList<StepResult> results;
                try
                {
                    results = await ShowAfterTeardownAsync(forwardingGone: true, retry: true);
                }
                finally
                {
                    lock (_lock)
                    {
                        _retryShowing = false;
                    }
                }

                if (results.All(r => r.Outcome == StepOutcome.Ok))
                {
                    lock (_lock)
                    {
                        if (_phase != Phase.Emulating && _journal.HeldProcesses.Count == 0)
                        {
                            // The teardown that left this is complete now.
                            _journal.Emulating = false;
                            SaveJournal();
                        }
                    }

                    _log($"Steam Controller shown on retry {attempt + 1}");
                    return;
                }

                lock (_lock)
                {
                    if (!_journal.HidHideChanged && attempt + 1 >= ShowRetryDelays.Count)
                    {
                        // Shown; only the restart fails, which Windows does by replug: no use repeating it every few minutes.
                        _log("Steam Controller shown, its restart needs a replug; tried again at the next trigger");
                        return;
                    }
                }

                _log($"Steam Controller still not shown on retry {attempt + 1}; trying again in {ShowRetryDelays[Math.Min(attempt + 1, ShowRetryDelays.Count - 1)].TotalSeconds:0} s");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log($"Retrying the show failed: {ex}");
        }
        finally
        {
            lock (_lock)
            {
                _showRetry?.Dispose();
                _showRetry = null;
            }
        }
    }

    private void ThrowIfRetryIsMoot(bool retry)
    {
        lock (_lock)
        {
            if (retry && (_phase == Phase.Emulating || _stopping))
            {
                throw new InvalidOperationException("an emulation started; its teardown shows the controller");
            }
        }
    }
}
