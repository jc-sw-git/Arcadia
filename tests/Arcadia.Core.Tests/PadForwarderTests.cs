using Arcadia.ControllerSupport;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>
/// #238 D9: a controller's state goes to its pad on the thread that read it; a send VIIPER doesn't finish at once never
/// holds up the reads, and the newest state follows it.
/// </summary>
[Trait("TestCase", "T238-11")]
public sealed class PadForwarderTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _summaries = [];
    private readonly List<Exception> _failures = [];
    private readonly List<(int State, int Thread)> _sent = [];
    private readonly PadForwarder<int> _forwarder;

    public PadForwarderTests()
    {
        _forwarder = new PadForwarder<int>(_time, _summaries.Add, _failures.Add);
    }

    private Task SendAtOnce(int state, CancellationToken cancellationToken)
    {
        lock (_sent) _sent.Add((state, Environment.CurrentManagedThreadId));
        return Task.CompletedTask;
    }

    [Fact]
    public void A_state_reaches_the_pad_before_Send_returns_on_the_reading_thread_with_its_delay_counted()
    {
        _forwarder.Plug(SendAtOnce);
        _sent.Clear();
        var readAt = _time.GetTimestamp();
        _time.Advance(TimeSpan.FromMilliseconds(0.3));

        _forwarder.Send(7, readAt);

        Assert.Equal([(7, Environment.CurrentManagedThreadId)], _sent);
        Assert.Equal(2, _forwarder.Delays.Count);
        Assert.Contains("max 300 µs", _forwarder.Delays.Summary());
    }

    [Fact]
    public void Without_a_pad_states_are_only_kept_and_the_newest_goes_out_when_one_is_plugged()
    {
        _forwarder.Send(1, _time.GetTimestamp());
        _forwarder.Send(2, _time.GetTimestamp());

        _forwarder.Plug(SendAtOnce);

        Assert.Equal([2], _sent.Select(sent => sent.State));
    }

    [Fact]
    public void After_an_unplug_nothing_is_sent()
    {
        _forwarder.Plug(SendAtOnce);
        _forwarder.Unplug();
        _sent.Clear();

        _forwarder.Send(3, _time.GetTimestamp());

        Assert.Empty(_sent);
    }

    [Fact]
    public async Task While_VIIPER_takes_its_time_reads_go_on_and_only_the_newest_state_follows_the_slow_one()
    {
        var slow = new TaskCompletionSource();
        var calls = new List<int>();
        _forwarder.Plug((state, _) =>
        {
            lock (calls) calls.Add(state);
            return state == 1 ? slow.Task : Task.CompletedTask;
        });
        lock (calls) calls.Clear();

        _forwarder.Send(1, _time.GetTimestamp());
        _forwarder.Send(2, _time.GetTimestamp());
        _forwarder.Send(3, _time.GetTimestamp());

        lock (calls) Assert.Equal([1], calls);
        slow.SetResult();
        await WaitUntil(() => { lock (calls) return calls.Count == 2; });
        lock (calls) Assert.Equal([1, 3], calls);
    }

    [Fact]
    public async Task A_send_that_hangs_is_cut_off_after_its_timeout_and_reported()
    {
        var cutOff = new TaskCompletionSource();
        _forwarder.Plug((state, cancellationToken) =>
        {
            cancellationToken.Register(() => cutOff.TrySetResult());
            return Task.Delay(Timeout.Infinite, cancellationToken);
        });

        await cutOff.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await WaitUntil(() => { lock (_failures) return _failures.Count == 1; });
        Assert.IsAssignableFrom<OperationCanceledException>(_failures[0]);
        Assert.Equal(0, _forwarder.Delays.Count);
    }

    [Fact]
    public void A_send_that_throws_at_once_is_reported_and_the_next_state_is_tried_again()
    {
        var fail = true;
        _forwarder.Plug((state, cancellationToken) => fail ? throw new IOException("VIIPER weg") : SendAtOnce(state, cancellationToken));
        fail = false;

        _forwarder.Send(4, _time.GetTimestamp());

        Assert.Equal("VIIPER weg", Assert.Single(_failures).Message);
        Assert.Equal([4], _sent.Select(sent => sent.State));
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "timed out");
            await Task.Delay(10);
        }
    }
}
