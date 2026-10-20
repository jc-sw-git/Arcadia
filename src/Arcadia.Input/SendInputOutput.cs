using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.Input;

/// <summary>
/// Sends the mouse mode's input to Windows with <c>SendInput</c>, as a real mouse and keyboard would.
/// Windows doesn't let it reach the secure desktop (UAC) or windows of programs running as administrator.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed partial class SendInputOutput : IMouseKeyboardOutput
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;

    private const uint MouseMove = 0x0001;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint MouseRightDown = 0x0008;
    private const uint MouseRightUp = 0x0010;
    private const uint MouseWheel = 0x0800;
    private const uint MouseHorizontalWheel = 0x1000;
    private const uint MouseVirtualDesk = 0x4000;
    private const uint MouseAbsolute = 0x8000;

    private const uint KeyExtended = 0x0001;
    private const uint KeyUp = 0x0002;

    private const int VirtualScreenX = 76;
    private const int VirtualScreenY = 77;
    private const int VirtualScreenWidth = 78;
    private const int VirtualScreenHeight = 79;

    /// <summary>
    /// Moves to an absolute position: relative moves would be scaled by the user's mouse speed and
    /// "Zeigerbeschleunigung verbessern", so the stick would feel different on every PC.
    /// </summary>
    public void MovePointer(int dx, int dy)
    {
        if (!GetCursorPos(out var cursor))
        {
            return;
        }

        var left = GetSystemMetrics(VirtualScreenX);
        var top = GetSystemMetrics(VirtualScreenY);
        var width = GetSystemMetrics(VirtualScreenWidth);
        var height = GetSystemMetrics(VirtualScreenHeight);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var x = Math.Clamp(cursor.X + dx, left, left + width - 1);
        var y = Math.Clamp(cursor.Y + dy, top, top + height - 1);

        // Absolute coordinates run from 0 to 65535 across the whole virtual desktop (all monitors).
        Send(MouseInput(Normalized(x - left, width), Normalized(y - top, height), 0, MouseMove | MouseAbsolute | MouseVirtualDesk));
    }

    /// <summary>
    /// Windows maps back with <c>pixel = value * size / 65536</c>, rounded down; rounding up here
    /// makes that land on exactly <paramref name="pixel"/>, so a one-pixel move is never lost.
    /// </summary>
    private static int Normalized(int pixel, int size) => (int)((((long)pixel * 65536) + size - 1) / size);

    public void SetButton(MouseButton button, bool down)
    {
        var flags = (button, down) switch
        {
            (MouseButton.Left, true) => MouseLeftDown,
            (MouseButton.Left, false) => MouseLeftUp,
            (MouseButton.Right, true) => MouseRightDown,
            _ => MouseRightUp,
        };
        Send(MouseInput(0, 0, 0, flags));
    }

    public void Scroll(int vertical, int horizontal)
    {
        if (vertical != 0)
        {
            Send(MouseInput(0, 0, unchecked((uint)vertical), MouseWheel));
        }

        if (horizontal != 0)
        {
            Send(MouseInput(0, 0, unchecked((uint)horizontal), MouseHorizontalWheel));
        }
    }

    public void SetKey(OutputKey key, bool down)
    {
        var (virtualKey, extended) = key switch
        {
            OutputKey.Up => ((ushort)0x26, true),
            OutputKey.Down => ((ushort)0x28, true),
            OutputKey.Left => ((ushort)0x25, true),
            OutputKey.Right => ((ushort)0x27, true),
            OutputKey.Escape => ((ushort)0x1B, false),
            OutputKey.Tab => ((ushort)0x09, false),
            OutputKey.Shift => ((ushort)0xA0, false),
            _ => ((ushort)0x0D, false),
        };

        // The scan code too: some programs read it instead of the virtual key.
        var scanCode = (ushort)MapVirtualKey(virtualKey, 0);
        var flags = (extended ? KeyExtended : 0) | (down ? 0 : KeyUp);
        var input = new Input
        {
            Type = InputKeyboard,
            Data = new InputData { Keyboard = new KeyboardInput { VirtualKey = virtualKey, ScanCode = scanCode, Flags = flags } },
        };
        Send(input);
    }

    private static Input MouseInput(int dx, int dy, uint data, uint flags) => new()
    {
        Type = InputMouse,
        Data = new InputData { Mouse = new MouseInputData { X = dx, Y = dy, Data = data, Flags = flags } },
    };

    private static void Send(Input input)
    {
        // Fails only when something blocks input (UIPI, the secure desktop); there is nothing to do about it then.
        _ = SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint count, [In] Input[] inputs, int size);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
    private static partial uint MapVirtualKey(uint code, uint mapType);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputData Data;
    }

    /// <summary>The union in INPUT; its size is the largest member's (MOUSEINPUT), as SendInput expects.</summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)]
        public MouseInputData Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputData
    {
        public int X;
        public int Y;
        public uint Data;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }
}
