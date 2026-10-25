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
    public const ushort ValveVendor = 0x28DE;

    /// <summary>The pad Steam Input creates for a game; it only mirrors a physical controller Arcadia reads anyway.</summary>
    public const ushort SteamVirtualProduct = 0x11FF;

    /// <summary>For <c>arcadia.log</c>: name, SDL's type and the USB ids.</summary>
    public override string ToString() => $"\"{Name}\" ({Type}, {Vendor:X4}:{Product:X4})";
}

/// <summary>
/// The one place that decides which devices Arcadia reads (#72): only physical gamepads, each once. Steam's virtual
/// pads only repeat a physical controller, so a pad behaves the same with Steam Input on or off. The Steam Controller
/// still arrives as SISR's virtual Xbox pad until Arcadia reads it itself (#212), so its physical device stays out too.
/// </summary>
public static class GamepadDevices
{
    /// <returns>Null to read the device; otherwise why not, for <c>arcadia.log</c>.</returns>
    public static string? WhyIgnored(PadDevice device)
    {
        if (device.Vendor == PadDevice.ValveVendor && device.Product == PadDevice.SteamVirtualProduct)
        {
            return "virtueller Controller von Steam";
        }

        if (device.Vendor == PadDevice.ValveVendor || device.Type == PadType.Steam)
        {
            return "Steam Controller, kommt über SISR";
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
