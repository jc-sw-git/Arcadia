namespace Arcadia.Input;

/// <summary>
/// Turns what the controllers report in each poll into button presses and releases. The left stick counts as the
/// D-pad; the D-pad, the right stick, LB/RB and LT/RT repeat while held, so holding one moves the focus or turns on
/// (#476). A repeat comes as <see cref="ButtonRepeated"/>, not as a press, so a list can stop at its end for a hold and
/// wrap round only on a fresh press. Platform-free, so the acceptance tests hold the D-pad and tilt the stick through
/// the same rules as <see cref="SdlGamepadSource"/>.
/// </summary>
public sealed class GamepadPolls
{
    public const int RepeatDelayMs = 400;
    public const int RepeatIntervalMs = 90;

    /// <summary>LB/RB and LT/RT turn a tab, a page or a game; slower than a step of the focus, so each turn can be seen.</summary>
    public const int TurnRepeatIntervalMs = 150;

    /// <summary>About half way; far outside any stick's dead zone, so a resting stick never navigates.</summary>
    private const double StickThreshold = 16000 / 32767.0;

    /// <summary>
    /// A held stick direction or trigger lets go only below this, well under where it pressed: one held near the press
    /// point and wobbling across it is no new press, which would wrap round at a list's end (#476). Still outside the
    /// sticks' dead zone, so a stick let go does let go.
    /// </summary>
    private const double StickReleaseThreshold = 0.3;

    /// <summary>The triggers press past half way (<see cref="SdlPadMapping"/>) and let go below a quarter.</summary>
    private const double TriggerReleaseThreshold = 0.25;

    private static readonly GamepadButton[] Moving =
    [
        GamepadButton.DPadUp, GamepadButton.DPadDown, GamepadButton.DPadLeft, GamepadButton.DPadRight,
        GamepadButton.RightStickUp, GamepadButton.RightStickDown, GamepadButton.RightStickLeft, GamepadButton.RightStickRight,
    ];

    private static readonly GamepadButton[] Turning =
    [
        GamepadButton.LeftShoulder, GamepadButton.RightShoulder, GamepadButton.LeftTrigger, GamepadButton.RightTrigger,
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

    // Released by a let-go while still held (#367): not pressed again before the player let go of them once.
    private readonly HashSet<GamepadButton> _releasedWhileHeld = [];

    public event Action<GamepadButton>? ButtonPressed;

    /// <summary>A held button acts again (<see cref="IGamepadSource.ButtonRepeated"/>).</summary>
    public event Action<GamepadButton>? ButtonRepeated;

    public event Action<GamepadButton>? ButtonReleased;

    /// <summary>Takes one poll of all controllers.</summary>
    /// <param name="nowMs">A clock in milliseconds; only differences count.</param>
    public void Update(IReadOnlyList<GamepadReading> readings, long nowMs)
    {
        var current = Down(readings, _previous);
        _releasedWhileHeld.IntersectWith(current);
        current.ExceptWith(_releasedWhileHeld);
        foreach (var button in current)
        {
            if (!_previous.Contains(button))
            {
                _heldSince[button] = nowMs;
                _lastRepeat[button] = nowMs;
                ButtonPressed?.Invoke(button);
            }
            else if (RepeatIntervalOf(button) is { } interval
                     && nowMs - _heldSince[button] >= RepeatDelayMs
                     && nowMs - _lastRepeat[button] >= interval)
            {
                _lastRepeat[button] = nowMs;
                ButtonRepeated?.Invoke(button);
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

    /// <summary>How often <paramref name="button"/> repeats once held past the delay; null if it doesn't repeat.</summary>
    private static int? RepeatIntervalOf(GamepadButton button) =>
        Array.IndexOf(Moving, button) >= 0 ? RepeatIntervalMs
        : Array.IndexOf(Turning, button) >= 0 ? TurnRepeatIntervalMs
        : null;

    /// <summary>
    /// Every controller was let go (Pan hides or shows one): what is held is released now, and a button still held
    /// when the controllers come back is no new press, so the A that started a game doesn't act a second time.
    /// </summary>
    public void LetGo()
    {
        foreach (var button in _previous)
        {
            ButtonReleased?.Invoke(button);
        }

        _releasedWhileHeld.UnionWith(_previous);
        _previous = [];
    }

    /// <summary>The buttons held on any of the controllers, the sticks as directions.</summary>
    /// <param name="held">
    /// What was down in the poll before: a stick direction or trigger among them stays down until it drops below its
    /// release threshold.
    /// </param>
    public static HashSet<GamepadButton> Down(IReadOnlyList<GamepadReading> readings, IReadOnlySet<GamepadButton>? held = null)
    {
        held ??= new HashSet<GamepadButton>();
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

            AddHeldTrigger(reading.Triggers.Left, GamepadButton.LeftTrigger, held, down);
            AddHeldTrigger(reading.Triggers.Right, GamepadButton.RightTrigger, held, down);

            AddDirections(reading.LeftStick, held, down, GamepadButton.DPadUp, GamepadButton.DPadDown, GamepadButton.DPadLeft, GamepadButton.DPadRight);

            // The right stick moves a second focus, e.g. through the hits beside the on-screen keyboard.
            AddDirections(reading.RightStick, held, down, GamepadButton.RightStickUp, GamepadButton.RightStickDown, GamepadButton.RightStickLeft, GamepadButton.RightStickRight);
        }

        return down;
    }

    private static void AddHeldTrigger(double position, GamepadButton trigger, IReadOnlySet<GamepadButton> held, HashSet<GamepadButton> down)
    {
        if (held.Contains(trigger) && position >= TriggerReleaseThreshold)
        {
            down.Add(trigger);
        }
    }

    private static void AddDirections(StickPosition stick, IReadOnlySet<GamepadButton> held, HashSet<GamepadButton> down, GamepadButton up, GamepadButton downward, GamepadButton left, GamepadButton right)
    {
        AddAxis(stick.Y, up, downward, held, down);
        AddAxis(stick.X, right, left, held, down);
    }

    /// <summary>One axis of a stick: <paramref name="positive"/> or <paramref name="negative"/>, or neither.</summary>
    private static void AddAxis(double value, GamepadButton positive, GamepadButton negative, IReadOnlySet<GamepadButton> held, HashSet<GamepadButton> down)
    {
        if (value > Threshold(positive))
        {
            down.Add(positive);
        }
        else if (value < -Threshold(negative))
        {
            down.Add(negative);
        }

        double Threshold(GamepadButton direction) => held.Contains(direction) ? StickReleaseThreshold : StickThreshold;
    }
}
