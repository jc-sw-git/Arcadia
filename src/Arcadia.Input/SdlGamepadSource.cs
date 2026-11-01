using System.Collections.Concurrent;
using System.Diagnostics;

namespace Arcadia.Input;

/// <summary>
/// The SDL3 calls <see cref="SdlGamepadSource"/> needs. Behind an interface so the tests drive the source without
/// the native library; <see cref="SdlGamepads"/> is the real one. Only one thread calls it at a time.
/// </summary>
public interface ISdlGamepads
{
    /// <summary>Sets SDL's hints and starts its gamepad subsystem; throws when the library is missing or fails.</summary>
    /// <returns>SDL's version, for the log.</returns>
    string Init();

    /// <summary>The next gamepad that came or went since the last call; reads SDL's events, dropping all others.</summary>
    bool NextDeviceChange(out uint id, out bool added);

    PadDevice Describe(uint id);

    /// <returns>The open gamepad, or 0 with <see cref="LastError"/> saying why not.</returns>
    nint Open(uint id);

    string LastError();

    /// <summary>Reads the controllers anew between two calls of <see cref="NextDeviceChange"/>.</summary>
    void Update();

    /// <summary>
    /// Switches SDL's own driver for the Steam Controller and its puck on or off (<c>SDL_JOYSTICK_HIDAPI_STEAM</c>); off,
    /// SDL closes them at the next <see cref="Update"/>, on, it opens them again.
    /// </summary>
    void ReadSteamControllers(bool on);

    SdlPadState Read(nint pad);

    /// <summary>
    /// Rumbles the low and high frequency motors for <paramref name="durationMs"/>, then stops by itself; a pad without
    /// motors ignores it.
    /// </summary>
    void Rumble(nint pad, ushort low, ushort high, uint durationMs);

    /// <summary>Sets the light bar's colour; a pad without one ignores it.</summary>
    void SetLight(nint pad, byte red, byte green, byte blue);

    /// <summary>Switches gyro and accelerometer on or off.</summary>
    /// <returns>False when the pad has no gyro.</returns>
    bool SetMotionSensors(nint pad, bool on);

    void Close(nint pad);

    void Quit();
}

/// <summary>
/// Reads every controller through SDL3's gamepad API (#72), Xbox pads included, and merges them into one stream of
/// button presses and releases. <see cref="GamepadReading.PadId"/> is SDL's id of the device, new on every connect.
/// SDL keeps reading while another program is in front, so View held and Back + Start work beside a game. The Steam
/// Controller is read raw too (#212), trackpads included; the Xbox pad SISR emulates of it is left out
/// (<see cref="IEmulatedPads"/>).
/// </summary>
/// <remarks>
/// Polled every <see cref="PollIntervalMs"/>, like XInput before: the repeat of held directions and the mouse mode's
/// pointer need a steady clock anyway. While emulated pads are forwarded (<see cref="ReadFast"/>) it reads every
/// millisecond in between for <see cref="Sampled"/> (<see cref="ReadPacing"/>, #238 D9). Missing or failing SDL ends
/// only the controller input: it is reported once through the error callback, and keyboard and mouse keep working.
/// </remarks>
public sealed class SdlGamepadSource : IGamepadSource
{
    public const int PollIntervalMs = 8;

    /// <summary>Long enough to feel, short enough not to get in the way.</summary>
    private const uint RumbleMs = 150;
    private const ushort RumbleStrength = 30000;

    /// <summary>
    /// A held rumble (<see cref="HoldRumble"/>) is sent with this duration and renewed after <see cref="HeldRumbleRenewMs"/>:
    /// SDL has no endless rumble, and should Arcadia stop polling, the motors stop by themselves.
    /// </summary>
    private const uint HeldRumbleMs = 2000;
    private const long HeldRumbleRenewMs = 1000;

    /// <summary>How long the low-level hook may wait for a fresh look while the poll thread reads; it must answer fast.</summary>
    private static readonly TimeSpan FreshLookWait = TimeSpan.FromMilliseconds(2);

