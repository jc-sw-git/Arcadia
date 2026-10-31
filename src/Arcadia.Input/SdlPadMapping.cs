namespace Arcadia.Input;

/// <summary>
/// What SDL reports for one gamepad at one moment: <see cref="Buttons"/> has bit <c>n</c> set while
/// <c>SDL_GamepadButton</c> <c>n</c> is held; the axes as SDL gives them (sticks -32768 to 32767 with down positive,
/// triggers 0 to 32767). Touchpads and motion sensors only on controllers that have them.
/// </summary>
public readonly record struct SdlPadState(uint Buttons, short LeftX, short LeftY, short RightX, short RightY, short LeftTrigger, short RightTrigger)
{
    /// <summary>SDL's first touchpad: the Steam Controller's left trackpad, a PlayStation pad's only touchpad.</summary>
    public SdlTouch FirstTouchpad { get; init; }

    /// <summary>SDL's second touchpad: the Steam Controller's right trackpad.</summary>
    public SdlTouch SecondTouchpad { get; init; }

    /// <summary>Gyro and accelerometer, only while switched on (<see cref="ISdlGamepads.SetMotionSensors"/>).</summary>
    public MotionReading Motion { get; init; }

    /// <summary>The <c>SDL_GamepadButton</c> numbers Arcadia reads; the order is SDL's.</summary>
    public enum Button
    {
        South,
        East,
        West,
        North,
        Back,
        Guide,
        Start,
        LeftStick,
        RightStick,
        LeftShoulder,
        RightShoulder,
        DPadUp,
        DPadDown,
        DPadLeft,
        DPadRight,
        Misc1,
        RightPaddle1,
        LeftPaddle1,
        RightPaddle2,
        LeftPaddle2,
        Touchpad,
        Misc2,
    }

    public static uint Bit(Button button) => 1u << (int)button;
}

/// <summary>The first finger on one of SDL's touchpads: position from 0 to 1, left to right and top to bottom.</summary>
public readonly record struct SdlTouch(bool Down, float X, float Y, float Pressure);

/// <summary>
/// Turns SDL's gamepad state into Arcadia's <see cref="GamepadReading"/>. By position, not by label (#72): the bottom
/// face button is A on every controller (Cross, Nintendo's B), the right one B, and so on. Guide/PS/Home/Steam,
/// Capture/"…", the touchpad clicks and paddles have no function, so they behave the same with and without Steam
/// (#212: Steam button and "…" are Steam's, the back grips assignable later). The trackpads come along without a
/// function yet (#111).
/// </summary>
public static class SdlPadMapping
{
    /// <summary>Past half way, as with XInput before.</summary>
    private const short TriggerThreshold = 16384;

    private static readonly (SdlPadState.Button From, PadButtons To)[] Buttons =
    [
        (SdlPadState.Button.South, PadButtons.A),
        (SdlPadState.Button.East, PadButtons.B),
        (SdlPadState.Button.West, PadButtons.X),
        (SdlPadState.Button.North, PadButtons.Y),
        (SdlPadState.Button.Back, PadButtons.View),
        (SdlPadState.Button.Start, PadButtons.Menu),
        (SdlPadState.Button.LeftStick, PadButtons.LeftStickClick),
        (SdlPadState.Button.RightStick, PadButtons.RightStickClick),
        (SdlPadState.Button.LeftShoulder, PadButtons.LeftShoulder),
        (SdlPadState.Button.RightShoulder, PadButtons.RightShoulder),
        (SdlPadState.Button.DPadUp, PadButtons.DPadUp),
        (SdlPadState.Button.DPadDown, PadButtons.DPadDown),
        (SdlPadState.Button.DPadLeft, PadButtons.DPadLeft),
        (SdlPadState.Button.DPadRight, PadButtons.DPadRight),
    ];

    public static GamepadReading ToReading(int padId, SdlPadState state)
    {
        var buttons = PadButtons.None;
        foreach (var (from, to) in Buttons)
        {
            if ((state.Buttons & SdlPadState.Bit(from)) != 0)
            {
                buttons |= to;
            }
        }

        if (state.LeftTrigger >= TriggerThreshold)
        {
            buttons |= PadButtons.LeftTrigger;
        }

        if (state.RightTrigger >= TriggerThreshold)
        {
            buttons |= PadButtons.RightTrigger;
        }

        return new GamepadReading(padId, buttons, Stick(state.LeftX, state.LeftY), Stick(state.RightX, state.RightY))
        {
            Triggers = new TriggerPosition(Trigger(state.LeftTrigger), Trigger(state.RightTrigger)),
            // SDL's Steam Controller mapping (3.4) clicks the left trackpad as "touchpad" and the right one as "misc2".
            LeftTouchpad = Touch(state.FirstTouchpad, (state.Buttons & SdlPadState.Bit(SdlPadState.Button.Touchpad)) != 0),
            RightTouchpad = Touch(state.SecondTouchpad, (state.Buttons & SdlPadState.Bit(SdlPadState.Button.Misc2)) != 0),
            Motion = state.Motion,
            Guide = (state.Buttons & SdlPadState.Bit(SdlPadState.Button.Guide)) != 0,
            Extra = Extra(state.Buttons),
        };
    }

    private static readonly (SdlPadState.Button From, ExtraButtons To)[] Extras =
    [
        (SdlPadState.Button.Misc1, ExtraButtons.Misc1),
        (SdlPadState.Button.Misc2, ExtraButtons.Misc2),
        (SdlPadState.Button.LeftPaddle1, ExtraButtons.LeftPaddle1),
        (SdlPadState.Button.RightPaddle1, ExtraButtons.RightPaddle1),
        (SdlPadState.Button.LeftPaddle2, ExtraButtons.LeftPaddle2),
        (SdlPadState.Button.RightPaddle2, ExtraButtons.RightPaddle2),
    ];

    private static ExtraButtons Extra(uint sdlButtons)
    {
        var extra = ExtraButtons.None;
        foreach (var (from, to) in Extras)
        {
            if ((sdlButtons & SdlPadState.Bit(from)) != 0)
            {
                extra |= to;
            }
        }

        return extra;
    }

    private static double Trigger(short value) => Math.Clamp(value / 32767.0, 0, 1);

    // Arcadia's up is positive, as for the sticks; the touchpad's centre is 0.
    private static TouchpadReading Touch(SdlTouch touch, bool clicked) =>
        new(touch.Down, touch.X * 2 - 1, 1 - touch.Y * 2, clicked);

    // SDL counts down as positive, Arcadia up. short.MinValue reaches a little further than short.MaxValue; clamped so both ends are 1.
    private static StickPosition Stick(short x, short y) =>
        new(Math.Max(x / 32767.0, -1), Math.Clamp(-y / 32767.0, -1, 1));
}
