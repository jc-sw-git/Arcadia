namespace Arcadia.Pan.Tests;

public sealed class IdleStopTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan Ample = TimeSpan.FromSeconds(10);
    private readonly List<string> _log = [];
    private readonly ManualResetEventSlim _stopped = new();

    private IdleStop NewIdleStop(Func<bool>? keepRunning = null) =>
        new(Timeout, keepRunning ?? (() => false), _stopped.Set, line => { lock (_log) _log.Add(line); });

    public void Dispose() => _stopped.Dispose();

    [Fact]
    public void Pan_stops_without_a_client()
    {
        using var idle = NewIdleStop();

        idle.ClientPresent(false);

        Assert.True(_stopped.Wait(Ample));
    }

    [Fact]
    public void A_client_that_comes_cancels_the_stop()
    {
        using var idle = NewIdleStop();

        idle.ClientPresent(false);
        idle.ClientPresent(true);

        Assert.False(_stopped.Wait(Timeout * 5));
    }

    [Fact]
    public void Pan_stays_while_a_retry_still_has_to_show_the_controller()
    {
        var showPending = 1;
        using var asked = new SemaphoreSlim(0);
        using var idle = NewIdleStop(() =>
        {
            asked.Release();
            return Volatile.Read(ref showPending) == 1;
        });

        idle.ClientPresent(false);

        // Past the timeout twice and still running: the retry's later attempts (30 s, 2 min) aren't cut off.
        Assert.True(asked.Wait(Ample));
        Assert.True(asked.Wait(Ample));
        Assert.False(_stopped.IsSet);
        Volatile.Write(ref showPending, 0);
        Assert.True(_stopped.Wait(Ample));
        lock (_log) Assert.Single(_log, line => line.Contains("staying"));
    }
}
