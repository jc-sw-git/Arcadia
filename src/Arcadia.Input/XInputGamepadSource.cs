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

    private static readonly GamepadButton[] Repeating =
        [GamepadButton.DPadUp, GamepadButton.DPadDown, GamepadButton.DPadLeft, GamepadButton.DPadRight];

    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<GamepadButton, long> _heldSince = [];
    private readonly Dictionary<GamepadButton, long> _lastRepeat = [];
    private Thread? _thread;

    public event Action<GamepadButton>? ButtonPressed;

    public event Action<GamepadButton>? ButtonReleased;

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "Arcadia XInput" };
        _thread.Start();
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
            var current = ReadAllControllers();
            var now = clock.ElapsedMilliseconds;

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
            Thread.Sleep(PollIntervalMs);
        }
    }

    private static HashSet<GamepadButton> ReadAllControllers()
    {
        var pressed = new HashSet<GamepadButton>();
        for (uint index = 0; index < 4; index++)
        {
            if (XInputGetState(index, out var state) != 0)
            {
                continue;
            }

            Collect(state.Gamepad, pressed);
        }

        return pressed;
    }

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
    }

    [LibraryImport("xinput1_4.dll")]
    private static partial uint XInputGetState(uint userIndex, out XInputState state);

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
