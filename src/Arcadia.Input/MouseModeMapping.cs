namespace Arcadia.Input;

/// <summary>
/// The controller as a mouse: left stick pointer, A/RT left button, X/LT right button, right stick
/// wheel, D-pad arrows, B Esc, LB/RB Shift+Tab/Tab, Menu Enter, L3 the slow pointer. View is left
/// to <see cref="ReturnWithView"/>. All controllers act together.
/// </summary>
/// <remarks>Runs on the gamepad's poll thread, like <see cref="MouseMode"/>.</remarks>
internal sealed class MouseModeMapping
{
    /// <summary>Held arrows repeat like a keyboard's: after this delay, then every interval.</summary>
    private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(400);

    private static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(90);

    /// <summary>L3 held this long without moving the pointer counts as held: the slow pointer ends on release.</summary>
    private static readonly TimeSpan SlowHoldTime = TimeSpan.FromMilliseconds(500);

    /// <summary>A stalled poll (e.g. the PC woke from sleep) must not fling the pointer across the screen.</summary>
    private static readonly TimeSpan MaxStep = TimeSpan.FromMilliseconds(50);

    /// <summary>The wheel turns in quarter notches: smooth where an app scrolls by the delta, and never more than a notch per poll.</summary>
    private const int WheelStep = StickCurve.WheelNotch / 4;

    private static readonly (PadButtons Button, OutputKey Key)[] Arrows =
    [
        (PadButtons.DPadUp, OutputKey.Up),
        (PadButtons.DPadDown, OutputKey.Down),
        (PadButtons.DPadLeft, OutputKey.Left),
        (PadButtons.DPadRight, OutputKey.Right),
    ];

    private readonly IMouseKeyboardOutput _output;
    private readonly TimeProvider _time;
    private readonly LatchGesture _slow;
    private readonly SoloButtons _solo;
    private readonly Dictionary<OutputKey, long> _nextRepeat = [];
    private readonly HashSet<OutputKey> _keysDown = [];

    // Held when the mode began; ignored until let go, so a held A doesn't click.
    private PadButtons _ignored;
    private PadButtons _previous;
    private bool _leftDown;
    private bool _rightDown;
    private long _lastUpdate;
    private double _pointerRestX;
    private double _pointerRestY;
    private double _wheelRestVertical;
    private double _wheelRestHorizontal;

    public MouseModeMapping(IMouseKeyboardOutput output, TimeProvider time)
    {
        _output = output;
        _time = time;
        _slow = new LatchGesture(SlowHoldTime, time);

        // Only on release: Menu held for the combination must not press Enter in a dialog first.
        _solo = new SoloButtons(GamepadButton.View, GamepadButton.Menu, soloDelay: null, time);
        _solo.Pressed += OnSoloPressed;
    }

    public bool IsSlow => _slow.IsOn;

    public void Begin(PadButtons held)
    {
        _ignored = held;
        _previous = PadButtons.None;
        _lastUpdate = _time.GetTimestamp();
        _pointerRestX = _pointerRestY = _wheelRestVertical = _wheelRestHorizontal = 0;
        _slow.Reset();
        _solo.Reset();
    }

    /// <summary>Lets go of every button and key the mode holds down.</summary>
    public void End()
    {
        SetButton(MouseButton.Left, ref _leftDown, false);
        SetButton(MouseButton.Right, ref _rightDown, false);
        foreach (var key in _keysDown.ToList())
        {
            KeyUp(key);
        }

        _slow.Reset();
        _solo.Reset();
    }

    public void Update(IReadOnlyList<GamepadReading> pads, int pointerSpeed)
    {
        var now = _time.GetTimestamp();
        var step = _time.GetElapsedTime(_lastUpdate, now);
        _lastUpdate = now;
        if (step > MaxStep)
        {
            step = MaxStep;
        }

        var merged = PadButtons.None;
        foreach (var pad in pads)
        {
            merged |= pad.Buttons;
        }

        _ignored &= merged;
        var current = merged & ~_ignored;
        var pressed = current & ~_previous;
        var released = _previous & ~current;
        _previous = current;

        SetButton(MouseButton.Left, ref _leftDown, (current & (PadButtons.A | PadButtons.RightTrigger)) != 0);
        SetButton(MouseButton.Right, ref _rightDown, (current & (PadButtons.X | PadButtons.LeftTrigger)) != 0);

        FollowKey(PadButtons.B, OutputKey.Escape, pressed, released);
        FollowKey(PadButtons.RightShoulder, OutputKey.Tab, pressed, released);
        if ((pressed & PadButtons.LeftShoulder) != 0)
        {
            KeyDown(OutputKey.Shift);
            KeyDown(OutputKey.Tab);
        }
        else if ((released & PadButtons.LeftShoulder) != 0)
        {
            KeyUp(OutputKey.Tab);
            KeyUp(OutputKey.Shift);
        }

        foreach (var (button, key) in Arrows)
        {
            FollowKey(button, key, pressed, released);
            if ((current & button) != 0 && _nextRepeat.TryGetValue(key, out var due) && now >= due)
            {
                _output.SetKey(key, true);
                _nextRepeat[key] = now + Ticks(RepeatInterval);
            }
        }

        if ((pressed & PadButtons.LeftStickClick) != 0)
        {
            _slow.Press();
        }
        else if ((released & PadButtons.LeftStickClick) != 0)
        {
            _slow.Release();
        }

        Solo(PadButtons.View, GamepadButton.View, pressed, released);
        Solo(PadButtons.Menu, GamepadButton.Menu, pressed, released);

        MovePointer(pads, pointerSpeed, step);
        TurnWheel(pads, step);
    }

