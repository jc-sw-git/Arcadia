using System.Diagnostics;
using Microsoft.Win32;

namespace SteamInputProbe;

/// <summary>
/// The SISR technique without SISR (#112): pose as the Steam shortcut "SISR Marker" by loading Steam's overlay DLL
/// with the marker's game id, force the marker's Steam Input layout, then read Steam's virtual gamepad through
/// XInput. Optionally forwards it to a VIIPER Xbox 360 controller for games outside Steam.
/// </summary>
internal sealed class SteamPadProbe(ProbeOptions options, ProbeLog log)
{
    private readonly XInputState?[] _last = new XInputState?[4];
    private readonly int[] _packets = new int[4];
    private volatile int _forwardSlot = -1;
    private volatile ViiperForwarder? _viiper;
    private bool _forced;

    public async Task<int> RunAsync(CancellationToken cancel)
    {
        var steamDir = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
        if (steamDir is null || Process.GetProcessesByName("steam").Length == 0)
        {
            log.Line("Steam does not run (or SteamPath is missing). Start Steam and try again.");
            return 2;
        }
        if (Process.GetProcessesByName("SISR").Length > 0)
        {
            log.Line("SISR runs. Switch Arcadia's Steam Controller support off (or close Arcadia) first, else both read the controller.");
            return 2;
        }
        steamDir = Path.GetFullPath(steamDir);

        if (options.LoadOverlay) LoadOverlay(steamDir);
        else log.Line("overlay NOT loaded (control run)");
        if (options.Window) CreateToolWindow();

        using var watch = new DesktopWatch(log);
        var poller = Task.Run(() => Poll(cancel), cancel);
        var schedule = Task.Run(() => RunScheduleAsync(cancel), cancel);
        try
        {
            while (!options.ShouldStop(log)) await Task.Delay(100, cancel);
        }
        catch (OperationCanceledException)
        {
            log.Line("stopped by Ctrl+C");
        }
        finally
        {
            // SISR does the same on exit. Without it the marker's layout would stay forced on every window.
            if (_forced) OpenSteamUrl("steam://forceinputappid/0", "release layout on exit");
            if (_viiper is { } viiper) await viiper.DisposeAsync();
        }
        await Task.WhenAny(poller, Task.Delay(500, CancellationToken.None));
        return 0;
    }

    /// <summary>
    /// Picks the pad to forward before VIIPER's own Xbox 360 controller exists, so the probe never forwards its own
    /// output back into itself.
    /// </summary>
    private async Task StartViiperAsync(CancellationToken cancel)
    {
        _forwardSlot = Array.FindIndex(_last, s => s is not null);
        if (_forwardSlot < 0)
        {
            log.Line("VIIPER: no XInput pad to forward (Steam's virtual pad is missing), not started");
            return;
        }
        log.Line($"VIIPER: slot {_forwardSlot} will be forwarded");
        _viiper = await ViiperForwarder.StartAsync(log, cancel);
    }

    private void LoadOverlay(string steamDir)
    {
        // Same variables SISR sets (SISR steam/markerenv.go): the overlay reads them when it loads.
        var gameId = ((ulong)options.MarkerAppId << 32) | 0x02000000;
        Environment.SetEnvironmentVariable("SteamClientLaunch", "0");
        Environment.SetEnvironmentVariable("SteamAppId", "0");
        Environment.SetEnvironmentVariable("SteamGameId", gameId.ToString());
        Environment.SetEnvironmentVariable("SteamOverlayGameId", gameId.ToString());
        Environment.SetEnvironmentVariable("SteamPath", steamDir);
        Environment.SetEnvironmentVariable("SteamVirtualGamepadInfo", Path.Combine(steamDir, "config", "virtualgamepadinfo.txt"));
        var overlay = Path.Combine(steamDir, "GameOverlayRenderer64.dll");
        var handle = Native.LoadLibraryW(overlay);
        log.Line(handle != 0
            ? $"overlay loaded as marker {options.MarkerAppId} (game id {gameId})"
            : $"overlay FAILED to load from {overlay}, error {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}");
    }

    private void CreateToolWindow()
    {
        var window = Native.CreateWindowExW(Native.WsExToolWindow | Native.WsExNoActivate, "STATIC", "SteamInputProbe",
            Native.WsPopup, 0, 0, 1, 1, 0, 0, 0, 0);
        if (window != 0) Native.ShowWindow(window, Native.SwShowNoActivate);
        log.Line(window != 0 ? "1x1 tool window created" : "tool window FAILED");
    }

    private async Task RunScheduleAsync(CancellationToken cancel)
    {
        var events = new List<(double At, Action Run)>();
        foreach (var (at, text) in options.Prompts) events.Add((at, () => log.Line($"PROMPT >>> {text}")));
        if (options.Force) events.Add((options.ForceAt, () => _forced = OpenSteamUrl($"steam://forceinputappid/{options.MarkerAppId}", "force marker layout")));
        if (options.ReleaseAt is { } release) events.Add((release, () => _forced = !OpenSteamUrl("steam://forceinputappid/0", "release layout")));
        if (options.ReforceAt is { } reforce) events.Add((reforce, () => _forced = OpenSteamUrl($"steam://forceinputappid/{options.MarkerAppId}", "force marker layout again")));
        if (options.Viiper) events.Add((options.ForceAt + 2, () => StartViiperAsync(cancel).GetAwaiter().GetResult()));
        foreach (var (at, run) in events.OrderBy(e => e.At))
        {
            var wait = TimeSpan.FromSeconds(at) - TimeSpan.FromMilliseconds(log.ElapsedMs);
            if (wait > TimeSpan.Zero) await Task.Delay(wait, cancel);
            run();
        }
    }

    private bool OpenSteamUrl(string url, string why)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            log.Line($"{why}: {url}");
            return true;
        }
        catch (Exception ex)
        {
            log.Line($"{why} FAILED: {ex.Message}");
            return false;
        }
    }

    private void Poll(CancellationToken cancel)
    {
        var rate = Stopwatch.StartNew();
        var nextDisconnectedCheck = new long[4];
        while (!cancel.IsCancellationRequested)
        {
            for (uint slot = 0; slot < 4; slot++)
            {
                // XInput is slow on empty slots; look at those only twice a second.
                if (_last[slot] is null && log.ElapsedMs < nextDisconnectedCheck[slot]) continue;
                var connected = Native.XInputGetStateEx(slot, out var state) == Native.ErrorSuccess;
                if (!connected)
                {
                    if (_last[slot] is not null) log.Line($"slot {slot}: disconnected");
                    _last[slot] = null;
                    nextDisconnectedCheck[slot] = log.ElapsedMs + 500;
                    continue;
                }
                if (_last[slot] is not { } previous) log.Line($"slot {slot}: CONNECTED  {state.Describe()}");
                else
                {
                    if (state.PacketNumber != previous.PacketNumber) _packets[slot]++;
                    if (state.DiffersNoticeably(previous)) log.Line($"slot {slot}: {state.Describe()}");
                }
                _last[slot] = state;
                if (slot == _forwardSlot) _viiper?.Send(state);
            }
            if (rate.ElapsedMilliseconds >= 1000)
            {
                var busy = Enumerable.Range(0, 4).Where(s => _packets[s] > 0).Select(s => $"slot {s}: {_packets[s]} packets/s");
                var line = string.Join(", ", busy);
                if (line.Length > 0) log.Line(line);
                Array.Clear(_packets);
                rate.Restart();
            }
            Thread.Sleep(2);
        }
    }
}
