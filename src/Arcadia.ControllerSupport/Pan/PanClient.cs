using Arcadia.Pan;

namespace Arcadia.ControllerSupport.Pan;

/// <summary>
/// Arcadia's side of the pipe to Pan (#243). Keeps the connection (reconnects in the background, starting the
/// service when needed), registers emulations and sends the heartbeat: the feeding thread only bumps a counter
/// (<see cref="Pulse"/>), a sender task sends every half second when it moved, with a timeout on every write, so a
/// hung Pan can never stall the input (review point 5). A Pan that doesn't answer for a few seconds counts as lost:
/// Arcadia removes its own emulation (review point 15).
/// </summary>
public sealed class PanClient : IEmulationGuardian, IHidingCheck, IAsyncDisposable
{
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// <see cref="PanCommand.CheckHiding"/> waits behind a hide or show in Pan (a puck restart, up to ~20 s if a device
    /// step hangs), so it has its own reply timeout (<c>docs/controller-support/pan-hiding.md</c>).
    /// </summary>
    public static readonly TimeSpan HidingRequestTimeout = TimeSpan.FromSeconds(25);

    /// <summary>A request to Pan (write and reply) that takes longer than this means Pan hangs. Real time; tests widen it.</summary>
    public TimeSpan RequestTimeout { get; init; } = DefaultRequestTimeout;

    /// <summary>Reconnecting after a loss; a permanent reason (not set up, wrong protocol) waits <see cref="SlowRetry"/>.</summary>
    public static readonly TimeSpan Retry = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan SlowRetry = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Pan shows the real Steam Controller again (a teardown or its retry, every 10 s while HidHide is busy): no pad is
    /// tried meanwhile, and no loss counted for it (overall review of #243, point 1).
    /// </summary>
    public static readonly TimeSpan BeingShownWait = TimeSpan.FromSeconds(10);

    private readonly IPanTransport _transport;
    private readonly TimeProvider _time;
    private readonly Action<string> _log;
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _requests = new(1, 1);
    private readonly CancellationTokenSource _disposed = new();

    private PanLink? _link;
    private TaskCompletionSource<PanMessage>? _pending;
    private string? _unavailable = "Pan ist noch nicht verbunden";
    private uint _currentGeneration;
    private uint _tornDownGeneration;
    private bool _teardownPending;
    private bool _heldForFirmware;
    private DateTimeOffset _beingShownUntil;
    private bool _emulating;
    // Pan announced a restart of its own (this request number) and hasn't said it's over: a lost link ends it too.
    private uint? _puckRestart;

    // Raises PuckRestarting and PuckRestarted one at a time, so a late let-go can't follow the restart's end.
    private readonly Lock _puckEvents = new();
    private long _pulses;
    private long _sentPulses;
    private Task? _keeper;
    private Task? _sender;

    public PanClient(IPanTransport transport, TimeProvider time, Action<string> log)
    {
        _transport = transport;
        _time = time;
        _log = log;
    }

    public event Action<uint, PanReason>? TornDown;

    public event Action<string>? Lost;

    public event Action? PuckRestarting;

    public event Action? PuckRestarted;

    /// <summary>Connected and spoken: the hello went through.</summary>
    public event Action? Connected;

    /// <summary>Pan couldn't read its journal and fell back or started empty (review point 8): worth a toast.</summary>
    public event Action? JournalProblem;

    /// <summary>The pipe is open and Pan has answered hello; while hello is under way the client is not yet connected.</summary>
    public bool IsConnected
    {
        get { lock (_lock) return _link is not null && _unavailable is null; }
    }

    /// <summary>Starts keeping the connection; the first attempt runs at once.</summary>
    public void Start()
    {
        lock (_lock)
        {
            _keeper ??= Task.Run(KeepConnectedAsync);
            _sender ??= Task.Run(SendHeartbeatsAsync);
        }
    }

    /// <summary>What Arcadia says while Pan's teardown still runs: no pad until Pan is done, and no loss counted for it.</summary>
    public const string CleaningUp = "Pan räumt noch auf";

    public string? Unavailable()
    {
        lock (_lock) return _unavailable ?? (_teardownPending || _time.GetUtcNow() < _beingShownUntil ? CleaningUp : null);
    }

