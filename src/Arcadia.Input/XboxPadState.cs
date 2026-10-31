namespace Arcadia.Input;

/// <summary>
/// What a virtual Xbox 360 controller reports (#213): XInput's button bits, triggers 0–255 and sticks as XInput counts
/// them (up and right positive).
/// </summary>
public readonly record struct XboxPadState(ushort Buttons, byte LeftTrigger, byte RightTrigger, short LeftX, short LeftY, short RightX, short RightY)
{
    // PadButtons uses XInput's bits, except for the two unused ones that carry the triggers as buttons.
    private const ushort XInputButtons = 0xF3FF;

    /// <summary>XInput's Guide bit (0x0400), which <see cref="PadButtons"/> uses for the left trigger.</summary>
    public const ushort GuideBit = 0x0400;

    public static XboxPadState Neutral => default;

    /// <param name="buttons">The reading's buttons after <see cref="ComboWithholding"/>.</param>
    /// <param name="withGuide">Guide/PS/Home passes on (#238 D6); not for the Steam Controller, whose Steam button is Steam's.</param>
    public static XboxPadState From(GamepadReading reading, PadButtons buttons, bool withGuide = false) => new(
        (ushort)(((ushort)buttons & XInputButtons) | (withGuide && reading.Guide ? GuideBit : 0)),
        TriggerByte(reading.Triggers.Left),
        TriggerByte(reading.Triggers.Right),
        Axis(reading.LeftStick.X),
        Axis(reading.LeftStick.Y),
        Axis(reading.RightStick.X),
        Axis(reading.RightStick.Y));

    /// <summary>A game's rumble on the pad as SDL takes it: each motor from 0–255 to 0–65535.</summary>
    public static (ushort Low, ushort High) RumbleStrengths(XboxRumble rumble) => ((ushort)(rumble.Large * 257), (ushort)(rumble.Small * 257));

    private static byte TriggerByte(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);

    private static short Axis(double value) => (short)Math.Round(Math.Clamp(value, -1, 1) * 32767);
}

/// <summary>What a game asks of a virtual Xbox 360 controller's motors: the large (low frequency) and small one, 0–255.</summary>
public readonly record struct XboxRumble(byte Large, byte Small);
