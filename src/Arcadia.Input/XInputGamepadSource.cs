using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.Input;

/// <summary>
/// Polls up to four XInput controllers and merges them into one stream of button presses and releases.
/// While Steam is running, Steam Input exposes the Steam Controller (and PlayStation pads)
/// as a virtual XInput device, so they arrive here too.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class XInputGamepadSource : IGamepadSource
{
    private const int PollIntervalMs = 8;
    private const byte TriggerThreshold = 128;
    private const int Slots = 4;

    /// <summary>Long enough to feel, short enough not to get in the way.</summary>
    private const int RumbleMs = 150;
    private const ushort RumbleStrength = 30000;

    private readonly CancellationTokenSource _stop = new();
    private readonly GamepadPolls _polls = new();
    private readonly List<GamepadReading> _readings = new(Slots);
    private readonly ConcurrentQueue<int> _rumbleRequests = new();

    // When each slot's rumble ends (poll clock, ms); 0 while it doesn't rumble. Only the poll thread touches it.
    private readonly long[] _rumbleEnds = new long[Slots];
    private Thread? _thread;

    public XInputGamepadSource()
    {
        _polls.ButtonPressed += button => ButtonPressed?.Invoke(button);
        _polls.ButtonReleased += button => ButtonReleased?.Invoke(button);
    }

    public event Action<GamepadButton>? ButtonPressed;

    public event Action<GamepadButton>? ButtonReleased;

    public event Action<IReadOnlyList<GamepadReading>>? Polled;

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "Arcadia XInput" };
        _thread.Start();
    }

    public void Rumble(int slot)
    {
        if (slot is >= 0 and < Slots)
        {
            _rumbleRequests.Enqueue(slot);
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _thread?.Join(TimeSpan.FromMilliseconds(200));
        _stop.Dispose();
    }

    private void Run()
    {
        var clock = Stopwatch.StartNew();

        while (!_stop.IsCancellationRequested)
        {
            ReadAllControllers(_readings);
            var now = clock.ElapsedMilliseconds;
            Polled?.Invoke(_readings);
            _polls.Update(_readings, now);
            UpdateRumble(now);
            Thread.Sleep(PollIntervalMs);
        }

        // Don't leave a controller rumbling when Arcadia closes mid-rumble.
        for (uint slot = 0; slot < Slots; slot++)
        {
            if (_rumbleEnds[slot] != 0)
            {
                SetRumble(slot, 0);
            }
        }
    }

    private void UpdateRumble(long now)
    {
        while (_rumbleRequests.TryDequeue(out var slot))
        {
            SetRumble((uint)slot, RumbleStrength);
            _rumbleEnds[slot] = now + RumbleMs;
        }

        for (uint slot = 0; slot < Slots; slot++)
        {
            if (_rumbleEnds[slot] != 0 && now >= _rumbleEnds[slot])
            {
                SetRumble(slot, 0);
                _rumbleEnds[slot] = 0;
            }
        }
    }

    private static void SetRumble(uint slot, ushort strength)
    {
        // A slot without a controller (or a pad without motors) just reports an error; nothing to do about it.
        var vibration = new XInputVibration { LeftMotorSpeed = strength, RightMotorSpeed = strength };
        _ = XInputSetState(slot, ref vibration);
    }

    private static void ReadAllControllers(List<GamepadReading> readings)
    {
        readings.Clear();
        for (uint index = 0; index < Slots; index++)
        {
            if (XInputGetState(index, out var state) != 0)
            {
                continue;
            }

            readings.Add(ToReading((int)index, state.Gamepad));
        }
    }

    /// <summary>A fresh look at the given controllers, between two polls: does one of them pass <paramref name="test"/> right now?</summary>
    internal static bool AnyNow(IReadOnlyList<int> slots, Func<GamepadReading, bool> test)
    {
        foreach (var slot in slots)
        {
            if (XInputGetState((uint)slot, out var state) == 0 && test(ToReading(slot, state.Gamepad)))
            {
                return true;
            }
        }

        return false;
    }

    private static GamepadReading ToReading(int slot, XInputGamepad pad)
    {
        var buttons = (PadButtons)pad.Buttons & ~(PadButtons.LeftTrigger | PadButtons.RightTrigger);
        if (pad.LeftTrigger >= TriggerThreshold)
        {
            buttons |= PadButtons.LeftTrigger;
        }

        if (pad.RightTrigger >= TriggerThreshold)
        {
            buttons |= PadButtons.RightTrigger;
        }

        return new GamepadReading(slot, buttons, Stick(pad.ThumbLX, pad.ThumbLY), Stick(pad.ThumbRX, pad.ThumbRY));
    }

    // short.MinValue reaches a little further than short.MaxValue; clamped so both ends are 1.
    private static StickPosition Stick(short x, short y) =>
        new(Math.Max(x / 32767.0, -1), Math.Max(y / 32767.0, -1));

    [LibraryImport("xinput1_4.dll")]
    private static partial uint XInputGetState(uint userIndex, out XInputState state);

    [LibraryImport("xinput1_4.dll")]
    private static partial uint XInputSetState(uint userIndex, ref XInputVibration vibration);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputVibration
    {
        public ushort LeftMotorSpeed;
        public ushort RightMotorSpeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }
}
