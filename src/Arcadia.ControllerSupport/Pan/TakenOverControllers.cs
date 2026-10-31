using Arcadia.Input;
using Arcadia.Launchers;
using Arcadia.Pan;

namespace Arcadia.ControllerSupport.Pan;

/// <summary>Why a connected controller reaches games directly (#238 D7): Pan couldn't hide it, so it isn't emulated either.</summary>
public sealed record ControllerNotHidden(string Name, ControllerModel Model, PanReason Reason);

/// <summary>A vendor's firmware tool runs, so Pan waits with this controller (#238 D8).</summary>
public sealed record ControllerWaitingForTool(string Name, ControllerModel Model);

/// <summary>
/// Every controller but the Steam Controller, for games (#238): within Pan's emulation, Pan hides each model Arcadia
/// reads (#335), and Arcadia plugs one virtual pad per controller, of the controller's own kind where VIIPER has it
/// (<see cref="PadKinds.Default"/>), in the order they connected, and feeds it what the controller does, without
/// Arcadia's combos, at rest while Arcadia is in front or the mouse mode is on. A game's rumble and light bar go back to
/// the controller. A controller is emulated only while it is hidden, never both (D7).
/// </summary>
/// <remarks>
/// <see cref="Feed"/> runs on the gamepad source's reading thread, on every read (every 1 ms while pads get states, #238
/// D9), and sends each controller's new state to its pad from there (<see cref="PadForwarder{TState}"/>), as
/// <see cref="SteamControllerXboxPad"/> does; a VIIPER write that ends at once thus runs under <c>_lock</c>. Hiding and plugging happen in <see cref="KeepAsync"/>,
/// which <see cref="PanEmulation"/> calls under its gate each tick its emulation runs.
/// </remarks>
public sealed class TakenOverControllers
{
    /// <summary>A controller's pad lost more often than this in <see cref="CrashWindow"/>: Arcadia stops, as for the Steam Controller.</summary>
    public const int MaxLossesPerWindow = SteamControllerXboxPad.MaxReplugsPerWindow;
    public static readonly TimeSpan CrashWindow = SteamControllerXboxPad.CrashWindow;

    /// <summary>While a vendor's firmware tool runs, the hide is asked again after this.</summary>
    public static readonly TimeSpan FirmwareToolRetry = TimeSpan.FromSeconds(10);

    /// <summary>
    /// After SDL starts again, controllers come back over a few polls: one missing this soon after isn't gone, so its pad
    /// stays and a controller waiting for its reconnect (A3) doesn't count as reconnected.
    /// </summary>
    public static readonly TimeSpan RereadGrace = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Whether a process of this name (without <c>.exe</c>) runs: a model that waits for its vendor's firmware tool (D8)
    /// is tried again only once the tool has ended. Pan decides; this only spares the let-go while the tool still runs.
    /// </summary>
    public Func<string, bool> ProcessRuns { get; init; } = ProcessWatch.IsRunning;

    /// <summary>Pan's heartbeat while SDL is let go: no poll pulses then, and a hide with its restart takes seconds.</summary>
    private static readonly TimeSpan PulseWhileLetGo = TimeSpan.FromMilliseconds(250);

    private readonly TimeProvider _time;
    private readonly Action<string> _log;
    private readonly Lock _lock = new();
    private readonly List<Controller> _controllers = [];
    private readonly List<Controller> _gone = [];
    private readonly Dictionary<ControllerModel, ModelState> _models = [];
    private readonly Dictionary<ControllerModel, int> _withoutPath = [];
    private readonly List<DateTimeOffset> _losses = [];
    private long _nextOrder;
    private long _rereadAt = long.MinValue;
    private uint _generation;
    private bool _motionSensorsOn;
    private volatile bool _gaveUp;
    private IReadOnlyList<ControllerNotHidden> _notHidden = [];
    private IReadOnlyList<ControllerWaitingForTool> _waiting = [];
    private IReadOnlyList<ConnectedModel> _connected = [];
    private bool _connectedChanged;
    private bool _forwarding;

    public TakenOverControllers(TimeProvider time, Action<string> log)
    {
        _time = time;
        _log = log;
    }

