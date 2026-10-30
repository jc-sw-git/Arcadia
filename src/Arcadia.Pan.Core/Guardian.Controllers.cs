namespace Arcadia.Pan;

/// <summary>
/// #335: hiding every other controller the client takes over, one model at a time, with the puck's guarantees: each
/// change journaled first, only Pan's own entries removed, arrivals hidden without polling, every teardown shows them.
/// Unlike the puck, an XInput interface can't be restarted in place (Windows wants a reconnect, #335 A3): it is hidden
/// at once for every program that opens it later, and the client is told that the rest waits for the reconnect.
/// </summary>
public sealed partial class Guardian
{
    /// <summary>The models the client took over in the current emulation; under the lock. A teardown forgets them.</summary>
    private readonly HashSet<ControllerModel> _takenOver = [];

    /// <summary>Models whose arrival is queued or running, so a burst of arrivals (one per interface) runs once; under the lock.</summary>
    private readonly HashSet<ControllerModel> _arrivalsQueued = [];

    /// <summary>
    /// Whether a process of this name (without <c>.exe</c>) runs in any session, for the vendors' firmware tools
    /// (#238 D8); the service passes the real one.
    /// </summary>
    public Func<string, bool> ProcessRuns { get; init; } = _ => false;

    private async Task<PanMessage> HideControllerAsync(PanMessage message)
    {
        var model = ControllerModel.From(message.Value);
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
            if (_hiding!.Controllers is not { } controllers)
            {
                return PanMessage.Of(PanReply.Unsupported, message.Generation);
            }

            if (!ControllerList.MayTakeOver(model))
            {
                _log($"Controller {model} not hidden: Pan doesn't take over this model");
                return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.ControllerNotHideable);
            }

