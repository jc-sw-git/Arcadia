using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.Input;

/// <summary>When simulated mouse and keyboard input from other programs is dropped.</summary>
public enum DesktopFilterScope
{
    Off,

    /// <summary>While a controller is in use: outside games and in the mouse mode, where Steam's desktop layout only gets in the way.</summary>
    ControllerInUse,

    /// <summary>
    /// Only while ⧉ + ≡ are held: in a game, whose own Steam layout may map buttons to keys, Steam's
    /// desktop layout still made Alt+Tab of Arcadia's gesture (Steam Input off for the game; owner's PC, 2026-10-20).
    /// </summary>
    ComboHeld,
}

/// <summary>Drops what Steam's desktop layout makes of the controller (#64).</summary>
public interface IDesktopInputFilter : IDisposable
{
    /// <returns>When switched off: how many inputs it dropped since it was switched on.</returns>
    int? SetScope(DesktopFilterScope scope);
}

public static class DesktopInputFilter
{
    /// <summary>Low-level hooks on Windows; nothing elsewhere.</summary>
    /// <param name="log">Problems of the hooks, for <c>arcadia.log</c>; called from any thread.</param>
    public static IDesktopInputFilter CreateDefault(ControllerActivity activity, Action<string> log) =>
        OperatingSystem.IsWindows() ? new SteamDesktopInputFilter(activity, log) : new NullDesktopInputFilter();
}

/// <summary>Used where Steam's desktop layout can't be filtered (macOS / Linux for now).</summary>
public sealed class NullDesktopInputFilter : IDesktopInputFilter
{
    public int? SetScope(DesktopFilterScope scope) => null;

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
/// Every key (and in <see cref="DesktopFilterScope.ControllerInUse"/> every mouse event) on the PC passes
/// the hooks while they're in, so they are only in while needed and answer in microseconds: input from
/// real devices only reads a flag. Beside a game only the keyboard hook is in: Steam's Alt+Tab is keys,
/// and a gaming mouse sends up to 8,000 events a second that would each wait for Arcadia's process.
/// Windows removes a low-level hook that once took too long, silently (e.g. during a long GC pause), so
/// the hooks are set anew every <see cref="RehookInterval"/> and on every change of scope.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed unsafe partial class SteamDesktopInputFilter : IDesktopInputFilter
{
    private const int KeyboardHook = 13;
    private const int MouseHook = 14;
    private const uint KeyInjected = 0x10;
    private const uint MouseInjected = 0x01;
    private const uint TimerMessage = 0x0113;

    // WM_APP + 1: read the scope again and set the hooks anew.
    private const uint RehookMessage = 0x8001;

    private static readonly TimeSpan RehookInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ThreadTimeout = TimeSpan.FromSeconds(1);

    // The hooks call back into a static method; only one filter exists per Arcadia.
    private static SteamDesktopInputFilter? s_current;

    private readonly ControllerActivity _activity;
    private readonly Action<string> _log;
    private readonly Lock _lock = new();
    private readonly InjectedInputGate _gate = new();
    private readonly Func<bool> _controllerInUse;
    private Thread? _thread;
    private ManualResetEventSlim? _ready;
    private volatile uint _threadId;
    private volatile DesktopFilterScope _scope;

    // Only the hook thread touches these.
    private nint _keyboardHook;
    private nint _mouseHook;
    private bool _hookFailureLogged;

    public SteamDesktopInputFilter(ControllerActivity activity, Action<string> log)
    {
        _activity = activity;
        _log = log;
        _controllerInUse = () => _scope == DesktopFilterScope.ComboHeld
            ? _activity.IsComboHeld() || XInputGamepadSource.AnyNow(_activity.ConnectedSlots, ControllerActivity.IsComboHeld)
            : _activity.IsInUse() || XInputGamepadSource.AnyNow(_activity.ConnectedSlots, ControllerActivity.IsInUse);
    }

    public int? SetScope(DesktopFilterScope scope)
    {
        lock (_lock)
        {
            _scope = scope;
            var on = scope != DesktopFilterScope.Off;
            if (on && _thread is not { IsAlive: true })
            {
                Start();
                return null;
            }

            if (_thread is not { } thread)
            {
                return null;
            }

            // The thread reads the scope itself: it sets the hooks this scope needs, or ends when it is off.
            if (_ready!.Wait(ThreadTimeout))
            {
                _ = PostThreadMessage(_threadId, RehookMessage, 0, 0);
            }

            if (on)
            {
                return null;
            }

            if (!thread.Join(ThreadTimeout))
            {
                // Kept, so the next start doesn't add a second thread; it ends itself once it runs, seeing the scope off.
                _log("Filter für Steams Desktop-Belegung endet nicht rechtzeitig");
                return null;
            }

            _thread = null;
            return _gate.TakeDroppedCount();
        }
    }

    public void Dispose() => SetScope(DesktopFilterScope.Off);

    private void Start()
    {
        s_current = this;

        // Not disposed: after a timed-out wait the thread may still set it.
        _ready = new ManualResetEventSlim();
        var ready = _ready;
        // Every key and mouse event waits for this thread while the hooks are in, also beside a busy game.
        _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "Steam desktop filter", Priority = ThreadPriority.Highest };
        _thread.Start();

        // The thread id is needed to reach it; starting takes microseconds.
        if (!ready.Wait(ThreadTimeout))
        {
            _log("Filter für Steams Desktop-Belegung startet nicht rechtzeitig");
        }
    }

