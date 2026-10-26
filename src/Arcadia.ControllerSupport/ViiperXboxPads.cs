using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Arcadia.Input;
using Viiper.Client;
using Viiper.Client.Devices.Xbox360;
using Viiper.Client.Types;

namespace Arcadia.ControllerSupport;

/// <summary>
/// Virtual Xbox 360 pads from VIIPER (https://github.com/Alia5/VIIPER, #213): Arcadia runs <c>viiper.exe</c> as its own
/// process, so a crash of either leaves the other, and drives it through VIIPER's MIT client over its local TCP API.
/// usbip-win2 attaches each pad as a USB device. Until #214 the <c>viiper.exe</c> is the one in SISR's folder.
/// </summary>
/// <param name="cleanUpLeftovers">
/// Ends a VIIPER from SISR's folder that outlived an earlier Arcadia and detaches its pads (SISR's own clean-up); run
/// once, before Arcadia's first server starts.
/// </param>
public sealed partial class ViiperXboxPads(string viiperExecutable, string logDirectory, Func<CancellationToken, Task<int>> cleanUpLeftovers, Action<string> log)
    : IVirtualXboxPads
{
    public const int ApiPort = 3242;

    private static readonly TimeSpan ServerStartTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(3);

    private readonly SemaphoreSlim _serverGate = new(1, 1);
    private Process? _server;
    private bool _cleanedUp;

    public string? Unavailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "nur unter Windows";
        }

        if (!File.Exists(viiperExecutable))
        {
            return $"{viiperExecutable} fehlt";
        }

        return UsbipDriver.IsRunning() ? null : "Treiber usbip nicht geladen";
    }

    public async Task<IVirtualXboxPad> PlugAsync(CancellationToken cancellationToken)
    {
        var client = new ViiperClient("localhost", ApiPort);
        try
        {
            await EnsureServerAsync(client, cancellationToken);
            using var timeout = Timeout(cancellationToken);
            var bus = (await client.BusCreateAsync(null, timeout.Token)).BusID;
            var device = await client.BusDeviceAddAsync(bus, new DeviceCreateRequest { Type = "xbox360" }, timeout.Token);
            var stream = await client.ConnectDeviceAsync(bus, device.DevID, timeout.Token);
            log($"Xbox-Pad: VIIPER-Controller {device.Vid}:{device.Pid} angesteckt (Bus {bus}, Gerät {device.DevID})");
            return new ViiperXboxPad(client, bus, device.DevID, stream, () => _server is { HasExited: true });
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task ShutdownAsync()
    {
        await _serverGate.WaitAsync();
        try
        {
            StopServer();
        }
        finally
        {
            _serverGate.Release();
        }
    }

    private async Task EnsureServerAsync(ViiperClient client, CancellationToken cancellationToken)
    {
        await _serverGate.WaitAsync(cancellationToken);
        try
        {
            if (_server is { HasExited: false } && await AnswersAsync(client, cancellationToken))
            {
                return;
            }

            if (_server is { HasExited: true } ended)
            {
                log($"Xbox-Pad: viiper.exe beendet (Code {ended.ExitCode})");
                ended.Dispose();
                _server = null;
            }

            if (!_cleanedUp)
            {
                _cleanedUp = true;
                var leftovers = await cleanUpLeftovers(cancellationToken);
                if (leftovers > 0)
                {
                    log($"Xbox-Pad: {leftovers} Reste eines früheren VIIPER aufgeräumt");
                }
            }

            if (_server is null && await AnswersAsync(client, cancellationToken))
            {
                // Another program's VIIPER holds the port: its server serves Arcadia's pad on a bus of its own.
                log("Xbox-Pad: ein VIIPER läuft schon, Arcadia nutzt ihn mit");
                return;
            }

            _server ??= StartServer();
            var clock = Stopwatch.StartNew();
            while (!await AnswersAsync(client, cancellationToken))
            {
                if (_server.HasExited || clock.Elapsed > ServerStartTimeout)
                {
                    StopServer();
                    throw new TimeoutException($"viiper.exe antwortet nicht nach {clock.Elapsed.TotalSeconds:0} s");
                }

                await Task.Delay(200, cancellationToken);
            }

            log($"Xbox-Pad: viiper.exe bereit nach {clock.ElapsedMilliseconds} ms");
        }
        finally
        {
            _serverGate.Release();
        }
    }

    private Process StartServer()
    {
        var start = new ProcessStartInfo(viiperExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "server", "--no-tray", $"--log.file={Path.Combine(logDirectory, "viiper.log")}" },
        };
        var process = Process.Start(start) ?? throw new InvalidOperationException("viiper.exe startet nicht");
        log($"Xbox-Pad: viiper.exe gestartet (PID {process.Id})");
        EndsWithArcadia.Add(process, log);
        return process;
    }

    private void StopServer()
    {
        if (_server is not { } server)
        {
            return;
        }

        _server = null;
        try
        {
            if (!server.HasExited)
            {
                server.Kill();
                server.WaitForExit(TimeSpan.FromSeconds(2));
                log("Xbox-Pad: viiper.exe beendet");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            log($"Xbox-Pad: viiper.exe nicht beendet: {ex.Message}");
        }
        finally
        {
            server.Dispose();
        }
    }

    private static async Task<bool> AnswersAsync(ViiperClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(1));
            await client.PingAsync(timeout.Token);
            return true;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static CancellationTokenSource Timeout(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ApiTimeout);
        return timeout;
    }

    private sealed class ViiperXboxPad : IVirtualXboxPad
    {
        private readonly ViiperClient client;
        private readonly uint bus;
        private readonly string deviceId;
        private readonly ViiperDevice stream;
        private readonly Func<bool> serverEnded;
        private volatile bool _broken;

        public ViiperXboxPad(ViiperClient client, uint bus, string deviceId, ViiperDevice stream, Func<bool> serverEnded)
        {
            this.client = client;
            this.bus = bus;
            this.deviceId = deviceId;
            this.stream = stream;
            this.serverEnded = serverEnded;
            stream.OnDisconnect = () => _broken = true;
        }

        public bool IsConnected => !_broken && !serverEnded();

        public async Task SendAsync(XboxPadState state, CancellationToken cancellationToken)
        {
            try
            {
                await stream.SendAsync(new Xbox360Input
                {
                    Buttons = state.Buttons,
                    Lt = state.LeftTrigger,
                    Rt = state.RightTrigger,
                    Lx = state.LeftX,
                    Ly = state.LeftY,
                    Rx = state.RightX,
                    Ry = state.RightY,
                }, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                _broken = true;
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            stream.OnDisconnect = null;
            try
            {
                await stream.DisposeAsync();
                using var timeout = new CancellationTokenSource(ApiTimeout);
                await client.BusDeviceRemoveAsync(bus, deviceId, timeout.Token);
                await client.BusRemoveAsync(bus, timeout.Token);
            }
            catch (Exception) when (_broken || serverEnded())
            {
                // The server is gone, and the pad with it.
            }
            finally
            {
                client.Dispose();
            }
        }
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

        public static void Add(Process process, Action<string> log)
        {
            if (!OperatingSystem.IsWindows() || Job.Value == 0 || !AssignProcessToJobObject(Job.Value, process.Handle))
            {
                log($"Xbox-Pad: viiper.exe endet nicht mit Arcadia (Fehler {Marshal.GetLastPInvokeError()})");
            }
        }

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