    private readonly ISdlGamepads _sdl;
    private readonly Action<string> _log;
    private readonly Action<Exception> _failed;
    private readonly GuardedHandlers<GamepadButton> _buttonPressed;
    private readonly GuardedHandlers<GamepadButton> _buttonReleased;
    private readonly GuardedHandlers<IReadOnlyList<GamepadReading>> _polled;
    private readonly GuardedHandlers<IReadOnlyList<GamepadReading>> _sampled;
    private readonly IEmulatedPads _emulated;
    private readonly CancellationTokenSource _stop = new();
    private readonly GamepadPolls _polls = new();
    private readonly List<GamepadReading> _readings = [];
    private readonly ConcurrentQueue<int> _rumbleRequests = new();
    private readonly ConcurrentQueue<(int PadId, ushort Low, ushort High)> _heldRumbleRequests = new();
    private readonly ConcurrentQueue<(int PadId, PadLight Light)> _lightRequests = new();

    // Poll thread only: the motors a game holds on, and when they were last sent.
    private readonly Dictionary<uint, (ushort Low, ushort High, long SentAtMs)> _heldRumbles = [];
    private readonly List<uint> _renew = [];

    // Guards every SDL call: the poll thread and the fresh look of the desktop filter's hook thread.
    private readonly Lock _sdlLock = new();
    private readonly Dictionary<uint, (nint Handle, PadDevice Device)> _pads = [];
    private bool _open;
    private volatile bool _lost;
    private volatile bool _motionWanted;
    private volatile bool _motionChanged;
    private volatile bool _fastWanted;

    // Every controller let go (ReadControllers(false)): no poll reads, and SDL is opened again once it ends.
    private volatile bool _letGo;
    private bool _reopen;
    private bool _steamControllersRead = true;
    private Thread? _thread;

    /// <param name="log">Connects and disconnects, for <c>arcadia.log</c>; called on the poll thread.</param>
    /// <param name="failed">Called once if SDL can't be used, or fails later; the controllers are then off.</param>
    /// <param name="emulated">Tells emulated Xbox pads (SISR's) from real ones; none by default.</param>
    /// <param name="unexpected">
    /// Called once per handler of <see cref="Polled"/>, <see cref="Sampled"/> or a button event that throws, with a
    /// context for the log and the toast; the controllers keep working. Logs the error with its stack by default.
    /// </param>
    public SdlGamepadSource(
        ISdlGamepads sdl,
        Action<string> log,
        Action<Exception> failed,
        IEmulatedPads? emulated = null,
        Action<Exception, string>? unexpected = null)
    {
        _sdl = sdl;
        _log = log;
        _failed = failed;
        _emulated = emulated ?? NoEmulatedPads.Instance;
        unexpected ??= (error, context) => log($"Unerwarteter Fehler ({context}): {error}");
        _buttonPressed = new(HandlerContext, unexpected, log);
        _buttonReleased = new(HandlerContext, unexpected, log);
        _polled = new(HandlerContext, unexpected, log);
        _sampled = new(HandlerContext, unexpected, log);
        _polls.ButtonPressed += _buttonPressed.Raise;
        _polls.ButtonReleased += _buttonReleased.Raise;
    }

    /// <summary>What a throwing handler was doing, in German for the log and the toast.</summary>
    private const string HandlerContext = "Controller-Eingabe";

    // Each handler on its own (GuardedHandlers): one that throws must not cost every controller (Lose).
    public event Action<GamepadButton>? ButtonPressed
    {
        add => _buttonPressed.Add(value);
        remove => _buttonPressed.Remove(value);
    }

    public event Action<GamepadButton>? ButtonReleased
    {
        add => _buttonReleased.Add(value);
        remove => _buttonReleased.Remove(value);
    }

    public event Action<IReadOnlyList<GamepadReading>>? Polled
    {
        add => _polled.Add(value);
        remove => _polled.Remove(value);
    }

    public event Action<IReadOnlyList<GamepadReading>>? Sampled
    {
        add => _sampled.Add(value);
        remove => _sampled.Remove(value);
    }

