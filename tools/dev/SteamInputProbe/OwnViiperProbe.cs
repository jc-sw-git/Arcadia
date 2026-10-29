using System.Diagnostics;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Copy;
using Arcadia.Input;
using Arcadia.Pan;

namespace SteamInputProbe;

/// <summary>
/// #304's assumptions, checked with Arcadia's own code (ViiperServer, ViiperXboxPads): its viiper.exe on 127.0.0.1 with
/// ports from Windows, its own key, refusing clients without it; a pad usbip-win2 attaches; an authenticated raw stream.
/// Steps: start (the start and its checks), pad (an Xbox pad until the stop file), raw (a hidproxy device and the write
/// times of its encrypted stream).
/// </summary>
internal sealed class OwnViiperProbe(ProbeOptions options, ProbeLog log)
{
    public async Task<int> RunAsync(CancellationToken cancel)
    {
        var executable = options.ViiperPath ?? throw new ArgumentException("--viiper-exe fehlt");
        var configRoot = options.ConfigRoot ?? throw new ArgumentException("--config fehlt");
        var logDirectory = Path.GetDirectoryName(log.Path)!;
        var host = new WindowsViiperHost(log.Line);
        var server = new Arcadia.ControllerSupport.ViiperServer(executable, configRoot, logDirectory, host, log.Line);
        log.Line($"Probe 304, Schritt {options.Step}: {executable}, Schlüsselordner {configRoot}");
        return options.Step switch
        {
            "start" => await StartAsync(server, host, configRoot, cancel),
            "pad" => await PadAsync(server, cancel),
            "raw" => await RawAsync(server, cancel),
            _ => throw new ArgumentException($"unbekannter Schritt {options.Step}"),
        };
    }

    private async Task<int> StartAsync(Arcadia.ControllerSupport.ViiperServer server, WindowsViiperHost host, string configRoot, CancellationToken cancel)
    {
        RunningViiper running;
        try
        {
            running = await server.StartAsync(cancel);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Line($"ERGEBNIS start: NICHT OK ({ex.GetType().Name}: {ex.Message})");
            return 2;
        }

        using (running)
        {
            foreach (var listener in host.Listeners().Where(l => l.Pid == running.ProcessId))
            {
                log.Line($"viiper.exe lauscht auf {listener.Address}:{listener.Port}");
            }

            var ok = Result("nur 127.0.0.1, zwei Ports, kein 3241/3242",
                host.Listeners().Where(l => l.Pid == running.ProcessId).ToList() is { Count: 2 } own
                && own.All(l => System.Net.IPAddress.Loopback.Equals(l.Address) && l.Port is not (3241 or 3242)));
            ok &= Result("mit Schlüssel: Antwort", await host.PingAsync(running.ApiPort, running.DerivedKey, TimeSpan.FromSeconds(2), cancel) == ViiperAnswer.Answered);
            ok &= Result("ohne Schlüssel: abgelehnt", await host.PingAsync(running.ApiPort, null, TimeSpan.FromSeconds(2), cancel) == ViiperAnswer.Refused);
            ok &= Result("falscher Schlüssel: abgelehnt", await host.PingAsync(running.ApiPort, new byte[32], TimeSpan.FromSeconds(2), cancel) == ViiperAnswer.Refused);
            ok &= Result("Schlüsseldatei gelöscht", !File.Exists(ViiperKeyFile.PathIn(configRoot)));
            ok &= Result("Schlüssel nicht in viiper.log", !LogContains(running.Key));
            return ok ? 0 : 1;
        }
    }

