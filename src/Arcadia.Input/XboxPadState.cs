namespace Arcadia.Input;

/// <summary>
/// What a virtual Xbox 360 controller reports (#213): XInput's button bits, triggers 0–255 and sticks as XInput counts
/// them (up and right positive).
/// </summary>
public readonly record struct XboxPadState(ushort Buttons, byte LeftTrigger, byte RightTrigger, short LeftX, short LeftY, short RightX, short RightY)
{
    // PadButtons uses XInput's bits, except for the two unused ones that carry the triggers as buttons.
    private const ushort XInputButtons = 0xF3FF;

    public static XboxPadState Neutral => default;

    /// <param name="buttons">The reading's buttons after <see cref="ComboWithholding"/>.</param>
    public static XboxPadState From(GamepadReading reading, PadButtons buttons) => new(
        (ushort)((ushort)buttons & XInputButtons),
        TriggerByte(reading.Triggers.Left),
        TriggerByte(reading.Triggers.Right),
        Axis(reading.LeftStick.X),
        Axis(reading.LeftStick.Y),
        Axis(reading.RightStick.X),
        Axis(reading.RightStick.Y));

    private static byte TriggerByte(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);

    private static short Axis(double value) => (short)Math.Round(Math.Clamp(value, -1, 1) * 32767);
}
