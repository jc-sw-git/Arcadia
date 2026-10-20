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
    private const int RepeatDelayMs = 400;
    private const int RepeatIntervalMs = 90;
    private const short StickThreshold = 16000;
    private const byte TriggerThreshold = 128;
    private const int Slots = 4;

    /// <summary>Long enough to feel, short enough not to get in the way.</summary>
    private const int RumbleMs = 150;
    private const ushort RumbleStrength = 30000;

    private static readonly GamepadButton[] Repeating =
    [
        GamepadButton.DPadUp, GamepadButton.DPadDown, GamepadButton.DPadLeft, GamepadButton.DPadRight,
        GamepadButton.RightStickUp, GamepadButton.RightStickDown, GamepadButton.RightStickLeft, GamepadButton.RightStickRight,
    ];

    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<GamepadButton, long> _heldSince = [];
    private readonly Dictionary<GamepadButton, long> _lastRepeat = [];
    private readonly List<GamepadReading> _readings = new(Slots);
    private readonly ConcurrentQueue<int> _rumbleRequests = new();

    // When each slot's rumble ends (poll clock, ms); 0 while it doesn't rumble. Only the poll thread touches it.
    private readonly long[] _rumbleEnds = new long[Slots];
    private Thread? _thread;

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
        var previous = new HashSet<GamepadButton>();

        while (!_stop.IsCancellationRequested)
        {
            var current = ReadAllControllers(_readings);
            var now = clock.ElapsedMilliseconds;
            Polled?.Invoke(_readings);

            foreach (var button in current)
            {
                if (!previous.Contains(button))
                {
                    _heldSince[button] = now;
                    _lastRepeat[button] = now;
                    ButtonPressed?.Invoke(button);
                }
                else if (Array.IndexOf(Repeating, button) >= 0
                         && now - _heldSince[button] >= RepeatDelayMs
                         && now - _lastRepeat[button] >= RepeatIntervalMs)
                {
                    _lastRepeat[button] = now;
                    ButtonPressed?.Invoke(button);
                }
            }

            foreach (var button in previous)
            {
                if (!current.Contains(button))
                {
                    ButtonReleased?.Invoke(button);
                }
            }

            previous = current;
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

    private static HashSet<GamepadButton> ReadAllControllers(List<GamepadReading> readings)
    {
        readings.Clear();
        var pressed = new HashSet<GamepadButton>();
        for (uint index = 0; index < Slots; index++)
        {
            if (XInputGetState(index, out var state) != 0)
            {
                continue;
            }

            readings.Add(ToReading((int)index, state.Gamepad));
            Collect(state.Gamepad, pressed);
        }

        return pressed;
    }

    /// <summary>A fresh look at the given controllers, between two polls: is one of them in use right now?</summary>
    internal static bool AnyInUseNow(IReadOnlyList<int> slots)
    {
        foreach (var slot in slots)
        {
            if (XInputGetState((uint)slot, out var state) == 0 && ControllerActivity.IsInUse(ToReading(slot, state.Gamepad)))
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

    private static void Collect(XInputGamepad pad, HashSet<GamepadButton> pressed)
    {
        var buttons = pad.Buttons;
        void Map(ushort mask, GamepadButton button)
        {
            if ((buttons & mask) != 0)
            {
                pressed.Add(button);
            }
        }

        Map(0x0001, GamepadButton.DPadUp);
        Map(0x0002, GamepadButton.DPadDown);
        Map(0x0004, GamepadButton.DPadLeft);
        Map(0x0008, GamepadButton.DPadRight);
        Map(0x0010, GamepadButton.Menu);
        Map(0x0020, GamepadButton.View);
        Map(0x0040, GamepadButton.LeftStickClick);
        Map(0x0080, GamepadButton.RightStickClick);
        Map(0x0100, GamepadButton.LeftShoulder);
        Map(0x0200, GamepadButton.RightShoulder);
        Map(0x1000, GamepadButton.A);
        Map(0x2000, GamepadButton.B);
        Map(0x4000, GamepadButton.X);
        Map(0x8000, GamepadButton.Y);

        if (pad.LeftTrigger >= TriggerThreshold)
        {
            pressed.Add(GamepadButton.LeftTrigger);
        }

        if (pad.RightTrigger >= TriggerThreshold)
        {
            pressed.Add(GamepadButton.RightTrigger);
        }

        // The left stick navigates like the D-pad.
        if (pad.ThumbLY > StickThreshold)
        {
            pressed.Add(GamepadButton.DPadUp);
        }
        else if (pad.ThumbLY < -StickThreshold)
        {
            pressed.Add(GamepadButton.DPadDown);
        }

        if (pad.ThumbLX > StickThreshold)
        {
            pressed.Add(GamepadButton.DPadRight);
        }
        else if (pad.ThumbLX < -StickThreshold)
        {
            pressed.Add(GamepadButton.DPadLeft);
        }

        // The right stick moves a second focus, e.g. through the hits beside the on-screen keyboard.
        if (pad.ThumbRY > StickThreshold)
        {
            pressed.Add(GamepadButton.RightStickUp);
        }
        else if (pad.ThumbRY < -StickThreshold)
        {
            pressed.Add(GamepadButton.RightStickDown);
        }

        if (pad.ThumbRX > StickThreshold)
        {
            pressed.Add(GamepadButton.RightStickRight);
        }
        else if (pad.ThumbRX < -StickThreshold)
        {
            pressed.Add(GamepadButton.RightStickLeft);
        }
    }

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
