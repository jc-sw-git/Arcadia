using System.Threading.Channels;
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
/// reads (#335), and Arcadia plugs one virtual Xbox 360 pad per controller, in the order they connected, and feeds it
/// what the controller does, without Arcadia's combos, at rest while Arcadia is in front or the mouse mode is on. A
/// game's rumble goes back to the controller. A controller is emulated only while it is hidden, never both (D7).
/// </summary>
/// <remarks>
/// <see cref="Feed"/> runs on the poll thread and hands each controller's newest state to its sender task through a
/// one-slot channel, as <see cref="SteamControllerXboxPad"/> does. Hiding and plugging happen in <see cref="KeepAsync"/>,
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

    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(1);

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
    private volatile bool _gaveUp;
    private IReadOnlyList<ControllerNotHidden> _notHidden = [];
    private IReadOnlyList<ControllerWaitingForTool> _waiting = [];

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

    /// <summary>A game set a pad's motors: the controller with this <see cref="GamepadReading.PadId"/> rumbles so (0, 0 stops).</summary>
    public Action<int, XboxRumble> Rumble { get; init; } = (_, _) => { };

    /// <summary>The pads were lost too often: Arcadia stops until "Erneut versuchen" (a toast with this text).</summary>
    public event Action<string>? GaveUp;

    /// <summary><see cref="NotHidden"/> or <see cref="WaitingForTool"/> changed; off the UI thread.</summary>
    public event Action? Changed;

    /// <summary>A controller Arcadia may take over is connected: Pan's emulation starts for it, also without a Steam Controller (D2).</summary>
    public bool AnyConnected
    {
        get
        {
            lock (_lock) return _controllers.Count > 0;
        }
    }

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

    /// <summary>Every poll of the gamepad source, on its thread. Never throws.</summary>
    public void Feed(IReadOnlyList<GamepadReading> pads)
    {
        var allowed = ForwardingAllowed();
        var combos = Combos();
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
                controller.PadId = reading.PadId;

                // Filtered on every poll, also while nothing is forwarded, so a combo begun in Arcadia stays Arcadia's.
                var buttons = controller.Withholding.Filter(reading.Buttons, combos);
                controller.Offer(allowed ? XboxPadState.From(reading, buttons, withGuide: true) : XboxPadState.Neutral, _time.GetTimestamp());
            }

            if (_rereadAt != long.MinValue && _time.GetElapsedTime(_rereadAt) < RereadGrace)
            {
                return;
            }

            for (var i = _controllers.Count - 1; i >= 0; i--)
            {
                if (!_controllers[i].Seen)
                {
                    RemoveAt(i);
                }
            }
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
            await HideNewModelsAsync(generation, guardian, cancellationToken);
            await KeepPadsAsync(pads, cancellationToken);
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

    /// <summary>Arcadia exits: the sender tasks end (the pads go with the emulation).</summary>
    public void Shutdown()
    {
        lock (_lock)
        {
            foreach (var controller in _controllers.Concat(_gone))
            {
                controller.Complete();
            }
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
        var controller = new Controller(key, Model(reading), reading.Device.Name, _nextOrder++, _time);
        _controllers.Add(controller);
        return controller;
    }

    /// <summary>Under the lock: the controller is gone; its pad rests now and goes at the next tick.</summary>
    private void RemoveAt(int index)
    {
        var controller = _controllers[index];
        _controllers.RemoveAt(index);
        controller.Offer(XboxPadState.Neutral, _time.GetTimestamp());
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
            controller.Complete();
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
                .Where(model => NeedsHide(model.Key, model.Item2))];
        }

        if (toHide.Count == 0)
        {
            return;
        }

        // Pan restarts what it hides: Arcadia lets go of every controller first, and the pads rest meanwhile (after the
        // let-go, so no poll offers a held state again).
        ReadControllers(false);
        RestAllPads();
        using var pulsing = new CancellationTokenSource();
        var pulse = PulseAsync(pulsing.Token);
        try
        {
            foreach (var (model, keys) in toHide)
            {
                var result = await guardian.HideControllerAsync(generation, model, cancellationToken);
                Record(model, keys, result);
            }
        }
        finally
        {
            await pulsing.CancelAsync();
            await pulse;
            lock (_lock) _rereadAt = _time.GetTimestamp();
            ReadControllers(true);
        }
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
                controller.Offer(XboxPadState.Neutral, _time.GetTimestamp());
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

            try
            {
                var pad = await pads.PlugAsync(cancellationToken);
                controller.Plug(pad, Rumble);
                _log($"Controller für Spiele: Xbox-Pad für \"{controller.Name}\" ({controller.Model}) angesteckt");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
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

        _log($"Controller für Spiele: Xbox-Pad für \"{controller.Name}\" abgesteckt ({why}); {controller.Delays}");
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
                if (!_models.TryGetValue(controller.Model, out var state))
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
    private sealed class Controller(string key, ControllerModel model, string name, long order, TimeProvider time)
    {
        private readonly Channel<(XboxPadState State, long PolledAt)> _states =
            Channel.CreateBounded<(XboxPadState, long)>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

        private XboxPadState _lastOffered;
        private volatile IVirtualXboxPad? _pad;
        private Task? _sender;
        private volatile int _padId;

        public string Key { get; } = key;

        public ControllerModel Model { get; } = model;

        public string Name { get; } = name;

        public long Order { get; } = order;

        public ComboWithholding Withholding { get; } = new(time);

        public bool Seen { get; set; }

        /// <summary>SDL's id now (it changes when SDL starts again); read on VIIPER's thread for a rumble.</summary>
        public int PadId
        {
            get => _padId;
            set => _padId = value;
        }

        public IVirtualXboxPad? Pad => _pad;

        public DelayStats Delays;

        /// <summary>Under the owner's lock: only changes go to the pad, VIIPER keeps the last state.</summary>
        public void Offer(XboxPadState state, long polledAt)
        {
            if (state == _lastOffered)
            {
                return;
            }

            _lastOffered = state;
            _states.Writer.TryWrite((state, polledAt));
        }

        public void Plug(IVirtualXboxPad pad, Action<int, XboxRumble> rumble)
        {
            Delays = default;
            pad.Rumbled += motors => rumble(PadId, motors);
            _pad = pad;
            _sender ??= Task.Run(SendLoopAsync, CancellationToken.None);

            // What the controller holds right now, since the pad missed it.
            _states.Writer.TryWrite((_lastOffered, time.GetTimestamp()));
        }

        public IVirtualXboxPad? TakePad() => Interlocked.Exchange(ref _pad, null);

        /// <summary>The emulation took the pad out itself.</summary>
        public void ForgetPad() => _pad = null;

        public void Complete() => _states.Writer.TryComplete();

        private async Task SendLoopAsync()
        {
            await foreach (var (state, polledAt) in _states.Reader.ReadAllAsync())
            {
                if (_pad is not { IsConnected: true } pad)
                {
                    continue;
                }

                try
                {
                    // A write that hangs counts as a lost pad, like one that fails; the next tick plugs a new one in.
                    using var timeout = new CancellationTokenSource(SendTimeout);
                    await pad.SendAsync(state, timeout.Token);
                    Delays.Add(time.GetElapsedTime(polledAt));
                }
                catch (Exception)
                {
                    // The pad reports itself lost now.
                }
            }
        }
    }

    /// <summary>From a poll to the state being with VIIPER.</summary>
    private struct DelayStats
    {
        private int _count;
        private double _sumMs;
        private double _maxMs;

        public void Add(TimeSpan delay)
        {
            _count++;
            _sumMs += delay.TotalMilliseconds;
            _maxMs = Math.Max(_maxMs, delay.TotalMilliseconds);
        }

        public override readonly string ToString() => _count == 0
            ? "nichts gesendet"
            : $"{_count} Zustände gesendet, Verzögerung im Mittel {_sumMs / _count:0.0} ms, höchstens {_maxMs:0.0} ms";
    }
}