    private async Task<int> PadAsync(Arcadia.ControllerSupport.ViiperServer server, CancellationToken cancel)
    {
        var pads = new ViiperXboxPads(server, log.Line);
        try
        {
            var port = await pads.PrepareAsync(cancel);
            log.Line($"usbip-Port des Servers: {port}");
            await using var pad = await pads.PlugAsync(cancel);
            Result("Xbox-Pad angesteckt", true);
            var pressed = false;
            while (!options.ShouldStop(log) && pad.IsConnected)
            {
                // A blinks once a second, so joy.cpl shows the pad is alive.
                pressed = !pressed;
                await pad.SendAsync(new XboxPadState(pressed ? (ushort)0x1000 : (ushort)0, 0, 0, 0, 0, 0, 0), cancel);
                await Task.Delay(1000, cancel);
            }

            return Result("Pad blieb verbunden", pad.IsConnected) ? 0 : 1;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Line($"ERGEBNIS pad: NICHT OK ({ex.GetType().Name}: {ex.Message})");
            return 2;
        }
        finally
        {
            await pads.ShutdownAsync();
        }
    }

    private async Task<int> RawAsync(Arcadia.ControllerSupport.ViiperServer server, CancellationToken cancel)
    {
        var pads = new ViiperXboxPads(server, log.Line);
        try
        {
            await pads.PrepareAsync(cancel);
            await using var device = await pads.AddRawDeviceAsync("hidproxy", NeutralDevice(), cancel);
            Result("hidproxy-Gerät über verschlüsselten Strom angesteckt", true);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            var answering = AnswerControlsAsync(device.Stream, stop.Token);
            await Task.Delay(3000, cancel);

            var times = new List<double>();
            var frame = new byte[HidProxyFrames.HeaderSize + 64];
            var report = new byte[64];
            for (var i = 0; i < 3000; i++)
            {
                report[1] = (byte)i;
                var length = HidProxyFrames.Write(frame, HidProxyFrames.Input, 0, report);
                var start = Stopwatch.GetTimestamp();
                await device.Stream.WriteAsync(frame.AsMemory(0, length), cancel);
                times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                await Task.Delay(1, cancel);
            }

            times.Sort();
            var p99 = times[(int)(times.Count * 0.99)];
            log.Line($"Schreibzeit verschlüsselt, {times.Count} Berichte: Mittel {times.Average():0.000} ms, p50 {times[times.Count / 2]:0.000} ms, p99 {p99:0.000} ms, max {times[^1]:0.000} ms");
            var ok = Result("p99 unter 1 ms (T242-18)", p99 < 1);
            while (!options.ShouldStop(log))
            {
                await Task.Delay(200, cancel);
            }

            await stop.CancelAsync();
            await answering;
            return ok ? 0 : 1;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Line($"ERGEBNIS raw: NICHT OK ({ex.GetType().Name}: {ex.Message})");
            return 2;
        }
        finally
        {
            await pads.ShutdownAsync();
        }
    }

    /// <summary>The puck's descriptors with a test vendor and product id (pid.codes 1209:0001), so Steam leaves it alone.</summary>
    private static Dictionary<string, object?> NeutralDevice()
    {
        var specific = PuckDescriptors.Triton.DeviceSpecific("PROBE304");
        var device = Convert.FromHexString((string)specific["device"]!);
        device[8] = 0x09;
        device[9] = 0x12;
        device[10] = 0x01;
        device[11] = 0x00;
        specific["device"] = Convert.ToHexString(device);
        return specific;
    }

    /// <summary>Windows asks the new HID device things at enumeration; the probe refuses all (a stall), which HID accepts.</summary>
    private async Task AnswerControlsAsync(Stream stream, CancellationToken cancel)
    {
        var reply = new byte[HidProxyFrames.HeaderSize + 8];
        try
        {
            while (await HidProxyFrames.ReadAsync(stream, cancel) is { } frame)
            {
                if (frame.Kind == HidProxyFrames.Control && frame.Payload.Length >= 2)
                {
                    var sequence = BitConverter.ToUInt16(frame.Payload, 0);
                    var length = HidProxyFrames.WriteReply(reply, frame.Interface, sequence, done: false, []);
                    await stream.WriteAsync(reply.AsMemory(0, length), cancel);
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // Done.
        }
    }

    private bool LogContains(string key)
    {
        var file = Path.Combine(Path.GetDirectoryName(log.Path)!, "viiper.log");
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Contains(key, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private bool Result(string what, bool ok)
    {
        log.Line($"ERGEBNIS {what}: {(ok ? "OK" : "NICHT OK")}");
        return ok;
    }
}
