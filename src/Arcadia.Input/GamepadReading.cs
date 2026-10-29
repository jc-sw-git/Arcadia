namespace Arcadia.Input;

/// <summary>The buttons of one controller in Xbox naming, by position, plus the triggers pressed past half way.</summary>
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

    // The bits XInput left unused; Arcadia puts the triggers there.
    LeftTrigger = 0x0400,
    RightTrigger = 0x0800,

    A = 0x1000,
    B = 0x2000,
    X = 0x4000,
    Y = 0x8000,
}

/// <summary>How far each trigger is pulled, from 0 (let go) to 1 (fully).</summary>
public readonly record struct TriggerPosition(double Left, double Right);

/// <summary>A stick's position, each axis from -1 to 1; up and right are positive.</summary>
public readonly record struct StickPosition(double X, double Y);

/// <summary>
/// A touchpad (the Steam Controller's trackpads, #212): touched and where, each axis from -1 to 1 with up and right
/// positive like a stick, and whether it is clicked. Not touched: the position means nothing.
/// </summary>
public readonly record struct TouchpadReading(bool Touched, double X, double Y, bool Clicked);

/// <summary>A motion sensor's three axes as SDL gives them (gyro in rad/s, accelerometer in m/s²).</summary>
public readonly record struct SensorAxes(float X, float Y, float Z);

/// <summary>Gyro and accelerometer; <see cref="IsOn"/> is false while they are off or the controller has none.</summary>
public readonly record struct MotionReading(bool IsOn, SensorAxes Gyro, SensorAxes Accelerometer);

/// <summary>What one controller reports in one poll. <see cref="PadId"/> tells the controllers apart; SDL gives a new one on every connect.</summary>
public readonly record struct GamepadReading(int PadId, PadButtons Buttons, StickPosition LeftStick, StickPosition RightStick)
{
    /// <summary>The triggers as analog values; <see cref="Buttons"/> has them only as pressed past half way.</summary>
    public TriggerPosition Triggers { get; init; }

    /// <summary>The Steam Controller's left trackpad, or a PlayStation pad's touchpad. No consumer yet (#111).</summary>
    public TouchpadReading LeftTouchpad { get; init; }

    /// <summary>The Steam Controller's right trackpad. No consumer yet (#111).</summary>
    public TouchpadReading RightTouchpad { get; init; }

    /// <summary>Only while switched on (<see cref="SdlGamepadSource.UseMotionSensors"/>). No consumer yet (#111).</summary>
    public MotionReading Motion { get; init; }

    /// <summary>SDL's type of the controller: which button symbols the hints show (#215).</summary>
    public PadType Type { get; init; }

    /// <summary>A Steam Controller, read raw (#212); "connected" for the per-game mode follows it.</summary>
    public bool IsSteamController => Type == PadType.Steam;

    public bool IsDown(PadButtons buttons) => (Buttons & buttons) == buttons;
}
