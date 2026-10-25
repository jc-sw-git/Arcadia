namespace Arcadia.Input;

/// <summary>
/// Turns what the controllers report in each poll into button presses and releases. The left stick counts as the
/// D-pad; the D-pad and the right stick repeat while held, so holding one moves the focus on. Platform-free, so the
/// acceptance tests hold the D-pad and tilt the stick through the same rules as <see cref="SdlGamepadSource"/>.
/// </summary>
public sealed class GamepadPolls
{
    public const int RepeatDelayMs = 400;
    public const int RepeatIntervalMs = 90;

    /// <summary>About half way; far outside any stick's dead zone, so a resting stick never navigates.</summary>
    private const double StickThreshold = 16000 / 32767.0;

    private static readonly GamepadButton[] Repeating =
    [
        GamepadButton.DPadUp, GamepadButton.DPadDown, GamepadButton.DPadLeft, GamepadButton.DPadRight,
        GamepadButton.RightStickUp, GamepadButton.RightStickDown, GamepadButton.RightStickLeft, GamepadButton.RightStickRight,
    ];

    private static readonly (PadButtons Flag, GamepadButton Button)[] Buttons =
    [
        (PadButtons.DPadUp, GamepadButton.DPadUp),
        (PadButtons.DPadDown, GamepadButton.DPadDown),
        (PadButtons.DPadLeft, GamepadButton.DPadLeft),
        (PadButtons.DPadRight, GamepadButton.DPadRight),
        (PadButtons.Menu, GamepadButton.Menu),
        (PadButtons.View, GamepadButton.View),
        (PadButtons.LeftStickClick, GamepadButton.LeftStickClick),
        (PadButtons.RightStickClick, GamepadButton.RightStickClick),
        (PadButtons.LeftShoulder, GamepadButton.LeftShoulder),
        (PadButtons.RightShoulder, GamepadButton.RightShoulder),
        (PadButtons.LeftTrigger, GamepadButton.LeftTrigger),
        (PadButtons.RightTrigger, GamepadButton.RightTrigger),
        (PadButtons.A, GamepadButton.A),
        (PadButtons.B, GamepadButton.B),
        (PadButtons.X, GamepadButton.X),
        (PadButtons.Y, GamepadButton.Y),
    ];

    private readonly Dictionary<GamepadButton, long> _heldSince = [];
    private readonly Dictionary<GamepadButton, long> _lastRepeat = [];
    private HashSet<GamepadButton> _previous = [];

    public event Action<GamepadButton>? ButtonPressed;

    public event Action<GamepadButton>? ButtonReleased;

    /// <summary>Takes one poll of all controllers.</summary>
    /// <param name="nowMs">A clock in milliseconds; only differences count.</param>
    public void Update(IReadOnlyList<GamepadReading> readings, long nowMs)
    {
        var current = Down(readings);
        foreach (var button in current)
        {
            if (!_previous.Contains(button))
            {
                _heldSince[button] = nowMs;
                _lastRepeat[button] = nowMs;
                ButtonPressed?.Invoke(button);
            }
            else if (Array.IndexOf(Repeating, button) >= 0
                     && nowMs - _heldSince[button] >= RepeatDelayMs
                     && nowMs - _lastRepeat[button] >= RepeatIntervalMs)
            {
                _lastRepeat[button] = nowMs;
                ButtonPressed?.Invoke(button);
            }
        }

        foreach (var button in _previous)
        {
            if (!current.Contains(button))
            {
                ButtonReleased?.Invoke(button);
            }
        }

        _previous = current;
    }

    /// <summary>The buttons held on any of the controllers, the sticks as directions.</summary>
    public static HashSet<GamepadButton> Down(IReadOnlyList<GamepadReading> readings)
    {
        var down = new HashSet<GamepadButton>();
        foreach (var reading in readings)
        {
            foreach (var (flag, button) in Buttons)
            {
                if (reading.IsDown(flag))
                {
                    down.Add(button);
                }
            }

            AddDirections(reading.LeftStick, down, GamepadButton.DPadUp, GamepadButton.DPadDown, GamepadButton.DPadLeft, GamepadButton.DPadRight);

            // The right stick moves a second focus, e.g. through the hits beside the on-screen keyboard.
            AddDirections(reading.RightStick, down, GamepadButton.RightStickUp, GamepadButton.RightStickDown, GamepadButton.RightStickLeft, GamepadButton.RightStickRight);
        }

        return down;
    }

    private static void AddDirections(StickPosition stick, HashSet<GamepadButton> down, GamepadButton up, GamepadButton downward, GamepadButton left, GamepadButton right)
    {
        if (stick.Y > StickThreshold)
        {
            down.Add(up);
        }
        else if (stick.Y < -StickThreshold)
        {
            down.Add(downward);
        }

        if (stick.X > StickThreshold)
        {
            down.Add(right);
        }
        else if (stick.X < -StickThreshold)
        {
            down.Add(left);
        }
    }
}
