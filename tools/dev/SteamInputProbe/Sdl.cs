using System.Runtime.InteropServices;

namespace SteamInputProbe;

/// <summary>The few SDL3 calls the raw probe needs (SDL 3.4.0+ has the driver for the new Steam Controller).</summary>
internal static class Sdl
{
    private const string Lib = "SDL3";

    public const uint InitGamepad = 0x2000;
    public const uint InitSensor = 0x8000;

    public const uint EventGamepadAxisMotion = 0x650;
    public const uint EventGamepadButtonDown = 0x651;
    public const uint EventGamepadButtonUp = 0x652;
    public const uint EventGamepadAdded = 0x653;
    public const uint EventGamepadRemoved = 0x654;
    public const uint EventGamepadTouchpadDown = 0x656;
    public const uint EventGamepadTouchpadMotion = 0x657;
    public const uint EventGamepadTouchpadUp = 0x658;
    public const uint EventGamepadSensorUpdate = 0x659;

    public const int SensorAccel = 1;
    public const int SensorGyro = 2;

    [DllImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool SDL_SetHint([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    [DllImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool SDL_Init(uint flags);

    [DllImport(Lib)]
    public static extern void SDL_Quit();

    [DllImport(Lib)]
    public static extern IntPtr SDL_GetError();

    [DllImport(Lib)]
    public static extern int SDL_GetVersion();

    [DllImport(Lib)]
    public static extern IntPtr SDL_OpenGamepad(uint id);

    [DllImport(Lib)]
    public static extern void SDL_CloseGamepad(IntPtr gamepad);

    [DllImport(Lib)]
    public static extern IntPtr SDL_GetGamepadName(IntPtr gamepad);

    [DllImport(Lib)]
    public static extern IntPtr SDL_GetGamepadPath(IntPtr gamepad);

    [DllImport(Lib)]
    public static extern ushort SDL_GetGamepadVendor(IntPtr gamepad);

    [DllImport(Lib)]
    public static extern ushort SDL_GetGamepadProduct(IntPtr gamepad);

    [DllImport(Lib)]
    public static extern ulong SDL_GetGamepadSteamHandle(IntPtr gamepad);

    [DllImport(Lib)]
    public static extern int SDL_GetNumGamepadTouchpads(IntPtr gamepad);

    [DllImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool SDL_GamepadHasSensor(IntPtr gamepad, int sensor);

    [DllImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool SDL_SetGamepadSensorEnabled(IntPtr gamepad, int sensor, [MarshalAs(UnmanagedType.U1)] bool enabled);

    [DllImport(Lib)]
    public static extern IntPtr SDL_GetGamepadStringForButton(int button);

    [DllImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool SDL_WaitEventTimeout(out SdlEvent sdlEvent, int timeoutMs);

    public static string Text(IntPtr utf8) => Marshal.PtrToStringUTF8(utf8) ?? "";
}

/// <summary>SDL_Event is a 128-byte union; the probe reads the few fields it needs by their offsets.</summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
internal struct SdlEvent
{
    [FieldOffset(0)] public uint Type;
    [FieldOffset(16)] public uint Which;
    // Button and axis events: the button or axis index, then (button) down.
    [FieldOffset(20)] public byte ButtonOrAxis;
    [FieldOffset(21)] public byte Down;
    [FieldOffset(24)] public short AxisValue;
    // Touchpad events: touchpad, finger, x, y, pressure. Sensor events: sensor, data[3].
    [FieldOffset(20)] public int TouchpadOrSensor;
    [FieldOffset(24)] public int Finger;
    [FieldOffset(28)] public float TouchX;
    [FieldOffset(32)] public float TouchY;
    [FieldOffset(24)] public float Data0;
    [FieldOffset(28)] public float Data1;
    [FieldOffset(32)] public float Data2;
}
