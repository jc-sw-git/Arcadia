using Arcadia.Input;
using Viiper.Client;
using Viiper.Client.Devices.Xbox360;
using Viiper.Client.Types;

namespace Arcadia.ControllerSupport;

/// <summary>
/// Virtual Xbox 360 pads from VIIPER (https://github.com/Alia5/VIIPER, #213): Arcadia runs <c>viiper.exe</c> as its own
/// process, so a crash of either leaves the other, and drives it through VIIPER's MIT client over its local TCP API.
/// usbip-win2 attaches each pad as a USB device. Only Arcadia's own build, started and checked by
/// <see cref="ViiperServer"/> (#304): never another program's VIIPER, and every connection with its key.
/// </summary>
public sealed class ViiperXboxPads(ViiperServer server, Action<string> log) : IVirtualXboxPadServer
{
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(3);

    private readonly SemaphoreSlim _serverGate = new(1, 1);
    private RunningViiper? _server;

    public string? Unavailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "nur unter Windows";
        }

        if (!File.Exists(server.Executable))
        {
            return $"{server.Executable} fehlt";
        }

        return UsbipDriver.IsRunning() ? null : "Treiber usbip nicht geladen";
    }

    /// <summary>The usbip port of the VIIPER that runs now, or null; a new one means a new emulation (#304, decision 11).</summary>
    public int? ServerPort => _server is { HasExited: false } running ? running.UsbipPort : null;

    /// <summary>
    /// Arcadia's own VIIPER, or none (#304): only the pinned build next to Arcadia (<paramref name="ownExecutable"/>, null
    /// without a pin), never another program's <c>viiper.exe</c>; its key below <paramref name="dataFolder"/>.
    /// </summary>
    public static ViiperXboxPads? Own(string? ownExecutable, string dataFolder, string logDirectory, IViiperHost? host, Action<string> log) =>
        ownExecutable is null || host is null
            ? null
            : new ViiperXboxPads(new ViiperServer(ownExecutable, Path.Combine(dataFolder, "viiper"), logDirectory, host, log), log);

    public async Task<int> PrepareAsync(CancellationToken cancellationToken)
    {
        await _serverGate.WaitAsync(cancellationToken);
        try
        {
            // A VIIPER whose port another process took too would be refused by Pan at every start; a new one gets new ports.
            if (_server is { } running && await server.IsStillTrustedAsync(running, cancellationToken))
            {
                return running.UsbipPort;
            }

            StopServer(_server is { HasExited: true } ? "viiper.exe hat sich beendet" : null);
            _server = await server.StartAsync(cancellationToken);
            return _server.UsbipPort;
        }
        finally
        {
            _serverGate.Release();
        }
    }

    public async Task<IVirtualXboxPad> PlugAsync(CancellationToken cancellationToken)
    {
        var running = Trusted();
        var client = new ViiperClient(LoopbackHost, running.ApiPort, running.Key);
        uint? bus = null;
        try
        {
            using var timeout = Timeout(cancellationToken);
            bus = (await client.BusCreateAsync(ViiperBusIds.Next(), timeout.Token)).BusID;
            var device = await client.BusDeviceAddAsync(bus.Value, new DeviceCreateRequest { Type = "xbox360" }, timeout.Token);
            var stream = await client.ConnectDeviceAsync(bus.Value, device.DevID, timeout.Token);
            log($"Xbox-Pad: VIIPER-Controller {device.Vid}:{device.Pid} angesteckt (Bus {bus}, Gerät {device.DevID})");
            return new ViiperXboxPad(client, bus.Value, device.DevID, stream, () => running.HasExited);
        }
        catch
        {
            // Half plugged in: removing the bus removes its device too, so no pad without a stream is left behind.
            if (bus is { } created)
            {
                await RemoveBusAsync(client, created);
            }

            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// A pad of the kind (#238 D3): the Xbox 360 pad as before, every other kind on VIIPER's raw stream
    /// (<see cref="VirtualPadReports"/>).
    /// </summary>
    public async Task<IVirtualPad> PlugAsync(PadKind kind, CancellationToken cancellationToken)
    {
        if (kind == PadKind.Xbox360)
        {
            return new XboxPadAsVirtualPad(await PlugAsync(cancellationToken));
        }

        var device = await AddRawDeviceAsync(VirtualPadReports.DeviceType(kind), [], cancellationToken);
        return new ViiperReportPad(kind, device);
    }

    /// <summary>
    /// Adds a device of another type on a bus of its own on the same server, and opens its raw stream, authenticated and
    /// encrypted: the copy of the Steam Controller's puck (#242, type <c>hidproxy</c> of the VIIPER fork). Throws when
    /// that fails; then nothing is left behind. The device goes when the returned one is disposed.
    /// </summary>
    public async Task<ViiperRawDevice> AddRawDeviceAsync(string type, Dictionary<string, object?> deviceSpecific, CancellationToken cancellationToken)
    {
        var running = Trusted();
        var client = new ViiperClient(LoopbackHost, running.ApiPort, running.Key);
        uint? bus = null;
        Stream? stream = null;
        try
        {
            using var timeout = Timeout(cancellationToken);
            bus = (await client.BusCreateAsync(ViiperBusIds.Next(), timeout.Token)).BusID;
            var device = await client.BusDeviceAddAsync(bus.Value, new DeviceCreateRequest { Type = type, DeviceSpecific = deviceSpecific }, timeout.Token);
            stream = await ConnectRawAsync(running, bus.Value, device.DevID, timeout.Token);
            log($"VIIPER: Gerät {type} angesteckt (Bus {bus}, Gerät {device.DevID})");
            return new ViiperRawDevice(client, bus.Value, stream, () => running.HasExited);
        }
        catch
        {
            stream?.Dispose();
            if (bus is { } created)
            {
                await RemoveBusAsync(client, created);
            }

            client.Dispose();
            throw;
        }
    }

    /// <summary>"127.0.0.1", never "localhost": that may reach another program listening on <c>::1</c>.</summary>
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>
    /// The VIIPER started in <see cref="PrepareAsync"/>, its listeners checked again: a device never goes onto a server
    /// Arcadia didn't start and check, nor starts a new one (that is a new emulation, so Pan learns its port first).
    /// </summary>
    private RunningViiper Trusted()
    {
        if (_server is not { HasExited: false } running)
        {
            throw new InvalidOperationException("VIIPER läuft nicht");
        }

        server.CheckListeners(running);
        return running;
    }

    /// <summary>VIIPER's device stream as plain bytes after the handshake: the path, ended by a zero byte (as VIIPER's own client does).</summary>
    private static async Task<Stream> ConnectRawAsync(RunningViiper running, uint bus, string deviceId, CancellationToken cancellationToken)
    {
        var stream = await ViiperConnection.ConnectAsync(running.ApiPort, running.DerivedKey, cancellationToken);
        try
        {
            await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"bus/{bus}/{deviceId}\0"), cancellationToken);
            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    public async Task ShutdownAsync()
    {
        await _serverGate.WaitAsync();
        try
        {
            StopServer(null);
        }
        finally
        {
            _serverGate.Release();
        }
    }

    private void StopServer(string? why)
    {
        if (_server is not { } running)
        {
            return;
        }

        _server = null;
        if (why is not null)
        {
            log($"VIIPER: {why}");
        }

        running.Dispose();
    }

    private async Task RemoveBusAsync(ViiperClient client, uint bus)
    {
        try
        {
            using var timeout = new CancellationTokenSource(ApiTimeout);
            await client.BusRemoveAsync(bus, timeout.Token);
        }
        catch (Exception ex)
        {
            log($"Xbox-Pad: Bus {bus} nicht entfernt: {ex.Message}");
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

        // Viiper.Client's DisposeAsync waits for its read loop before it closes the stream, and that loop waits for the
        // output reader: a read without this token hangs every unplug until the game rumbles (QE review of #338).
        private readonly CancellationTokenSource _reading = new();
        private volatile bool _broken;

        public ViiperXboxPad(ViiperClient client, uint bus, string deviceId, ViiperDevice stream, Func<bool> serverEnded)
        {
            this.client = client;
            this.bus = bus;
            this.deviceId = deviceId;
            this.stream = stream;
            this.serverEnded = serverEnded;
            stream.OnDisconnect = () => _broken = true;
            stream.OnOutput = ReadOutputAsync;
        }

        public event Action<XboxRumble>? Rumbled;

        /// <summary>VIIPER's output of an Xbox 360 pad: the two motors, one byte each (large, then small).</summary>
        private async Task ReadOutputAsync(Stream output)
        {
            var motors = new byte[2];
            try
            {
                await output.ReadExactlyAsync(motors, _reading.Token);
            }
            catch (OperationCanceledException) when (_reading.IsCancellationRequested)
            {
                // The pad is being unplugged: no exception into Viiper.Client's read loop, it ends with the dispose.
                return;
            }

            Rumbled?.Invoke(new XboxRumble(motors[0], motors[1]));
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
            catch (Exception)
            {
                // Failed or timed out (the caller's token): either way the pad no longer follows the controller.
                _broken = true;
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            stream.OnDisconnect = null;
            stream.OnOutput = null;
            await _reading.CancelAsync();
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
                _reading.Dispose();
            }
        }
    }
}

/// <summary>
/// The bus of every device Arcadia puts on a VIIPER, never one used before in this run (#331). usbip-win2 attaches a
/// device again by itself when its connection ends without a detach (as when a device is removed from VIIPER), retrying
/// for up to two hours. With VIIPER's lowest free bus the retry found the next pad or copy under the same bus id, and
/// Steam listed each twice; a bus id that is never served again leaves the retry nothing to attach.
/// </summary>
public static class ViiperBusIds
{
    /// <summary>
    /// A random start: Arcadia started again may get its old usbip port from Windows while the driver still retries a
    /// device of the run before.
    /// </summary>
    private static uint _last = (uint)Random.Shared.Next(0, 1 << 20);

    /// <summary>A bus id not handed out before in this process; never 0, which VIIPER reads as "the lowest free".</summary>
    public static uint Next()
    {
        var next = Interlocked.Increment(ref _last);
        return next != 0 ? next : Interlocked.Increment(ref _last);
    }
}
