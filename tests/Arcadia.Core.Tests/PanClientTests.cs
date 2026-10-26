using System.IO.Pipes;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Pan;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>
/// Arcadia's client against Pan's real <see cref="Guardian"/> over a stream pair, as the pipe would carry it (#243):
/// only the Windows pipe, the service and the processes are left out.
/// </summary>
public sealed class PanClientTests : IAsyncDisposable
{
    // Real time, generous: on the CI runner these tests share the machine with Pan.Tests' `dotnet publish`.
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(30);

    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly FakePanServer _server;
    private readonly PanClient _client;
    private readonly List<(uint Generation, PanReason Reason)> _tornDown = [];
    private readonly List<string> _lost = [];

    public PanClientTests()
    {
        _server = new FakePanServer(line => Log($"[Pan] {line}"));
        // Real time, so wider than Arcadia's 2 s: a loaded CI runner must not count as a hanging Pan.
        _client = new PanClient(_server, _time, Log) { RequestTimeout = TimeSpan.FromSeconds(10) };
        _client.TornDown += (generation, reason) => { lock (_tornDown) _tornDown.Add((generation, reason)); };
        _client.Lost += reason => { lock (_lost) _lost.Add(reason); };
    }

    private void Log(string line) { lock (_log) _log.Add(line); }

    private string LogText() { lock (_log) return string.Join("\n", _log); }

    public async ValueTask DisposeAsync()
    {
        await _client.DisposeAsync();
        _server.Dispose();
    }

    private async Task ConnectedAsync()
    {
        _client.Start();
        // The clock runs, so a hello that timed out on a loaded runner is retried instead of waiting forever.
        await AdvanceUntil(() => _client.IsConnected, "verbunden");
    }

    /// <summary>Lets the client's clock run in small steps until the condition holds: the sender wakes at its next delay.</summary>
    private async Task AdvanceUntil(Func<bool> condition, string what)
    {
        var until = DateTime.UtcNow + Soon;
        while (!condition())
        {
            if (DateTime.UtcNow > until)
            {
                throw new TimeoutException($"nicht eingetreten: {what}");
            }

            _time.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(5);
        }
    }

