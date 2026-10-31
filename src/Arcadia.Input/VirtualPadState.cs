namespace Arcadia.Input;

/// <summary>
/// What an emulated pad of any kind reports (#238 part 2): one controller's reading after <see cref="ComboWithholding"/>,
/// everything a 1:1 kind can carry. Each kind takes what it has (<c>VirtualPadReports</c>); the Xbox 360 pad takes
/// <see cref="XboxPadState.From(VirtualPadState)"/>.
/// </summary>
/// <param name="Buttons">The buttons by position, after the combos were held back; the trigger bits are pressed past half way.</param>
/// <param name="Touchpad">The PlayStation pad's touchpad (first finger) and its click.</param>
/// <param name="Motion">Gyro and accelerometer in SDL's units and axes; off while the controller sends none.</param>
public readonly record struct VirtualPadState(
    PadButtons Buttons,
    bool Guide,
    ExtraButtons Extra,
    StickPosition LeftStick,
    StickPosition RightStick,
    TriggerPosition Triggers,
    TouchpadReading Touchpad,
    MotionReading Motion)
{
    /// <summary>Everything let go; no motion, which each kind reports as lying flat and still.</summary>
    public static VirtualPadState Neutral => default;

    /// <param name="buttons">The reading's buttons after <see cref="ComboWithholding"/>.</param>
    public static VirtualPadState From(GamepadReading reading, PadButtons buttons) => new(
        buttons,
        reading.Guide,
        reading.Extra,
        reading.LeftStick,
        reading.RightStick,
        reading.Triggers,
        reading.LeftTouchpad,
        reading.Motion);
}

/// <summary>A light bar's colour a game set on an emulated PlayStation pad (#238 A5).</summary>
public readonly record struct PadLight(byte Red, byte Green, byte Blue);
