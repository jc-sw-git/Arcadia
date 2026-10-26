using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;

namespace Arcadia.Pan.Windows;

/// <summary>
/// The teardown on Windows (#243): usbip-win2's command line for the ports (fixed path, fixed arguments) and
/// process handles for the forwarding processes, held from the moment they are taken so an id reused by another
/// process can never be hit.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCleanup(Action<string> log) : IEmulationCleanup
{
    private static readonly TimeSpan UsbipTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(3);

    private readonly Lock _lock = new();
    private readonly Dictionary<HeldProcess, nint> _handles = [];

    /// <summary>usbip.exe from usbip-win2's default install folder; without it there are no ports to detach.</summary>
    public static string UsbipExecutable => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "USBip", "usbip.exe");

    public async Task<IReadOnlyList<int>> ListLocalViiperPortsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(UsbipExecutable))
        {
            return [];
        }

        var listing = await RunUsbipAsync(["port"], cancellationToken);
        return UsbipPorts.FromLocalViiper(listing);
    }

    public async Task DetachPortAsync(int port, CancellationToken cancellationToken)
    {
        await RunUsbipAsync(["detach", "-p", port.ToString(CultureInfo.InvariantCulture)], cancellationToken);
        log($"usbip port {port} detached");
    }

    public unsafe IReadOnlyList<HeldProcess> TakeChildrenOf(int clientPid, long clientStartTime, string imageName)
    {
        var taken = new List<HeldProcess>();
        var snapshot = Native.CreateToolhelp32Snapshot(Native.Th32csSnapProcess, 0);
        if (snapshot == -1)
        {
            log($"Process list not readable: {Native.LastErrorText()}");
            return taken;
        }

        try
        {
            var entry = new Native.ProcessEntry { Size = (uint)sizeof(Native.ProcessEntry) };
            for (var more = Native.Process32FirstW(snapshot, ref entry); more; more = Native.Process32NextW(snapshot, ref entry))
            {
                if (entry.ParentProcessId != (uint)clientPid || entry.ProcessId == (uint)clientPid)
                {
                    continue;
                }

                var handle = Native.OpenProcess(Native.ProcessQueryLimitedInformation | Native.ProcessTerminate | Native.Synchronize, false, entry.ProcessId);
                if (handle == 0)
                {
                    continue;
                }

                // A child created before the client is a reused parent id; one with another name isn't ours.
                if (StartTimeOf(handle) is { } start && start > clientStartTime
                    && string.Equals(ImageNameOf(handle), imageName, StringComparison.OrdinalIgnoreCase))
                {
                    var process = new HeldProcess((int)entry.ProcessId, start, imageName);
                    lock (_lock)
                    {
                        // Held since an earlier emulation (the client took it up again): the first handle stays.
                        if (!_handles.TryAdd(process, handle))
                        {
                            Native.CloseHandle(handle);
                        }
                    }

                    taken.Add(process);
                }
                else
                {
                    Native.CloseHandle(handle);
                }
            }
        }
        finally
        {
            Native.CloseHandle(snapshot);
        }

        return taken;
    }

    public bool TryHold(HeldProcess process)
    {
        var handle = Native.OpenProcess(Native.ProcessQueryLimitedInformation | Native.ProcessTerminate | Native.Synchronize, false, (uint)process.Pid);
        if (handle == 0)
        {
            return false;
        }

        if (StartTimeOf(handle) != process.StartTime)
        {
            Native.CloseHandle(handle);
            return false;
        }

        lock (_lock)
        {
            _handles[process] = handle;
        }

        return true;
    }

    public Task EndAsync(HeldProcess process, CancellationToken cancellationToken)
    {
        nint handle;
        lock (_lock)
        {
            if (!_handles.TryGetValue(process, out handle))
            {
                throw new InvalidOperationException("not held");
            }
        }

        if (!Native.TerminateProcess(handle, 1) && Native.WaitForSingleObject(handle, 0) != Native.WaitObject0)
        {
            throw new Win32Exception();
        }

        if (Native.WaitForSingleObject(handle, (uint)ExitWait.TotalMilliseconds) != Native.WaitObject0)
        {
            throw new TimeoutException($"still running after {ExitWait.TotalSeconds:0} s");
        }

        Release(process);
        log($"{process.ImageName} ({process.Pid}) ended");
        return Task.CompletedTask;
    }

    public void Release(HeldProcess process)
    {
        lock (_lock)
        {
            if (_handles.Remove(process, out var handle))
            {
                Native.CloseHandle(handle);
            }
        }
    }

    public long? StartTimeOf(int pid)
    {
        var handle = Native.OpenProcess(Native.ProcessQueryLimitedInformation, false, (uint)pid);
        if (handle == 0)
        {
            return null;
        }

        try
        {
            return StartTimeOf(handle);
        }
        finally
        {
            Native.CloseHandle(handle);
        }
    }

    public static long? StartTimeOf(nint handle) => Native.GetProcessTimes(handle, out var creation, out _, out _, out _) ? creation : null;

    private static unsafe string? ImageNameOf(nint handle)
    {
        var buffer = stackalloc char[1024];
        var size = 1024u;
        return Native.QueryFullProcessImageNameW(handle, 0, buffer, ref size) ? Path.GetFileName(new string(buffer, 0, (int)size)) : null;
    }

    /// <summary>Runs usbip.exe with fixed arguments; throws when it fails, times out or exits with an error.</summary>
    private static async Task<string> RunUsbipAsync(string[] arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(UsbipExecutable) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(UsbipTimeout);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("usbip.exe did not start");
        try
        {
            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return process.ExitCode == 0 ? output : throw new InvalidOperationException($"usbip {arguments[0]} exited with {process.ExitCode}");
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            throw;
        }
    }
}