    private static async Task WaitUntil(Func<bool> condition, string what)
    {
        var until = DateTime.UtcNow + Soon;
        while (!condition())
        {
            if (DateTime.UtcNow > until)
            {
                throw new TimeoutException($"nicht eingetreten: {what}");
            }

            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Connects_says_hello_and_registers_an_emulation_with_the_generation_Pan_gives()
    {
        await ConnectedAsync();
        Assert.Null(_client.Unavailable());
        Assert.Contains("Pan: verbunden (Testserver, Protokoll 1, Generation 0)", LogText());

        var generation = await _client.EmulationStartedAsync(CancellationToken.None);

        Assert.Equal(1u, generation);
        Assert.True(_client.IsCurrent(generation));
        Assert.True(_server.Guardian.IsEmulating);

        await _client.EmulationStoppedAsync(generation, CancellationToken.None);
        Assert.False(_server.Guardian.IsEmulating);
        Assert.False(_client.IsCurrent(generation));
    }

    [Fact]
    public async Task The_heartbeat_goes_only_while_the_feeder_pulses_and_its_absence_lets_Pan_tear_down()
    {
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(CancellationToken.None);

        // Alive: pulses every poll, heartbeats every half second, Pan's clock running too.
        for (var i = 0; i < 10; i++)
        {
            _client.Pulse();
            await AdvanceUntil(() => _server.Heartbeats > i, $"Heartbeat {i + 1}");
            _server.Clock.Advance(Guardian.HeartbeatInterval);
            _server.Guardian.Tick();
        }

        Assert.True(_server.Guardian.IsEmulating);
        Assert.Empty(_tornDown);

        // The feeder stops pulsing: no heartbeat is sent, and three seconds later Pan tears the emulation down.
        for (var i = 0; i < 7; i++)
        {
            _time.Advance(Guardian.HeartbeatInterval);
            await Task.Delay(20);
            _server.Clock.Advance(Guardian.HeartbeatInterval);
            _server.Guardian.Tick();
        }

        await WaitUntil(() => _tornDown.Count == 1, "TornDown");
        Assert.Equal((generation, PanReason.Hang), _tornDown[0]);
        Assert.Equal(10, _server.Heartbeats);
        Assert.False(_client.IsCurrent(generation));
        Assert.True(_client.WasTornDown(generation));
        Assert.Null(_client.Unavailable());

        // Arcadia drops its devices, acknowledges, and the next emulation gets a new generation.
        await _client.AcknowledgeTeardownAsync(generation, CancellationToken.None);
        Assert.Equal(generation + 1, await _client.EmulationStartedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task While_Pan_still_tears_down_no_pad_is_possible_and_nothing_counts_as_a_loss()
    {
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(CancellationToken.None);
        _client.Pulse();
        await AdvanceUntil(() => _server.Heartbeats == 1, "Heartbeat 1");

        // Pan sees a hang and starts a teardown whose usbip step takes its time (review, point 3).
        var gate = new SemaphoreSlim(0);
        _server.Cleanup.TeardownGate = gate;
        _server.Clock.Advance(Guardian.DefaultHangTimeout);
        _server.Guardian.Tick();

        // The next heartbeat is rejected: the pad is lost once, and until Pan's notice Arcadia is told to wait.
        _client.Pulse();
        await AdvanceUntil(() => !_client.IsCurrent(generation), "Heartbeat abgelehnt");
        Assert.Equal(PanClient.CleaningUp, _client.Unavailable());
        Assert.Empty(_tornDown);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _client.EmulationStartedAsync(CancellationToken.None));
        Assert.Equal(PanClient.CleaningUp, ex.Message);
        Assert.Equal(PanClient.CleaningUp, _client.Unavailable());

        // The teardown ends: the notice comes, the next pad gets the next generation.
        gate.Release();
        await WaitUntil(() => _tornDown.Count == 1, "TornDown");
        Assert.Null(_client.Unavailable());
        await _client.AcknowledgeTeardownAsync(generation, CancellationToken.None);
        Assert.Equal(generation + 1, await _client.EmulationStartedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_Pan_of_another_protocol_version_is_reported_and_not_used()
    {
        _server.HelloReply = PanMessage.Of(PanReply.Rejected, 0, PanReason.WrongProtocolVersion);
        _client.Start();

        await WaitUntil(() => _client.Unavailable()?.Contains("anderes Protokoll", StringComparison.Ordinal) == true, "Protokollfehler");

        Assert.False(_client.IsConnected);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.EmulationStartedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Without_the_service_the_reason_is_shown_and_the_client_keeps_trying_slowly()
    {
        _server.Refuse = new PanUnavailableException("Pan ist nicht eingerichtet", permanent: true);
        _client.Start();

        await WaitUntil(() => _client.Unavailable() == "Pan ist nicht eingerichtet", "Grund");
        Assert.Equal(1, _server.Connections);

        _server.Refuse = null;
        _time.Advance(PanClient.Retry);
        await Task.Delay(50);
        Assert.False(_client.IsConnected);
        _time.Advance(PanClient.SlowRetry);
        await WaitUntil(() => _client.IsConnected, "verbunden nach dem langsamen Wiederholen");
    }

    [Fact]
    public async Task A_Pan_that_stops_answering_counts_as_lost_and_the_emulation_with_it()
    {
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(CancellationToken.None);
        _server.Pause();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _client.EmulationStartedAsync(CancellationToken.None));

        Assert.Contains("antwortet nicht", ex.Message);
        Assert.False(_client.IsConnected);
        Assert.False(_client.IsCurrent(generation));
        Assert.Contains("antwortet nicht innerhalb von 10 s", Assert.Single(_lost));
        Assert.Contains("die Emulation wird entfernt", LogText());

        // Pan is back (restarted by the service manager): the keeper reconnects.
        _server.Resume();
        _time.Advance(PanClient.Retry);
        await WaitUntil(() => _client.IsConnected, "wieder verbunden");
    }

    [Fact]
    public async Task A_broken_pipe_is_a_loss_and_a_restarted_Pan_goes_on_from_its_journal()
    {
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(CancellationToken.None);

        // Pan dies: its pipe breaks without a goodbye; the service manager restarts it, and its journal says the
        // emulation was open, so it undoes it before serving anyone.
        _server.Die();
        await WaitUntil(() => !_client.IsConnected, "Verbindung weg");
        Assert.Contains("Verbindung", Assert.Single(_lost));
        Assert.False(_client.IsCurrent(generation));

        _server.RestartFromJournal();
        _time.Advance(PanClient.Retry);
        await ConnectedAsync();

        Assert.Equal(generation + 1, await _client.EmulationStartedAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_generation_torn_down_on_an_earlier_connection_is_current_again_on_a_Pan_that_counts_anew()
    {
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(CancellationToken.None);
        _server.Clock.Advance(Guardian.DefaultHangTimeout);
        _server.Guardian.Tick();
        await WaitUntil(() => _tornDown.Count == 1, "TornDown");
        await _client.AcknowledgeTeardownAsync(generation, CancellationToken.None);
        Assert.True(_client.WasTornDown(generation));

        // Pan's journal went with a reinstall, so the restarted Pan hands out generation 1 again: for this connection
        // it is a live one, not the torn-down one of before (review, bug 4).
        _server.Die();
        await WaitUntil(() => !_client.IsConnected, "Verbindung weg");
        _server.RestartWithoutJournal();
        _time.Advance(PanClient.Retry);
        await ConnectedAsync();

        Assert.Equal(generation, await _client.EmulationStartedAsync(CancellationToken.None));
        Assert.False(_client.WasTornDown(generation));
        Assert.True(_client.IsCurrent(generation));
        await _client.EmulationStoppedAsync(generation, CancellationToken.None);
        Assert.False(_server.Guardian.IsEmulating);
    }

    [Fact]
    public async Task An_unreadable_journal_is_passed_on_for_a_toast()
    {
        var problems = 0;
        _client.JournalProblem += () => problems++;
        _server.HelloReply = PanMessage.Of(PanReply.HelloOk, 0, PanReason.JournalUnreadable);

        await ConnectedAsync();

        Assert.Equal(1, problems);
        Assert.Contains("Journal war nicht lesbar", LogText());
    }

    /// <summary>Pan's guardian served over anonymous pipes, as <c>PipeServer</c> would serve it over the named pipe.</summary>
    private sealed class FakePanServer : IPanTransport, IDisposable
    {
        private readonly Action<string> _log;
        private readonly FakeCleanupForClient _cleanup = new();
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "arcadia-pan-client-tests", Guid.NewGuid().ToString("N"));
        private readonly SemaphoreSlim _paused = new(1, 1);
        private DuplexStream? _serverSide;
        private Task _serving = Task.CompletedTask;
        private volatile bool _dead;
        private int _heartbeats;

        public FakePanServer(Action<string> log)
        {
            _log = log;
            Guardian = new Guardian(Clock, new JournalFile(Path.Combine(_folder, "journal.json")), _cleanup, log);
        }

        public Guardian Guardian { get; private set; }

        /// <summary>The fake machine the guardian cleans up; a test can hold its teardown.</summary>
        public FakeCleanupForClient Cleanup => _cleanup;

        public FakeClockForClient Clock { get; } = new();

        public PanUnavailableException? Refuse { get; set; }

        /// <summary>Overrides the guardian's answer to hello.</summary>
        public PanMessage? HelloReply { get; set; }

        public int Connections { get; private set; }

        public int Heartbeats => Volatile.Read(ref _heartbeats);

        public async Task<PanLink> ConnectAsync(CancellationToken cancellationToken)
        {
            Connections++;
            if (Refuse is { } refuse)
            {
                throw refuse;
            }

            // As with the real pipe: the last connection has ended (its client unregistered) before the next one is served.
            await _serving.WaitAsync(cancellationToken);
            var toServer = new AnonymousPipeServerStream(PipeDirection.Out);
            var toClient = new AnonymousPipeServerStream(PipeDirection.Out);
            var clientSide = new DuplexStream(new AnonymousPipeClientStream(PipeDirection.In, toClient.ClientSafePipeHandle), toServer);
            _serverSide = new DuplexStream(new AnonymousPipeClientStream(PipeDirection.In, toServer.ClientSafePipeHandle), toClient);
            var guardian = Guardian;
            guardian.Register(new ClientIdentity(Environment.ProcessId, 1, InConsoleSession: true));
            var serverSide = _serverSide;
            _serving = Task.Run(() => ServeAsync(guardian, serverSide));
            return new PanLink(clientSide, "Testserver");
        }

        /// <summary>Pan hangs: nothing is answered until <see cref="Resume"/>.</summary>
        public void Pause() => _paused.Wait();

        public void Resume() => _paused.Release();

        /// <summary>Pan died: its end of the pipe closes and nothing of it runs on.</summary>
        public void Die()
        {
            _dead = true;
            _serverSide?.Dispose();
        }

        /// <summary>A restarted Pan: it undoes what its journal lists, then serves again.</summary>
        public void RestartFromJournal()
        {
            _dead = false;
            Guardian = new Guardian(Clock, new JournalFile(Path.Combine(_folder, "journal.json")), _cleanup, _log);
            Guardian.RecoverAsync().GetAwaiter().GetResult();
        }

        /// <summary>A reinstalled Pan with an empty data folder: it counts the generations from 0 again.</summary>
        public void RestartWithoutJournal()
        {
            _dead = false;
            Guardian = new Guardian(Clock, new JournalFile(Path.Combine(_folder, "reinstalled", "journal.json")), _cleanup, _log);
            Guardian.RecoverAsync().GetAwaiter().GetResult();
        }

        private async Task ServeAsync(Guardian guardian, DuplexStream stream)
        {
            var buffer = new byte[PanMessage.Size];
            void Notify(PanMessage message) => stream.WriteAsync(message.ToBytes()).AsTask().ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);
            guardian.Notify += Notify;
            try
            {
                while (true)
                {
                    var read = 0;
                    while (read < buffer.Length)
                    {
                        var n = await stream.ReadAsync(buffer.AsMemory(read));
                        if (n == 0)
                        {
                            return;
                        }

                        read += n;
                    }

                    var message = PanMessage.Read(buffer);
                    if (message.Command == PanCommand.Heartbeat)
                    {
                        Interlocked.Increment(ref _heartbeats);
                    }

                    var reply = guardian.Handle(message);
                    if (message.Command == PanCommand.Hello && HelloReply is { } forced)
                    {
                        reply = forced;
                    }

                    await _paused.WaitAsync();
                    _paused.Release();
                    await stream.WriteAsync(reply.ToBytes());
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
            }
            finally
            {
                guardian.Notify -= Notify;
                if (!_dead)
                {
                    // A dead Pan notices nothing; a living one sees the client go, as the pipe server does.
                    guardian.ClientGone();
                }
            }
        }

        public void Dispose()
        {
            _serverSide?.Dispose();
            try
            {
                Directory.Delete(_folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private sealed class FakeClockForClient : IPanClock
    {
        public long UnbiasedMilliseconds { get; private set; }

        public void Advance(TimeSpan by) => UnbiasedMilliseconds += (long)by.TotalMilliseconds;
    }

    /// <summary>Nothing to clean up: the client tests look at the protocol, not at processes.</summary>
    private sealed class FakeCleanupForClient : IEmulationCleanup
    {
        /// <summary>Set, the teardown's first step waits here: a usbip step that takes its time.</summary>
        public SemaphoreSlim? TeardownGate { get; set; }

        public async Task<IReadOnlyList<int>> ListLocalViiperPortsAsync(CancellationToken cancellationToken)
        {
            if (TeardownGate is { } gate)
            {
                await gate.WaitAsync(cancellationToken);
            }

            return [];
        }

        public Task DetachPortAsync(int port, CancellationToken cancellationToken) => Task.CompletedTask;

        public IReadOnlyList<HeldProcess> TakeChildrenOf(int clientPid, long clientStartTime, string imageName) => [];

        public bool TryHold(HeldProcess process) => false;

        public Task EndAsync(HeldProcess process, CancellationToken cancellationToken) => Task.CompletedTask;

        public void Release(HeldProcess process)
        {
        }

        public long? StartTimeOf(int pid) => null;
    }

    /// <summary>Reads from one stream, writes to another: the two directions of a pipe.</summary>
    private sealed class DuplexStream(Stream input, Stream output) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => output.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => input.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => input.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => output.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                input.Dispose();
                output.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
