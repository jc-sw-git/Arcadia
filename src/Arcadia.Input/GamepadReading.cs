namespace Arcadia.Input;

/// <summary>The buttons of one controller as XInput reports them, plus the triggers pressed past half way.</summary>
[Flags]
public enum PadButtons : ushort
{
    None = 0,
    DPadUp = 0x0001,
    DPadDown = 0x0002,
    DPadLeft = 0x0004,
    DPadRight = 0x0008,
    Menu = 0x0010,
    View = 0x0020,
    LeftStickClick = 0x0040,
    RightStickClick = 0x0080,
    LeftShoulder = 0x0100,
    RightShoulder = 0x0200,

    // XInputGetState leaves these two bits unused; Arcadia puts the triggers there.
    LeftTrigger = 0x0400,
    RightTrigger = 0x0800,

    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

/// <summary>A stick's position, each axis from -1 to 1; up and right are positive.</summary>
public readonly record struct StickPosition(double X, double Y);

/// <summary>What one controller reports in one poll. <see cref="Slot"/> is its XInput slot (0–3).</summary>
public readonly record struct GamepadReading(int Slot, PadButtons Buttons, StickPosition LeftStick, StickPosition RightStick)
{
    public bool IsDown(PadButtons buttons) => (Buttons & buttons) == buttons;
}
