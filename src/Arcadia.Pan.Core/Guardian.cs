namespace Arcadia.Pan;

/// <summary>Who connected, as read from the pipe and the process, never from a message.</summary>
/// <param name="Pid">The client's process id (<c>GetNamedPipeClientProcessId</c>).</param>
/// <param name="StartTime">Its process start time (FILETIME ticks), journaled to tell it from a reused id.</param>
/// <param name="InConsoleSession">The process runs in the active console session (review point 2).</param>
public sealed record ClientIdentity(int Pid, long StartTime, bool InConsoleSession);

/// <summary>The hook for the crash report (#244): Pan saw an unplanned end or a hang, with the emulation state.</summary>
public sealed record UnplannedEnd(PanReason Reason, uint Generation, int ClientPid, bool WasEmulating);

/// <summary>
/// Pan's decisions (#243), free of Windows: one client at a time, protocol version and generation checks, the hang
/// watch on an unbiased clock, the journal written before each change, and the teardown when Arcadia hangs, ends
/// unplanned or Pan is stopped. Thread-safe: the pipe reader, the watch timer and the process wait all call in.
/// </summary>
public sealed class Guardian
{
    /// <summary>Three seconds without a heartbeat while emulating count as a hang (owner, 2026-10-26).</summary>
    public static readonly TimeSpan DefaultHangTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Arcadia sends a heartbeat this often while emulating; Pan's watch ticks at the same pace.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>What Arcadia starts to forward to the pad; only children of the client with this name are taken.</summary>
    public const string ForwardingImageName = "viiper.exe";

    private readonly Lock _lock = new();
    private readonly IPanClock _clock;
    private readonly JournalFile _journalFile;
    private readonly IEmulationCleanup _cleanup;
    private readonly Action<string> _log;
    private readonly long _hangMs;

    private JournalState _journal = new();
    private string? _journalProblem;
    private ClientIdentity? _client;
    private bool _helloDone;
    private Phase _phase = Phase.Idle;
    private uint _generation;
    private long _lastHeartbeatMs;
    private Task? _teardown;
    private bool _stopping;

    private enum Phase
    {
        Idle,
        Emulating,

        /// <summary>A teardown runs or ran; the client must acknowledge before a new emulation starts (review point 7).</summary>
        TornDown,
    }

    public Guardian(IPanClock clock, JournalFile journalFile, IEmulationCleanup cleanup, Action<string> log, TimeSpan? hangTimeout = null)
    {
        _clock = clock;
        _journalFile = journalFile;
        _cleanup = cleanup;
        _log = log;
        _hangMs = (long)(hangTimeout ?? DefaultHangTimeout).TotalMilliseconds;
    }

    /// <summary>A message Pan sends on its own (a teardown), to the registered client.</summary>
    public event Action<PanMessage>? Notify;

    /// <summary>The hook for #244.</summary>
    public event Action<UnplannedEnd>? UnplannedEnd;

    public bool IsEmulating
    {
        get { lock (_lock) return _phase == Phase.Emulating; }
    }

    public bool HasClient
    {
        get { lock (_lock) return _client is not null; }
    }

    /// <summary>The teardown running right now, for a caller that must wait for it (stop, tests).</summary>
    public Task? Teardown
    {
        get { lock (_lock) return _teardown; }
    }

    /// <summary>
    /// At start: reads the journal and undoes what it still lists (a blue screen or power loss left it, or Pan was
    /// restarted). A journal that can't be read is reported, never fatal.
    /// </summary>
    public async Task RecoverAsync()
    {
        var load = _journalFile.Load();
        lock (_lock)
        {
            _journal = load.State;
            _journalProblem = load.Problem;
            _generation = _journal.Generation;
        }

        if (load.Problem is not null)
        {
            _log($"Journal: {load.Problem}");
        }

        if (!load.State.HasWork)
        {
            return;
        }

        var clientAlive = _cleanup.StartTimeOf(load.State.ClientPid) == load.State.ClientStartTime;
        _log($"Journal: emulation of generation {load.State.Generation} left open by client {load.State.ClientPid} ({(clientAlive ? "still running" : "gone")}); undoing it");
        var held = 0;
        foreach (var process in load.State.HeldProcesses.ToList())
        {
            if (_cleanup.TryHold(process))
            {
                held++;
            }
            else
            {
                // Gone, or the id belongs to another process now: nothing of ours to end.
                load.State.HeldProcesses.Remove(process);
            }
        }

        _log($"Journal: {held} forwarding process(es) still ours");
        await TearDownAsync(PanReason.ClientGone, notify: false, unplanned: !clientAlive);
    }

    /// <summary>A connection on the pipe: accepted, or refused with the reason the client gets before the pipe closes.</summary>
    public PanReason Register(ClientIdentity client)
    {
        lock (_lock)
        {
            if (!client.InConsoleSession)
            {
                _log($"Client {client.Pid} refused: not in the console session");
                return PanReason.ForeignSession;
            }

            if (_client is not null)
            {
                _log($"Client {client.Pid} refused: client {_client.Pid} is registered");
                return PanReason.OtherClient;
            }

            if (_phase == Phase.TornDown && _teardown is { IsCompleted: true })
            {
                // The client that would have acknowledged is gone (only one is ever registered): start fresh.
                _phase = Phase.Idle;
            }

            _client = client;
            _helloDone = false;
            _log($"Client {client.Pid} connected");
            return PanReason.None;
        }
    }

