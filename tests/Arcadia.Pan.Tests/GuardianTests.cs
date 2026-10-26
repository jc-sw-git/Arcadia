using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

public sealed class GuardianTests : IDisposable
{
    private static readonly ClientIdentity Arcadia = new(Pid: 500, StartTime: 1000, InConsoleSession: true);

    private readonly TempJournal _journal = new();
    private readonly FakeClock _clock = new();
    private readonly FakeCleanup _cleanup = new();
    private readonly List<string> _log = [];
    private readonly List<PanMessage> _notified = [];
    private readonly List<UnplannedEnd> _unplanned = [];
    private readonly Guardian _guardian;

    public GuardianTests()
    {
        _guardian = new Guardian(_clock, _journal.File, _cleanup, line => { lock (_log) _log.Add(line); });
        _guardian.Notify += message => { lock (_notified) _notified.Add(message); };
        _guardian.UnplannedEnd += end => { lock (_unplanned) _unplanned.Add(end); };
        _cleanup.Children = [new HeldProcess(600, 1500, "viiper.exe"), new HeldProcess(601, 1600, "cmd.exe")];
        _cleanup.Ports = [3];
    }

    public void Dispose() => _journal.Dispose();

    private string Log() { lock (_log) return string.Join("\n", _log); }

    private PanMessage Send(PanCommand command, uint generation = 0) => _guardian.Handle(PanMessage.Of(command, generation));

    private uint Connect(ClientIdentity? client = null)
    {
        Assert.Equal(PanReason.None, _guardian.Register(client ?? Arcadia));
        var hello = _guardian.Handle(PanMessage.Of(PanCommand.Hello));
        Assert.Equal(PanReply.HelloOk, hello.Reply);
        return hello.Generation;
    }

    private uint StartEmulation()
    {
        var started = Send(PanCommand.EmulationStarted);
        Assert.Equal(PanReply.Started, started.Reply);
        return started.Generation;
    }

    private async Task TeardownDoneAsync()
    {
        if (_guardian.Teardown is { } teardown)
        {
            await teardown;
        }
    }

    /// <summary>The journal stays after a teardown, with nothing to undo and the generation it reached (review, bug 4).</summary>
    private void AssertNothingToUndo(uint generation)
    {
        var state = _journal.File.Load().State;
        Assert.False(state.HasWork);
        Assert.Equal(generation, state.Generation);
    }

    [Fact]
    public void Nothing_is_accepted_before_hello_and_a_wrong_protocol_version_is_rejected()
    {
        Assert.Equal(PanReason.None, _guardian.Register(Arcadia));
        Assert.Equal(PanReason.NotRegistered, Send(PanCommand.EmulationStarted).Reason);

        var hello = _guardian.Handle(new PanMessage((byte)PanCommand.Hello, Version: 99, 0, 0));
        Assert.Equal(PanReply.Rejected, hello.Reply);
        Assert.Equal(PanReason.WrongProtocolVersion, hello.Reason);
        Assert.Equal(PanReason.NotRegistered, Send(PanCommand.Heartbeat).Reason);
    }

    [Fact]
    public void Only_one_client_from_the_console_session_is_served()
    {
        Assert.Equal(PanReason.ForeignSession, _guardian.Register(Arcadia with { Pid = 7, InConsoleSession = false }));
        Connect();
        Assert.Equal(PanReason.OtherClient, _guardian.Register(Arcadia with { Pid = 8 }));

        _guardian.ClientGone();
        Assert.Equal(PanReason.None, _guardian.Register(Arcadia with { Pid = 8 }));
    }

    [Fact]
    public void An_emulation_gets_a_generation_and_is_journaled_with_the_forwarding_processes_before_the_reply()
    {
        Connect();
        var generation = StartEmulation();

        Assert.Equal(1u, generation);
        Assert.True(_guardian.IsEmulating);
        var saved = _journal.File.Load().State;
        Assert.True(saved.Emulating);
        Assert.Equal(500, saved.ClientPid);
        Assert.Equal(1000, saved.ClientStartTime);
        Assert.Equal(new HeldProcess(600, 1500, "viiper.exe"), Assert.Single(saved.HeldProcesses));
        Assert.Equal(PanReason.AlreadyEmulating, Send(PanCommand.EmulationStarted).Reason);
    }

