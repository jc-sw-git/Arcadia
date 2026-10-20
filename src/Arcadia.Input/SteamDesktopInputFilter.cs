using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.Input;

/// <summary>Drops what Steam's desktop layout makes of the controller, while switched on (#64).</summary>
public interface IDesktopInputFilter : IDisposable
{
    /// <summary>On: simulated mouse and keyboard input from other programs is dropped while a controller is in use.</summary>
    /// <returns>When switched off: how many inputs it dropped while it was on.</returns>
    int? SetOn(bool on);
}

public static class DesktopInputFilter
{
    /// <summary>Low-level hooks on Windows; nothing elsewhere.</summary>
    public static IDesktopInputFilter CreateDefault(ControllerActivity activity) =>
        OperatingSystem.IsWindows() ? new SteamDesktopInputFilter(activity) : new NullDesktopInputFilter();
}

/// <summary>Used where Steam's desktop layout can't be filtered (macOS / Linux for now).</summary>
public sealed class NullDesktopInputFilter : IDesktopInputFilter
{
    public int? SetOn(bool on) => null;

    public void Dispose()
    {
    }
}

/// <summary>
/// Windows' low-level keyboard and mouse hooks on a thread of their own, deciding with
/// <see cref="InjectedInputGate"/>. Steam sends its desktop layout with <c>SendInput</c>, so it is
/// marked as simulated; real devices aren't, and Arcadia's own input carries <see cref="SendInputOutput.ArcadiaMarker"/>.
/// </summary>
/// <remarks>
/// Every mouse event and key on the PC passes the hooks while they're in, so they are only in
/// while needed (never beside a game without the mouse mode) and answer in microseconds. Windows
/// skips a hook that takes too long, so a stall here can't freeze the mouse.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed unsafe partial class SteamDesktopInputFilter : IDesktopInputFilter
{
    private const int KeyboardHook = 13;
    private const int MouseHook = 14;
    private const uint KeyInjected = 0x10;
    private const uint MouseInjected = 0x01;
    private const uint Quit = 0x0012;

    // The hooks call back into a static method; only one filter exists per Arcadia.
    private static SteamDesktopInputFilter? s_current;

    private readonly ControllerActivity _activity;
    private readonly Lock _lock = new();
    private readonly InjectedInputGate _gate = new();
    private readonly Func<bool> _controllerInUse;
    private Thread? _thread;
    private uint _threadId;

    public SteamDesktopInputFilter(ControllerActivity activity)
    {
        _activity = activity;
        _controllerInUse = () => _activity.IsInUse() || XInputGamepadSource.AnyInUseNow(_activity.ConnectedSlots);
    }

    public int? SetOn(bool on)
    {
        lock (_lock)
        {
            if (on && _thread is null)
            {
                Start();
                return null;
            }

            if (!on && _thread is { } thread)
            {
                _ = PostThreadMessage(_threadId, Quit, 0, 0);
                thread.Join(TimeSpan.FromSeconds(1));
                _thread = null;
                return _gate.TakeDroppedCount();
            }

            return null;
        }
    }

    public void Dispose() => SetOn(false);

    private void Start()
    {
        s_current = this;

        // Not disposed: after a timed-out wait the thread may still set it.
        var ready = new ManualResetEventSlim();
        _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "Steam desktop filter" };
        _thread.Start();

        // The thread id is needed to stop it; installing the hooks takes microseconds.
        ready.Wait(TimeSpan.FromSeconds(1));
    }

    private void Run(ManualResetEventSlim ready)
    {
        nint keyboard = 0, mouse = 0;
        try
        {
            _threadId = GetCurrentThreadId();
            var module = GetModuleHandle(null);
            keyboard = SetWindowsHookEx(KeyboardHook, &OnKeyboard, module, 0);
            mouse = SetWindowsHookEx(MouseHook, &OnMouse, module, 0);

            // Creates the thread's message queue first, so a stop right after the start isn't lost.
            _ = PeekMessage(out _, 0, 0, 0, 0);
            ready.Set();

            // The hooks are called on this thread, between its messages; WM_QUIT ends it.
            while (GetMessage(out _, 0, 0, 0) > 0)
            {
            }
        }
        catch (Exception)
        {
            // An exception here would end Arcadia; without the filter Steam's input simply passes.
        }
        finally
        {
            if (keyboard != 0)
            {
                _ = UnhookWindowsHookEx(keyboard);
            }

            if (mouse != 0)
            {
                _ = UnhookWindowsHookEx(mouse);
            }
        }
    }

    [UnmanagedCallersOnly]
    private static nint OnKeyboard(int code, nint message, nint data)
    {
        try
        {
            if (code >= 0 && s_current is { } filter)
            {
                var key = (KeyboardData*)data;
                var down = message is 0x0100 or 0x0104;
                var foreign = (key->Flags & KeyInjected) != 0 && key->ExtraInfo != SendInputOutput.ArcadiaMarker;
                if (filter._gate.Press((int)key->VirtualKey, down, foreign, filter._controllerInUse))
                {
                    return 1;
                }
            }
        }
        catch (Exception)
        {
            // An exception must never cross back into Windows: the input then simply passes.
        }

        return CallNextHookEx(0, code, message, data);
    }

    [UnmanagedCallersOnly]
    private static nint OnMouse(int code, nint message, nint data)
    {
        try
        {
            if (code >= 0 && s_current is { } filter)
            {
                var mouse = (MouseData*)data;
                var foreign = (mouse->Flags & MouseInjected) != 0 && mouse->ExtraInfo != SendInputOutput.ArcadiaMarker;
                var dropped = message switch
                {
                    0x0201 or 0x0202 => filter._gate.Press(-1, message == 0x0201, foreign, filter._controllerInUse),
                    0x0204 or 0x0205 => filter._gate.Press(-2, message == 0x0204, foreign, filter._controllerInUse),
                    0x0207 or 0x0208 => filter._gate.Press(-3, message == 0x0207, foreign, filter._controllerInUse),
                    0x020B or 0x020C => filter._gate.Press(-4 - (int)(mouse->Extra >> 16), message == 0x020B, foreign, filter._controllerInUse),
                    _ => filter._gate.Motion(foreign, filter._controllerInUse),
                };
                if (dropped)
                {
                    return 1;
                }
            }
        }
        catch (Exception)
        {
            // As above: never throw into Windows.
        }

        return CallNextHookEx(0, code, message, data);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardData
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseData
    {
        public int X;
        public int Y;
        // The wheel's turn, or which X button.
        public uint Extra;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public nint Window;
        public uint Id;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW")]
    private static partial nint SetWindowsHookEx(int hook, delegate* unmanaged<int, nint, nint, nint> callback, nint module, uint threadId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookEx(nint hook);

    [LibraryImport("user32.dll")]
    private static partial nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    private static partial int GetMessage(out Message message, nint window, uint filterMin, uint filterMax);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessage(out Message message, nint window, uint filterMin, uint filterMax, uint remove);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostThreadMessage(uint threadId, uint message, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandle(string? moduleName);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();
}