    /// <summary>A message from the registered client; the reply goes back on the pipe.</summary>
    public PanMessage Handle(PanMessage message)
    {
        lock (_lock)
        {
            if (_client is null)
            {
                return PanMessage.Of(PanReply.Rejected, reason: PanReason.NotRegistered);
            }

            if (message.Command == PanCommand.Hello)
            {
                if (message.Version != PanMessage.ProtocolVersion)
                {
                    _log($"Client {_client.Pid} speaks protocol {message.Version}, Pan speaks {PanMessage.ProtocolVersion}");
                    return PanMessage.Of(PanReply.Rejected, reason: PanReason.WrongProtocolVersion);
                }

                _helloDone = true;
                // The client learns of an open teardown it must acknowledge, and of a journal Pan couldn't read.
                return _phase == Phase.TornDown
                    ? PanMessage.Of(PanReply.HelloOk, _generation, PanReason.TeardownNotAcknowledged)
                    : PanMessage.Of(PanReply.HelloOk, _generation, _journalProblem is null ? PanReason.None : PanReason.JournalUnreadable);
            }

            if (!_helloDone)
            {
                return PanMessage.Of(PanReply.Rejected, reason: PanReason.NotRegistered);
            }

            if (_stopping)
            {
                return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.ServiceStopping);
            }

            return message.Command switch
            {
                PanCommand.EmulationStarted => StartEmulation(),
                PanCommand.EmulationStopped => WithGeneration(message, StopEmulation),
                PanCommand.Heartbeat => WithGeneration(message, Heartbeat),
                PanCommand.TeardownAcknowledged => Acknowledge(message.Generation),
                PanCommand.PlannedExit => PlannedExit(),
                PanCommand.Hide or PanCommand.Unhide => PanMessage.Of(PanReply.Unsupported, message.Generation),
                _ => PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.NotRegistered),
            };
        }
    }

    /// <summary>The client's process ended or its pipe broke: an open emulation is torn down (review point 3, the job object is the first line).</summary>
    public void ClientGone()
    {
        lock (_lock)
        {
            if (_client is null)
            {
                return;
            }

            var client = _client;
            _client = null;
            _helloDone = false;
            if (_phase != Phase.Emulating)
            {
                _log($"Client {client.Pid} disconnected");
                if (_phase == Phase.TornDown && _teardown is { IsCompleted: true })
                {
                    // Nobody is left to acknowledge: the next client starts fresh.
                    _phase = Phase.Idle;
                }

                return;
            }

            _log($"Client {client.Pid} gone while emulating generation {_generation}");
            BeginTeardown(PanReason.ClientGone, notify: false, unplanned: true);
        }
    }

    /// <summary>The watch, every <see cref="HeartbeatInterval"/> while emulating: three seconds of silence are a hang.</summary>
    public void Tick()
    {
        lock (_lock)
        {
            if (_phase != Phase.Emulating)
            {
                return;
            }

            var silence = _clock.UnbiasedMilliseconds - _lastHeartbeatMs;
            if (silence < _hangMs)
            {
                return;
            }

            _log($"Client {_client?.Pid} silent for {silence} ms while emulating generation {_generation}: hang");
            BeginTeardown(PanReason.Hang, notify: true, unplanned: true);
        }
    }

    /// <summary>The PC resumed from sleep: the silence so far doesn't count (review point 6).</summary>
    public void PowerResumed()
    {
        lock (_lock)
        {
            _lastHeartbeatMs = _clock.UnbiasedMilliseconds;
        }
    }

    /// <summary>The service is stopped (SCM stop, pre-shutdown): the full teardown, as for a crash (review point 4).</summary>
    public async Task StopAsync()
    {
        Task? teardown;
        lock (_lock)
        {
            _stopping = true;
            if (_phase == Phase.Emulating)
            {
                _log($"Service stopping while emulating generation {_generation}: tearing down");
                BeginTeardown(PanReason.ServiceStopping, notify: true, unplanned: false);
            }

            teardown = _teardown;
        }

        if (teardown is not null)
        {
            await teardown;
        }
    }

    private PanMessage StartEmulation()
    {
        if (_phase == Phase.TornDown)
        {
            return PanMessage.Of(PanReply.Rejected, _generation, PanReason.TeardownNotAcknowledged);
        }

        if (_phase == Phase.Emulating)
        {
            return PanMessage.Of(PanReply.Rejected, _generation, PanReason.AlreadyEmulating);
        }

        var client = _client!;
        var held = _cleanup.TakeChildrenOf(client.Pid, client.StartTime, ForwardingImageName);
        _generation++;
        _journal.Generation = _generation;
        _journal.ClientPid = client.Pid;
        _journal.ClientStartTime = client.StartTime;
        _journal.Emulating = true;
        _journal.HeldProcesses = [.. held];
        try
        {
            // Written before the reply: once Arcadia emulates, the journal already says what to undo.
            _journalFile.Save(_journal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log($"Journal not written: {ex.Message}; emulation refused");
            foreach (var process in held)
            {
                _cleanup.Release(process);
            }

            _journal.Emulating = false;
            _journal.HeldProcesses = [];
            return PanMessage.Of(PanReply.Rejected, _generation, PanReason.JournalUnreadable);
        }

        _phase = Phase.Emulating;
        _lastHeartbeatMs = _clock.UnbiasedMilliseconds;
        _log($"Emulation generation {_generation} started by client {client.Pid}, holding {held.Count} process(es)");
        return PanMessage.Of(PanReply.Started, _generation);
    }

    private PanMessage WithGeneration(PanMessage message, Func<PanMessage> handle)
    {
        if (_phase == Phase.Idle)
        {
            return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.NotEmulating);
        }

        if (message.Generation != _generation)
        {
            return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.WrongGeneration);
        }

        if (_phase == Phase.TornDown)
        {
            return PanMessage.Of(PanReply.Rejected, message.Generation, PanReason.TeardownNotAcknowledged);
        }

        return handle();
    }

    private PanMessage StopEmulation()
    {
        // Arcadia removed its emulation itself: let go of its processes, nothing to undo.
        foreach (var process in _journal.HeldProcesses)
        {
            _cleanup.Release(process);
        }

        _journal.Emulating = false;
        _journal.HeldProcesses = [];
        SaveJournal();
        _phase = Phase.Idle;
        _log($"Emulation generation {_generation} stopped by the client");
        return PanMessage.Of(PanReply.Stopped, _generation);
    }

    private PanMessage Heartbeat()
    {
        _lastHeartbeatMs = _clock.UnbiasedMilliseconds;
        return PanMessage.Of(PanReply.HeartbeatOk, _generation);
    }

    private PanMessage Acknowledge(uint generation)
    {
        if (_phase != Phase.TornDown || generation != _generation)
        {
            return PanMessage.Of(PanReply.Rejected, generation, PanReason.WrongGeneration);
        }

        if (_teardown is { IsCompleted: false })
        {
            // Still running: the client asks again at its next tick.
            return PanMessage.Of(PanReply.Rejected, generation, PanReason.TeardownNotAcknowledged);
        }

        _phase = Phase.Idle;
        _log($"Teardown of generation {generation} acknowledged by the client");
        return PanMessage.Of(PanReply.Acknowledged, generation);
    }

    private PanMessage PlannedExit()
    {
        if (_phase == Phase.Emulating)
        {
            // Said goodbye without stopping first: treat it as a stop, the client cleaned up itself.
            StopEmulation();
        }

        _log($"Client {_client?.Pid} exits on purpose");
        return PanMessage.Of(PanReply.Acknowledged, _generation);
    }

    /// <summary>Under the lock: marks the generation torn down at once and runs the steps outside the lock.</summary>
    private void BeginTeardown(PanReason reason, bool notify, bool unplanned)
    {
        _phase = Phase.TornDown;
        _teardown = TearDownAsync(reason, notify, unplanned);
    }

    private async Task TearDownAsync(PanReason reason, bool notify, bool unplanned)
    {
        // Off the caller: the pipe reader, the watch and the SCM must not wait on the PnP lock.
        await Task.Yield();
        uint generation;
        JournalState journal;
        int clientPid;
        lock (_lock)
        {
            generation = _generation;
            journal = _journal;
            clientPid = journal.ClientPid;
        }

        if (unplanned)
        {
            UnplannedEnd?.Invoke(new UnplannedEnd(reason, generation, clientPid, journal.Emulating));
        }

        var plan = new TeardownPlan(_cleanup, journal, SaveJournalFrom);
        var results = await TeardownRunner.RunAsync(plan.Steps(), _log);
        var complete = results.All(r => r.Outcome == StepOutcome.Ok);
        lock (_lock)
        {
            if (complete)
            {
                _journal.Emulating = false;
                SaveJournal();
                _log($"Teardown of generation {generation} complete ({reason})");
            }
            else
            {
                // What failed stays in the journal and is retried at the next trigger (a stop, a restart, --restore).
                _log($"Teardown of generation {generation} incomplete ({reason}); the rest stays journaled");
            }

            if (_client is null)
            {
                // Nobody is left to acknowledge.
                _phase = Phase.Idle;
            }
        }

        if (notify)
        {
            Notify?.Invoke(PanMessage.Of(PanReply.TornDown, generation, reason));
        }
    }

    private void SaveJournalFrom(JournalState state)
    {
        lock (_lock)
        {
            SaveJournal();
        }
    }

    /// <summary>Under the lock. A journal that can't be written is logged; the state in memory stays right.</summary>
    private void SaveJournal()
    {
        try
        {
            if (_journal.HasWork)
            {
                _journalFile.Save(_journal);
            }
            else
            {
                _journalFile.Clear();
                // The generation lives on in memory; a restart with an empty journal starts from 0 again, which a
                // client can't confuse with an old one (it learns the generation from Pan's reply).
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log($"Journal not written: {ex.Message}");
        }
    }
}
