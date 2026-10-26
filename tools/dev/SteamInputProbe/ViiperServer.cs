using System.Diagnostics;
using Viiper.Client;

namespace SteamInputProbe;

/// <summary>
/// Gets a VIIPER server to talk to: a running one on localhost:3242, else starts SISR's own viiper.exe.
/// Shared by the Xbox 360 forwarder (spike #112) and the keyboard/mouse forwarder (spike #118), both of which
/// drive VIIPER from C# without SISR.exe.
/// </summary>
internal static class ViiperServer
{
    public const int ApiPort = 3242;

    /// <summary>The client, and the viiper.exe this helper started (null if one was already listening).</summary>
    public static async Task<(ViiperClient Client, Process? OwnServer)?> EnsureAsync(ProbeLog log, CancellationToken cancel)
    {
        var client = new ViiperClient("localhost", ApiPort);
        if (await PingAsync(client, log, cancel)) return (client, null);

        var ownServer = StartServer(log);
        if (ownServer is null) { client.Dispose(); return null; }
        var clock = Stopwatch.StartNew();
        while (!await PingAsync(client, log: null, cancel))
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(10))
            {
                log.Line("VIIPER: server did not answer within 10 s");
                ownServer.Kill();
                client.Dispose();
                return null;
            }
            await Task.Delay(200, cancel);
        }
        log.Line($"VIIPER: own server up after {clock.ElapsedMilliseconds} ms");
        return (client, ownServer);
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
}