    [Fact]
    public void A_planned_stop_lets_the_processes_go_and_clears_the_journal()
    {
        Connect();
        var generation = StartEmulation();

        Assert.Equal(PanReply.Stopped, Send(PanCommand.EmulationStopped, generation).Reply);

        Assert.False(_guardian.IsEmulating);
        Assert.Single(_cleanup.Released);
        Assert.Empty(_cleanup.Ended);
        AssertNothingToUndo(generation);
        Assert.Equal(2u, StartEmulation());
    }

    [Fact]
    public void Commands_of_another_generation_are_rejected()
    {
        Connect();
        var generation = StartEmulation();

        Assert.Equal(PanReason.WrongGeneration, Send(PanCommand.Heartbeat, generation + 1).Reason);
        Assert.Equal(PanReason.WrongGeneration, Send(PanCommand.EmulationStopped, generation - 1).Reason);
        Assert.Equal(PanReply.HeartbeatOk, Send(PanCommand.Heartbeat, generation).Reply);
        Assert.True(_guardian.IsEmulating);
    }

    [Fact]
    public async Task Three_seconds_without_a_heartbeat_tear_the_emulation_down_until_the_client_acknowledges()
    {
        Connect();
        var generation = StartEmulation();
        _clock.Advance(TimeSpan.FromSeconds(2.9));
        _guardian.Tick();
        Assert.True(_guardian.IsEmulating);
        Send(PanCommand.Heartbeat, generation);
        _clock.Advance(TimeSpan.FromSeconds(2.9));
        _guardian.Tick();
        Assert.True(_guardian.IsEmulating);

        _clock.Advance(TimeSpan.FromMilliseconds(200));
        _guardian.Tick();
        await TeardownDoneAsync();

        Assert.False(_guardian.IsEmulating);
        Assert.Equal([3], _cleanup.Detached);
        Assert.Equal(600, Assert.Single(_cleanup.Ended).Pid);
        var notice = Assert.Single(_notified);
        Assert.Equal(PanReply.TornDown, notice.Reply);
        Assert.Equal(generation, notice.Generation);
        Assert.Equal(PanReason.Hang, notice.Reason);
        Assert.Equal(PanReason.Hang, Assert.Single(_unplanned).Reason);
        AssertNothingToUndo(generation);

        // The old generation is dead, and nothing new starts before the acknowledgement.
        Assert.Equal(PanReason.TeardownNotAcknowledged, Send(PanCommand.Heartbeat, generation).Reason);
        Assert.Equal(PanReason.TeardownNotAcknowledged, Send(PanCommand.EmulationStarted).Reason);
        Assert.Equal(PanReason.WrongGeneration, Send(PanCommand.TeardownAcknowledged, generation + 1).Reason);
        Assert.Equal(PanReply.Acknowledged, Send(PanCommand.TeardownAcknowledged, generation).Reply);
        Assert.Equal(generation + 1, StartEmulation());
    }

    [Fact]
    public void Sleep_does_not_count_as_silence()
    {
        Connect();
        StartEmulation();

        // The unbiased clock stands still while the PC sleeps; on resume the watch starts over.
        _guardian.Tick();
        _guardian.PowerResumed();
        _clock.Advance(TimeSpan.FromSeconds(2));
        _guardian.Tick();

        Assert.True(_guardian.IsEmulating);
        Assert.Null(_guardian.Teardown);
    }

    [Fact]
    public async Task A_client_that_dies_while_emulating_has_its_emulation_torn_down_and_the_next_client_starts_fresh()
    {
        Connect();
        var generation = StartEmulation();

        _guardian.ClientGone();
        await TeardownDoneAsync();

        Assert.False(_guardian.HasClient);
        Assert.Equal([3], _cleanup.Detached);
        Assert.Single(_cleanup.Ended);
        Assert.Empty(_notified);
        var end = Assert.Single(_unplanned);
        Assert.Equal((PanReason.ClientGone, generation, 500, true), (end.Reason, end.Generation, end.ClientPid, end.WasEmulating));
        Assert.Contains("gone while emulating", Log());

        Connect(Arcadia with { Pid = 501 });
        Assert.Equal(generation + 1, StartEmulation());
    }

    [Fact]
    public async Task A_service_stop_runs_the_full_teardown_and_refuses_further_commands()
    {
        Connect();
        var generation = StartEmulation();

        await _guardian.StopAsync();

        Assert.Equal([3], _cleanup.Detached);
        Assert.Single(_cleanup.Ended);
        Assert.Equal(PanReason.ServiceStopping, Assert.Single(_notified).Reason);
        Assert.Empty(_unplanned);
        Assert.Equal(PanReason.ServiceStopping, Send(PanCommand.Heartbeat, generation).Reason);
    }

