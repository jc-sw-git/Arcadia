using System.Runtime.InteropServices;
using Arcadia.App.Views;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Arcadia.App.Services;

/// <summary>
/// Foreground handling around SISR. While SISR starts, other processes may not take the
/// foreground (and if one did, Arcadia takes it back). After SISR stops, a short focus change is
/// triggered, because Steam only switches the Steam Controller back to its desktop layout when
/// the foreground window changes.
/// </summary>
public sealed partial class ForegroundGuard(MainWindow window)
{
    private const uint Lock = 1;
    private const uint Unlock = 2;

    private bool _guarding;

    public void Begin()
    {
        _guarding = window.IsActive;
        if (_guarding && OperatingSystem.IsWindows())
        {
            // Only the foreground process may call this — which Arcadia is, if it is active.
            LockSetForegroundWindow(Lock);
        }
    }

    public void End()
    {
        if (!_guarding)
        {
            return;
        }

        _guarding = false;
        if (OperatingSystem.IsWindows())
        {
            LockSetForegroundWindow(Unlock);
        }

        if (!window.IsActive)
        {
            AppLog.Write("Fokus nach dem Start von SISR zurückgeholt.");
            window.BringToFront();
        }
    }

    /// <summary>
    /// Moves the foreground to the desktop for a moment and back, so Steam re-evaluates which
    /// controller layout applies. Only done while Arcadia is in front — it is the only time
    /// Windows lets Arcadia move the foreground, and otherwise a focus change happens anyway.
    /// </summary>
    public async Task BounceFocusAsync()
    {
        if (!OperatingSystem.IsWindows() || !window.IsActive
            || window.TryGetPlatformHandle() is not { } handle || GetShellWindow() is var shell && shell == IntPtr.Zero)
        {
            return;
        }

        // Tested: switching to another window of Arcadia's own is not enough — Steam only
        // reacts when the foreground moves to a different process, so the desktop takes it.
        if (!SetForegroundWindow(shell))
        {
            return;
        }

        await Task.Delay(BounceDuration);
        var back = TakeForeground(handle.Handle);
        window.Activate();
        AppLog.Write($"Fokus kurz zum Desktop gewechselt, damit Steam die Desktop-Belegung übernimmt (zurück: {back}).");
    }

    /// <summary>
    /// Brings Arcadia to the front while another program has it, e.g. View in the mouse mode (#64):
    /// Windows lets a background program take the foreground only with its input queue attached.
    /// </summary>
    public static void BringBack(MainWindow window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        if (OperatingSystem.IsWindows() && window.TryGetPlatformHandle() is { } handle)
        {
            TakeForeground(handle.Handle);
        }

        window.BringToFront();

        // BringToFront leaves Arcadia below every topmost window, e.g. a game in borderless full screen.
        // Then it stays topmost until it loses the focus, and the game, topmost too, comes back above it.
        if (OperatingSystem.IsWindows() && window.TryGetPlatformHandle() is { } shown && !IsUncovered(shown.Handle))
        {
            window.Topmost = true;
            window.Deactivated += EndTopmost;

            void EndTopmost(object? sender, EventArgs e)
            {
                window.Deactivated -= EndTopmost;
                window.Topmost = false;
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="window"/> can be seen: the foreground window can still be hidden behind a
    /// topmost window, e.g. a game in borderless full screen. Hidden means another program's window lies
    /// at its centre and covers it whole; a small window kept on top (say, a task manager) doesn't hide it.
    /// Callable from any thread.
    /// </summary>
    public static bool IsUncovered(IntPtr window)
    {
        if (!OperatingSystem.IsWindows() || !GetWindowRect(window, out var bounds))
        {
            return true;
        }

        const uint Root = 2;
        var centre = new Point((bounds.Left + bounds.Right) / 2, (bounds.Top + bounds.Bottom) / 2);
        var atCentre = GetAncestor(WindowFromPoint(centre), Root);
        if (atCentre == IntPtr.Zero || GetWindowThreadProcessId(atCentre, out var processId) == 0 || processId == Environment.ProcessId)
        {
            return true;
        }

        return !GetWindowRect(atCentre, out var cover)
            || cover.Left > bounds.Left || cover.Top > bounds.Top || cover.Right < bounds.Right || cover.Bottom < bounds.Bottom;
    }

    /// <summary>
    /// Whether the desktop is the foreground window: the shell's desktop window, the one behind the icons, or the taskbar.
    /// Callable from any thread.
    /// </summary>
    public static unsafe bool IsDesktopInFront()
    {
        if (!OperatingSystem.IsWindows() || GetForegroundWindow() is var foreground && foreground == IntPtr.Zero)
        {
            return false;
        }

        if (foreground == GetShellWindow())
        {
            return true;
        }

        const int Capacity = 32;
        var name = stackalloc char[Capacity];
        var length = GetClassName(foreground, name, Capacity);
        return new ReadOnlySpan<char>(name, Math.Max(length, 0)) is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }

    /// <summary>
    /// Puts the pointer in the middle of the foreground window. Games hide the pointer, so after the
    /// mouse mode came on in a game it may sit over Arcadia on another monitor, and the first click
    /// (RT or A) would bring Arcadia to the front unseen (review of #64, three monitors).
    /// </summary>
    public static void CentrePointerOnForeground()
    {
        if (OperatingSystem.IsWindows() && GetForegroundWindow() is var foreground && foreground != IntPtr.Zero
            && GetWindowRect(foreground, out var bounds))
        {
            _ = SetCursorPos((bounds.Left + bounds.Right) / 2, (bounds.Top + bounds.Bottom) / 2);
        }
    }

    /// <summary>Tested: 150 ms is reliably noticed by Steam, 40 ms is not.</summary>
    private static readonly TimeSpan BounceDuration = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// Brings <paramref name="target"/> to the front although another process owns the
    /// foreground: Windows allows it while our input queue is attached to the foreground thread.
    /// </summary>
    private static bool TakeForeground(IntPtr target)
    {
        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
        var ownThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != ownThread && AttachThreadInput(ownThread, foregroundThread, true);
        try
        {
            BringWindowToTop(target);
            return SetForegroundWindow(target);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(ownThread, foregroundThread, false);
            }
        }
    }

    /// <summary>Puts the window at the bottom of the z-order without activating it.</summary>
    public static void SendToBack(Window window)
    {
        if (OperatingSystem.IsWindows() && window.TryGetPlatformHandle() is { } handle)
        {
            const uint NoSize = 0x1, NoMove = 0x2, NoActivate = 0x10;
            SetWindowPos(handle.Handle, new IntPtr(1) /* HWND_BOTTOM */, 0, 0, 0, 0, NoSize | NoMove | NoActivate);
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr window, out Rect bounds);

    [LibraryImport("user32.dll")]
    private static partial IntPtr WindowFromPoint(Point point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetAncestor(IntPtr window, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct Point(int X, int Y);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint attach, uint attachTo, [MarshalAs(UnmanagedType.Bool)] bool doAttach);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BringWindowToTop(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LockSetForegroundWindow(uint code);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetShellWindow();

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    private static unsafe partial int GetClassName(IntPtr window, char* name, int capacity);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr window);
}
