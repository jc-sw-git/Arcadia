using System.Runtime.InteropServices;

namespace Arcadia.Input;

/// <summary>SDL's error after a call that failed, e.g. <c>SDL_Init</c>.</summary>
public sealed class SdlException(string message) : Exception(message);

/// <summary>
/// The few SDL3 calls Arcadia needs, as plain P/Invoke against the official <c>SDL3.dll</c> next to Arcadia.exe
/// (pinned and checked in the build: <c>src/Arcadia.Input/Sdl3.targets</c>). No third-party wrapper.
/// </summary>
public sealed partial class SdlGamepads : ISdlGamepads
{
    private const string Lib = "SDL3";

    private const uint InitGamepad = 0x2000;
    private const uint EventGamepadAdded = 0x653;
    private const uint EventGamepadRemoved = 0x654;

    private const int AxisLeftX = 0;
    private const int AxisLeftY = 1;
    private const int AxisRightX = 2;
    private const int AxisRightY = 3;
    private const int AxisLeftTrigger = 4;
    private const int AxisRightTrigger = 5;
    private const int ButtonsRead = (int)SdlPadState.Button.Touchpad + 1;

    public string Init()
    {
        // Keep reading while a game is in front: View held and Back + Start must work beside it.
        SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");

        // SDL's own HID drivers read PlayStation and Nintendo pads raw, past Steam Input.
        SDL_SetHint("SDL_JOYSTICK_HIDAPI", "1");

        // Valve's devices stay Steam's (and SISR's) until Arcadia reads the Steam Controller itself (#212): SDL
        // writes settings to a Steam Controller it opens (lizard mode, IMU).
        SDL_SetHint("SDL_HIDAPI_IGNORE_DEVICES", "0x28de/0x0000");

        // The report mode of Bluetooth PlayStation pads changes (until they are switched off) only once Arcadia
        // rumbles them; the enhanced mode breaks DirectInput for games without SDL.
        SDL_SetHint("SDL_JOYSTICK_ENHANCED_REPORTS", "auto");
        if (!SDL_Init(InitGamepad))
        {
            throw new SdlException($"SDL_Init: {LastError()}");
        }

        var version = SDL_GetVersion();
        return $"{version / 1000000}.{version / 1000 % 1000}.{version % 1000}";
    }

    public bool NextDeviceChange(out uint id, out bool added)
    {
        while (SDL_PollEvent(out var e))
        {
            if (e.Type is EventGamepadAdded or EventGamepadRemoved)
            {
                id = e.Which;
                added = e.Type == EventGamepadAdded;
                return true;
            }
        }

        id = 0;
        added = false;
        return false;
    }

    public PadDevice Describe(uint id) => new(
        Marshal.PtrToStringUTF8(SDL_GetGamepadNameForID(id)) ?? string.Empty,
        SDL_GetGamepadVendorForID(id),
        SDL_GetGamepadProductForID(id),
        (PadType)SDL_GetRealGamepadTypeForID(id),
        (StickDeviceType)SDL_GetJoystickTypeForID(id));

    public nint Open(uint id) => SDL_OpenGamepad(id);

    public string LastError() => Marshal.PtrToStringUTF8(SDL_GetError()) ?? string.Empty;

    public void Update() => SDL_UpdateGamepads();

    public SdlPadState Read(nint pad)
    {
        uint buttons = 0;
        for (var button = 0; button < ButtonsRead; button++)
        {
            if (SDL_GetGamepadButton(pad, button))
            {
                buttons |= 1u << button;
            }
        }

        return new SdlPadState(
            buttons,
            SDL_GetGamepadAxis(pad, AxisLeftX),
            SDL_GetGamepadAxis(pad, AxisLeftY),
            SDL_GetGamepadAxis(pad, AxisRightX),
            SDL_GetGamepadAxis(pad, AxisRightY),
            SDL_GetGamepadAxis(pad, AxisLeftTrigger),
            SDL_GetGamepadAxis(pad, AxisRightTrigger));
    }

    // A pad without motors reports an error; nothing to do about it.
    public void Rumble(nint pad, ushort strength, uint durationMs) => _ = SDL_RumbleGamepad(pad, strength, strength, durationMs);

    public void Close(nint pad) => SDL_CloseGamepad(pad);

    public void Quit() => SDL_Quit();

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SDL_SetHint(string name, string value);

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SDL_Init(uint flags);

    [LibraryImport(Lib)]
    private static partial void SDL_Quit();

    [LibraryImport(Lib)]
    private static partial nint SDL_GetError();

    [LibraryImport(Lib)]
    private static partial int SDL_GetVersion();

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SDL_PollEvent(out SdlEvent sdlEvent);

    [LibraryImport(Lib)]
    private static partial nint SDL_GetGamepadNameForID(uint id);

    [LibraryImport(Lib)]
    private static partial ushort SDL_GetGamepadVendorForID(uint id);

    [LibraryImport(Lib)]
    private static partial ushort SDL_GetGamepadProductForID(uint id);

    [LibraryImport(Lib)]
    private static partial int SDL_GetRealGamepadTypeForID(uint id);

    [LibraryImport(Lib)]
    private static partial int SDL_GetJoystickTypeForID(uint id);

    [LibraryImport(Lib)]
    private static partial nint SDL_OpenGamepad(uint id);

    [LibraryImport(Lib)]
    private static partial void SDL_CloseGamepad(nint gamepad);

    [LibraryImport(Lib)]
    private static partial void SDL_UpdateGamepads();

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SDL_GetGamepadButton(nint gamepad, int button);

    [LibraryImport(Lib)]
    private static partial short SDL_GetGamepadAxis(nint gamepad, int axis);

    [LibraryImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool SDL_RumbleGamepad(nint gamepad, ushort low, ushort high, uint durationMs);

    /// <summary>SDL_Event is a 128-byte union; only the type and, for gamepad events, the device id are read.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct SdlEvent
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(16)]
        public uint Which;
    }
}
