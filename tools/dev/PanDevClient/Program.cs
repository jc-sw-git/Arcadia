using System.Runtime.Versioning;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Pan;

namespace Arcadia.PanDevClient;

/// <summary>
/// Connects to Pan as Arcadia does (service started if needed, the pipe's server checked) and takes commands, one per
/// line on standard input: hide, unhide, check, freeze, resume, exit, quit. Every event goes to the log; the state the
/// check script waits for is written to the status file.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Pan runs on Windows only.");
            return 2;
        }

        var log = Option(args, "--log");
        var status = Option(args, "--status");
        var quiet = args.Contains("--quiet");
        using var client = new DevClient(log, status, quiet);
        return await client.RunAsync(Console.In);
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

[SupportedOSPlatform("windows")]
internal sealed class DevClient(string? logFile, string? statusFile, bool quiet) : IDisposable
{
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(40);

    private readonly SemaphoreSlim _requests = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private PanLink? _link;
    private TaskCompletionSource<PanMessage>? _reply;
    private uint _generation;
    private bool _emulating;
    private volatile bool _frozen;
    private uint? _tornDown;

    public async Task<int> RunAsync(TextReader commands)
    {
        try
        {
            _link = await new PanPipeTransport(Write).ConnectAsync(_stop.Token);
            Write($"connected: {_link.Description}");
            _ = Task.Run(ReadAsync);
            var hello = await RequestAsync(PanMessage.Of(PanCommand.Hello));
            if (hello.Reply != PanReply.HelloOk)
            {
                Status($"refused {hello.Reason}");
                return 3;
            }

            _ = Task.Run(HeartbeatAsync);
            Status("connected");
            while (await commands.ReadLineAsync() is { } line)
            {
                if (!await RunCommandAsync(line.Trim().ToLowerInvariant()))
                {
                    return 0;
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Write($"failed: {ex.Message}");
            Status($"error {ex.Message}");
            return 1;
        }
    }

    private async Task<bool> RunCommandAsync(string command)
    {
        switch (command)
        {
            case "hide":
                await EnsureEmulatingAsync();
                var hidden = await RequestAsync(PanMessage.Of(PanCommand.Hide, _generation));
                Status(hidden.Reply == PanReply.Hidden ? $"hidden {hidden.Value}" : $"not hidden {hidden.Reason}");
                break;
            case "unhide":
                var shown = await RequestAsync(PanMessage.Of(PanCommand.Unhide, _generation));
                Status(shown.Reply == PanReply.Shown ? "shown" : $"not shown {shown.Reason}");
                break;
            case "check":
                var check = await RequestAsync(PanMessage.Of(PanCommand.CheckHiding));
                Status($"check {check.Reason}");
                break;
            case "freeze":
                // Heartbeats stop, the pipe stays open: Pan sees a hang after 3 s, as with a frozen Arcadia.
                _frozen = true;
                Status("frozen");
                break;
            case "resume":
                _frozen = false;
                if (_tornDown is { } generation)
                {
                    await RequestAsync(PanMessage.Of(PanCommand.TeardownAcknowledged, generation));
                    _tornDown = null;
                    _emulating = false;
                }

                Status("resumed");
                break;
            case "exit":
                // Arcadia's planned exit: show the controller, stop, say goodbye.
                if (_emulating)
                {
                    await RequestAsync(PanMessage.Of(PanCommand.Unhide, _generation));
                    await RequestAsync(PanMessage.Of(PanCommand.EmulationStopped, _generation));
                }

                await RequestAsync(PanMessage.Of(PanCommand.PlannedExit, _generation));
                Status("exited");
                return false;
            case "quit":
                // A planned exit that leaves the controller hidden: Pan's teardown undoes it.
                await RequestAsync(PanMessage.Of(PanCommand.PlannedExit, _generation));
                Status("quit");
                return false;
            case "":
                break;
            default:
                Write($"unknown command {command}: hide, unhide, check, freeze, resume, exit, quit");
                break;
        }

        return true;
    }

    private async Task EnsureEmulatingAsync()
    {
        if (_emulating)
        {
            return;
        }

        var started = await RequestAsync(PanMessage.Of(PanCommand.EmulationStarted));
        if (started.Reply != PanReply.Started)
        {
            throw new InvalidOperationException($"emulation not started: {started}");
        }

        _generation = started.Generation;
        _emulating = true;
    }

    private async Task<PanMessage> RequestAsync(PanMessage message)
    {
        await _requests.WaitAsync(_stop.Token);
        try
        {
            _reply = new TaskCompletionSource<PanMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            await _link!.Stream.WriteAsync(message.ToBytes(), _stop.Token);
            await _link.Stream.FlushAsync(_stop.Token);
            var reply = await _reply.Task.WaitAsync(ReplyTimeout, _stop.Token);
            if (message.Command != PanCommand.Heartbeat || reply.Reply != PanReply.HeartbeatOk)
            {
                Write($"{message} -> {reply}");
            }

            return reply;
        }
        finally
        {
            _requests.Release();
        }
    }

    private async Task ReadAsync()
    {
        var buffer = new byte[PanMessage.Size];
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                await _link!.Stream.ReadExactlyAsync(buffer, _stop.Token);
                var message = PanMessage.Read(buffer);
                if (message.Reply == PanReply.TornDown)
                {
                    _tornDown = message.Generation;
                    Write($"Pan tore generation {message.Generation} down: {message.Reason}");
                    Status($"torn down {message.Reason}");
                    continue;
                }

                _reply?.TrySetResult(message);
            }
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or OperationCanceledException or ObjectDisposedException)
        {
            Write($"pipe closed: {ex.Message}");
            Status("disconnected");
            _reply?.TrySetException(ex);
        }
    }

    private async Task HeartbeatAsync()
    {
        using var timer = new PeriodicTimer(Guardian.HeartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                if (_emulating && !_frozen && _tornDown is null)
                {
                    await RequestAsync(PanMessage.Of(PanCommand.Heartbeat, _generation));
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Write($"heartbeat stopped: {ex.Message}");
        }
    }

    private void Status(string state)
    {
        Write($"status: {state}");
        for (var attempt = 0; statusFile is not null && attempt < 10; attempt++)
        {
            try
            {
                File.WriteAllText(statusFile, state);
                return;
            }
            catch (IOException)
            {
                // The check script is reading it right now.
                Thread.Sleep(50);
            }
        }
    }

    private void Write(string line)
    {
        var stamped = $"{DateTime.Now:HH:mm:ss.fff} {line}";
        if (!quiet)
        {
            Console.WriteLine(stamped);
        }

        if (logFile is not null)
        {
            lock (_requests)
            {
                File.AppendAllText(logFile, stamped + Environment.NewLine);
            }
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _link?.Dispose();
        _stop.Dispose();
    }
}
