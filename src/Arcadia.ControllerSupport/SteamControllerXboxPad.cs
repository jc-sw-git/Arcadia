using System.Threading.Channels;
using Arcadia.Input;

namespace Arcadia.ControllerSupport;

/// <summary>
/// The Steam Controller as a virtual Xbox 360 controller for games (#213): Arcadia reads it with SDL3 (#212) and feeds
/// what it reads, without Arcadia's own combos, to a pad from <see cref="IVirtualXboxPads"/>. The pad is plugged in
/// only while it is wanted and a Steam Controller is connected, and removed otherwise, so a game never sees an idle
/// extra controller.
/// </summary>
/// <remarks>
/// <see cref="Feed"/> runs on the gamepad's poll thread and only hands the newest state to a sender task, so a slow
/// VIIPER never holds up Arcadia's input. Plugging in and out happens in <see cref="TickAsync"/>, under one gate.
/// </remarks>
public sealed class SteamControllerXboxPad
{
    /// <summary>Plugging in again after the pad was lost; more often than this in <see cref="CrashWindow"/> and Arcadia stops trying.</summary>
    public const int MaxReplugsPerWindow = 3;
    public static readonly TimeSpan CrashWindow = TimeSpan.FromMinutes(5);

    /// <summary>VIIPER takes a state in well under a millisecond; a write this slow means it is stuck.</summary>
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(1);

    private readonly IVirtualXboxPads _pads;
    private readonly TimeProvider _time;
    private readonly Action<string> _log;
    private readonly ComboWithholding _withholding;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<DateTimeOffset> _losses = [];
    private readonly Channel<(XboxPadState State, long PolledAt)> _states =
        Channel.CreateBounded<(XboxPadState, long)>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

    private volatile IVirtualXboxPad? _pad;
    private volatile bool _steamControllerConnected;
    private int? _steamControllerId;
    private XboxPadState _lastFed;
    private Task? _sender;
    private bool _gaveUp;
    private string? _lastUnavailable;
    private DelayStats _delays;

    public SteamControllerXboxPad(IVirtualXboxPads pads, TimeProvider time, Action<string> log)
    {
        _pads = pads;
        _time = time;
        _log = log;
        _withholding = new ComboWithholding(time);
    }

    /// <summary>Whether a pad is wanted at all now (read in <see cref="TickAsync"/>); until #213's per-game mode: SISR is off.</summary>
    public Func<bool> Wanted { get; init; } = () => true;

    /// <summary>
    /// Read on every poll: false while Arcadia is in front or the mouse mode is on, then the game gets a neutral pad,
    /// as the Steam Controller acts in Arcadia or as the mouse.
    /// </summary>
    public Func<bool> ForwardingAllowed { get; init; } = () => true;

    /// <summary>Read on every poll: which of Arcadia's combos are on.</summary>
    public Func<ComboWithholdingOptions> Combos { get; init; } = () => new ComboWithholdingOptions(true, TimeSpan.FromSeconds(1));

    /// <summary>Raised once when the pad was lost too often and Arcadia stops plugging it in (shown as a toast).</summary>
    public event Action<string>? GaveUp;

    public bool IsPlugged => _pad is not null;

    /// <summary>A Steam Controller is connected, as of the last poll.</summary>
    public bool SteamControllerConnected => _steamControllerConnected;

    /// <summary>Every poll of the gamepad source, on its thread. Never throws.</summary>
    public void Feed(IReadOnlyList<GamepadReading> pads)
    {
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
            _withholding.Reset();
        }

        var state = XboxPadState.Neutral;
        if (steamController is { } reading)
        {
            // Filtered on every poll, also while nothing is forwarded, so a combo begun in Arcadia stays Arcadia's.
            var buttons = _withholding.Filter(reading.Buttons, Combos());
            if (ForwardingAllowed())
            {
                state = XboxPadState.From(reading, buttons);
            }
        }

        if (state != _lastFed)
        {
            _lastFed = state;
            _states.Writer.TryWrite((state, _time.GetTimestamp()));
        }
    }

    /// <summary>Plugs the pad in or out as needed, and again after it was lost. Called every 2 s; never throws.</summary>
    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            if (_pad is { IsConnected: false } lost)
            {
                _log("Xbox-Pad: Verbindung zu VIIPER verloren");
                await UnplugAsync(lost);
                NoteLoss();
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

            _states.Writer.TryComplete();
            await Guarded(_pads.ShutdownAsync, "VIIPER beenden");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The gamepad source stopped for good after an error (<see cref="IGamepadSource.Lost"/>): no poll lets go of what
    /// the Steam Controller held, so the game gets a pad at rest now, and the next tick unplugs it. Poll thread.
    /// </summary>
    public void Drop()
    {
        _steamControllerConnected = false;
        _steamControllerId = null;
        _withholding.Reset();
        _lastFed = XboxPadState.Neutral;
        _states.Writer.TryWrite((XboxPadState.Neutral, _time.GetTimestamp()));
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
            NoteLoss();
            return;
        }

        _delays = default;
        _pad = pad;
        _sender ??= Task.Run(SendLoopAsync, CancellationToken.None);

        // What the controller holds right now, since the pad missed it.
        _states.Writer.TryWrite((_lastFed, _time.GetTimestamp()));
    }

    private async Task UnplugAsync(IVirtualXboxPad pad)
    {
        _pad = null;
        await pad.DisposeAsync();
        _log($"Xbox-Pad: abgesteckt; {_delays}");
    }

    private void NoteLoss()
    {
        var now = _time.GetUtcNow();
        _losses.RemoveAll(at => now - at > CrashWindow);
        _losses.Add(now);
        if (_losses.Count > MaxReplugsPerWindow)
        {
            _gaveUp = true;
            var message = $"Steam Controller für Spiele: VIIPER fiel {_losses.Count}-mal in {CrashWindow.TotalMinutes:0} Minuten aus, Arcadia versucht es bis zum nächsten Start nicht mehr";
            _log($"Xbox-Pad: {message}");
            GaveUp?.Invoke(message);
        }
    }

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
                // A write that hangs counts as a lost pad, like one that fails.
                using var timeout = new CancellationTokenSource(SendTimeout);
                await pad.SendAsync(state, timeout.Token);
                _delays.Add(_time.GetElapsedTime(polledAt));
            }
            catch (Exception ex)
            {
                // The pad counts as lost now; the next tick plugs a new one in.
                _log($"Xbox-Pad: Senden fehlgeschlagen: {ex.Message}");
            }
        }
    }

    /// <summary>From a poll to the state being with VIIPER, for the input budget (CLAUDE.md: under 50 ms to a visible move).</summary>
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
