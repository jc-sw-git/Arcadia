using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Arcadia.Pan;

namespace Arcadia.ControllerSupport;

/// <summary>Starting and checking Arcadia's VIIPER on Windows (#304).</summary>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsViiperHost(Action<string> log) : IViiperHost
{
    public void EndLeftovers(string executable)
    {
        var path = Path.GetFullPath(executable);
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
        {
            using (process)
            {
                try
                {
                    // Another user's or an elevated process can't be read: it isn't Arcadia's.
                    if (!string.Equals(process.MainModule?.FileName, path, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    process.Kill();
                    process.WaitForExit(TimeSpan.FromSeconds(2));
                    log($"VIIPER: liegengebliebenen viiper.exe beendet (PID {process.Id})");
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Gone meanwhile, or not readable.
                }
            }
        }
    }

    public IViiperProcess Start(ProcessStartInfo start)
    {
        var process = Process.Start(start) ?? throw new InvalidOperationException("viiper.exe startet nicht");
        if (!EndsWithArcadia.TryAdd(process))
        {
            var error = Marshal.GetLastPInvokeError();
            var started = new ViiperProcess(process, log);
            started.Stop();
            started.Dispose();
            throw new InvalidOperationException($"viiper.exe ließe sich nicht mit Arcadia beenden (Fehler {error})");
        }

        return new ViiperProcess(process, log);
    }

    public IReadOnlyCollection<TcpListening> Listeners() => TcpListeners.All();

    public Task<ViiperAnswer> PingAsync(int port, byte[]? key, TimeSpan timeout, CancellationToken cancellationToken) =>
        ViiperConnection.PingAsync(port, key, timeout, cancellationToken);

    private sealed class ViiperProcess(Process process, Action<string> log) : IViiperProcess
    {
        public int Id { get; } = process.Id;

        public bool HasExited
        {
            get
            {
                try
                {
                    return process.HasExited;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    return true;
                }
            }
        }

        public void Stop()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(TimeSpan.FromSeconds(2));
                    log("VIIPER: viiper.exe beendet");
                }
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
                log($"VIIPER: viiper.exe nicht beendet: {ex.Message}");
            }
        }

        public void Dispose() => process.Dispose();
    }

    /// <summary>
    /// A job object that ends <c>viiper.exe</c> when Arcadia's process ends, also by a crash: Windows closes the job's
    /// handle then, and a pad nobody feeds would otherwise stay plugged in for every game.
    /// </summary>
    private static partial class EndsWithArcadia
    {
        private const int ExtendedLimitInformation = 9;
        private const uint KillOnJobClose = 0x2000;
        private static readonly Lazy<nint> Job = new(Create);

        /// <summary>False when it isn't bound to Arcadia; the caller must not keep it running then (#304).</summary>
        public static bool TryAdd(Process process) => Job.Value != 0 && AssignProcessToJobObject(Job.Value, process.Handle);

        private static nint Create()
        {
            var job = CreateJobObjectW(0, null);
            if (job == 0)
            {
                return 0;
            }

            var limits = new ExtendedLimits { BasicLimitFlags = KillOnJobClose };
            return SetInformationJobObject(job, ExtendedLimitInformation, ref limits, Marshal.SizeOf<ExtendedLimits>()) ? job : 0;
        }

        // JOBOBJECT_EXTENDED_LIMIT_INFORMATION on x64; only the flags are set.
        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimits
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint BasicLimitFlags;
            public nuint MinimumWorkingSetSize;
            public nuint MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public nuint Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
            public nuint ProcessMemoryLimit;
            public nuint JobMemoryLimit;
            public nuint PeakProcessMemoryUsed;
            public nuint PeakJobMemoryUsed;
        }

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        private static partial nint CreateJobObjectW(nint attributes, string? name);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetInformationJobObject(nint job, int infoClass, ref ExtendedLimits info, int length);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool AssignProcessToJobObject(nint job, nint process);
    }
}
