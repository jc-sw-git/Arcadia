using System.Diagnostics;
using System.Threading.Channels;
using Viiper.Client;
using Viiper.Client.Devices.Xbox360;
using Viiper.Client.Types;

namespace SteamInputProbe;

/// <summary>
/// Drives a VIIPER Xbox 360 controller through VIIPER's TCP API (MIT client "Viiper.Client"), the way SISR does,
/// but from C#. Uses a VIIPER that already listens on localhost:3242, else starts the viiper.exe from SISR's folder.
/// </summary>
internal sealed class ViiperForwarder : IAsyncDisposable
{
    private const int ApiPort = 3242;
    private readonly ProbeLog _log;
    private readonly ViiperClient _client;
    private readonly Process? _ownServer;
    private readonly uint _bus;
    private readonly Device _device;
    private readonly ViiperDevice _stream;
    private readonly Channel<XInputState> _states = Channel.CreateBounded<XInputState>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Task _sender;
    private ushort _lastButtons;
    private int _sent;

    private ViiperForwarder(ProbeLog log, ViiperClient client, Process? ownServer, uint bus, Device device, ViiperDevice stream)
    {
        _log = log;
        _client = client;
        _ownServer = ownServer;
        _bus = bus;
        _device = device;
        _stream = stream;
        _stream.OnDisconnect = () => _log.Line("VIIPER: device stream closed by the server");
        _sender = Task.Run(SendLoopAsync);
    }

    public static async Task<ViiperForwarder?> StartAsync(ProbeLog log, CancellationToken cancel)
    {
        var client = new ViiperClient("localhost", ApiPort);
        Process? ownServer = null;
        try
        {
            if (!await PingAsync(client, log, cancel))
            {
                ownServer = StartServer(log);
                if (ownServer is null) return null;
                var clock = Stopwatch.StartNew();
                while (!await PingAsync(client, log: null, cancel))
                {
                    if (clock.Elapsed > TimeSpan.FromSeconds(10))
                    {
                        log.Line("VIIPER: server did not answer within 10 s");
                        ownServer.Kill();
                        return null;
                    }
                    await Task.Delay(200, cancel);
                }
                log.Line($"VIIPER: own server up after {clock.ElapsedMilliseconds} ms");
            }
            var bus = (await client.BusCreateAsync(null, cancel)).BusID;
            var device = await client.BusDeviceAddAsync(bus, new DeviceCreateRequest { Type = "xbox360" }, cancel);
            var stream = await client.ConnectDeviceAsync(bus, device.DevID, cancel);
            log.Line($"VIIPER: Xbox 360 controller {device.Vid}:{device.Pid} created on bus {bus}, device {device.DevID}");
            return new ViiperForwarder(log, client, ownServer, bus, device, stream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Line($"VIIPER FAILED: {ex.GetType().Name}: {ex.Message}");
            ownServer?.Kill();
            client.Dispose();
            return null;
        }
    }

    private static async Task<bool> PingAsync(ViiperClient client, ProbeLog? log, CancellationToken cancel)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            timeout.CancelAfter(TimeSpan.FromSeconds(1));
            var ping = await client.PingAsync(timeout.Token);
            log?.Line($"VIIPER: {ping.Server} {ping.Version} already listens on {ApiPort}");
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancel.IsCancellationRequested)
        {
            return false;
        }
    }

    private static Process? StartServer(ProbeLog log)
    {
        var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SISR", "viiper.exe");
        if (!File.Exists(exe))
        {
            log.Line($"VIIPER: {exe} not found");
            return null;
        }
        var logFile = Path.Combine(Path.GetDirectoryName(log.Path)!, "viiper.log");
        var start = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "server", "--no-tray", $"--log.file={logFile}" },
        };
        log.Line($"VIIPER: starting {exe} server --no-tray (log {logFile})");
        return Process.Start(start);
    }

    public void Send(XInputState state) => _states.Writer.TryWrite(state);

    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (var state in _states.Reader.ReadAllAsync())
            {
                await _stream.SendAsync(new Xbox360Input
                {
                    Buttons = state.Buttons,
                    Lt = state.LeftTrigger,
                    Rt = state.RightTrigger,
                    Lx = state.LeftX,
                    Ly = state.LeftY,
                    Rx = state.RightX,
                    Ry = state.RightY,
                });
                _sent++;
                if (state.Buttons != _lastButtons) _log.Line($"VIIPER: sent buttons=0x{state.Buttons:X4}");
                _lastButtons = state.Buttons;
            }
        }
        catch (Exception ex)
        {
            _log.Line($"VIIPER send FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _states.Writer.TryComplete();
        await Task.WhenAny(_sender, Task.Delay(1000));
        _log.Line($"VIIPER: {_sent} states sent; removing the device");
        try
        {
            await _stream.DisposeAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _client.BusDeviceRemoveAsync(_bus, _device.DevID, timeout.Token);
            await _client.BusRemoveAsync(_bus, timeout.Token);
        }
        catch (Exception ex)
        {
            _log.Line($"VIIPER cleanup: {ex.GetType().Name}: {ex.Message}");
        }
        _client.Dispose();
        if (_ownServer is { HasExited: false })
        {
            _ownServer.Kill();
            _log.Line("VIIPER: own server stopped");
        }
        _ownServer?.Dispose();
    }
}