    private void Run(ManualResetEventSlim ready)
    {
        nint timer = 0;
        try
        {
            // Creates the thread's message queue first, so a message right after the start isn't lost.
            _ = PeekMessage(out _, 0, 0, 0, 0);
            _threadId = GetCurrentThreadId();
            ready.Set();

            timer = SetTimer(0, 0, (uint)RehookInterval.TotalMilliseconds, 0);

            // The hooks are called on this thread, inside GetMessage; it returns only for the messages posted here.
            while (SetHooks())
            {
                int result;
                while ((result = GetMessage(out var message, 0, 0, 0)) > 0 && message.Id is not (RehookMessage or TimerMessage))
                {
                }

                if (result <= 0)
                {
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            // An exception here would end Arcadia; without the filter Steam's input simply passes.
            _log($"Filter für Steams Desktop-Belegung beendet: {exception}");
        }
        finally
        {
            if (timer != 0)
            {
                _ = KillTimer(0, timer);
            }

            Replace(ref _keyboardHook, 0);
            Replace(ref _mouseHook, 0);
        }
    }

    /// <summary>Sets the hooks the scope needs anew, the new one before the old one goes, so no input slips through between.</summary>
    /// <returns>False when the scope is off: the thread ends.</returns>
    private bool SetHooks()
    {
        var scope = _scope;
        if (scope == DesktopFilterScope.Off)
        {
            return false;
        }

        var module = GetModuleHandle(null);
        var keyboard = SetWindowsHookEx(KeyboardHook, &OnKeyboard, module, 0);
        var mouse = scope == DesktopFilterScope.ControllerInUse ? SetWindowsHookEx(MouseHook, &OnMouse, module, 0) : 0;
        var failed = keyboard == 0 || (scope == DesktopFilterScope.ControllerInUse && mouse == 0);
        if (failed && !_hookFailureLogged)
        {
            _log($"Hook für Steams Desktop-Belegung nicht gesetzt (Fehler {Marshal.GetLastPInvokeError()})");
        }

        _hookFailureLogged = failed;

        // A hook that failed now keeps the old one, if Windows still has it.
        if (keyboard != 0)
        {
            Replace(ref _keyboardHook, keyboard);
        }

        if (mouse != 0 || scope != DesktopFilterScope.ControllerInUse)
        {
            Replace(ref _mouseHook, mouse);
        }

        return true;
    }

    private static void Replace(ref nint hook, nint replacement)
    {
        if (hook != 0)
        {
            // False for a hook Windows already removed; nothing to do then.
            _ = UnhookWindowsHookEx(hook);
        }

        hook = replacement;
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

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
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

    [LibraryImport("user32.dll")]
    private static partial nint SetTimer(nint window, nint id, uint milliseconds, nint callback);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool KillTimer(nint window, nint id);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandle(string? moduleName);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();
}
