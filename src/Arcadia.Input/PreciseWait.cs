using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.Input;

/// <summary>
/// Waits a millisecond or less without raising the system's timer resolution (#238 D9): <see cref="Thread.Sleep(int)"/>
/// on Windows waits for the next tick of the system timer, 15.6 ms by default, and Windows 11 ignores a raised
/// resolution for a process whose window is hidden, as Arcadia's is behind a game. A high-resolution waitable timer
/// (Windows 10 1803 and later) wakes on time either way. Elsewhere, or where Windows has none, it sleeps.
/// </summary>
public sealed partial class PreciseWait : IDisposable
{
    private const uint CreateWaitableTimerHighResolution = 0x2;
    private const uint TimerAllAccess = 0x1F0003;
    private const uint Infinite = 0xFFFFFFFF;

    private nint _timer;

    private PreciseWait(nint timer)
    {
        _timer = timer;
    }

    /// <summary>False where it falls back to <see cref="Thread.Sleep(TimeSpan)"/>, for the log.</summary>
    public bool IsPrecise => _timer != 0;

    /// <summary>For the thread that waits; one timer per thread.</summary>
    public static PreciseWait Create() => new(OperatingSystem.IsWindows() ? CreateTimer() : 0);

    /// <summary>Returns after about <paramref name="duration"/>; at once for zero or less.</summary>
    public void Wait(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        if (_timer == 0 || !OperatingSystem.IsWindows() || !WaitOnTimer(_timer, duration))
        {
            Thread.Sleep(duration);
        }
    }

    public void Dispose()
    {
        if (_timer != 0 && OperatingSystem.IsWindows())
        {
            CloseHandle(_timer);
        }

        _timer = 0;
    }

    [SupportedOSPlatform("windows")]
    private static nint CreateTimer() => CreateWaitableTimerExW(0, null, CreateWaitableTimerHighResolution, TimerAllAccess);

    [SupportedOSPlatform("windows")]
    private static bool WaitOnTimer(nint timer, TimeSpan duration)
    {
        // Negative: relative, in 100 ns.
        var due = -Math.Max(1, duration.Ticks);
        return SetWaitableTimer(timer, in due, 0, 0, 0, false) && WaitForSingleObject(timer, Infinite) == 0;
    }

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateWaitableTimerExW(nint attributes, string? name, uint flags, uint access);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWaitableTimer(nint timer, in long dueTime, int period, nint completion, nint argument, [MarshalAs(UnmanagedType.Bool)] bool resume);

    [LibraryImport("kernel32.dll")]
    private static partial uint WaitForSingleObject(nint handle, uint milliseconds);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}