    /// <summary>
    /// Done once the first try to reach Pan has ended, connected or not: then <see cref="HeldForFirmware"/> says whether a
    /// firmware attempt from before the start still holds the puck (#297 waits for it before the update).
    /// </summary>
    public Task FirstAttempt => _firstAttempt.Task;

    private readonly TaskCompletionSource _firstAttempt = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool HeldForFirmware
    {
        get { lock (_lock) return _heldForFirmware; }
    }

    public async Task<bool> HoldForFirmwareAsync(uint generation, bool hold, CancellationToken cancellationToken)
    {
        try
        {
            var reply = await RequestAsync(PanMessage.Of(PanCommand.HoldForFirmware, generation, hold ? 1u : 0u), cancellationToken);
            if (reply.Reply != PanReply.Acknowledged)
            {
                _log($"Pan: Firmware-Halt {(hold ? "an" : "aus")} abgelehnt ({reply})");
                return false;
            }

            if (!hold)
            {
                lock (_lock)
                {
                    _heldForFirmware = false;
                }
            }

            _log($"Pan: Firmware-Halt {(hold ? "an: der Steam Controller bleibt versteckt, auch wenn Arcadia unerwartet endet" : "aus")}");
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _log($"Pan: Firmware-Halt {(hold ? "an" : "aus")} nicht gesendet: {ex.Message}");
            return false;
        }
    }

    public bool IsCurrent(uint generation)
    {
        lock (_lock) return _link is not null && _emulating && _currentGeneration == generation;
    }

    public bool WasTornDown(uint generation)
    {
        lock (_lock) return _tornDownGeneration == generation && generation != 0;
    }

    public void Pulse() => Interlocked.Increment(ref _pulses);

    public async Task<uint> EmulationStartedAsync(int usbipPort, CancellationToken cancellationToken)
    {
        var start = PanMessage.Of(PanCommand.EmulationStarted, value: (uint)usbipPort);
        var reply = await RequestAsync(start, cancellationToken);
        if (reply.Reply == PanReply.Rejected && reply.Reason == PanReason.TeardownNotAcknowledged)
        {
            // A teardown from before this connection (Arcadia was restarted, or reconnected): nothing of ours is left.
            // One that still runs can't be acknowledged yet: no pad until its notice comes (review, point 3).
            if (!await AcknowledgeAsync(reply.Generation, cancellationToken))
            {
                throw new InvalidOperationException(CleaningUp);
            }

            reply = await RequestAsync(start, cancellationToken);
        }

        if (reply.Reply == PanReply.Rejected && reply.Reason == PanReason.SteamControllerBeingShown)
        {
            // Pan undoes an emulation of before; there is no notice when it is done, so try again after a while.
            lock (_lock) _beingShownUntil = _time.GetUtcNow() + BeingShownWait;
            throw new InvalidOperationException(CleaningUp);
        }

        if (reply.Reply != PanReply.Started)
        {
            throw new InvalidOperationException($"Pan lehnt die Emulation ab: {reply}");
        }

        lock (_lock)
        {
            _currentGeneration = reply.Generation;
            _emulating = true;
            _sentPulses = Interlocked.Read(ref _pulses);
        }

        _log($"Pan: Emulation {reply.Generation} angemeldet");
        return reply.Generation;
    }

