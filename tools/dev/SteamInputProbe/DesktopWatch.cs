using System.Diagnostics;
using Microsoft.Win32;

namespace SteamInputProbe;

/// <summary>
/// Watches what Steam's desktop layout would show as double input (mouse and key events from anything but a real
/// device), which window is in front, and whether Steam thinks a game runs.
/// </summary>
internal sealed class DesktopWatch : IDisposable
{
    private readonly ProbeLog _log;
    private readonly Thread _hookThread;
    private readonly Timer _timer;
    private uint _hookThreadId;
    private int _injectedMouse;
    private int _injectedKeys;
    private int _realMouse;
    private int _realKeys;
    private readonly List<string> _injectedKeyNames = [];
    private readonly Lock _lock = new();
    private string? _foreground;
    private string? _runningAppId;
    // Kept in fields so the GC doesn't collect the delegates Windows calls.
    private Native.HookProc? _mouseProc;
    private Native.HookProc? _keyProc;

    public DesktopWatch(ProbeLog log)
    {
        _log = log;
        _hookThread = new Thread(RunHooks) { IsBackground = true, Name = "hooks", Priority = ThreadPriority.Highest };
        _hookThread.Start();
        _timer = new Timer(_ => Report(), null, 1000, 1000);
    }

    private void RunHooks()
    {
        _hookThreadId = Native.GetCurrentThreadId();
        _mouseProc = OnMouse;
        _keyProc = OnKey;
        var module = Native.GetModuleHandleW(null);
        var mouseHook = Native.SetWindowsHookExW(Native.WhMouseLl, _mouseProc, module, 0);
        var keyHook = Native.SetWindowsHookExW(Native.WhKeyboardLl, _keyProc, module, 0);
        _log.Line($"desktop watch: mouse hook {(mouseHook != 0 ? "on" : "FAILED")}, key hook {(keyHook != 0 ? "on" : "FAILED")}");
        while (Native.GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            Native.TranslateMessage(ref msg);
            Native.DispatchMessageW(ref msg);
        }
        Native.UnhookWindowsHookEx(mouseHook);
        Native.UnhookWindowsHookEx(keyHook);
    }

    private IntPtr OnMouse(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var info = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.MsLlHookStruct>(lParam);
            if ((info.Flags & Native.LlmhfInjected) != 0) Interlocked.Increment(ref _injectedMouse);
            else Interlocked.Increment(ref _realMouse);
        }
        return Native.CallNextHookEx(0, code, wParam, lParam);
    }

    private IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var info = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.KbdLlHookStruct>(lParam);
            if ((info.Flags & Native.LlkhfInjected) != 0)
            {
                Interlocked.Increment(ref _injectedKeys);
                // 0x100 = WM_KEYDOWN, 0x104 = WM_SYSKEYDOWN: name only presses, not releases.
                if (wParam is 0x100 or 0x104)
                    lock (_lock) _injectedKeyNames.Add($"0x{info.VkCode:X2}");
            }
            else Interlocked.Increment(ref _realKeys);
        }
        return Native.CallNextHookEx(0, code, wParam, lParam);
    }

    /// <summary>Once a second: only what changed, so a quiet second leaves no line.</summary>
    private void Report()
    {
        try
        {
            var injectedMouse = Interlocked.Exchange(ref _injectedMouse, 0);
            var injectedKeys = Interlocked.Exchange(ref _injectedKeys, 0);
            var realMouse = Interlocked.Exchange(ref _realMouse, 0);
            var realKeys = Interlocked.Exchange(ref _realKeys, 0);
            string keys;
            lock (_lock)
            {
                keys = string.Join(" ", _injectedKeyNames);
                _injectedKeyNames.Clear();
            }
            if (injectedMouse + injectedKeys > 0)
                _log.Line($"DESKTOP INPUT (simulated): mouse {injectedMouse}/s, key events {injectedKeys}/s {(keys.Length > 0 ? "presses " + keys : "")}");
            if (realMouse + realKeys > 0)
                _log.Line($"desktop input (real devices): mouse {realMouse}/s, key events {realKeys}/s");

            var foreground = ForegroundProcess();
            if (foreground != _foreground)
            {
                _log.Line($"foreground: {foreground}");
                _foreground = foreground;
            }

            var runningAppId = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "RunningAppID", null)?.ToString();
            if (runningAppId != _runningAppId)
            {
                _log.Line($"Steam RunningAppID: {runningAppId ?? "(none)"}");
                _runningAppId = runningAppId;
            }
        }
        catch (Exception ex)
        {
            _log.Line($"desktop watch error: {ex.Message}");
        }
    }

    private static string ForegroundProcess()
    {
        var window = Native.GetForegroundWindow();
        if (window == 0) return "(no window)";
        Native.GetWindowThreadProcessId(window, out var pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return pid == Environment.ProcessId ? "this probe" : process.ProcessName;
        }
        catch (ArgumentException)
        {
            return $"pid {pid} (gone)";
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        Report();
        if (_hookThreadId != 0) Native.PostThreadMessageW(_hookThreadId, Native.WmQuit, 0, 0);
    }
}
