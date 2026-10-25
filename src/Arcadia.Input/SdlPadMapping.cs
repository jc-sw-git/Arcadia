namespace Arcadia.Input;

/// <summary>
/// What SDL reports for one gamepad at one moment: <see cref="Buttons"/> has bit <c>n</c> set while
/// <c>SDL_GamepadButton</c> <c>n</c> is held; the axes as SDL gives them (sticks -32768 to 32767 with down positive,
/// triggers 0 to 32767).
/// </summary>
public readonly record struct SdlPadState(uint Buttons, short LeftX, short LeftY, short RightX, short RightY, short LeftTrigger, short RightTrigger)
{
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
    }

    public static uint Bit(Button button) => 1u << (int)button;
}

/// <summary>
/// Turns SDL's gamepad state into Arcadia's <see cref="GamepadReading"/>. By position, not by label (#72): the bottom
/// face button is A on every controller (Cross, Nintendo's B), the right one B, and so on. Guide/PS/Home, Capture,
/// the touchpad click and paddles have no function, so they behave the same with and without Steam.
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

        return new GamepadReading(padId, buttons, Stick(state.LeftX, state.LeftY), Stick(state.RightX, state.RightY));
    }

    // SDL counts down as positive, Arcadia up. short.MinValue reaches a little further than short.MaxValue; clamped so both ends are 1.
    private static StickPosition Stick(short x, short y) =>
        new(Math.Max(x / 32767.0, -1), Math.Clamp(-y / 32767.0, -1, 1));
}