    [Fact]
    public async Task A_failed_step_stays_journaled_and_is_retried_at_the_next_trigger()
    {
        Connect();
        StartEmulation();
        _cleanup.EndFails = true;

        _guardian.ClientGone();
        await TeardownDoneAsync();

        Assert.Contains("incomplete", Log());
        var left = _journal.File.Load().State;
        Assert.True(left.Emulating);
        Assert.Single(left.HeldProcesses);

        // Pan restarted: the process is still the same one (same start time), so it is ended now.
        _cleanup.EndFails = false;
        _cleanup.StartTimes[600] = 1500;
        var restarted = new Guardian(_clock, _journal.File, _cleanup, _log.Add);
        await restarted.RecoverAsync();

        Assert.Equal(600, Assert.Single(_cleanup.Ended).Pid);
        AssertNothingToUndo(1);
    }

    [Fact]
    public async Task Recovery_never_ends_a_process_whose_id_was_reused_and_tells_a_live_client_from_a_gone_one()
    {
        _journal.File.Save(new JournalState
        {
            Generation = 4,
            ClientPid = 500,
            ClientStartTime = 1000,
            Emulating = true,
            HeldProcesses = [new(600, 1500, "viiper.exe"), new(700, 1700, "viiper.exe")],
        });
        _cleanup.StartTimes[600] = 9999;   // another process has this id now
        _cleanup.StartTimes[700] = 1700;   // still ours
        _cleanup.StartTimes[500] = 1000;   // Arcadia still runs

        await _guardian.RecoverAsync();

        Assert.Equal(700, Assert.Single(_cleanup.Ended).Pid);
        Assert.Equal([3], _cleanup.Detached);
        Assert.Contains("client 500 (still running)", Log());
        Assert.Empty(_unplanned);
        AssertNothingToUndo(4);

        // The generation goes on from the journal, so a stale client can't match a new one by accident.
        Connect();
        Assert.Equal(5u, StartEmulation());
    }

    [Fact]
    public async Task Recovery_after_a_crash_reports_the_unplanned_end_for_the_crash_report()
    {
        _journal.File.Save(new JournalState { Generation = 2, ClientPid = 500, ClientStartTime = 1000, Emulating = true });

        await _guardian.RecoverAsync();

        var end = Assert.Single(_unplanned);
        Assert.Equal((PanReason.ClientGone, 2u, 500), (end.Reason, end.Generation, end.ClientPid));
    }

    [Fact]
    public async Task An_unreadable_journal_is_reported_to_the_client_instead_of_crashing_Pan()
    {
        Directory.CreateDirectory(_journal.Folder);
        File.WriteAllText(_journal.File.Path, "{ not json");

        await _guardian.RecoverAsync();
        Assert.Equal(PanReason.None, _guardian.Register(Arcadia));
        var hello = _guardian.Handle(PanMessage.Of(PanCommand.Hello));

        Assert.Equal(PanReply.HelloOk, hello.Reply);
        Assert.Equal(PanReason.JournalUnreadable, hello.Reason);
        Assert.Contains("unreadable", Log());
        Assert.Equal(PanReply.Started, Send(PanCommand.EmulationStarted).Reply);
    }

    [Fact]
    public void An_emulation_is_refused_when_the_journal_cannot_be_written()
    {
        Connect();
        Directory.CreateDirectory(_journal.File.Path);

        var started = Send(PanCommand.EmulationStarted);

        Assert.Equal(PanReply.Rejected, started.Reply);
        Assert.Equal(PanReason.JournalUnreadable, started.Reason);
        Assert.False(_guardian.IsEmulating);
        Assert.Single(_cleanup.Released);
        Assert.Contains("Journal not written", Log());
    }

    [Fact]
    public void Hiding_is_not_supported_before_part_B_and_a_planned_exit_ends_the_emulation()
    {
        Connect();
        var generation = StartEmulation();

        Assert.Equal(PanReply.Unsupported, Send(PanCommand.Hide, generation).Reply);
        Assert.Equal(PanReply.Acknowledged, Send(PanCommand.PlannedExit, generation).Reply);
        Assert.False(_guardian.IsEmulating);
        Assert.Empty(_cleanup.Ended);
    }