    /// <summary>Read on every poll: false while Arcadia is in front or the mouse mode is on, then every pad reports rest.</summary>
    public Func<bool> ForwardingAllowed { get; init; } = () => true;

    /// <summary>Read on every poll: which of Arcadia's combos are on.</summary>
    public Func<ComboWithholdingOptions> Combos { get; init; } = () => new ComboWithholdingOptions(BackAndStart: true);

    /// <summary>
    /// Lets go of every controller (false) or reads them again (true): Pan restarts a model it hides, and Windows refuses
    /// that while Arcadia holds it (<c>IGamepadSource.ReadControllers</c>).
    /// </summary>
    public Action<bool> ReadControllers { get; init; } = _ => { };

    /// <summary>Pan's heartbeat (<see cref="IEmulationGuardian.Pulse"/>), kept up while the controllers are let go.</summary>
    public Action Pulse { get; init; } = () => { };

    /// <summary>
    /// Called on the reading thread when it changes: true while a plugged pad gets the controller's states (not resting
    /// for Arcadia in front or the mouse mode), so the source reads every millisecond only then (<see cref="FastReads"/>).
    /// </summary>
    public Action<bool> Forwarding { get; init; } = _ => { };

    /// <summary>A game set a pad's motors: the controller with this <see cref="GamepadReading.PadId"/> rumbles so (0, 0 stops).</summary>
    public Action<int, XboxRumble> Rumble { get; init; } = (_, _) => { };

    /// <summary>Arcadia's own pads plugged in now, for telling them from real controllers (<see cref="OwnPadModels"/>).</summary>
    public OwnPadModels OwnPads { get; init; } = new();

    /// <summary>A game set a PlayStation pad's light bar: the controller with this <see cref="GamepadReading.PadId"/> shows it.</summary>
    public Action<int, PadLight> Light { get; init; } = (_, _) => { };

    /// <summary>
    /// The controllers' gyro and accelerometer on (true) while a plugged pad carries motion, off once none does: they
    /// cost battery. Called from the tick.
    /// </summary>
    public Action<bool> MotionSensors { get; init; } = _ => { };

    /// <summary>
    /// The player's choice for a model in "Controller zuordnen" (#238 D3a), or null for its default (D3b). Read each tick
    /// and by <see cref="AnyConnected"/>, so a change applies at the next tick.
    /// </summary>
    public Func<ControllerModel, ControllerLayout?> LayoutOf { get; init; } = _ => null;

    /// <summary>The pads were lost too often: Arcadia stops until "Erneut versuchen" (a toast with this text).</summary>
    public event Action<string>? GaveUp;

    /// <summary><see cref="NotHidden"/>, <see cref="WaitingForTool"/> or <see cref="Connected"/> changed; off the UI thread.</summary>
    public event Action? Changed;

    /// <summary>A controller Arcadia may take over is connected: Pan's emulation starts for it, also without a Steam Controller (D2).</summary>
    public bool AnyConnected
    {
        get
        {
            lock (_lock) return _controllers.Any(controller => IsTakenOver(controller.Model));
        }
    }

    /// <summary>
    /// The models connected now, in the order they connected, one entry per model, those not taken over included: the
    /// rows of "Controller zuordnen". Kept while Pan is off too, as the gamepad source feeds them.
    /// </summary>
    public IReadOnlyList<ConnectedModel> Connected => Volatile.Read(ref _connected);

    /// <summary>The connected controllers that reach games directly as Pan couldn't hide them (D7).</summary>
    public IReadOnlyList<ControllerNotHidden> NotHidden => Volatile.Read(ref _notHidden);

    /// <summary>The connected controllers Pan waits with while their vendor's firmware tool runs (D8).</summary>
    public IReadOnlyList<ControllerWaitingForTool> WaitingForTool => Volatile.Read(ref _waiting);

    /// <summary>How many pads are plugged in now.</summary>
    public int Plugged
    {
        get
        {
            lock (_lock) return _controllers.Count(controller => controller.Pad is not null);
        }
    }

