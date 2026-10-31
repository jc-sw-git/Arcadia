namespace Arcadia.Input;

/// <summary>SDL's idea of what kind of gamepad a device is (<c>SDL_GamepadType</c>, same numbers).</summary>
public enum PadType
{
    Unknown = 0,
    Standard,
    Xbox360,
    XboxOne,
    PS3,
    PS4,
    PS5,
    SwitchPro,
    JoyConLeft,
    JoyConRight,
    JoyConPair,
    GameCube,
    Steam,
}

/// <summary>SDL's idea of what kind of joystick a device is (<c>SDL_JoystickType</c>, same numbers).</summary>
public enum StickDeviceType
{
    Unknown = 0,
    Gamepad,
    Wheel,
    ArcadeStick,
    FlightStick,
    DancePad,
    Guitar,
    DrumKit,
    ArcadePad,
    Throttle,
}

/// <summary>A device SDL offers as a gamepad, as far as Arcadia needs to know it. No serial number: it isn't logged.</summary>
public readonly record struct PadDevice(string Name, ushort Vendor, ushort Product, PadType Type, StickDeviceType StickType)
{
    /// <summary>The prefix of SDL's path for a pad it reads through XInput, followed by the XInput slot.</summary>
    public const string XInputPathPrefix = "XInput#";

    public const ushort ValveVendor = 0x28DE;

    /// <summary>The pad Steam Input creates for a game; it only mirrors a physical controller Arcadia reads anyway.</summary>
    public const ushort SteamVirtualProduct = 0x11FF;

    /// <summary>
    /// SDL's path of the device: <c>XInput#&lt;slot&gt;</c> for a pad read through XInput, a HID device path otherwise.
    /// Not logged: a HID path holds the device's instance id.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>The XInput slot (0–3) of a pad SDL reads through XInput, otherwise null.</summary>
    public int? XInputSlot =>
        Path.StartsWith(XInputPathPrefix, StringComparison.Ordinal) && int.TryParse(Path.AsSpan(XInputPathPrefix.Length), out var slot)
            ? slot
            : null;

    /// <summary>
    /// The name to show the player. Through XInput SDL knows only "XInput Controller #n": a known model gets its own
    /// name, any other "Xbox-kompatibler Controller".
    /// </summary>
    public string DisplayName => !Name.StartsWith("XInput Controller", StringComparison.OrdinalIgnoreCase)
        ? Name
        : (Vendor, Product) switch
        {
            (0x2DC8, 0x310B) => "8BitDo Ultimate 2",
            (0x045E, _) => "Xbox Controller",
            _ => "Xbox-kompatibler Controller",
        };

    /// <summary>For <c>arcadia.log</c>: name, SDL's type and the USB ids.</summary>
    public override string ToString() => $"\"{Name}\" ({Type}, {Vendor:X4}:{Product:X4})";
}

/// <summary>
/// Tells whether a pad is one a program emulates on a software USB host, such as SISR's virtual Xbox 360 pad (VIIPER
/// over usbip-win2) or, later, the pad of #213's helper. It mirrors a controller Arcadia reads itself, so it must not
/// count a second time. SDL alone can't tell it from a real wired Xbox 360 pad (same USB ids, path <c>XInput#n</c>;
/// #212, P3), so this looks at where the device sits in Windows' device tree.
/// </summary>
public interface IEmulatedPads
{
    bool IsEmulated(PadDevice device);

    /// <summary>
    /// The pad is Pan's copy of the Steam Controller's puck for Steam (#242): VIIPER's device on usbip-win2's software
    /// host, with the puck's ids and serial number. Arcadia reads the real puck, so the copy must not count a second time.
    /// </summary>
    bool IsCopy(PadDevice device) => false;
}

/// <summary>Where nothing emulates pads (no Windows, the tests by default).</summary>
public sealed class NoEmulatedPads : IEmulatedPads
{
    public static readonly NoEmulatedPads Instance = new();

    public bool IsEmulated(PadDevice device) => false;
}

/// <summary>
/// The one place that decides which devices Arcadia reads (#72, #212): only physical gamepads, each once. Steam's
/// virtual pads only repeat a physical controller, so a pad behaves the same with Steam Input on or off. The Steam
/// Controller is read raw, so the Xbox pad SISR emulates of it stays out.
/// </summary>
public static class GamepadDevices
{
    /// <param name="emulated">Asked for pads SDL reads through XInput, as an emulated Xbox pad is one, and for Steam Controllers.</param>
    /// <returns>Null to read the device; otherwise why not, for <c>arcadia.log</c>.</returns>
    public static string? WhyIgnored(PadDevice device, IEmulatedPads emulated)
    {
        if (device.Vendor == PadDevice.ValveVendor && device.Product == PadDevice.SteamVirtualProduct)
        {
            return "virtueller Controller von Steam";
        }

        // SDL's type Steam covers both Steam Controllers (and Hori's licensed one). Other Valve devices, such as the
        // Steam Deck's own controls, stay Steam's.
        if (device.Type == PadType.Steam)
        {
            return emulated.IsCopy(device) ? "Kopie des Steam Controllers für Steam" : null;
        }

        if (device.Vendor == PadDevice.ValveVendor)
        {
            return "Gerät von Valve, kein Steam Controller";
        }

        if (device.XInputSlot is not null && emulated.IsEmulated(device))
        {
            return "virtueller Xbox Controller, z. B. von SISR";
        }

        if (device.Type is PadType.JoyConLeft or PadType.JoyConRight)
        {
            return "einzelner Joy-Con";
        }

        // Wheels, flight sticks and the like can be in SDL's controller database, but aren't controllers for Arcadia.
        if (device.StickType is not (StickDeviceType.Unknown or StickDeviceType.Gamepad))
        {
            return $"kein Gamepad ({device.StickType})";
        }

        return null;
    }
}
