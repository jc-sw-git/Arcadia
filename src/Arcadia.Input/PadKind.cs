namespace Arcadia.Input;

/// <summary>The kinds of pad Arcadia can emulate for a game (#238 D3): VIIPER's virtual devices.</summary>
public enum PadKind
{
    Xbox360,
    DualShock4,
    DualSense,
    DualSenseEdge,
    Switch2Pro,
}

/// <summary>Which kind a controller is emulated as.</summary>
public static class PadKinds
{
    private const ushort Sony = 0x054C;
    private const ushort DualSenseEdgeProduct = 0x0DF2;
    private const ushort Nintendo = 0x057E;
    private const ushort Switch2ProProduct = 0x2069;

    /// <summary>
    /// A controller's own kind where VIIPER has it (D3), every other one an Xbox 360 pad (D3b): SDL's type for the
    /// PlayStation pads (licensed ones included), the USB ids for the Edge and the Switch 2 Pro, which SDL types like
    /// their plain siblings. The Switch Pro of the first Switch, the GameCube pad and the PS3 pad have no 1:1 kind.
    /// </summary>
    public static PadKind Default(PadDevice device) => device switch
    {
        { Vendor: Sony, Product: DualSenseEdgeProduct } => PadKind.DualSenseEdge,
        { Vendor: Nintendo, Product: Switch2ProProduct } => PadKind.Switch2Pro,
        { Type: PadType.PS5 } => PadKind.DualSense,
        { Type: PadType.PS4 } => PadKind.DualShock4,
        _ => PadKind.Xbox360,
    };

    /// <summary>The kind's name, as the settings show it.</summary>
    public static string DisplayName(PadKind kind) => kind switch
    {
        PadKind.DualShock4 => "DualShock 4",
        PadKind.DualSense => "DualSense",
        PadKind.DualSenseEdge => "DualSense Edge",
        PadKind.Switch2Pro => "Switch 2 Pro Controller",
        _ => "Xbox 360 Controller",
    };

    /// <summary>Whether the kind carries gyro and accelerometer: the controller's motion sensors must be read for it.</summary>
    public static bool HasMotion(PadKind kind) => kind != PadKind.Xbox360;
}