    /// <summary>Whether Arcadia takes this controller over: a gamepad SDL reads that isn't the Steam Controller (it has its own path).</summary>
    public static bool TakesOver(GamepadReading reading) =>
        !reading.IsSteamController && ControllerList.MayTakeOver(Model(reading));

    private static ControllerModel Model(GamepadReading reading) => new(reading.Device.Vendor, reading.Device.Product);

    /// <summary>Every read of the gamepad source (<see cref="IGamepadSource.Sampled"/>), on its thread. Never throws.</summary>
    public void Feed(IReadOnlyList<GamepadReading> pads)
    {
        var allowed = ForwardingAllowed();
        var combos = Combos();
        // Made only when needed: this runs every millisecond while pads get states.
        List<(int PadId, XboxRumble? Motors, PadLight? Light)>? feedbackAgain = null;
        var forwarding = false;
        lock (_lock)
        {
            foreach (var controller in _controllers)
            {
                controller.Seen = false;
            }

            _withoutPath.Clear();
            for (var i = 0; i < pads.Count; i++)
            {
                var reading = pads[i];
                if (!TakesOver(reading))
                {
                    continue;
                }

                var key = KeyOf(reading);
                var controller = Find(key) ?? Add(key, reading);
                controller.Seen = true;
                if (controller.PadId != reading.PadId && (controller.Rumbling is not null || controller.Lit is not null))
                {
                    // SDL started again (a let-go for Pan's hide) and its new handle neither rumbles nor keeps the
                    // light bar: a game that set them once would otherwise lose them until it changes them.
                    (feedbackAgain ??= []).Add((reading.PadId, controller.Rumbling, controller.Lit));
                }

                controller.PadId = reading.PadId;

                // Filtered on every poll, also while nothing is forwarded, so a combo begun in Arcadia stays Arcadia's.
                var buttons = controller.Withholding.Filter(reading.Buttons, combos);
                controller.Offer(allowed ? VirtualPadState.From(reading, buttons) : VirtualPadState.Neutral, _time.GetTimestamp());
                forwarding |= allowed && controller.Pad is not null;
            }

            if (_rereadAt == long.MinValue || _time.GetElapsedTime(_rereadAt) >= RereadGrace)
            {
                for (var i = _controllers.Count - 1; i >= 0; i--)
                {
                    if (!_controllers[i].Seen)
                    {
                        RemoveAt(i);
                    }
                }
            }
        }

        PublishConnected();
        NoteForwarding(forwarding);
        foreach (var (padId, motors, light) in feedbackAgain ?? [])
        {
            if (motors is { } rumbling)
            {
                Rumble(padId, rumbling);
            }

            if (light is { } lit)
            {
                Light(padId, lit);
            }
        }
    }

    private void NoteForwarding(bool forwarding)
    {
        if (forwarding != _forwarding)
        {
            _forwarding = forwarding;
            Forwarding(forwarding);
        }
    }

    /// <summary>The gamepad source stopped for good (<see cref="IGamepadSource.Lost"/>): every pad rests now, the next tick unplugs them. Poll thread.</summary>
    public void Drop()
    {
        lock (_lock)
        {
            for (var i = _controllers.Count - 1; i >= 0; i--)
            {
                RemoveAt(i);
            }
        }

        PublishConnected();
        NoteForwarding(false);
    }