            if (imagePath is null)
            {
                _log($"Controller {model} not hidden: the client's image path is unknown, so it can't be allow-listed");
                return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.HidingFailed);
            }

            if (RunningFirmwareTool(model) is { } tool)
            {
                _log($"Controller {model} not hidden: {tool.Name} runs");
                return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.FirmwareToolRuns);
            }

            await _hidingGate.WaitAsync();
            try
            {
                List<string> hiddenBefore;
                lock (_lock)
                {
                    // A teardown may have begun while this waited: it shows the controllers, so don't hide one again.
                    if (RefuseHiding(message) is { } refused)
                    {
                        return refused;
                    }

                    hiddenBefore = [.. _journal.HiddenControllers];
                }

                var (scanned, scan) = await ScanAsync(controllers, model);
                if (scanned != PanReason.None)
                {
                    return PanMessage.Of(PanReply.Rejected, message.Generation, scanned);
                }

                var instances = scan!.Hideable.Select(instance => instance.InstanceId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var hid = instances.Where(ControllerList.IsHid).ToList();
                var reason = AddToHidHide(instances, imagePath, controllerRestarts: hid);
                if (reason != PanReason.None)
                {
                    _log($"Controller {model} not hidden for generation {message.Generation}: {reason}");
                    return PanMessage.Of(PanReply.Rejected, message.Generation, reason);
                }

                lock (_lock)
                {
                    _takenOver.Add(model);
                }

                // The client let go of the model before it asked, so the restart isn't refused for Arcadia's own handle.
                var replug = await RestartControllersAsync(id => ControllerList.IsOfModel(id, model), CancellationToken.None);
                if (replug is null)
                {
                    return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.HidingFailed);
                }

                var xinput = scan.Hideable.Count(instance => instance.Interface == ControllerInterface.XInput && !SteamControllerHiding.Contains(hiddenBefore, instance.InstanceId));
                var needsReplug = xinput > 0 || replug.Count > 0;
                _log($"Controller {model} hidden for generation {message.Generation}: {instances.Count} instance(s)"
                    + (needsReplug ? $"; {xinput + replug.Count} device(s) are only fully taken over once reconnected" : string.Empty));
                return PanMessage.Of(PanReply.ControllerHidden, message.Generation, needsReplug ? PanReason.ControllerNeedsReplug : PanReason.None);
            }
            finally
            {
                _hidingGate.Release();
            }
        }
        catch (Exception ex)
        {
            _log($"Hiding controller {model} failed: {ex}");
            return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.HidingFailed);
        }
        finally
        {
            HidingDone();
        }
    }

    private async Task<PanMessage> ShowControllerAsync(PanMessage message)
    {
        var model = ControllerModel.From(message.Value);
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
                bool xinput;
                lock (_lock)
                {
                    if (RefuseHiding(message) is { } refused)
                    {
                        return refused;
                    }

                    // From now on an arrival of the model is left alone.
                    _takenOver.Remove(model);
                    xinput = _journal.HiddenControllers.Any(id => !ControllerList.IsHid(id) && ControllerList.IsOfModel(id, model));
                }

                var reason = ShowCore(ShowScope.Model(model));
                var replug = reason == PanReason.None ? await RestartControllersAsync(id => ControllerList.IsOfModel(id, model), CancellationToken.None) : null;
                if (replug is null)
                {
                    // Left journaled: the teardown shows and restarts it, with its retries.
                    _log($"Controller {model} not shown for generation {message.Generation}: {(reason == PanReason.None ? "its restart failed" : reason)}; a teardown tries again");
                    return PanMessage.Of(PanReply.Rejected, message.Generation, reason == PanReason.None ? PanReason.HidingFailed : reason);
                }

                var needsReplug = xinput || replug.Count > 0;
                _log($"Controller {model} shown for generation {message.Generation}{(needsReplug ? "; running programs see it again once it is reconnected" : string.Empty)}");
                return PanMessage.Of(PanReply.ControllerShown, message.Generation, needsReplug ? PanReason.ControllerNeedsReplug : PanReason.None);
            }
            finally
            {
                _hidingGate.Release();
            }
        }
        catch (Exception ex)
        {
            _log($"Showing controller {model} failed: {ex}");
            return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.HidingFailed);
        }
        finally
        {
            HidingDone();
        }
    }

    /// <summary>
    /// A device of <paramref name="model"/> arrived (Windows' notification, or a test). While the client has the model
    /// taken over, its new instances are hidden at once. No restart: Arcadia, allow-listed, opens the controller on the
    /// same notification and would make Windows refuse it; programs that aren't allow-listed are already blind to it.
    /// Never throws.
    /// </summary>
    public async Task ControllerArrivedAsync(ControllerModel model)
    {
        lock (_lock)
        {
            if (_hiding?.Controllers is null || !_takenOver.Contains(model) || !_arrivalsQueued.Add(model))
            {
                return;
            }
        }

        try
        {
            await _hidingGate.WaitAsync();
            try
            {
                lock (_lock)
                {
                    _arrivalsQueued.Remove(model);
                    if (!_takenOver.Contains(model))
                    {
                        return;
                    }
                }

                if (RunningFirmwareTool(model) is { } tool)
                {
                    _log($"Controller {model} arrived while {tool.Name} runs: not hidden");
                    return;
                }

                var (scanned, scan) = await ScanAsync(_hiding.Controllers, model);
                var reason = scanned != PanReason.None
                    ? scanned
                    : AddToHidHide(scan!.Hideable.Select(instance => instance.InstanceId).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), imagePath: null, controllerRestarts: []);
                if (reason != PanReason.None)
                {
                    _log($"Controller {model} arrived while hiding, not hidden: {reason}");
                }
            }
            finally
            {
                _hidingGate.Release();
            }
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _arrivalsQueued.Remove(model);
            }

            _log($"Hiding an arrived controller failed: {ex}");
        }
    }

    /// <summary>Under the lock: the client's models are no longer taken over (a teardown, or the emulation stopped).</summary>
    private void ForgetTakenOver() => _takenOver.Clear();

    private ControllerList.FirmwareTool? RunningFirmwareTool(ControllerModel model)
    {
        try
        {
            return ControllerList.RunningToolFor(model, ProcessRuns);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Unknown counts as not running: a process list that can't be read would otherwise stop all hiding.
            _log($"Whether a firmware tool runs not known: {ex.Message}");
            return null;
        }
    }

    /// <summary>Inside the gate: the model's devices, with a timeout; why not, if they can't be hidden.</summary>
    private async Task<(PanReason Reason, ControllerScan? Scan)> ScanAsync(IControllerDevices controllers, ControllerModel model)
    {
        ControllerScan scan;
        try
        {
            scan = await Task.Run(() => controllers.Scan(model)).WaitAsync(_deviceStepTimeout);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log($"Controller {model}: devices not listed: {ex.Message}");
            return (PanReason.HidingFailed, null);
        }

        if (!scan.Connected)
        {
            _log($"Controller {model} not hidden: not connected");
            return (PanReason.ControllerNotConnected, null);
        }

        // Only the model's own devices, also if the scan returned something else.
        var hideable = scan.Hideable.Where(instance => ControllerList.IsControllerInstance(instance.InstanceId) && ControllerList.IsOfModel(instance.InstanceId, model)
            && ControllerList.IsHid(instance.InstanceId) == (instance.Interface == ControllerInterface.Hid)).ToList();
        if (hideable.Count == 0)
        {
            _log($"Controller {model} not hidden: none of its devices can be hidden");
            return (PanReason.ControllerNotHideable, null);
        }

        return (PanReason.None, scan with { Hideable = hideable });
    }

    /// <summary>
    /// Inside the gate: restarts the journaled controller instances that <paramref name="which"/> picks, with a timeout.
    /// Null when that failed (they stay pending for the next show or teardown); else the instances that wait for a
    /// reconnect, which aren't tried again.
    /// </summary>
    private async Task<IReadOnlyList<string>?> RestartControllersAsync(Func<string, bool> which, CancellationToken cancellationToken)
    {
        List<string> pending;
        lock (_lock)
        {
            pending = _journal.ControllerRestarts.Where(which).ToList();
        }

        if (pending.Count == 0)
        {
            return [];
        }

        if (_hiding?.Controllers is not { } controllers)
        {
            _log("Controller restarts are journaled, but this Pan has no access to the devices");
            return null;
        }

        IReadOnlyList<string> replug;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_deviceStepTimeout);
        try
        {
            replug = await Task.Run(() => controllers.RestartAsync(pending, timeout.Token), CancellationToken.None).WaitAsync(_deviceStepTimeout, cancellationToken);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var timedOut = ex is TimeoutException || timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested;
            await timeout.CancelAsync();
            _log($"Controllers not restarted: {(timedOut ? $"no answer within {_deviceStepTimeout.TotalSeconds:0} s" : ex.Message)}");
            return null;
        }

        lock (_lock)
        {
            _journal.ControllerRestarts = SteamControllerHiding.Without(_journal.ControllerRestarts, pending);
            SaveJournal();
        }

        _log(replug.Count == 0
            ? $"{pending.Count} controller device(s) restarted"
            : $"{pending.Count - replug.Count} controller device(s) restarted, {replug.Count} restart once reconnected");
        return replug;
    }
}
