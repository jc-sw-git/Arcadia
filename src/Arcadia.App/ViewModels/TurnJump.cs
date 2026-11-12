namespace Arcadia.App.ViewModels;

/// <summary>
/// Whether a turn of the detail page's carousel lets title and content jump in (#506). A turn that comes while the jump
/// of the one before still runs, or as fast after it (LT/RT tapped, the wheel spun), stops that jump and shows the game at
/// once; so quick turns always show the newest game and leave no jump to replay. Only a turn on its own jumps.
/// </summary>
public sealed class TurnJump(TimeProvider time, TimeSpan length)
{
    private long? _lastTurn;
    private CancellationTokenSource? _running;

    /// <summary>
    /// A turn: stops the running jump; the token for the new jump, or null when the game is shown at once. The token is
    /// cancelled by the next turn.
    /// </summary>
    public CancellationToken? Turn()
    {
        var now = time.GetTimestamp();
        var quick = _lastTurn is { } last && time.GetElapsedTime(last, now) < length;
        _lastTurn = now;
        Stop();
        if (quick)
        {
            return null;
        }

        _running = new CancellationTokenSource();
        return _running.Token;
    }

    /// <summary>Stops the running jump, e.g. when the page goes; its values fall back to the shown state.</summary>
    public void Stop()
    {
        _running?.Cancel();
        _running?.Dispose();
        _running = null;
    }
}
