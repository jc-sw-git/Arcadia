using Viiper.Client;
using Viiper.Client.Devices.Keyboard;
using Viiper.Client.Devices.Mouse;
using Viiper.Client.Types;

namespace SteamInputProbe;

/// <summary>
/// Spike #118: a VIIPER virtual HID keyboard and mouse, driven from C# without SISR.exe (the way spike #112 drove a
/// VIIPER Xbox 360 pad). These are real USB devices via usbip-win2, so their input reaches the Secure Desktop that
/// a UAC prompt runs on — unlike SendInput or Steam's desktop layout. Used to test whether UAC can be answered.
/// </summary>
internal sealed class ViiperHid : IAsyncDisposable
{
    private readonly ProbeLog _log;
    private readonly ViiperClient _client;
    private readonly System.Diagnostics.Process? _ownServer;
    private readonly uint _bus;
    private readonly Device _keyboard;
    private readonly Device _mouse;
    private readonly ViiperDevice _keyStream;
    private readonly ViiperDevice _mouseStream;

    private ViiperHid(ProbeLog log, ViiperClient client, System.Diagnostics.Process? ownServer, uint bus,
        Device keyboard, Device mouse, ViiperDevice keyStream, ViiperDevice mouseStream)
    {
        _log = log;
        _client = client;
        _ownServer = ownServer;
        _bus = bus;
        _keyboard = keyboard;
        _mouse = mouse;
        _keyStream = keyStream;
        _mouseStream = mouseStream;
        _keyStream.OnDisconnect = () => _log.Line("VIIPER keyboard: stream closed by the server");
        _mouseStream.OnDisconnect = () => _log.Line("VIIPER mouse: stream closed by the server");
    }

    public static async Task<ViiperHid?> StartAsync(ProbeLog log, CancellationToken cancel)
    {
        if (await ViiperServer.EnsureAsync(log, cancel) is not { } server) return null;
        var (client, ownServer) = server;
        try
        {
            var bus = (await client.BusCreateAsync(null, cancel)).BusID;
            var keyboard = await client.BusDeviceAddAsync(bus, new DeviceCreateRequest { Type = "keyboard" }, cancel);
            var keyStream = await client.ConnectDeviceAsync(bus, keyboard.DevID, cancel);
            var mouse = await client.BusDeviceAddAsync(bus, new DeviceCreateRequest { Type = "mouse" }, cancel);
            var mouseStream = await client.ConnectDeviceAsync(bus, mouse.DevID, cancel);
            log.Line($"VIIPER: keyboard {keyboard.Vid}:{keyboard.Pid} (dev {keyboard.DevID}) and mouse {mouse.Vid}:{mouse.Pid} "
                     + $"(dev {mouse.DevID}) created on bus {bus}");
            return new ViiperHid(log, client, ownServer, bus, keyboard, mouse, keyStream, mouseStream);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Line($"VIIPER HID FAILED: {ex.GetType().Name}: {ex.Message}");
            ownServer?.Kill();
            client.Dispose();
            return null;
        }
    }

    /// <summary>Taps one key: press, short hold, release. Modifiers unused here (UAC navigation needs none).</summary>
    public async Task TapKeyAsync(Key key, CancellationToken cancel)
    {
        await _keyStream.SendAsync(new KeyboardInput { Modifiers = 0, Count = 1, Keys = [(byte)key] }, cancel);
        await Task.Delay(40, cancel);
        await _keyStream.SendAsync(new KeyboardInput { Modifiers = 0, Count = 0, Keys = [] }, cancel);
    }

    /// <summary>Moves the pointer by a relative delta (one report).</summary>
    public Task MoveMouseAsync(short dx, short dy, CancellationToken cancel) =>
        _mouseStream.SendAsync(new MouseInput { Buttons = 0, Dx = dx, Dy = dy, Wheel = 0, Pan = 0 }, cancel);

    /// <summary>Presses and releases a mouse button (bit 0 = left, bit 1 = right).</summary>
    public async Task ClickAsync(byte button, CancellationToken cancel)
    {
        await _mouseStream.SendAsync(new MouseInput { Buttons = button, Dx = 0, Dy = 0, Wheel = 0, Pan = 0 }, cancel);
        await Task.Delay(40, cancel);
        await _mouseStream.SendAsync(new MouseInput { Buttons = 0, Dx = 0, Dy = 0, Wheel = 0, Pan = 0 }, cancel);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _keyStream.DisposeAsync();
            await _mouseStream.DisposeAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _client.BusDeviceRemoveAsync(_bus, _keyboard.DevID, timeout.Token);
            await _client.BusDeviceRemoveAsync(_bus, _mouse.DevID, timeout.Token);
            await _client.BusRemoveAsync(_bus, timeout.Token);
            _log.Line("VIIPER: keyboard and mouse removed");
        }
        catch (Exception ex)
        {
            _log.Line($"VIIPER HID cleanup: {ex.GetType().Name}: {ex.Message}");
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
