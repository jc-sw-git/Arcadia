using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Arcadia.Launchers;

/// <summary>Finds running processes by name or by install directory.</summary>
public static partial class ProcessWatch
{
    private const uint QueryLimitedInformation = 0x1000;

    public static bool IsRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }

    /// <summary>
    /// True if any process runs an executable inside <paramref name="directory"/>. Matching on the
    /// directory instead of the exe name also catches games that start through a bootstrapper.
    /// </summary>
    public static bool AnyRunningFrom(string directory) => FirstRunningFrom([directory]) is not null;


    /// <summary>The first of <paramref name="directories"/> that a running process was started from, or null.</summary>
    public static string? FirstRunningFrom(IEnumerable<string> directories)
    {
        var roots = directories
            .Select(d => (Directory: d, Root: Path.TrimEndingDirectorySeparator(Path.GetFullPath(d)) + Path.DirectorySeparatorChar))
            .ToList();
        if (roots.Count == 0)
        {
            return null;
        }

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (ExecutablePathOf(process) is { } path
                    && roots.Find(r => path.StartsWith(r.Root, StringComparison.OrdinalIgnoreCase)) is { Directory: { } match })
                {
                    return match;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// On Windows through <see cref="PathOf"/>: <c>MainModule</c> needs the full query and memory read rights,
    /// so it threw for every system, service and elevated process (about 150 exceptions per round beside a
    /// running game) and missed games running as administrator.
    /// </summary>
    private static string? ExecutablePathOf(Process process)
    {
        if (OperatingSystem.IsWindows())
        {
            return PathOf(process.Id);
        }

        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // Other users' processes, or one that just exited.
            return null;
        }
    }

    /// <summary>
    /// The full path of the executable <paramref name="processId"/> runs, or null if it can't be read
    /// (the process ended, or another user's). Unlike <c>Process.MainModule</c> this needs only the
    /// limited query right, so it also works for processes running as administrator, and doesn't throw.
    /// </summary>
    public static string? PathOf(int processId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var process = OpenProcess(QueryLimitedInformation, false, (uint)processId);
        if (process == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            // Long paths (\\?\…) can exceed MAX_PATH; 32,767 characters is the most Windows allows.
            foreach (var size in (int[])[1024, 32767])
            {
                if (TryQueryPath(process, size) is { } path)
                {
                    return path;
                }
            }

            return null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private static unsafe string? TryQueryPath(IntPtr process, int size)
    {
        var buffer = new char[size];
        var length = (uint)size;
        fixed (char* path = buffer)
        {
            return QueryFullProcessImageName(process, 0, path, ref length) ? new string(buffer, 0, (int)length) : null;
        }
    }

    /// <summary>The process that owns the foreground window, or null if there is none (e.g. the secure desktop shows).</summary>
    public static int? ForegroundProcessId()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var window = GetForegroundWindow();
        return window != IntPtr.Zero && GetWindowThreadProcessId(window, out var processId) != 0 && processId != 0
            ? (int)processId
            : null;
    }

    /// <summary>Polls <paramref name="condition"/> until it holds or the timeout expires.</summary>
    public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout, TimeProvider time, CancellationToken cancellationToken)
    {
        var deadline = time.GetUtcNow() + timeout;
        while (time.GetUtcNow() < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), time, cancellationToken);
        }

        return condition();
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageName(IntPtr process, uint flags, char* path, ref uint size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
