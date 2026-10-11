using System.ComponentModel;
using System.Diagnostics;

namespace Arcadia.Launchers;

/// <summary>Finds running processes by name or by install directory.</summary>
public static class ProcessWatch
{
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
    public static bool AnyRunningFrom(string directory) => AnyRunningFrom([directory]);

    /// <summary>True if any process runs an executable inside one of <paramref name="directories"/>.</summary>
    public static bool AnyRunningFrom(IEnumerable<string> directories)
    {
        var roots = directories
            .Select(d => Path.TrimEndingDirectorySeparator(Path.GetFullPath(d)) + Path.DirectorySeparatorChar)
            .ToList();
        if (roots.Count == 0)
        {
            return false;
        }

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string? path;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // System processes, other users' processes, or one that just exited.
                    continue;
                }

                if (path is not null && roots.Exists(root => path.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        return false;
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
}