    [Fact]
    public async Task Hello_after_a_hang_tells_the_client_that_it_must_acknowledge()
    {
        Connect();
        var generation = StartEmulation();
        _clock.Advance(TimeSpan.FromSeconds(4));
        _guardian.Tick();
        await TeardownDoneAsync();

        // Arcadia reconnects (its pipe broke, or it was restarted by Pan's notice).
        _guardian.ClientGone();
        Assert.Equal(PanReason.None, _guardian.Register(Arcadia));
        var hello = _guardian.Handle(PanMessage.Of(PanCommand.Hello));

        Assert.Equal(PanReason.None, hello.Reason);
        Assert.Equal(generation, hello.Generation);
        Assert.Equal(generation + 1, StartEmulation());
    }

    [Fact]
    public async Task A_restarted_Pan_counts_on_from_the_generation_its_journal_reached()
    {
        Connect();
        var generation = StartEmulation();
        Assert.Equal(PanReply.Stopped, Send(PanCommand.EmulationStopped, generation).Reply);

        // Pan restarted idle (crash, update, sc stop): a client that remembers generation 1 as torn down must never
        // get it again (review, bug 4).
        var restarted = new Guardian(_clock, _journal.File, _cleanup, _log.Add);
        await restarted.RecoverAsync();
        Assert.Equal(PanReason.None, restarted.Register(Arcadia));
        Assert.Equal(PanReply.HelloOk, restarted.Handle(PanMessage.Of(PanCommand.Hello)).Reply);

        Assert.Equal(generation + 1, restarted.Handle(PanMessage.Of(PanCommand.EmulationStarted)).Generation);
    }

    [Fact]
    public async Task What_an_incomplete_teardown_left_is_ended_with_the_next_emulation()
    {
        Connect();
        StartEmulation();
        _cleanup.EndFails = true;
        _guardian.ClientGone();
        await TeardownDoneAsync();
        Assert.Single(_journal.File.Load().State.HeldProcesses);

        // The next client's emulation keeps the leftover listed beside its own process (review, bug 5) …
        _cleanup.EndFails = false;
        _cleanup.Children = [new HeldProcess(610, 1610, "viiper.exe")];
        Connect(Arcadia with { Pid = 501 });
        var generation = StartEmulation();
        Assert.Equal([600, 610], _journal.File.Load().State.HeldProcesses.Select(p => p.Pid));
        Assert.Contains("1 process(es) of the last emulation still to end", Log());

        // … and its teardown ends both.
        _guardian.ClientGone();
        await TeardownDoneAsync();
        Assert.Equal([600, 610], _cleanup.Ended.Select(p => p.Pid).Order());
        AssertNothingToUndo(generation);
    }

    [Fact]
    public async Task A_leftover_the_client_takes_up_again_is_listed_once_and_ended_once()
    {
        Connect();
        StartEmulation();
        _cleanup.EndFails = true;
        _guardian.ClientGone();
        await TeardownDoneAsync();

        // Arcadia reuses the surviving viiper.exe (pid 600): it is this emulation's process now, not a leftover too.
        _cleanup.EndFails = false;
        _cleanup.Children = [new HeldProcess(600, 1500, "viiper.exe")];
        Connect(Arcadia with { Pid = 501 });
        var generation = StartEmulation();
        Assert.Equal([600], _journal.File.Load().State.HeldProcesses.Select(p => p.Pid));
        Assert.DoesNotContain("still to end", Log());

        _guardian.ClientGone();
        await TeardownDoneAsync();
        Assert.Equal([600], _cleanup.Ended.Select(p => p.Pid));
        AssertNothingToUndo(generation);
    }

    [Fact]
    public async Task A_planned_stop_lets_only_this_emulations_processes_go_and_keeps_the_leftovers()
    {
        Connect();
        StartEmulation();
        _cleanup.EndFails = true;
        _guardian.ClientGone();
        await TeardownDoneAsync();

        _cleanup.EndFails = false;
        _cleanup.Children = [new HeldProcess(610, 1610, "viiper.exe")];
        Connect(Arcadia with { Pid = 501 });
        var generation = StartEmulation();
        Assert.Equal(PanReply.Stopped, Send(PanCommand.EmulationStopped, generation).Reply);

        // 610 was Arcadia's and is let go; 600 of the earlier emulation is still to end, so it stays journaled …
        Assert.Equal([610], _cleanup.Released.Select(p => p.Pid));
        Assert.Equal([600], _journal.File.Load().State.HeldProcesses.Select(p => p.Pid));
        Assert.Contains("still to end at the next teardown", Log());

        // … and a restarted Pan ends it.
        _cleanup.StartTimes[600] = 1500;
        var restarted = new Guardian(_clock, _journal.File, _cleanup, _log.Add);
        await restarted.RecoverAsync();
        Assert.Equal([600], _cleanup.Ended.Select(p => p.Pid));
    }
}
