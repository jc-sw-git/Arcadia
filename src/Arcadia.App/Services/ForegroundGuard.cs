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

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr window);
}
