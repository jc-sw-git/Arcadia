namespace Arcadia.Pan;

/// <summary>
/// Stops Pan after <paramref name="timeout"/> without a client; a client that comes meanwhile cancels it. While
/// <paramref name="keepRunning"/> says Pan still has work of its own (a retry that shows the Steam Controller after a
/// partial teardown), the countdown starts over instead: stopping would end that retry with the controller hidden (review
/// of #252, P2 1).
/// </summary>
public sealed class IdleStop(TimeSpan timeout, Func<bool> keepRunning, Action stop, Action<string> log) : IDisposable
{
    private CancellationTokenSource? _countdown;
    private bool _toldStaying;

    public void ClientPresent(bool present)
    {
        lock (this)
        {
            CancelCountdown();
            if (present)
            {
                return;
            }

            _toldStaying = false;
            _countdown = new CancellationTokenSource();
            Arm(_countdown);
        }
    }

    /// <summary>No countdown runs on: a late one would stop a run that is already over.</summary>
    public void Dispose()
    {
        lock (this)
        {
            CancelCountdown();
        }
    }

    /// <summary>Under the lock, with <paramref name="countdown"/> the current one (so not disposed).</summary>
    private void Arm(CancellationTokenSource countdown)
    {
        Task.Delay(timeout, countdown.Token).ContinueWith(_ =>
        {
            lock (this)
            {
                if (countdown != _countdown)
                {
                    return;
                }

                if (keepRunning())
                {
                    if (!_toldStaying)
                    {
                        log($"No client for {timeout.TotalSeconds:0} s, but the Steam Controller isn't shown yet: staying until it is");
                        _toldStaying = true;
                    }

                    Arm(countdown);
                    return;
                }

                log($"No client for {timeout.TotalSeconds:0} s: stopping");
                stop();
            }
        }, TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    private void CancelCountdown()
    {
        _countdown?.Cancel();
        _countdown?.Dispose();
        _countdown = null;
    }
}