    /// <summary>
    /// Each tick of a running emulation, under its gate: Pan hides the models not hidden yet, then every hidden controller
    /// gets its pad, in connection order, and lost or gone pads go. Never throws but for cancellation.
    /// </summary>
    /// <param name="pads">Where the pads go: the emulation, so they are taken out with it.</param>
    public async Task KeepAsync(uint generation, IEmulationGuardian guardian, IVirtualXboxPads pads, CancellationToken cancellationToken)
    {
        try
        {
            if (generation != _generation)
            {
                // A new emulation: Pan's teardown of the last one showed every controller again.
                ForgetModels();
                _generation = generation;
            }

            await UnplugGoneAsync();
            await ApplyLayoutsAsync(generation, guardian, cancellationToken);
            await HideNewModelsAsync(generation, guardian, cancellationToken);
            await KeepPadsAsync(pads, cancellationToken);
            KeepMotionSensors();
            Publish();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"Controller für Spiele: Fehler: {ex}");
        }
    }

    /// <summary>The emulation ended (Pan showed every controller, or will): its pads are gone with it, nothing counts as hidden.</summary>
    public void EmulationEnded()
    {
        lock (_lock)
        {
            foreach (var controller in _controllers)
            {
                controller.ForgetPad();
            }

            foreach (var controller in _gone)
            {
                controller.ForgetPad();
            }

            _gone.Clear();
        }

        ForgetModels();
        KeepMotionSensors();
        Publish();
    }

    /// <summary>"Controller wiederherstellen" or "Erneut versuchen": the losses so far no longer count.</summary>
    public void ForgetLosses()
    {
        lock (_losses)
        {
            _losses.Clear();
            _gaveUp = false;
        }
    }

    private Controller? Find(string key)
    {
        foreach (var controller in _controllers)
        {
            if (controller.Key == key)
            {
                return controller;
            }
        }

        return null;
    }

    /// <summary>
    /// What tells a controller apart across SDL starting again (its <see cref="GamepadReading.PadId"/> changes then): SDL's
    /// path, which names the device; where SDL gives none, the model and its place among those of the model. Under the lock.
    /// </summary>
    private string KeyOf(GamepadReading reading)
    {
        if (reading.Device.Path.Length > 0)
        {
            return reading.Device.Path;
        }

        var model = Model(reading);
        var index = _withoutPath.GetValueOrDefault(model);
        _withoutPath[model] = index + 1;
        return $"{model}#{index}";
    }

    private Controller Add(string key, GamepadReading reading)
    {
        var model = Model(reading);
        var defaultKind = PadKinds.Default(reading.Device);
        var controller = new Controller(key, model, reading.Device.DisplayName, defaultKind, _nextOrder++, _time, OwnPads, _log)
        {
            Kind = ControllerLayouts.KindFor(LayoutOf(model), defaultKind),
        };
        _controllers.Add(controller);
        _connectedChanged = true;
        return controller;
    }

    /// <summary>Under the lock: the controller is gone; its pad rests now and goes at the next tick.</summary>
    private void RemoveAt(int index)
    {
        var controller = _controllers[index];
        _controllers.RemoveAt(index);
        _connectedChanged = true;
        controller.Offer(VirtualPadState.Neutral, _time.GetTimestamp());
        _gone.Add(controller);
        foreach (var model in _models.Values)
        {
            // Gone and back is a reconnect: a controller Windows restarts only then (A3) is fully hidden afterwards.
            model.ReplugPending.Remove(controller.Key);
        }
    }

    private async Task UnplugGoneAsync()
    {
        List<Controller> gone;
        lock (_lock)
        {
            gone = [.. _gone];
            _gone.Clear();
        }

        foreach (var controller in gone)
        {
            await UnplugAsync(controller, "getrennt");
        }
    }

    private async Task HideNewModelsAsync(uint generation, IEmulationGuardian guardian, CancellationToken cancellationToken)
    {
        List<(ControllerModel Model, List<string> Keys)> toHide;
        lock (_lock)
        {
            toHide = [.. _controllers
                .GroupBy(controller => controller.Model)
                .Select(group => (group.Key, group.Select(controller => controller.Key).ToList()))
                .Where(model => IsTakenOver(model.Key) && NeedsHide(model.Key, model.Item2))];
        }

        if (toHide.Count == 0)
        {
            return;
        }

        await WhileLetGoAsync(async () =>
        {
            foreach (var (model, keys) in toHide)
            {
                var result = await guardian.HideControllerAsync(generation, model, cancellationToken);
                Record(model, keys, result);
            }
        });
    }

    /// <summary>
    /// Pan restarts what it hides or shows: Arcadia lets go of every controller first, and the pads rest meanwhile (after
    /// the let-go, so no poll offers a held state again); Pan's heartbeat goes on.
    /// </summary>
    private async Task WhileLetGoAsync(Func<Task> requests)
    {
        ReadControllers(false);
        RestAllPads();
        using var pulsing = new CancellationTokenSource();
        var pulse = PulseAsync(pulsing.Token);
        try
        {
            await requests();
        }
        finally
        {
            await pulsing.CancelAsync();
            await pulse;
            lock (_lock) _rereadAt = _time.GetTimestamp();
            ReadControllers(true);
        }
    }

    /// <summary>Under the lock or not: whether the player left the model to Pan (anything but "Nicht übernehmen").</summary>
    private bool IsTakenOver(ControllerModel model) => LayoutOf(model) is not { IsTakenOver: false };

    /// <summary>
    /// The choices of "Controller zuordnen" (D3a), each tick: a pad of another kind than chosen goes (the next step plugs
    /// the chosen one, so the game sees it change once), and a model the player doesn't let Pan take over is shown
    /// again, its pads unplugged first, so a game never sees both. A show Pan refuses gets no pad either: Pan may have
    /// shown it already and only failed to restart it, so a pad could make two (QE review of #350).
    /// </summary>
    private async Task ApplyLayoutsAsync(uint generation, IEmulationGuardian guardian, CancellationToken cancellationToken)
    {
        List<(Controller Controller, string Why)> stale = [];
        List<ControllerModel> toShow = [];
        lock (_lock)
        {
            foreach (var group in _controllers.GroupBy(controller => controller.Model))
            {
                var layout = LayoutOf(group.Key);
                var show = false;
                if (layout is { IsTakenOver: false } && _models.TryGetValue(group.Key, out var state))
                {
                    if (state.Rejection is not null)
                    {
                        // Not hidden: nothing to show, and the D7/D8 rows don't name what the player excluded. Taken
                        // over again, it is tried anew.
                        _models.Remove(group.Key);
                    }
                    else
                    {
                        show = true;
                        toShow.Add(group.Key);
                    }
                }

                foreach (var controller in group)
                {
                    var kind = ControllerLayouts.KindFor(layout, controller.DefaultKind);
                    controller.Kind = kind;
                    if (controller.Pad is { } pad && (show || pad.Kind != kind))
                    {
                        stale.Add((controller, show ? "nicht übernehmen" : $"Layout {PadKinds.DisplayName(kind)}"));
                    }
                }
            }
        }

        foreach (var (controller, why) in stale)
        {
            await UnplugAsync(controller, why);
        }

        if (toShow.Count == 0)
        {
            return;
        }

        await WhileLetGoAsync(async () =>
        {
            foreach (var model in toShow)
            {
                var result = await guardian.ShowControllerAsync(generation, model, cancellationToken);
                RecordShow(model, result);
            }
        });
    }

    private void RecordShow(ControllerModel model, ControllerHidingResult result)
    {
        lock (_lock)
        {
            // Refused too: Pan answers HidingFailed also when it took the model off HidHide's list and only the
            // restart failed. Counted as not hidden, so no pad is plugged, and taken over again it is hidden anew.
            _models.Remove(model);
        }

        _log(result.Done
            ? $"Controller für Spiele: {model} nicht übernommen, wieder sichtbar{(result.Reason == PanReason.ControllerNeedsReplug ? " (Programme, die ihn offen hatten, sehen ihn erst nach neuem Verbinden)" : string.Empty)}"
            : $"Controller für Spiele: {model} nicht sicher wieder gezeigt ({result.Reason}), kein Pad; ist er im Spiel stumm, hilft neu verbinden oder „Controller wiederherstellen“");
    }

    /// <summary>Under the lock: a model not tried yet, a rejected one reconnected, or one whose firmware tool's wait is up.</summary>
    private bool NeedsHide(ControllerModel model, List<string> keys)
    {
        if (!_models.TryGetValue(model, out var state))
        {
            return true;
        }

        return state.Rejection switch
        {
            null => false,
            // Asked only once the tool has ended: each try lets go of every controller (QE review of #338).
            PanReason.FirmwareToolRuns => _time.GetElapsedTime(state.TriedAt) >= FirmwareToolRetry && !FirmwareToolRuns(model),
            // "Steckst du den Controller neu ein, versucht Pan es erneut" (D7).
            _ => keys.Any(key => !state.KeysAtRejection.Contains(key)),
        };
    }

    private bool FirmwareToolRuns(ControllerModel model)
    {
        try
        {
            return ControllerList.RunningToolFor(model, ProcessRuns) is not null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Unknown counts as ended: Pan checks again and refuses while the tool runs.
            _log($"Controller für Spiele: ob ein Firmware-Tool läuft, ist nicht bekannt: {ex.Message}");
            return false;
        }
    }

    private void Record(ControllerModel model, List<string> keys, ControllerHidingResult result)
    {
        lock (_lock)
        {
            var state = new ModelState { TriedAt = _time.GetTimestamp() };
            if (result.Done)
            {
                if (result.Reason == PanReason.ControllerNeedsReplug)
                {
                    // An XInput pad: hidden from programs that open it from now on, but whoever holds it (Steam, a game)
                    // keeps it until it reconnects. Emulated before that, a game would see it twice (D7).
                    state.ReplugPending.UnionWith(keys);
                }
            }
            else
            {
                state.Rejection = result.Reason;
                state.KeysAtRejection.UnionWith(keys);
            }

            _models[model] = state;
        }

        _log(result.Done
            ? $"Controller für Spiele: {model} versteckt{(result.Reason == PanReason.ControllerNeedsReplug ? ", ganz erst nach neuem Verbinden" : string.Empty)}"
            : $"Controller für Spiele: {model} nicht versteckt ({result.Reason}), Spiele sehen ihn direkt");
    }

    private async Task PulseAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                Pulse();
                await Task.Delay(PulseWhileLetGo, _time, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The hides are done.
        }
    }

    private void RestAllPads()
    {
        lock (_lock)
        {
            foreach (var controller in _controllers)
            {
                controller.Offer(VirtualPadState.Neutral, _time.GetTimestamp());
            }
        }
    }

    private async Task KeepPadsAsync(IVirtualXboxPads pads, CancellationToken cancellationToken)
    {
        List<Controller> controllers;
        lock (_lock)
        {
            controllers = [.. _controllers.OrderBy(controller => controller.Order)];
        }

        foreach (var controller in controllers)
        {
            if (controller.Pad is { IsConnected: false } lost)
            {
                var takenAway = lost.TakenAway;
                await UnplugAsync(controller, takenAway ? "von Pan entfernt" : "Verbindung zu VIIPER verloren");
                if (!takenAway)
                {
                    NoteLoss();
                }
            }
        }

        foreach (var controller in controllers)
        {
            if (controller.Pad is not null || _gaveUp || !IsHidden(controller))
            {
                continue;
            }

            if (pads.Unavailable() is { } why)
            {
                _log($"Controller für Spiele: kein Pad möglich ({why})");
                return;
            }

            // Known before the pad arrives in SDL, so it is never taken for a real controller (OwnPadModels).
            OwnPads.Plugged(controller.Kind);
            try
            {
                var pad = await pads.PlugAsync(controller.Kind, cancellationToken);
                controller.Plug(pad, Rumble, Light);
                _log($"Controller für Spiele: {PadKinds.DisplayName(controller.Kind)} für \"{controller.Name}\" ({controller.Model}) angesteckt");
            }
            catch (Exception ex)
            {
                OwnPads.Unplugged(controller.Kind);
                if (ex is OperationCanceledException)
                {
                    throw;
                }

                _log($"Controller für Spiele: Anstecken fehlgeschlagen: {ex.Message}");
                if (pads.Unavailable() is null)
                {
                    NoteLoss();
                }

                return;
            }
        }
    }

    /// <summary>Hidden from games now: its model is, and it reconnected since if Windows restarts it only then.</summary>
    private bool IsHidden(Controller controller)
    {
        lock (_lock)
        {
            return _models.TryGetValue(controller.Model, out var state)
                && state.Rejection is null
                && !state.ReplugPending.Contains(controller.Key);
        }
    }

    private async Task UnplugAsync(Controller controller, string why)
    {
        if (controller.TakePad() is not { } pad)
        {
            return;
        }

        Rumble(controller.PadId, default);
        try
        {
            await pad.DisposeAsync();
        }
        catch (Exception ex)
        {
            _log($"Controller für Spiele: Abstecken fehlgeschlagen: {ex.Message}");
        }

        _log($"Controller für Spiele: {PadKinds.DisplayName(pad.Kind)} für \"{controller.Name}\" abgesteckt ({why}); Verzögerung Lesen → VIIPER {controller.Delays.Summary()}");
    }

    /// <summary>Motion sensors on while a plugged pad carries motion; only changes are passed on.</summary>
    private void KeepMotionSensors()
    {
        bool wanted;
        lock (_lock)
        {
            wanted = _controllers.Any(controller => controller.Pad is { } pad && PadKinds.HasMotion(pad.Kind));
        }

        if (wanted != _motionSensorsOn)
        {
            _motionSensorsOn = wanted;
            MotionSensors(wanted);
        }
    }

    private void NoteLoss()
    {
        string message;
        lock (_losses)
        {
            var now = _time.GetUtcNow();
            _losses.RemoveAll(at => now - at > CrashWindow);
            _losses.Add(now);
            if (_losses.Count <= MaxLossesPerWindow || _gaveUp)
            {
                return;
            }

            _gaveUp = true;
            message = $"Controller für Spiele: VIIPER fiel {_losses.Count}-mal in {CrashWindow.TotalMinutes:0} Minuten aus, Arcadia versucht es erst nach einem Neustart oder „Controller wiederherstellen“ wieder";
        }

        _log(message);
        GaveUp?.Invoke(message);
    }

    private void ForgetModels()
    {
        lock (_lock)
        {
            _models.Clear();
            foreach (var controller in _controllers)
            {
                controller.ForgetPad();
            }
        }
    }

    /// <summary>After a controller came or went: the models for "Controller zuordnen", <see cref="Changed"/>. Outside the lock.</summary>
    private void PublishConnected()
    {
        lock (_lock)
        {
            if (!_connectedChanged)
            {
                return;
            }

            _connectedChanged = false;
            Volatile.Write(ref _connected, [.. _controllers
                .OrderBy(controller => controller.Order)
                .DistinctBy(controller => controller.Model)
                .Select(controller => new ConnectedModel(controller.Model, controller.Name, controller.DefaultKind))]);
        }

        Changed?.Invoke();
    }

    /// <summary>The lists for the settings (D7, D8), from the controllers connected now; <see cref="Changed"/> when they differ.</summary>
    private void Publish()
    {
        List<ControllerNotHidden> notHidden;
        List<ControllerWaitingForTool> waiting;
        lock (_lock)
        {
            notHidden = [];
            waiting = [];
            foreach (var controller in _controllers.OrderBy(controller => controller.Order))
            {
                if (!IsTakenOver(controller.Model) || !_models.TryGetValue(controller.Model, out var state))
                {
                    continue;
                }

                if (state.ReplugPending.Contains(controller.Key))
                {
                    notHidden.Add(new ControllerNotHidden(controller.Name, controller.Model, PanReason.ControllerNeedsReplug));
                    continue;
                }

                if (state.Rejection is not { } reason)
                {
                    continue;
                }

                if (reason == PanReason.FirmwareToolRuns)
                {
                    waiting.Add(new ControllerWaitingForTool(controller.Name, controller.Model));
                }
                else
                {
                    notHidden.Add(new ControllerNotHidden(controller.Name, controller.Model, reason));
                }
            }
        }

        var changed = !notHidden.SequenceEqual(NotHidden) || !waiting.SequenceEqual(WaitingForTool);
        Volatile.Write(ref _notHidden, notHidden);
        Volatile.Write(ref _waiting, waiting);
        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>What Pan did with one model in this emulation.</summary>
    private sealed class ModelState
    {
        public long TriedAt { get; init; }

        /// <summary>Why Pan didn't hide it, or null when hidden.</summary>
        public PanReason? Rejection { get; set; }

        /// <summary>The controllers connected when it was rejected: only another one, or one reconnected, tries again.</summary>
        public HashSet<string> KeysAtRejection { get; } = [];

        /// <summary>Hidden, but these controllers are held by whoever had them until they reconnect (A3).</summary>
        public HashSet<string> ReplugPending { get; } = [];
    }

    /// <summary>One connected controller and its pad.</summary>
    private sealed class Controller(string key, ControllerModel model, string name, PadKind defaultKind, long order, TimeProvider time, OwnPadModels ownPads, Action<string> log)
    {
        private readonly PadForwarder<VirtualPadState> _forwarder = new(
            time, delays => log($"Controller für Spiele: \"{name}\": Verzögerung Lesen → VIIPER {delays}"));

        private VirtualPadState _lastOffered;
        private volatile IVirtualPad? _pad;
        private volatile int _padId;
        private readonly Lock _rumbleLock = new();
        private XboxRumble? _rumbling;
        private PadLight? _lit;

        public string Key { get; } = key;

        public ControllerModel Model { get; } = model;

        public string Name { get; } = name;

        /// <summary>Its own kind where VIIPER has it (#238 D3b).</summary>
        public PadKind DefaultKind { get; } = defaultKind;

        /// <summary>The kind its pad is to be (D3a, else <see cref="DefaultKind"/>); set under the owner's lock.</summary>
        public PadKind Kind { get; set; } = defaultKind;

        public long Order { get; } = order;

        public ComboWithholding Withholding { get; } = new(time);

        public bool Seen { get; set; }

        /// <summary>SDL's id now (it changes when SDL starts again); read on VIIPER's thread for a rumble.</summary>
        public int PadId
        {
            get => _padId;
            set => _padId = value;
        }

        public IVirtualPad? Pad => _pad;

        /// <summary>From a read to VIIPER having taken the state, since the pad was plugged (D9).</summary>
        public LatencyStats Delays => _forwarder.Delays;

        /// <summary>Under the owner's lock: only changes go to the pad, VIIPER keeps the last state.</summary>
        public void Offer(VirtualPadState state, long readAt)
        {
            if (Kind == PadKind.Xbox360)
            {
                // What an Xbox 360 pad lacks: a gyro's every move would otherwise send an unchanged state each poll.
                state = state with { Extra = ExtraButtons.None, Touchpad = default, Motion = default };
            }

            if (state == _lastOffered)
            {
                return;
            }

            _lastOffered = state;
            _forwarder.Send(state, readAt);
        }

        /// <summary>The motors the game set last, while not (0, 0); read on the poll thread, set on VIIPER's.</summary>
        public XboxRumble? Rumbling
        {
            get { lock (_rumbleLock) return _rumbling; }
            private set { lock (_rumbleLock) _rumbling = value; }
        }

        /// <summary>The light bar's colour the game set last; read on the poll thread, set on VIIPER's.</summary>
        public PadLight? Lit
        {
            get { lock (_rumbleLock) return _lit; }
            private set { lock (_rumbleLock) _lit = value; }
        }

        public void Plug(IVirtualPad pad, Action<int, XboxRumble> rumble, Action<int, PadLight> light)
        {
            Rumbling = null;
            Lit = null;
            pad.Fed += feedback =>
            {
                if (feedback.Rumble is { } motors)
                {
                    Rumbling = motors == default ? null : motors;
                    rumble(PadId, motors);
                }

                if (feedback.Light is { } colour && colour != Lit)
                {
                    Lit = colour;
                    light(PadId, colour);
                }
            };
            _pad = pad;
            _forwarder.Plug((state, cancellationToken) => pad.IsConnected ? pad.SendAsync(state, cancellationToken) : Task.CompletedTask);
        }

        public IVirtualPad? TakePad()
        {
            Rumbling = null;
            Lit = null;
            _forwarder.Unplug();
            var pad = Interlocked.Exchange(ref _pad, null);
            if (pad is not null)
            {
                ownPads.Unplugged(pad.Kind);
            }

            return pad;
        }

        /// <summary>The emulation took the pad out itself.</summary>
        public void ForgetPad()
        {
            _forwarder.Unplug();
            if (Interlocked.Exchange(ref _pad, null) is { } pad)
            {
                ownPads.Unplugged(pad.Kind);
            }
        }
    }
}
