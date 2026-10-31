using Arcadia.Input;

namespace Arcadia.ControllerSupport;

/// <summary>
/// Hands a controller's newest state to its emulated pad on the thread that read it (#238 D9): no channel and no thread
/// hop on the way. VIIPER takes a state over loopback at once, so a send normally ends before this returns; when it
/// doesn't, the states read meanwhile wait in one slot (the newest wins) and go out once it is done, so a slow VIIPER
/// never holds up the reads. A send slower than <see cref="SendTimeout"/> is cut off, and the pad reports itself lost.
/// </summary>
/// <typeparam name="TState">What the pad takes: <see cref="VirtualPadState"/> or <see cref="XboxPadState"/>.</typeparam>
public sealed class PadForwarder<TState>
    where TState : struct
{
    /// <summary>VIIPER takes a state in well under a millisecond; a write this slow means it is stuck.</summary>
    public static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _time;
    private readonly Action<string> _summaryDue;
    private readonly Action<Exception> _sendFailed;
    private readonly Lock _lock = new();

    // Under _lock: where states go now, the newest state and whether the pad still lacks it, and whether a send runs.
    private Func<TState, CancellationToken, Task>? _send;
    private (TState State, long ReadAt) _newest;
    private bool _newestUnsent;
    private bool _sending;

    // Only the one running send uses it, so it is reset rather than made anew for every state.
    private CancellationTokenSource _timeout = new();

    /// <param name="time">The clock the read timestamps come from.</param>
    /// <param name="summaryDue">Gets <see cref="Delays"/>' summary every 10 minutes of forwarding, for <c>arcadia.log</c>.</param>
    /// <param name="sendFailed">A send failed or was cut off; the pad reports itself lost then. Must not throw.</param>
    public PadForwarder(TimeProvider time, Action<string> summaryDue, Action<Exception>? sendFailed = null)
    {
        _time = time;
        _summaryDue = summaryDue;
        _sendFailed = sendFailed ?? (_ => { });
    }

    /// <summary>From a state read to VIIPER having taken it, since the last <see cref="Plug"/>.</summary>
    public LatencyStats Delays { get; } = new();

    /// <summary>States go to <paramref name="send"/> from now on, starting with the newest one, which the pad missed.</summary>
    public void Plug(Func<TState, CancellationToken, Task> send)
    {
        Delays.Reset();
        lock (_lock)
        {
            _send = send;
            _newest = (_newest.State, _time.GetTimestamp());
            _newestUnsent = true;
            if (_sending)
            {
                return;
            }

            _sending = true;
        }

        SendWhileUnsent();
    }

    /// <summary>No pad: states are only remembered, for the next <see cref="Plug"/>.</summary>
    public void Unplug()
    {
        lock (_lock)
        {
            _send = null;
        }
    }

    /// <summary>The controller's newest state: sent now, or after the send under way. Never throws.</summary>
    /// <param name="readAt">When it was read, from <see cref="TimeProvider.GetTimestamp"/> of the clock given.</param>
    public void Send(TState state, long readAt)
    {
        lock (_lock)
        {
            _newest = (state, readAt);
            _newestUnsent = true;
            if (_sending || _send is null)
            {
                return;
            }

            _sending = true;
        }

        SendWhileUnsent();
    }

    /// <summary>The one running send: goes on while a newer state waits, stops at one that doesn't end at once.</summary>
    private void SendWhileUnsent()
    {
        while (true)
        {
            Func<TState, CancellationToken, Task> send;
            (TState State, long ReadAt) next;
            lock (_lock)
            {
                if (!_newestUnsent || _send is null)
                {
                    _sending = false;
                    return;
                }

                send = _send;
                next = _newest;
                _newestUnsent = false;
            }

            var sent = Start(send, next.State);
            if (!sent.IsCompleted)
            {
                // VIIPER doesn't take it at once: the rest goes on where the send ends, off the reading thread.
                sent.ContinueWith(
                    (task, readAt) =>
                    {
                        Finished(task, (long)readAt!);
                        SendWhileUnsent();
                    },
                    next.ReadAt,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                return;
            }

            Finished(sent, next.ReadAt);
        }
    }

    private Task Start(Func<TState, CancellationToken, Task> send, TState state)
    {
        try
        {
            if (!_timeout.TryReset())
            {
                _timeout.Dispose();
                _timeout = new CancellationTokenSource();
            }

            _timeout.CancelAfter(SendTimeout);
            return send(state, _timeout.Token);
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    private void Finished(Task sent, long readAt)
    {
        try
        {
            if (!sent.IsCompletedSuccessfully)
            {
                // Failed or cut off: the pad reports itself lost now, and the next tick plugs a new one.
                _sendFailed(sent.Exception?.InnerException ?? new OperationCanceledException("cut off after " + SendTimeout));
                return;
            }

            Delays.Add(_time.GetElapsedTime(readAt));
            if (Delays.Due())
            {
                _summaryDue(Delays.Summary());
            }
        }
        catch (Exception)
        {
            // Only the log failed; the next state must still go out, also when this ran off the reading thread.
        }
    }
}