    private void MovePointer(IReadOnlyList<GamepadReading> pads, int speed, TimeSpan step)
    {
        double x = 0, y = 0;
        foreach (var pad in pads)
        {
            var (vx, vy) = StickCurve.PointerVelocity(pad.LeftStick, speed, _slow.IsOn);
            x += vx;
            y += vy;
        }

        if (x == 0 && y == 0)
        {
            // Let go: the next push starts from the pixel, not from a remainder of the last one.
            _pointerRestX = _pointerRestY = 0;
            return;
        }

        _pointerRestX += x * step.TotalSeconds;
        _pointerRestY += y * step.TotalSeconds;
        var dx = (int)Math.Truncate(_pointerRestX);
        var dy = (int)Math.Truncate(_pointerRestY);
        _pointerRestX -= dx;
        _pointerRestY -= dy;
        if (dx != 0 || dy != 0)
        {
            // Moving doesn't make an L3 press a hold: a short click toggles while pointing too (owner, 2026-10-20).
            _output.MovePointer(dx, dy);
        }
    }

    private void TurnWheel(IReadOnlyList<GamepadReading> pads, TimeSpan step)
    {
        double vertical = 0, horizontal = 0;
        foreach (var pad in pads)
        {
            var (v, h) = StickCurve.WheelVelocity(pad.RightStick);
            vertical += v;
            horizontal += h;
        }

        if (vertical == 0 && horizontal == 0)
        {
            _wheelRestVertical = _wheelRestHorizontal = 0;
            return;
        }

        _wheelRestVertical += vertical * step.TotalSeconds;
        _wheelRestHorizontal += horizontal * step.TotalSeconds;
        var dv = (int)Math.Truncate(_wheelRestVertical / WheelStep) * WheelStep;
        var dh = (int)Math.Truncate(_wheelRestHorizontal / WheelStep) * WheelStep;
        _wheelRestVertical -= dv;
        _wheelRestHorizontal -= dh;
        if (dv != 0 || dh != 0)
        {
            _output.Scroll(dv, dh);
        }
    }

    private void Solo(PadButtons pad, GamepadButton button, PadButtons pressed, PadButtons released)
    {
        if ((pressed & pad) != 0)
        {
            _solo.Press(button);
        }
        else if ((released & pad) != 0)
        {
            _solo.Release(button);
        }
    }

    private void OnSoloPressed(GamepadButton button)
    {
        if (button == GamepadButton.Menu)
        {
            // Acts on release, so Enter is a tap: down and up at once.
            _output.SetKey(OutputKey.Enter, true);
            _output.SetKey(OutputKey.Enter, false);
        }
    }

    private void FollowKey(PadButtons button, OutputKey key, PadButtons pressed, PadButtons released)
    {
        if ((pressed & button) != 0)
        {
            KeyDown(key);
        }
        else if ((released & button) != 0)
        {
            KeyUp(key);
        }
    }

    private void KeyDown(OutputKey key)
    {
        _keysDown.Add(key);
        _output.SetKey(key, true);
        if (key is OutputKey.Up or OutputKey.Down or OutputKey.Left or OutputKey.Right)
        {
            _nextRepeat[key] = _time.GetTimestamp() + Ticks(RepeatDelay);
        }
    }

    private void KeyUp(OutputKey key)
    {
        _nextRepeat.Remove(key);
        if (_keysDown.Remove(key))
        {
            _output.SetKey(key, false);
        }
    }

    private void SetButton(MouseButton button, ref bool isDown, bool down)
    {
        if (isDown != down)
        {
            isDown = down;
            _output.SetButton(button, down);
        }
    }

    /// <summary>A span in the units of <see cref="TimeProvider.GetTimestamp"/>.</summary>
    private long Ticks(TimeSpan span) => (long)(span.TotalSeconds * _time.TimestampFrequency);
}