    public async Task EmulationStoppedAsync(uint generation, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_currentGeneration == generation)
            {
                _emulating = false;
            }
        }

        await QuietRequestAsync(PanMessage.Of(PanCommand.EmulationStopped, generation), cancellationToken);
    }

    public async Task AcknowledgeTeardownAsync(uint generation, CancellationToken cancellationToken)
    {
        try
        {
            await AcknowledgeAsync(generation, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _log($"Pan: TeardownAcknowledged nicht gesendet: {ex.Message}");
        }
    }

    /// <summary>False while Pan's teardown of that generation still runs; Arcadia asks again when the notice has come.</summary>
    private async Task<bool> AcknowledgeAsync(uint generation, CancellationToken cancellationToken)
    {
        var reply = await RequestAsync(PanMessage.Of(PanCommand.TeardownAcknowledged, generation), cancellationToken);
        var stillRunning = reply.Reply == PanReply.Rejected && reply.Reason == PanReason.TeardownNotAcknowledged;

        // Back during Pan's teardown after a crash: the hold comes with the acknowledgement, not the hello.
        var held = reply.Reply == PanReply.Acknowledged && reply.Reason == PanReason.HeldForFirmware;
        lock (_lock)
        {
            _teardownPending = stillRunning;
            _heldForFirmware |= held;
        }

        if (held)
        {
            _log("Pan: ein Firmware-Versuch über die Kopie hält den Steam Controller versteckt, Steam läuft noch");
        }

        if (reply.Reply == PanReply.Rejected && !stillRunning)
        {
            _log($"Pan: TeardownAcknowledged abgelehnt ({reply.Reason})");
        }

        return !stillRunning;
    }

    public Task<PanReason?> CheckHidingAsync(CancellationToken cancellationToken) => CheckHidingAsync(0, cancellationToken);

    public async Task<PanReason?> CheckHidingAsync(uint generation, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            // Not cancelled by the caller: a request given up after it reached Pan costs the link (its late reply would
            // pass for the next request's), and with it a running emulation. Bounded by its own timeout instead.
            var reply = await RequestAsync(PanMessage.Of(PanCommand.CheckHiding, generation), HidingRequestTimeout, CancellationToken.None);
            if (reply.Reply == PanReply.HidingState)
            {
                return reply.Reason;
            }

            if (reply.Reply != PanReply.Unsupported)
            {
                _log($"Pan: CheckHiding beantwortet mit {reply}");
            }

            return null;
        }
        catch (InvalidOperationException)
        {
            // Already logged and dropped by RequestAsync, or not connected any more.
            return null;
        }
        catch (OperationCanceledException) when (_disposed.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task<PanReason> HideAsync(uint generation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PanMessage reply;
        try
        {
            // A hide restarts the puck (~2.5 s, up to ~20 s if a device step hangs): its own timeout, never cancelled
            // halfway, for the same reason as the check.
            reply = await RequestAsync(PanMessage.Of(PanCommand.Hide, generation), HidingRequestTimeout, CancellationToken.None);
        }
        catch (InvalidOperationException ex)
        {
            // The link is gone; Pan's teardown for the lost client undoes whatever the hide had done.
            _log($"Pan: Hide ohne Antwort: {ex.Message}");
            return PanReason.HidingFailed;
        }

        if (reply.Reply == PanReply.Hidden)
        {
            _log($"Pan: Steam Controller versteckt ({reply.Value} Geräte)");
            return PanReason.None;
        }

        var reason = reply.Reply == PanReply.Rejected ? reply.Reason : PanReason.HidingFailed;
        _log(reply.Reply == PanReply.Unsupported ? "Pan: kann den Steam Controller nicht verstecken (Pan zu alt)" : $"Pan: Hide abgelehnt ({reply})");

        // A rejected hide may have changed HidHide already (hidden, then the restart failed): undo it (pan-hiding.md).
        if (reply.Reply == PanReply.Rejected)
        {
            await UnhideAsync(generation, CancellationToken.None);
        }

        return reason;
    }

    public async Task<bool> UnhideAsync(uint generation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reply = await RequestAsync(PanMessage.Of(PanCommand.Unhide, generation), HidingRequestTimeout, CancellationToken.None);
            if (reply.Reply == PanReply.Shown)
            {
                _log("Pan: Steam Controller wieder sichtbar");
                return true;
            }

            // Pan's teardown after EmulationStopped shows it then, with its own retries.
            _log($"Pan: Unhide abgelehnt ({reply})");
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _log($"Pan: Unhide ohne Antwort: {ex.Message}");
            return false;
        }
    }

    public async Task PlannedExitAsync()
    {
        lock (_lock)
        {
            _emulating = false;
        }

        await QuietRequestAsync(PanMessage.Of(PanCommand.PlannedExit, _currentGeneration), CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await _disposed.CancelAsync();
        Drop(null);
        foreach (var task in new[] { _keeper, _sender })
        {
            if (task is not null)
            {
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }

    private async Task QuietRequestAsync(PanMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var reply = await RequestAsync(message, cancellationToken);
            if (reply.Reply == PanReply.Rejected)
            {
                _log($"Pan: {message} abgelehnt ({reply.Reason})");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _log($"Pan: {message} nicht gesendet: {ex.Message}");
        }
    }

    private Task<PanMessage> RequestAsync(PanMessage message, CancellationToken cancellationToken) =>
        RequestAsync(message, RequestTimeout, cancellationToken);

    /// <summary>One request at a time; the reply is the next non-notice message. Slow Pan = lost Pan.</summary>
    private async Task<PanMessage> RequestAsync(PanMessage message, TimeSpan replyTimeout, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposed.Token);
        timeout.CancelAfter(replyTimeout);
        try
        {
            await _requests.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && !_disposed.IsCancellationRequested)
        {
            // The request before this one is still waiting for its reply; it drops the link itself when its time is up.
            throw new InvalidOperationException("Pan antwortet nicht", ex);
        }

        try
        {
            PanLink link;
            var pending = new TaskCompletionSource<PanMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
            {
                link = _link ?? throw new InvalidOperationException(_unavailable ?? "Pan ist nicht verbunden");
                _pending = pending;
            }

            try
            {
                await link.Stream.WriteAsync(message.ToBytes(), timeout.Token);
                await link.Stream.FlushAsync(timeout.Token);
                return await pending.Task.WaitAsync(timeout.Token);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && !_disposed.IsCancellationRequested)
            {
                // Timed out or the pipe broke: Pan hangs or is gone; the keeper reconnects.
                Drop(ex is OperationCanceledException ? $"Pan antwortet nicht innerhalb von {replyTimeout.TotalSeconds:0} s" : $"Verbindung zu Pan verloren: {ex.Message}");
                throw new InvalidOperationException("Pan antwortet nicht", ex);
            }
            catch (OperationCanceledException) when (!_disposed.IsCancellationRequested)
            {
                // The caller gave up after the request may have reached Pan. Its reply would still come and would pass
                // for the next request's (review point 5), so this link goes; the keeper connects anew.
                Drop("Anfrage an Pan abgebrochen", link);
                throw;
            }
            finally
            {
                lock (_lock)
                {
                    _pending = null;
                }
            }
        }
        finally
        {
            _requests.Release();
        }
    }

    private async Task KeepConnectedAsync()
    {
        var token = _disposed.Token;
        while (!token.IsCancellationRequested)
        {
            if (IsConnected)
            {
                await Task.Delay(Retry, _time, token);
                continue;
            }

            var wait = Retry;
            if (_transport.Unavailable() is { } why)
            {
                SetUnavailable(why.Reason);
                _firstAttempt.TrySetResult();
                await Task.Delay(why.Permanent ? SlowRetry : Retry, _time, token);
                continue;
            }

            try
            {
                await ConnectAsync(token);
            }
            catch (PanUnavailableException ex)
            {
                SetUnavailable(ex.Message);
                wait = ex.Permanent ? SlowRetry : Retry;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                SetUnavailable($"Pan nicht erreichbar: {ex.Message}");
            }

            _firstAttempt.TrySetResult();
            await Task.Delay(wait, _time, token);
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var link = await _transport.ConnectAsync(cancellationToken);
        lock (_lock)
        {
            _link = link;
            _pending = null;
            // A generation torn down on an earlier connection means nothing to this Pan (it may have restarted and
            // count differently); what it still wants acknowledged, hello says (review, bug 4).
            _tornDownGeneration = 0;
            _teardownPending = false;
        }

        _ = Task.Run(() => ReadGuardedAsync(link), CancellationToken.None);
        var hello = await RequestAsync(PanMessage.Of(PanCommand.Hello), cancellationToken);
        if (hello.Reply != PanReply.HelloOk)
        {
            var reason = hello.Reason == PanReason.WrongProtocolVersion
                ? $"Pan spricht ein anderes Protokoll (Arcadia: {PanMessage.ProtocolVersion}); ein Update von Pan ist nötig"
                : $"Pan weist Arcadia ab ({hello.Reason})";
            Drop(null);
            // Another client may be on its way out: that is retried soon; a wrong protocol or session is not.
            throw new PanUnavailableException(reason, permanent: hello.Reason != PanReason.OtherClient);
        }

        if (hello.Reason == PanReason.JournalUnreadable)
        {
            _log("Pan: das Journal war nicht lesbar; Pan nutzt die vorherige Fassung oder beginnt leer");
            JournalProblem?.Invoke();
        }

        lock (_lock)
        {
            _heldForFirmware = hello.Reason == PanReason.HeldForFirmware;
        }

        if (_heldForFirmware)
        {
            _log("Pan: ein Firmware-Versuch über die Kopie hält den Steam Controller versteckt, Steam läuft noch");
        }

        if (hello.Reason == PanReason.TeardownNotAcknowledged)
        {
            await AcknowledgeTeardownAsync(hello.Generation, cancellationToken);
        }

        lock (_lock)
        {
            _unavailable = null;
        }

        _log($"Pan: verbunden ({link.Description}, Protokoll {PanMessage.ProtocolVersion}, Generation {hello.Generation})");
        Connected?.Invoke();
    }

    /// <summary>The reader never ends silently: a throwing subscriber is logged with its stack and costs the link (DoD: robust).</summary>
    private async Task ReadGuardedAsync(PanLink link)
    {
        try
        {
            await ReadAsync(link);
        }
        catch (Exception ex)
        {
            _log($"Pan: Fehler beim Lesen der Verbindung: {ex}");
            Drop("Verbindung zu Pan nach einem Fehler geschlossen", link);
        }
    }

    private async Task ReadAsync(PanLink link)
    {
        var buffer = new byte[PanMessage.Size];
        try
        {
            while (true)
            {
                var read = 0;
                while (read < buffer.Length)
                {
                    var n = await link.Stream.ReadAsync(buffer.AsMemory(read), _disposed.Token);
                    if (n == 0)
                    {
                        Drop("Pan hat die Verbindung geschlossen", link);
                        return;
                    }

                    read += n;
                }

                if (!Dispatch(PanMessage.Read(buffer), link))
                {
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        {
            Drop(_disposed.IsCancellationRequested ? null : $"Verbindung zu Pan verloren: {ex.Message}", link);
        }
    }

    /// <summary>
    /// Hands a message read on <paramref name="link"/> to its waiting request or raises the notice. A dropped link can
    /// still complete a read that was in flight (an anonymous pipe on Windows reads synchronously), so the link is
    /// checked in the same lock that picks the request or changes the state: what it carries belongs to a connection
    /// that is over and is dropped. Returns false then, and the reader of that link ends.
    /// </summary>
    private bool Dispatch(PanMessage message, PanLink link)
    {
        TaskCompletionSource<PanMessage>? pending = null;
        lock (_lock)
        {
            if (!ReferenceEquals(_link, link))
            {
                return false;
            }

            if (message.Reply == PanReply.TornDown)
            {
                _tornDownGeneration = message.Generation;
                _teardownPending = false;
                if (_currentGeneration == message.Generation)
                {
                    _emulating = false;
                }
            }
            else if (message.Reply == PanReply.ReleasePuck)
            {
                _puckRestart = message.Value;
            }
            else if (message.Reply == PanReply.PuckRestarted)
            {
                _puckRestart = null;
            }
            else
            {
                pending = _pending;
            }
        }

        if (message.Reply == PanReply.TornDown)
        {
            _log($"Pan: Emulation {message.Generation} von Pan beendet ({message.Reason})");
            TornDown?.Invoke(message.Generation, message.Reason);
        }
        else if (message.Reply == PanReply.ReleasePuck)
        {
            // Off the reader: letting go waits for SDL's lock, and the answer is a request of its own.
            _ = Task.Run(() => LetGoOfPuckAsync(message.Value), CancellationToken.None);
        }
        else if (message.Reply == PanReply.PuckRestarted)
        {
            RaisePuckRestarted();
        }
        else if (pending is null || !pending.TrySetResult(message))
        {
            _log($"Pan: unerwartete Antwort {message}");
        }

        return true;
    }

    /// <summary>
    /// Pan asked to let go of the puck before it restarts it (#308). Never throws: a subscriber's error and an answer that
    /// doesn't go through are logged; Pan restarts after its own short wait anyway.
    /// </summary>
    private async Task LetGoOfPuckAsync(uint request)
    {
        _log("Pan startet den Steam Controller neu: Arcadia lässt ihn los");
        try
        {
            lock (_puckEvents)
            {
                bool stillAsked;
                lock (_lock)
                {
                    stillAsked = _puckRestart == request;
                }

                if (stillAsked)
                {
                    PuckRestarting?.Invoke();
                }
            }
        }
        catch (Exception ex)
        {
            _log($"Pan: Steam Controller nicht losgelassen: {ex}");
        }

        try
        {
            // Behind a running request it waits at most its own timeout, without dropping the link (RequestAsync).
            await RequestAsync(PanMessage.Of(PanCommand.PuckReleased, value: request), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _log($"Pan: Loslassen nicht gemeldet: {ex.Message}");
        }
    }

    private void RaisePuckRestarted()
    {
        try
        {
            lock (_puckEvents)
            {
                PuckRestarted?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _log($"Pan: Steam Controller nicht wieder gelesen: {ex}");
        }
    }

    /// <summary>Closes the connection; a reason is logged and raised as <see cref="Lost"/>. Idempotent per link.</summary>
    private void Drop(string? reason, PanLink? only = null)
    {
        PanLink? link;
        bool wasEmulating;
        bool puckRestarting;
        lock (_lock)
        {
            if (_link is null || (only is not null && !ReferenceEquals(_link, only)))
            {
                return;
            }

            link = _link;
            _link = null;
            wasEmulating = _emulating;
            _emulating = false;
            _pending?.TrySetException(new IOException(reason ?? "Verbindung geschlossen"));
            _pending = null;
            _teardownPending = false;
            _unavailable = reason ?? "Pan ist nicht verbunden";
            puckRestarting = _puckRestart is not null;
            _puckRestart = null;
        }

        link.Dispose();
        if (puckRestarting)
        {
            // Nobody will say the restart is over: the puck is read again.
            RaisePuckRestarted();
        }

        if (reason is not null)
        {
            _log($"Pan: {reason}{(wasEmulating ? " – die Emulation wird entfernt" : string.Empty)}");
            if (wasEmulating)
            {
                Lost?.Invoke(reason);
            }
        }
    }

    private void SetUnavailable(string reason)
    {
        bool changed;
        lock (_lock)
        {
            changed = _unavailable != reason;
            _unavailable = reason;
        }

        if (changed)
        {
            _log($"Pan: {reason}");
        }
    }

    /// <summary>Every half second: a heartbeat if the feeding thread pulsed since the last one, while emulating.</summary>
    private async Task SendHeartbeatsAsync()
    {
        var token = _disposed.Token;
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(Guardian.HeartbeatInterval, _time, token);
            uint generation;
            long pulses;
            lock (_lock)
            {
                if (_link is null || !_emulating)
                {
                    continue;
                }

                if (_requests.CurrentCount == 0)
                {
                    // Another request is under way (a hide takes ~2.5 s): Pan's hang watch waits for it, and a heartbeat
                    // queued behind it would only give up after its timeout, with an exception at every start (#308).
                    continue;
                }

                generation = _currentGeneration;
                pulses = Interlocked.Read(ref _pulses);
                if (pulses == _sentPulses)
                {
                    // The feeder didn't move: no heartbeat, Pan will see the silence.
                    continue;
                }

                _sentPulses = pulses;
            }

            try
            {
                var reply = await RequestAsync(PanMessage.Of(PanCommand.Heartbeat, generation), token);
                if (reply.Reply == PanReply.Rejected)
                {
                    _log($"Pan: Heartbeat für Generation {generation} abgelehnt ({reply.Reason})");
                    lock (_lock)
                    {
                        if (_currentGeneration == generation)
                        {
                            _emulating = false;
                        }

                        // Pan is tearing this generation down: the pad is lost once, and no new one until the notice.
                        _teardownPending |= reply.Reason == PanReason.TeardownNotAcknowledged;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Already dropped and logged by RequestAsync.
                _ = ex;
            }
        }
    }
}
