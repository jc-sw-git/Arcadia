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
    private const string LeftAlone = "Pan wird aktualisiert";

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

    /// <summary>A failure on a loaded CI runner says what the client and Pan logged.</summary>
    private async Task<T> LoggedAsync<T>(Func<Task<T>> request)
    {
        try
        {
            return await request();
        }
        catch (InvalidOperationException ex)
        {
            throw new Xunit.Sdk.XunitException($"{ex.Message}\n{LogText()}", ex);
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
        Assert.Contains("Pan: verbunden (Testserver, Protokoll 3, Generation 0)", LogText());

        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);

        Assert.Equal(1u, generation);
        Assert.True(_client.IsCurrent(generation));
        Assert.True(_server.Guardian.IsEmulating);

        await _client.EmulationStoppedAsync(generation, CancellationToken.None);
        Assert.False(_server.Guardian.IsEmulating);
        Assert.False(_client.IsCurrent(generation));
    }

    [Fact]
    public async Task An_unwanted_Pan_is_let_go_silently_and_not_reached_again_until_it_is_wanted()
    {
        // #326: an old Pan the player didn't update is left alone, so it gives everything back and ends itself.
        var wanted = true;
        await using var client = new PanClient(_server, _time, Log) { RequestTimeout = TimeSpan.FromSeconds(10), WhyLeftAlone = () => Volatile.Read(ref wanted) ? null : LeftAlone };
        var lost = 0;
        client.Lost += _ => Interlocked.Increment(ref lost);
        client.Start();
        await AdvanceUntil(() => client.IsConnected, "verbunden");

        Volatile.Write(ref wanted, false);
        await AdvanceUntil(() => !client.IsConnected, "getrennt");
        Assert.Equal(LeftAlone, client.Unavailable());
        Assert.Equal(0, lost);
        for (var i = 0; i < 5; i++)
        {
            _time.Advance(PanClient.Retry);
            await Task.Delay(20);
        }

        Assert.False(client.IsConnected);

        Volatile.Write(ref wanted, true);
        await AdvanceUntil(() => client.IsConnected, "wieder verbunden");
    }

    [Fact]
    public async Task Never_wanted_the_first_attempt_ends_without_reaching_Pan()
    {
        await using var client = new PanClient(_server, _time, Log) { WhyLeftAlone = () => LeftAlone };
        client.Start();

        await client.FirstAttempt.WaitAsync(Soon);

        Assert.False(client.IsConnected);
        Assert.Equal(LeftAlone, client.Unavailable());
    }

    [Fact]
    public async Task The_heartbeat_goes_only_while_the_feeder_pulses_and_its_absence_lets_Pan_tear_down()
    {
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);

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
        Assert.Equal(generation + 1, await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None));
    }

    [Fact]
    public async Task While_Pan_still_tears_down_no_pad_is_possible_and_nothing_counts_as_a_loss()
    {
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);
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
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None));
        Assert.Equal(PanClient.CleaningUp, ex.Message);
        Assert.Equal(PanClient.CleaningUp, _client.Unavailable());

        // The teardown ends: the notice comes, the next pad gets the next generation.
        gate.Release();
        await WaitUntil(() => _tornDown.Count == 1, "TornDown");
        Assert.Null(_client.Unavailable());
        await _client.AcknowledgeTeardownAsync(generation, CancellationToken.None);
        Assert.Equal(generation + 1, await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None));
    }

    [Fact]
    public async Task A_request_the_caller_gives_up_on_cannot_answer_the_next_one()
    {
        // Review point 5 (reproduced there): the reply to a cancelled request arrived while the next one waited and
        // passed for its reply, so a plug took a stale start confirmation.
        await ConnectedAsync();
        _server.Pause();
        using var giveUp = new CancellationTokenSource();
        var first = _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, giveUp.Token);
        await AdvanceUntil(() => _server.Cleanup.ChildrenTaken == 1, "Anfrage bei Pan");
        giveUp.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        // The link that still owes a reply is gone; the reply then goes nowhere.
        Assert.False(_client.IsConnected);
        _server.Resume();
        await AdvanceUntil(() => _client.IsConnected, "neu verbunden");

        var next = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);
        Assert.Equal(2u, next);
        Assert.DoesNotContain("unerwartete Antwort", LogText());
    }

    [Fact]
    public async Task A_Pan_of_another_protocol_version_is_reported_and_not_used()
    {
        _server.HelloReply = PanMessage.Of(PanReply.Rejected, 0, PanReason.WrongProtocolVersion);
        _client.Start();

        await WaitUntil(() => _client.Unavailable()?.Contains("anderes Protokoll", StringComparison.Ordinal) == true, "Protokollfehler");

        Assert.False(_client.IsConnected);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None));
    }

    [Fact]
    public async Task A_transport_that_knows_Pan_is_missing_is_not_asked_to_connect()
    {
        // Owner's test 2026-10-26: the debugger showed "Service 'ArcadiaPan' was not found" and "Pan ist nicht
        // eingerichtet" thrown at every retry; the keeper now asks first and waits without an exception.
        _server.NotThere = ("Pan ist nicht eingerichtet", true);
        _client.Start();

        await AdvanceUntil(() => _client.Unavailable() == "Pan ist nicht eingerichtet", "der Grund");
        _time.Advance(TimeSpan.FromMinutes(2));
        await Task.Delay(50);
        Assert.Equal(0, _server.Connections);

        _server.NotThere = null;
        await AdvanceUntil(() => _client.IsConnected, "verbunden, sobald Pan da ist");
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
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);
        _server.Pause();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None));

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

    /// <summary>#370: a broken subscriber of Lost is reported; the request that dropped the link fails for its own reason.</summary>
    [Fact]
    public async Task A_throwing_Lost_handler_does_not_fault_the_request_and_the_other_handlers_still_hear_it()
    {
        var reported = new List<string>();
        var heard = new List<string>();
        await using var client = new PanClient(_server, _time, Log)
        {
            RequestTimeout = TimeSpan.FromSeconds(10),
            Unexpected = (error, context) => { lock (reported) reported.Add($"{context}: {error.Message}"); },
        };
        client.Lost += _ => throw new ApplicationException("handler broke");
        client.Lost += reason => { lock (heard) heard.Add(reason); };
        client.Start();
        await AdvanceUntil(() => client.IsConnected, "verbunden");
        await client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);
        _server.Pause();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None));

        Assert.Contains("antwortet nicht", ex.Message);
        Assert.Contains("antwortet nicht", Assert.Single(heard));
        Assert.Equal("Pan-Ereignis: handler broke", Assert.Single(reported));
        Assert.Contains("ein Empfänger von Lost ist fehlgeschlagen", LogText());
        _server.Resume();
    }

    /// <summary>#370: an error nobody expected in one attempt is reported and the keeper tries again, not ends.</summary>
    [Fact]
    public async Task A_throwing_transport_is_reported_once_and_the_keeper_keeps_trying()
    {
        var reported = new List<string>();
        var transport = new ThrowingOnce(_server);
        await using var client = new PanClient(transport, _time, Log)
        {
            RequestTimeout = TimeSpan.FromSeconds(10),
            Unexpected = (error, context) => { lock (reported) reported.Add($"{context}: {error.Message}"); },
        };

        client.Start();
        await client.FirstAttempt.WaitAsync(Soon);
        await AdvanceUntil(() => client.IsConnected, "verbunden nach dem Fehler");

        Assert.Equal("Verbindung zu Pan halten: transport broke", Assert.Single(reported));
    }

    /// <summary>A transport whose first look throws something no caller expects.</summary>
    private sealed class ThrowingOnce(IPanTransport inner) : IPanTransport
    {
        private int _throws = 1;

        public (string Reason, bool Permanent)? Unavailable() =>
            Interlocked.Exchange(ref _throws, 0) == 1 ? throw new InvalidOperationException("transport broke") : inner.Unavailable();

        public Task<PanLink> ConnectAsync(CancellationToken cancellationToken) => inner.ConnectAsync(cancellationToken);
    }

    [Fact]
    public async Task A_broken_pipe_is_a_loss_and_a_restarted_Pan_goes_on_from_its_journal()
    {
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);

        // Pan dies: its pipe breaks without a goodbye; the service manager restarts it, and its journal says the
        // emulation was open, so it undoes it before serving anyone.
        _server.Die();
        // The loss is reported after the link is marked gone, so wait for the report itself (flaky before, QE review of PR #264).
        await WaitUntil(() => !_client.IsConnected && _lost.Count > 0, "Verbindung weg");
        Assert.Contains("Verbindung", Assert.Single(_lost));
        Assert.False(_client.IsCurrent(generation));

        _server.RestartFromJournal();
        _time.Advance(PanClient.Retry);
        await ConnectedAsync();

        Assert.Equal(generation + 1, await LoggedAsync(() => _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None)));
    }

    [Fact]
    public async Task While_Pan_shows_the_Steam_Controller_again_no_pad_is_tried_and_no_loss_counted_then_it_goes_on()
    {
        await ConnectedAsync();
        _server.StartReply = PanMessage.Of(PanReply.Rejected, 1, PanReason.SteamControllerBeingShown);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None));

        // "Pan räumt noch auf" is what keeps the pad from counting a loss (SteamControllerXboxPad.PlugAsync).
        Assert.Equal(PanClient.CleaningUp, refused.Message);
        Assert.Equal(PanClient.CleaningUp, _client.Unavailable());
        Assert.Empty(_lost);

        _server.StartReply = null;
        _time.Advance(PanClient.BeingShownWait);
        Assert.Null(_client.Unavailable());
        Assert.True(_client.IsCurrent(await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None)));
    }

    [Fact]
    public async Task A_generation_torn_down_on_an_earlier_connection_is_current_again_on_a_Pan_that_counts_anew()
    {
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);
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

        Assert.Equal(generation, await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None));
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
    [Fact]
    [Trait("TestCase", "T243-35")]
    public async Task Arcadia_learns_from_Pan_why_it_could_not_hide_now()
    {
        var hidHide = new SettableHidHide();
        _server.UseHiding(hidHide);
        await ConnectedAsync();

        Assert.Equal(PanReason.None, await _client.CheckHidingAsync(CancellationToken.None));
        hidHide.Busy = true;
        Assert.Equal(PanReason.HidHideBusy, await _client.CheckHidingAsync(CancellationToken.None));
        hidHide.Busy = false;
        hidHide.Inverse = true;
        Assert.Equal(PanReason.HidHideInverseMode, await _client.CheckHidingAsync(CancellationToken.None));
        Assert.True(_client.IsConnected);
    }

    [Fact]
    [Trait("TestCase", "T243-35")]
    public async Task A_Pan_without_hiding_or_without_a_connection_gives_no_answer()
    {
        Assert.Null(await _client.CheckHidingAsync(CancellationToken.None));

        await ConnectedAsync();
        Assert.Null(await _client.CheckHidingAsync(CancellationToken.None));
        Assert.True(_client.IsConnected);
    }

    [Fact]
    [Trait("TestCase", "T242-5")]
    public async Task Arcadia_hides_within_its_emulation_hears_when_the_hiding_is_lost_and_shows_the_controller_again()
    {
        var hidHide = new SettableHidHide();
        _server.UseHiding(hidHide);
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);

        Assert.Equal(PanReason.None, await _client.HideAsync(generation, CancellationToken.None));
        Assert.True(hidHide.Cloaking);
        Assert.Contains(ArcadiaImage, hidHide.Allowed);
        Assert.Equal(PanReason.None, await _client.CheckHidingAsync(generation, CancellationToken.None));

        // Someone turns hiding off in HidHide's configuration client.
        hidHide.Cloaking = false;
        Assert.Equal(PanReason.HidingLost, await _client.CheckHidingAsync(generation, CancellationToken.None));

        Assert.True(await _client.UnhideAsync(generation, CancellationToken.None));
        Assert.DoesNotContain(ArcadiaImage, hidHide.Allowed);
        Assert.True(_client.IsConnected);
    }

    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task A_rejected_hide_is_undone_at_once()
    {
        var hidHide = new SettableHidHide();
        _server.UseHiding(hidHide);
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);

        // The allow-list entry goes in, then turning cloaking on fails: the hide is rejected with part of it done.
        hidHide.ChangesBeforeFailing = 1;

        Assert.Equal(PanReason.HidingFailed, await _client.HideAsync(generation, CancellationToken.None));

        Assert.Contains("Pan: Hide abgelehnt", LogText());
        Assert.Contains("Pan: Steam Controller wieder sichtbar", LogText());
        Assert.DoesNotContain(ArcadiaImage, hidHide.Allowed);
        Assert.False(hidHide.Cloaking);
    }

    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task Hiding_outside_an_emulation_is_refused_and_a_Pan_without_hiding_cannot_hide()
    {
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);
        Assert.Equal(PanReason.HidingFailed, await _client.HideAsync(generation, CancellationToken.None));
        Assert.Contains("Pan zu alt", LogText());
        Assert.True(_client.IsConnected);
    }

    [Fact]
    [Trait("TestCase", "T335-4")]
    public async Task Arcadia_hides_two_controllers_and_shows_one_again_while_the_other_stays_hidden()
    {
        var hidHide = new SettableHidHide();
        _server.UseHiding(hidHide, controllers: new TwoControllers());
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);

        Assert.Equal(new ControllerHidingResult(true, PanReason.None), await _client.HideControllerAsync(generation, TwoControllers.DualSense, CancellationToken.None));
        Assert.Equal(new ControllerHidingResult(true, PanReason.ControllerNeedsReplug), await _client.HideControllerAsync(generation, TwoControllers.XInputPad, CancellationToken.None));
        Assert.Equal(new ControllerHidingResult(true, PanReason.ControllerNeedsReplug), await _client.ShowControllerAsync(generation, TwoControllers.XInputPad, CancellationToken.None));

        Assert.Equal([TwoControllers.DualSenseHid], hidHide.GetHiddenDevices());
        Assert.Contains(ArcadiaImage, hidHide.Allowed);
        Assert.True(hidHide.Cloaking);
    }

    [Fact]
    [Trait("TestCase", "T335-5")]
    public async Task A_controller_is_not_hidden_while_its_vendors_firmware_tool_runs_and_Arcadia_hears_why()
    {
        var hidHide = new SettableHidHide();
        _server.UseHiding(hidHide, controllers: new TwoControllers(), processRuns: name => name == "FWupdater");
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);

        Assert.Equal(new ControllerHidingResult(false, PanReason.FirmwareToolRuns), await _client.HideControllerAsync(generation, TwoControllers.DualSense, CancellationToken.None));

        Assert.Empty(hidHide.GetHiddenDevices());
        Assert.Contains("abgelehnt", LogText());
    }

    [Fact]
    [Trait("TestCase", "T335-4")]
    public async Task A_rejected_controller_hide_is_undone_at_once()
    {
        var hidHide = new SettableHidHide();
        _server.UseHiding(hidHide, controllers: new TwoControllers { RestartFails = true });
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);

        Assert.Equal(new ControllerHidingResult(false, PanReason.HidingFailed), await _client.HideControllerAsync(generation, TwoControllers.DualSense, CancellationToken.None));

        // Shown again by the client's undo; its restart is left to the teardown.
        Assert.Empty(hidHide.GetHiddenDevices());
        Assert.DoesNotContain(ArcadiaImage, hidHide.Allowed);
    }

    /// <summary>A DualSense by USB and an XInput pad (#335), each with the devices a game reads.</summary>
    private sealed class TwoControllers : IControllerDevices
    {
        public static readonly ControllerModel DualSense = new(0x054C, 0x0CE6);
        public static readonly ControllerModel XInputPad = new(0x2DC8, 0x310B);
        public const string DualSenseHid = @"HID\VID_054C&PID_0CE6&MI_03\7&2B7F&0&0000";

        public bool RestartFails;

        public ControllerScan Scan(ControllerModel model) =>
            model == DualSense ? new(true, [new(DualSenseHid, ControllerInterface.Hid)])
            : model == XInputPad ? new(true, [new(@"USB\VID_2DC8&PID_310B&MI_00\7&5E6F&0&0000", ControllerInterface.XInput)])
            : new(false, []);

        public Task<IReadOnlyList<string>> RestartAsync(IReadOnlyList<string> hidInstances, CancellationToken cancellationToken) =>
            RestartFails ? throw new InvalidOperationException("device not opened") : Task.FromResult<IReadOnlyList<string>>([]);
    }

    /// <summary>QE review point 8 of PR #264: a check holds the client's request lock; a start meanwhile waits for it.</summary>
    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task An_emulation_started_while_a_hiding_check_waits_on_Pan_goes_through_after_it()
    {
        var hidHide = new SettableHidHide();
        _server.UseHiding(hidHide);
        await ConnectedAsync();
        _server.Pause();
        var check = _client.CheckHidingAsync(CancellationToken.None);
        await WaitUntil(() => _server.Received(PanCommand.CheckHiding) > 0, "CheckHiding bei Pan");
        var start = _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);

        _server.Resume();

        Assert.Equal(PanReason.None, await check);
        Assert.Equal(1u, await LoggedAsync(() => start));
        Assert.True(_client.IsConnected);
    }

    /// <summary>#308, QE review: the teardown after a show that HidHide refused restarted the puck while Arcadia held it.</summary>
    [Fact]
    [Trait("TestCase", "T308-7")]
    public async Task Pan_restarting_the_puck_on_its_own_has_Arcadia_let_go_first_and_read_it_again_after()
    {
        var events = new List<string>();
        void Note(string e) { lock (events) events.Add(e); }
        _client.PuckRestarting += () => Note("let go");
        _client.PuckRestarted += () => Note("read again");
        var hidHide = new SettableHidHide();
        _server.UseHiding(hidHide, new RecordingPuck(events));
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);
        Assert.Equal(PanReason.None, await _client.HideAsync(generation, CancellationToken.None));

        // HidHide's configuration client is open: Arcadia's show is refused, the teardown after the stop shows it.
        hidHide.Busy = true;
        Assert.False(await _client.UnhideAsync(generation, CancellationToken.None));
        hidHide.Busy = false;
        await _client.EmulationStoppedAsync(generation, CancellationToken.None);
        await WaitUntil(() => { lock (events) return events.Count == 4; }, "Neustart von Pan");

        // The hide's own restart isn't announced: Arcadia let go before it asked (PanEmulation).
        Assert.Equal(["restart", "let go", "restart", "read again"], events);
        Assert.DoesNotContain("didn't let go", LogText());
        Assert.Contains("Pan startet den Steam Controller neu: Arcadia lässt ihn los", LogText());
    }

    [Fact]
    [Trait("TestCase", "T308-7")]
    public async Task Arcadia_reads_the_puck_again_when_Pan_is_lost_during_its_own_restart()
    {
        var events = new List<string>();
        void Note(string e) { lock (events) events.Add(e); }
        _client.PuckRestarting += () => Note("let go");
        _client.PuckRestarted += () => Note("read again");
        var puck = new RecordingPuck(events);
        _server.UseHiding(new SettableHidHide(), puck);
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);
        Assert.Equal(PanReason.None, await _client.HideAsync(generation, CancellationToken.None));

        puck.Hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await _client.EmulationStoppedAsync(generation, CancellationToken.None);
        await WaitUntil(() => { lock (events) return events.Count == 3; }, "Neustart von Pan begonnen");
        _server.Die();

        await WaitUntil(() => { lock (events) return events.Count == 4; }, "wieder gelesen");
        Assert.Equal(["restart", "let go", "restart", "read again"], events);
        puck.Hold.SetResult();
    }

    /// <summary>
    /// The owner's debugger showed "Pan antwortet nicht" at every start (#308): the heartbeat waited behind the ~2.5 s hide
    /// and gave up. While another request runs no heartbeat is queued; Pan's hang watch waits for that request.
    /// </summary>
    [Fact]
    public async Task No_heartbeat_waits_behind_a_request_that_takes_long()
    {
        await ConnectedAsync();
        var generation = await _client.EmulationStartedAsync(FakeCleanupForClient.ViiperPort, CancellationToken.None);
        _server.Pause();
        var check = _client.CheckHidingAsync(generation, CancellationToken.None);
        await WaitUntil(() => _server.Received(PanCommand.CheckHiding) > 0, "CheckHiding bei Pan");

        for (var i = 0; i < 4; i++)
        {
            _client.Pulse();
            _time.Advance(Guardian.HeartbeatInterval);
            await Task.Delay(20);
        }

        _server.Resume();
        await check;
        await Task.Delay(300);
        Assert.Equal(0, _server.Heartbeats);
        Assert.True(_client.IsConnected);
    }

    /// <summary>HidHide 1.5.230 whose state a test sets; "busy" is its configuration client holding it.</summary>
    private sealed class SettableHidHide : IHidHideDriver
    {
        private readonly Lock _lock = new();
        private List<string> _hidden = [];
        private List<string> _allowed = [];
        private bool _cloaking;

        public volatile bool Busy;
        public volatile bool Inverse;

        /// <summary>How many changes go through before one fails, as with HidHide's driver restarting for a moment; -1: none fails.</summary>
        public int ChangesBeforeFailing = -1;

        public bool Cloaking
        {
            get { lock (_lock) return _cloaking; }
            set { lock (_lock) _cloaking = value; }
        }

        public IReadOnlyList<string> Allowed
        {
            get { lock (_lock) return [.. _allowed]; }
        }

        public Version? InstalledVersion() => HidHideRelease.Version;

        public bool IsServiceRegistered() => true;

        public IReadOnlyList<string> GetHiddenDevices() => Reach<IReadOnlyList<string>>(() => [.. _hidden]);

        public void SetHiddenDevices(IReadOnlyList<string> instanceIds) => Change(() => _hidden = [.. instanceIds]);

        public IReadOnlyList<string> GetAllowedApplications() => Reach<IReadOnlyList<string>>(() => [.. _allowed]);

        public void SetAllowedApplications(IReadOnlyList<string> imagePaths) => Change(() => _allowed = [.. imagePaths]);

        public bool GetCloaking() => Reach(() => _cloaking);

        public void SetCloaking(bool on) => Change(() => _cloaking = on);

        public bool GetInverse() => Reach(() => Inverse);

        private T Reach<T>(Func<T> value)
        {
            lock (_lock) return Busy ? throw new HidHideException(PanReason.HidHideBusy, "HidHide is open in another program") : value();
        }

        private void Change(Action change)
        {
            if (ChangesBeforeFailing == 0)
            {
                ChangesBeforeFailing = -1;
                throw new HidHideException(PanReason.HidingFailed, "HidHide's driver is restarting");
            }

            if (ChangesBeforeFailing > 0)
            {
                ChangesBeforeFailing--;
            }

            Reach(() =>
            {
                change();
                return true;
            });
        }
    }

    /// <summary>No Steam Controller connected: checking needs none.</summary>
    private sealed class NoPuck : ISteamControllerDevices
    {
        public IReadOnlyList<string> PresentHidInstances() => [];

        public Task RestartAsync(IReadOnlyCollection<ListedDevice>? only, CancellationToken cancellationToken) => Task.CompletedTask;

        public IReadOnlyList<string> PresentBootloaders() => [];
    }

    /// <summary>A puck whose restarts go into the test's list of events; one can be held until the test lets it finish.</summary>
    private sealed class RecordingPuck(List<string> events) : ISteamControllerDevices
    {
        public TaskCompletionSource? Hold;

        public IReadOnlyList<string> PresentHidInstances() => [@"HID\VID_28DE&PID_1304&MI_02&COL03\8&1A2B3C4D&0&0002"];

        public IReadOnlyList<string> PresentBootloaders() => [];

        public async Task RestartAsync(IReadOnlyCollection<ListedDevice>? only, CancellationToken cancellationToken)
        {
            lock (events) events.Add("restart");
            if (Hold is { } hold)
            {
                await hold.Task.WaitAsync(cancellationToken);
            }
        }
    }

    private const string ArcadiaImage = @"\Device\HarddiskVolume3\Program Files\Arcadia\Arcadia.exe";

    private sealed class FakePanServer : IPanTransport, IDisposable
    {
        private readonly Action<string> _log;
        private readonly FakeCleanupForClient _cleanup = new();
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "arcadia-pan-client-tests", Guid.NewGuid().ToString("N"));
        private readonly SemaphoreSlim _paused = new(1, 1);
        private DuplexStream? _serverSide;
        private Task _serving = Task.CompletedTask;
        // The guardian of a Pan that died: its pipe can close only after the restart.
        private volatile Guardian? _deadGuardian;
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

        /// <summary>Set, the transport says so before any connection is tried, as the real one does without the service.</summary>
        public (string Reason, bool Permanent)? NotThere { get; set; }

        public (string Reason, bool Permanent)? Unavailable() => NotThere;

        /// <summary>Overrides the guardian's answer to hello.</summary>
        public PanMessage? HelloReply { get; set; }

        /// <summary>Overrides the guardian's answer to a teardown's acknowledgement.</summary>
        public PanMessage? AcknowledgeReply { get; set; }

        /// <summary>Overrides the guardian's answer to "emulation started".</summary>
        public PanMessage? StartReply { get; set; }

        public int Connections { get; private set; }

        public int Heartbeats => Volatile.Read(ref _heartbeats);

        private readonly int[] _received = new int[256];

        public int Received(PanCommand command) => Volatile.Read(ref _received[(byte)command]);

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
            guardian.Register(new ClientIdentity(Environment.ProcessId, 1, InConsoleSession: true, ArcadiaImage));
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
            _deadGuardian = Guardian;
            _serverSide?.Dispose();
        }

        /// <summary>A Pan with part B's hiding (#243 B2), on HidHide as the test sets it; before the client connects.</summary>
        public void UseHiding(IHidHideDriver hidHide, ISteamControllerDevices? devices = null, IControllerDevices? controllers = null, Func<string, bool>? processRuns = null) =>
            Guardian = new Guardian(Clock, new JournalFile(Path.Combine(_folder, "journal.json")), _cleanup, _log, hiding: new SteamControllerHiding(hidHide, devices ?? new NoPuck(), controllers))
            {
                ProcessRuns = processRuns ?? (_ => false),
            };

                /// <summary>A restarted Pan: it undoes what its journal lists, then serves again.</summary>
        public void RestartFromJournal()
        {
            Guardian = new Guardian(Clock, new JournalFile(Path.Combine(_folder, "journal.json")), _cleanup, _log);
            Guardian.RecoverAsync().GetAwaiter().GetResult();
        }

        /// <summary>A reinstalled Pan with an empty data folder: it counts the generations from 0 again.</summary>
        public void RestartWithoutJournal()
        {
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
                    Interlocked.Increment(ref _received[message.Kind]);
                    if (message.Command == PanCommand.Heartbeat)
                    {
                        Interlocked.Increment(ref _heartbeats);
                    }

                    // A forced start reply leaves the guardian alone, as a Pan that refused would be.
                    var reply = message.Command == PanCommand.EmulationStarted && StartReply is { } forcedStart
                        ? forcedStart
                        : await guardian.HandleAsync(message);
                    if (message.Command == PanCommand.Hello && HelloReply is { } forced)
                    {
                        reply = forced;
                    }

                    if (message.Command == PanCommand.TeardownAcknowledged && AcknowledgeReply is { } forcedAck)
                    {
                        reply = forcedAck;
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
                if (!ReferenceEquals(guardian, _deadGuardian))
                {
                    // A dead Pan notices nothing, even when its pipe closes only after the restart (it would tear down
                    // into the restarted Pan's journal); a living one sees the client go, as the pipe server does.
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
        /// <summary>The usbip port the client's viiper.exe listens on (#304).</summary>
        public const int ViiperPort = 50123;

        private static readonly HeldProcess Viiper = new(900, 1, "viiper.exe");

        /// <summary>Set, the teardown's first step waits here: a usbip step that takes its time.</summary>
        public SemaphoreSlim? TeardownGate { get; set; }

        public async Task<IReadOnlyList<int>> ListImportedPortsAsync(int serverPort, CancellationToken cancellationToken)
        {
            if (TeardownGate is { } gate)
            {
                await gate.WaitAsync(cancellationToken);
            }

            return [];
        }

        public Task DetachPortAsync(int port, CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>How many emulations Pan registered: one per <c>EmulationStarted</c> it handled.</summary>
        public int ChildrenTaken => Volatile.Read(ref _childrenTaken);

        private int _childrenTaken;

        public IReadOnlyList<HeldProcess> TakeChildrenOf(int clientPid, long clientStartTime, string imageName)
        {
            Interlocked.Increment(ref _childrenTaken);
            return [Viiper];
        }

        public IReadOnlyCollection<TcpListening> Listeners() => [new(Viiper.Pid, System.Net.IPAddress.Loopback, ViiperPort)];

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
