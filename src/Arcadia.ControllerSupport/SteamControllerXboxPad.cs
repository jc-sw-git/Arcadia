using Arcadia.Input;

namespace Arcadia.ControllerSupport;

/// <summary>
/// The Steam Controller as a virtual Xbox 360 controller for games (#213): Arcadia reads it with SDL3 (#212) and feeds
/// what it reads, without Arcadia's own combos, to a pad from <see cref="IVirtualXboxPads"/>. The pad is plugged in
/// only while it is wanted and a Steam Controller is connected, and removed otherwise, so a game never sees an idle
/// extra controller.
/// </summary>
/// <remarks>
/// <see cref="Feed"/> runs on the gamepad's reading thread and sends each new state from there (<see cref="PadForwarder{TState}"/>),
/// so a state reaches VIIPER without a thread hop and a slow VIIPER still never holds up Arcadia's input. Plugging in and out happens in <see cref="TickAsync"/>, under one gate.
/// </remarks>
public sealed class SteamControllerXboxPad
{
    /// <summary>Plugging in again after the pad was lost; more often than this in <see cref="CrashWindow"/> and Arcadia stops trying.</summary>
    public const int MaxReplugsPerWindow = 3;
    public static readonly TimeSpan CrashWindow = TimeSpan.FromMinutes(5);

    private readonly IVirtualXboxPads _pads;
    private readonly TimeProvider _time;
    private readonly Action<string> _log;
    private readonly ComboWithholding _withholding;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<DateTimeOffset> _losses = [];
    private readonly PadForwarder<XboxPadState> _forwarder;

    private volatile IVirtualXboxPad? _pad;
    private volatile bool _steamControllerConnected;
    private int? _steamControllerId;

    // The same, for VIIPER's thread (a game's rumble); -1 while none is connected.
    private volatile int _rumbleTarget = -1;
    private XboxPadState _lastFed;
    private volatile bool _gaveUp;
    private bool _forwarding;
    private string? _lastUnavailable;

    public SteamControllerXboxPad(IVirtualXboxPads pads, TimeProvider time, Action<string> log)
    {
        _pads = pads;
        _time = time;
        _log = log;
        _withholding = new ComboWithholding(time);
        _forwarder = new PadForwarder<XboxPadState>(
            time,
            delays => _log($"Xbox-Pad: Verzögerung Lesen → VIIPER {delays}"),
            ex => _log($"Xbox-Pad: Senden fehlgeschlagen: {ex.Message}"));
    }

    /// <summary>Whether a pad is wanted at all now (read in <see cref="TickAsync"/>): <see cref="SteamControllerRoute.PadWanted"/>.</summary>
    public Func<bool> Wanted { get; init; } = () => true;

    /// <summary>
    /// Read on every poll: false while Arcadia is in front or the mouse mode is on, then the game gets a neutral pad,
    /// as the Steam Controller acts in Arcadia or as the mouse.
    /// </summary>
    public Func<bool> ForwardingAllowed { get; init; } = () => true;

    /// <summary>Read on every poll: which of Arcadia's combos are on.</summary>
    public Func<ComboWithholdingOptions> Combos { get; init; } = () => new ComboWithholdingOptions(BackAndStart: true);

    /// <summary>Called on every poll: the thread that feeds the pad is alive (Pan's heartbeat, #243). Must not block.</summary>
    public Action? FeedAlive { get; init; }

    /// <summary>
    /// A game set the pad's motors (#238 D5): the Steam Controller (by its <see cref="GamepadReading.PadId"/>) rumbles so.
    /// On VIIPER's thread, must not block; a stop (0, 0) follows when the pad goes.
    /// </summary>
    public Action<int, XboxRumble>? Rumble { get; init; }

    /// <summary>
    /// Called on the reading thread when it changes: true while the plugged pad gets the Steam Controller's states, so the
    /// source reads every millisecond only then (<see cref="FastReads"/>).
    /// </summary>
    public Action<bool> Forwarding { get; init; } = _ => { };

    /// <summary>Raised once when the pad was lost too often and Arcadia stops plugging it in (shown as a toast).</summary>
    public event Action<string>? GaveUp;

    public bool IsPlugged => _pad is not null;

    /// <summary>A Steam Controller is connected, as of the last poll.</summary>
    public bool SteamControllerConnected => _steamControllerConnected;

    /// <summary>Every read of the gamepad source (<see cref="IGamepadSource.Sampled"/>), on its thread. Never throws.</summary>
    public void Feed(IReadOnlyList<GamepadReading> pads)
    {
        FeedAlive?.Invoke();
        GamepadReading? steamController = null;
        for (var i = 0; i < pads.Count; i++)
        {
            if (pads[i].IsSteamController)
            {
                steamController = pads[i];
                break;
            }
        }

        _steamControllerConnected = steamController is not null;
        if (steamController?.PadId != _steamControllerId)
        {
            _steamControllerId = steamController?.PadId;
            _rumbleTarget = steamController?.PadId ?? -1;
            _withholding.Reset();
        }

        var state = XboxPadState.Neutral;
        var forwarding = false;
        if (steamController is { } reading)
        {
            // Filtered on every poll, also while nothing is forwarded, so a combo begun in Arcadia stays Arcadia's.
            var buttons = _withholding.Filter(reading.Buttons, Combos());
            if (ForwardingAllowed())
            {
                state = XboxPadState.From(reading, buttons);
                forwarding = _pad is not null;
            }
        }

        NoteForwarding(forwarding);

        if (state != _lastFed)
        {
            _lastFed = state;
            _forwarder.Send(state, _time.GetTimestamp());
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

    /// <summary>Plugs the pad in or out as needed, and again after it was lost. Called every 2 s; never throws.</summary>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (await _gate.WaitAsync(0, cancellationToken))
        {
            await TickUnderGateAsync(cancellationToken);
        }
    }

    /// <summary>
    /// As <see cref="TickAsync"/>, but waits for a tick that runs: right before a game on "Steam-Eingabe" starts, the pad
    /// must be gone before the game looks for controllers. Never throws.
    /// </summary>
    public async Task TickNowAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        await TickUnderGateAsync(cancellationToken);
    }