    public event Action? Lost;

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "Arcadia SDL gamepads" };
        _thread.Start();
    }

    public void Rumble(int padId) => _rumbleRequests.Enqueue(padId);

    public void HoldRumble(int padId, ushort low, ushort high) => _heldRumbleRequests.Enqueue((padId, low, high));

    public void SetLight(int padId, PadLight light) => _lightRequests.Enqueue((padId, light));

    public void ReadFast(bool on) => _fastWanted = on;

    /// <summary>
    /// Off, SDL closes every controller and ends before this returns (the way check A3 of #335 found Windows lets Pan
    /// restart them); polls pause, so nobody sees the controllers go. On, the next poll starts SDL again and the
    /// controllers come back as newly connected. Never throws; a failure is logged.
    /// </summary>
    public void ReadControllers(bool on)
    {
        try
        {
            lock (_sdlLock)
            {
                if (on)
                {
                    _letGo = false;
                    return;
                }

                _letGo = true;
                if (!_open)
                {
                    return;
                }

                CloseUnderLock();
                _reopen = true;
                _log("Controller: alle Controller losgelassen, Pan startet einen neu");
            }
        }
        catch (Exception ex)
        {
            _log($"Controller: Controller nicht {(on ? "wieder geöffnet" : "losgelassen")}: {ex.Message}");
        }
    }

    /// <summary>
    /// Switches the gyro and accelerometer of every controller that has them on or off, from the next poll on; off by
    /// default. Only while a feature needs them (#111, #238's emulated pads): they cost the controller's battery.
    /// Callable from any thread.
    /// </summary>
    public void UseMotionSensors(bool on)
    {
        _motionWanted = on;
        _motionChanged = true;
    }

    /// <summary>
    /// SDL holds the puck open even without a controller connected, waiting for one, and an open handle makes Windows
    /// refuse the puck restart Pan's hide needs (#308): off, SDL lets go of every Steam Controller and puck before this
    /// returns; on, the next poll opens them again. Never throws; a failure is logged.
    /// </summary>
    public void ReadSteamControllers(bool on)
    {
        try
        {
            lock (_sdlLock)
            {
                _steamControllersRead = on;
                if (!_open)
                {
                    return;
                }

                _sdl.ReadSteamControllers(on);
                if (!on)
                {
                    // SDL closes a device of a driver switched off when it next looks for devices, at the end of an update.
                    _sdl.Update();
                }
            }
        }
        catch (Exception ex)
        {
            _log($"Controller: Steam Controller in SDL nicht {(on ? "wieder geöffnet" : "losgelassen")}: {ex.Message}");
        }
    }

    public bool AnyNow(Func<GamepadReading, bool> test)
    {
        if (!_sdlLock.TryEnter(FreshLookWait))
        {
            return false;
        }

        try
        {
            if (!_open)
            {
                return false;
            }

            _sdl.Update();
            foreach (var (id, pad) in _pads)
            {
                if (test(Read(id, pad.Handle, pad.Device)))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception)
        {
            // Called inside Windows' input hook, where an exception would end Arcadia; the next poll reports it.
            return false;
        }
        finally
        {
            _sdlLock.Exit();
        }
    }

    /// <summary>Starts SDL. The poll thread calls it first; the tests call it directly.</summary>
    /// <returns>False when SDL can't be used; already reported.</returns>
    public bool Open()
    {
        try
        {
            lock (_sdlLock)
            {
                var version = _sdl.Init();
                _open = true;
                _log($"Controller: SDL {version} bereit");
            }

            return true;
        }
        catch (Exception ex)
        {
            // DllNotFoundException, EntryPointNotFoundException, BadImageFormatException or SDL_Init failing: the
            // controllers are gone for good before they came, so the hints show the keyboard (#215).
            Lose(ex);
            return false;
        }
    }

    /// <summary>
    /// One read: who came or went, what every controller reports (<see cref="Sampled"/>), and on a poll also
    /// <see cref="Polled"/> and the button events. A failing native call must not end Arcadia: the controllers stop for
    /// good instead (<see cref="Lost"/>). A throwing handler is reported and the controllers go on (GuardedHandlers).
    /// </summary>
    /// <param name="nowMs">A clock in milliseconds; only differences count.</param>
    /// <param name="pollDue">False for a read between two polls, only for <see cref="Sampled"/> (<see cref="ReadPacing"/>).</param>
    /// <returns>False once the controllers are lost; there is nothing more to poll.</returns>
    public bool Poll(long nowMs, bool pollDue = true)
    {
        if (_lost)
        {
            return false;
        }

        if (_letGo)
        {
            return true;
        }

        try
        {
            if (_reopen && !Reopen())
            {
                return false;
            }

            ReadControllers(nowMs, pollDue);
            return true;
        }
        catch (Exception ex)
        {
            Lose(ex);
            return false;
        }
    }

    /// <summary>SDL again after every controller was let go; false when it can't start (already reported).</summary>
    private bool Reopen()
    {
        lock (_sdlLock)
        {
            if (_letGo || !_reopen)
            {
                return true;
            }

            _reopen = false;
        }

        if (!Open())
        {
            return false;
        }

        lock (_sdlLock)
        {
            if (!_steamControllersRead)
            {
                // Init switches SDL's Steam Controller driver on; a hide of the puck may still be under way (#308).
                _sdl.ReadSteamControllers(false);
                _sdl.Update();
            }
        }

        return true;
    }

    private void ReadControllers(long nowMs, bool pollDue)
    {
        lock (_sdlLock)
        {
            while (_sdl.NextDeviceChange(out var id, out var added))
            {
                if (added)
                {
                    Connect(id);
                }
                else
                {
                    Disconnect(id);
                }
            }

            if (_motionChanged)
            {
                _motionChanged = false;
                foreach (var pad in _pads.Values)
                {
                    _sdl.SetMotionSensors(pad.Handle, _motionWanted);
                }
            }

            _readings.Clear();
            foreach (var (id, pad) in _pads)
            {
                _readings.Add(Read(id, pad.Handle, pad.Device));
            }

            while (_rumbleRequests.TryDequeue(out var padId))
            {
                if (_pads.TryGetValue((uint)padId, out var pad))
                {
                    _sdl.Rumble(pad.Handle, RumbleStrength, RumbleStrength, RumbleMs);
                }
            }

            HoldRumbles(nowMs);
            while (_lightRequests.TryDequeue(out var request))
            {
                if (_pads.TryGetValue((uint)request.PadId, out var pad))
                {
                    _sdl.SetLight(pad.Handle, request.Light.Red, request.Light.Green, request.Light.Blue);
                }
            }
        }

        _sampled.Raise(_readings);
        if (pollDue)
        {
            _polled.Raise(_readings);
            _polls.Update(_readings, nowMs);
        }
    }

    /// <summary>
    /// A game's rumble on the emulated pad, on the real one: sent when it changes, and renewed before SDL ends it. Under
    /// the SDL lock.
    /// </summary>
    private void HoldRumbles(long nowMs)
    {
        while (_heldRumbleRequests.TryDequeue(out var request))
        {
            var id = (uint)request.PadId;
            if (!_pads.TryGetValue(id, out var pad))
            {
                continue;
            }

            _sdl.Rumble(pad.Handle, request.Low, request.High, request.Low == 0 && request.High == 0 ? 0 : HeldRumbleMs);
            if (request.Low == 0 && request.High == 0)
            {
                _heldRumbles.Remove(id);
            }
            else
            {
                _heldRumbles[id] = (request.Low, request.High, nowMs);
            }
        }

        _renew.Clear();
        foreach (var (id, held) in _heldRumbles)
        {
            if (nowMs - held.SentAtMs >= HeldRumbleRenewMs)
            {
                _renew.Add(id);
            }
        }

        foreach (var id in _renew)
        {
            var held = _heldRumbles[id];
            if (_pads.TryGetValue(id, out var pad))
            {
                _sdl.Rumble(pad.Handle, held.Low, held.High, HeldRumbleMs);
                _heldRumbles[id] = held with { SentAtMs = nowMs };
            }
        }
    }

    /// <summary>Lets go of every controller and of SDL.</summary>
    public void Close()
    {
        lock (_sdlLock)
        {
            CloseUnderLock();
        }
    }

    private void CloseUnderLock()
    {
        if (!_open)
        {
            return;
        }

        _open = false;
        foreach (var pad in _pads.Values)
        {
            _sdl.Close(pad.Handle);
        }

        _pads.Clear();
        _heldRumbles.Clear();
        _sdl.Quit();
    }

    public void Dispose()
    {
        if (_stop.IsCancellationRequested)
        {
            return;
        }

        _stop.Cancel();
        if (_thread is null)
        {
            Close();
        }
        else
        {
            // The thread closes SDL itself once its poll ends.
            _thread.Join(TimeSpan.FromMilliseconds(500));
        }

        _stop.Dispose();
    }

    private void Run()
    {
        if (!Open())
        {
            return;
        }

        var clock = Stopwatch.StartNew();
        using var wait = PreciseWait.Create();
        var pacing = new ReadPacing(_log, HighPriority, ProcessorTime, Environment.ProcessorCount);
        if (!wait.IsPrecise && OperatingSystem.IsWindows())
        {
            _log("Controller: kein hochauflösender Timer, schnelles Lesen wartet mit Thread.Sleep");
        }

        try
        {
            while (!_stop.IsCancellationRequested)
            {
                // While every controller is let go nothing is read or forwarded: such a pause is no gap between reads.
                var pollDue = pacing.StartRead(clock.Elapsed, _fastWanted && !_letGo);
                if (!Poll(clock.ElapsedMilliseconds, pollDue))
                {
                    break;
                }

                if (pacing.IsFast)
                {
                    wait.Wait(pacing.Wait(clock.Elapsed));
                }
                else
                {
                    Thread.Sleep(PollIntervalMs);
                }
            }
        }
        catch (Exception ex)
        {
            // Pacing or the wait failing on this thread would otherwise end the process; only the controllers stop.
            Lose(ex);
        }
        finally
        {
            try
            {
                pacing.Stop(clock.Elapsed);
                Close();
            }
            catch (Exception ex)
            {
                // Ending the thread must not end the process either; Lost is raised only once.
                if (_lost)
                {
                    Report(ex);
                }
                else
                {
                    Lose(ex);
                }
            }
        }
    }

    private static void HighPriority(bool on) =>
        Thread.CurrentThread.Priority = on ? ThreadPriority.Highest : ThreadPriority.Normal;

    private static TimeSpan ProcessorTime()
    {
        using var process = Process.GetCurrentProcess();
        return process.TotalProcessorTime;
    }

    /// <summary>
    /// The controllers stop for good: SDL is closed, every handler of <see cref="Lost"/> drops what it holds (each
    /// one, even if another throws), and the error is reported last.
    /// </summary>
    private void Lose(Exception error)
    {
        _lost = true;
        try
        {
            Close();
        }
        catch (Exception closeError)
        {
            Report(closeError);
        }

        foreach (var handler in Lost?.GetInvocationList() ?? [])
        {
            try
            {
                ((Action)handler)();
            }
            catch (Exception handlerError)
            {
                Report(handlerError);
            }
        }

        Report(error);
    }

    private void Report(Exception error)
    {
        try
        {
            _failed(error);
        }
        catch (Exception)
        {
            // Reporting must not end the poll thread in turn; the controllers are already stopped.
        }
    }

    private void Connect(uint id)
    {
        if (_pads.ContainsKey(id))
        {
            return;
        }

        var device = _sdl.Describe(id);
        if (GamepadDevices.WhyIgnored(device, _emulated) is { } reason)
        {
            _log($"Controller #{id} ignoriert ({reason}): {device}");
            return;
        }

        var pad = _sdl.Open(id);
        if (pad == 0)
        {
            _log($"Controller #{id} lässt sich nicht öffnen: {device}: {_sdl.LastError()}");
            return;
        }

        _pads[id] = (pad, device);
        _log($"Controller #{id} verbunden: {device}");
        if (_motionWanted)
        {
            _sdl.SetMotionSensors(pad, true);
        }
    }

    private GamepadReading Read(uint id, nint handle, PadDevice device) =>
        SdlPadMapping.ToReading((int)id, _sdl.Read(handle)) with { Type = device.Type, Device = device };

    private void Disconnect(uint id)
    {
        if (_pads.Remove(id, out var pad))
        {
            _heldRumbles.Remove(id);
            _sdl.Close(pad.Handle);
            _log($"Controller #{id} getrennt: {pad.Device}");
        }
    }
}
