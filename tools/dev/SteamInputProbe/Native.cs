using System.Runtime.InteropServices;

namespace SteamInputProbe;

[StructLayout(LayoutKind.Sequential)]
internal struct XInputState
{
    public uint PacketNumber;
    public ushort Buttons;
    public byte LeftTrigger;
    public byte RightTrigger;
    public short LeftX;
    public short LeftY;
    public short RightX;
    public short RightY;

    public readonly string Describe() =>
        $"buttons=0x{Buttons:X4} lt={LeftTrigger,3} rt={RightTrigger,3} l=({LeftX,6},{LeftY,6}) r=({RightX,6},{RightY,6})";

    /// <summary>Sticks rest a few hundred units off zero; only bigger moves count as a change worth logging.</summary>
    public readonly bool DiffersNoticeably(XInputState other) =>
        Buttons != other.Buttons || Math.Abs(LeftTrigger - other.LeftTrigger) > 8 || Math.Abs(RightTrigger - other.RightTrigger) > 8
        || Math.Abs(LeftX - other.LeftX) > 3000 || Math.Abs(LeftY - other.LeftY) > 3000
        || Math.Abs(RightX - other.RightX) > 3000 || Math.Abs(RightY - other.RightY) > 3000;
}

internal static class Native
{
    public const uint ErrorSuccess = 0;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr LoadLibraryW(string path);

    // Ordinal 100 is XInputGetStateEx: same as XInputGetState plus the Guide button (0x0400).
    [DllImport("xinput1_4.dll", EntryPoint = "#100")]
    public static extern uint XInputGetStateEx(uint index, out XInputState state);

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    public static extern uint XInputGetState(uint index, out XInputState state);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    public const int WhKeyboardLl = 13;
    public const int WhMouseLl = 14;
    public const uint LlkhfInjected = 0x10;
    public const uint LlmhfInjected = 0x01;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookExW(int hookType, HookProc proc, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    public static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandleW(string? name);

    [StructLayout(LayoutKind.Sequential)]
    public struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MsLlHookStruct
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Msg
    {
        public IntPtr Window;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern int GetMessageW(out Msg msg, IntPtr window, uint min, uint max);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref Msg msg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessageW(ref Msg msg);

    [DllImport("user32.dll")]
    public static extern bool PostThreadMessageW(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    public const uint WmQuit = 0x0012;

    // A tiny tool window (no taskbar entry) for the --window variant: tests whether Steam needs the
    // "game" process to own a window before it hands it the virtual gamepad.
    public const uint WsPopup = 0x80000000;
    public const uint WsExToolWindow = 0x00000080;
    public const uint WsExNoActivate = 0x08000000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr window, int command);

    public const int SwShowNoActivate = 4;
}
