using System.Runtime.InteropServices;
using Arcadia.Core.Search;

namespace Arcadia.App.Services;

/// <summary>Where the window in front and its caret are, for the mouse mode's on-screen keyboard (#110).</summary>
public static partial class ForegroundWindow
{
    /// <summary>The window in front on the desktop, in physical pixels; null without one, or off Windows.</summary>
    public static ScreenRect? Bounds()
    {
        if (!OperatingSystem.IsWindows() || GetForegroundWindow() is var window && window == IntPtr.Zero || !GetWindowRect(window, out var bounds))
        {
            return null;
        }

        return new ScreenRect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
    }

    /// <summary>
    /// The text caret of the window in front, as Windows knows it; null where the program draws its own
    /// (browsers and launchers built on them often do) or off Windows.
    /// </summary>
    public static ScreenRect? Caret()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var thread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        if (thread == 0 || !GetGUIThreadInfo(thread, ref info) || info.CaretWindow == IntPtr.Zero)
        {
            return null;
        }

        var topLeft = new Point(info.Caret.Left, info.Caret.Top);
        var bottomRight = new Point(info.Caret.Right, info.Caret.Bottom);
        if (!ClientToScreen(info.CaretWindow, ref topLeft) || !ClientToScreen(info.CaretWindow, ref bottomRight))
        {
            return null;
        }

        // A caret is often 1 pixel wide or reported empty; it still has a place.
        return new ScreenRect(topLeft.X, topLeft.Y, Math.Max(1, bottomRight.X - topLeft.X), Math.Max(1, bottomRight.Y - topLeft.Y));
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr window, out Rect bounds);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(IntPtr window, ref Point point);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point(int x, int y)
    {
        public int X = x;
        public int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr Active;
        public IntPtr Focus;
        public IntPtr Capture;
        public IntPtr MenuOwner;
        public IntPtr MoveSize;
        public IntPtr CaretWindow;
        public Rect Caret;
    }
}