    /// <summary>Releases the gate when done.</summary>
    private async Task TickUnderGateAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_pad is { IsConnected: false } lost)
            {
                // Pan's teardown (e.g. after the PC woke) is no crash of VIIPER's and doesn't count towards giving up.
                var takenAway = lost.TakenAway;
                _log(takenAway ? "Xbox-Pad: von Pan entfernt" : "Xbox-Pad: Verbindung zu VIIPER verloren");
                await UnplugAsync(lost);
                if (!takenAway)
                {
                    NoteLoss();
                }
            }

            var wanted = _steamControllerConnected && !_gaveUp && Wanted();
            if (wanted && _pad is null)
            {
                await PlugAsync(cancellationToken);
            }
            else if (!wanted && _pad is { } pad)
            {
                await UnplugAsync(pad);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"Xbox-Pad: Fehler: {ex}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Arcadia exits (also for an update): the pad goes, and VIIPER's server with it. Never throws.</summary>
    public async Task ShutdownAsync()
    {
        await _gate.WaitAsync();
        try
        {
            // Each step on its own: a pad that can't be unplugged must not keep VIIPER's server running.
            if (_pad is { } pad)
            {
                await Guarded(async () => await UnplugAsync(pad), "Abstecken");
            }

            await Guarded(_pads.ShutdownAsync, "VIIPER beenden");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Arcadia lets go of every controller while Pan restarts one it hides (#238): no poll comes meanwhile, so the game
    /// gets a pad at rest instead of what the Steam Controller held last. The next poll after the reread feeds it again.
    /// </summary>
    public void Rest()
    {
        _lastFed = XboxPadState.Neutral;
        _forwarder.Send(XboxPadState.Neutral, _time.GetTimestamp());
    }

    /// <summary>
    /// The gamepad source stopped for good after an error (<see cref="IGamepadSource.Lost"/>): no poll lets go of what
    /// the Steam Controller held, so the game gets a pad at rest now, and the next tick unplugs it. Poll thread.
    /// </summary>
    public void Drop()
    {
        _steamControllerConnected = false;
        NoteForwarding(false);
        _steamControllerId = null;
        _rumbleTarget = -1;
        _withholding.Reset();
        _lastFed = XboxPadState.Neutral;
        _forwarder.Send(XboxPadState.Neutral, _time.GetTimestamp());
    }

    private async Task Guarded(Func<Task> step, string what)
    {
        try
        {
            await step();
        }
        catch (Exception ex)
        {
            _log($"Xbox-Pad: {what} beim Beenden fehlgeschlagen: {ex}");
        }
    }

    private async Task PlugAsync(CancellationToken cancellationToken)
    {
        if (_pads.Unavailable() is { } reason)
        {
            if (reason != _lastUnavailable)
            {
                _log($"Xbox-Pad: nicht möglich ({reason})");
                _lastUnavailable = reason;
            }

            return;
        }

        _lastUnavailable = null;
        IVirtualXboxPad pad;
        try
        {
            pad = await _pads.PlugAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"Xbox-Pad: Anstecken fehlgeschlagen: {ex.Message}");
            if (_pads.Unavailable() is { } why)
            {
                // The pads went unavailable meanwhile (Pan still cleaning up): not VIIPER failing, no loss counted.
                _lastUnavailable = why;
                return;
            }

            NoteLoss();
            return;
        }

        if (Rumble is { } rumble)
        {
            pad.Rumbled += motors =>
            {
                if (_rumbleTarget is var id and >= 0)
                {
                    rumble(id, motors);
                }
            };
        }

        _pad = pad;
        _forwarder.Plug((state, cancellationToken) => pad.IsConnected ? pad.SendAsync(state, cancellationToken) : Task.CompletedTask);
    }

    private async Task UnplugAsync(IVirtualXboxPad pad)
    {
        _pad = null;
        _forwarder.Unplug();
        if (_rumbleTarget is var id and >= 0)
        {
            // A game that rumbled when its pad went would leave the motors running.
            Rumble?.Invoke(id, default);
        }

        await pad.DisposeAsync();
        _log($"Xbox-Pad: abgesteckt; Verzögerung Lesen → VIIPER {_forwarder.Delays.Summary()}");
    }

    /// <summary>
    /// "Controller wiederherstellen" (#243): Pan ended VIIPER on purpose, so the losses so far no longer count, and
    /// Arcadia tries again if it had given up.
    /// </summary>
    public void ForgetLosses()
    {
        lock (_losses)
        {
            _losses.Clear();
            _gaveUp = false;
        }

        _log("Xbox-Pad: Controller wiederhergestellt, bisherige Ausfälle zählen nicht mehr");
    }

    private void NoteLoss()
    {
        string message;
        lock (_losses)
        {
            var now = _time.GetUtcNow();
            _losses.RemoveAll(at => now - at > CrashWindow);
            _losses.Add(now);
            if (_losses.Count <= MaxReplugsPerWindow)
            {
                return;
            }

            _gaveUp = true;
            message = $"Steam Controller für Spiele: VIIPER fiel {_losses.Count}-mal in {CrashWindow.TotalMinutes:0} Minuten aus, Arcadia versucht es erst nach einem Neustart oder „Controller wiederherstellen“ wieder";
        }

        _log($"Xbox-Pad: {message}");
        GaveUp?.Invoke(message);
    }
}
