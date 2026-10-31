using Arcadia.Input;

namespace Arcadia.Core.Tests;

/// <summary>
/// #238 D9: while pads are forwarded the controllers are read every millisecond at high priority, Arcadia's own polls
/// keep their 8 ms clock, and the time between reads and the CPU are logged.
/// </summary>
[Trait("TestCase", "T238-11")]
public sealed class ReadPacingTests
{
    private readonly List<string> _log = [];
    private readonly List<bool> _priority = [];
    private TimeSpan _processorTime;
    private readonly ReadPacing _pacing;

    public ReadPacingTests()
    {
        _pacing = new ReadPacing(_log.Add, _priority.Add, () => _processorTime, processorCount: 4);
    }

    private static TimeSpan Ms(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    [Fact]
    public void Without_pads_every_read_is_a_poll_8_ms_apart_at_normal_priority()
    {
        Assert.True(_pacing.StartRead(Ms(0), fastWanted: false));
        Assert.True(_pacing.StartRead(Ms(8), fastWanted: false));

        Assert.Equal(Ms(8), _pacing.Wait(Ms(8.2)));
        Assert.False(_pacing.IsFast);
        Assert.Empty(_priority);
        Assert.Empty(_log);
    }

    [Fact]
    public void With_pads_it_reads_every_millisecond_but_polls_only_every_8_ms()
    {
        var polls = new List<double>();
        for (var ms = 0; ms <= 24; ms++)
        {
            if (_pacing.StartRead(Ms(ms), fastWanted: true))
            {
                polls.Add(ms);
            }
        }

        Assert.Equal([0, 8, 16, 24], polls);
        Assert.Equal([true], _priority);
        Assert.Equal("Controller: Pads werden weitergeleitet, Arcadia liest alle 1 ms mit hoher Priorität", Assert.Single(_log));
    }

    [Fact]
    public void A_read_slightly_early_still_polls_so_the_polls_keep_their_clock()
    {
        _pacing.StartRead(Ms(0), fastWanted: true);
        for (var read = 1; read <= 7; read++)
        {
            Assert.False(_pacing.StartRead(Ms(read * 0.99), fastWanted: true));
        }

        Assert.True(_pacing.StartRead(Ms(8 * 0.99), fastWanted: true));
    }

    [Fact]
    public void The_wait_ends_a_millisecond_after_the_read_began_and_is_short_but_never_none_when_the_read_ran_late()
    {
        _pacing.StartRead(Ms(10), fastWanted: true);

        Assert.Equal(Ms(0.8), _pacing.Wait(Ms(10.2)));
        Assert.Equal(ReadPacing.MinimumWait, _pacing.Wait(Ms(11.5)));
    }

    /// <summary>QE finding of #353: without the high-resolution timer, a wait under 1 ms must not become Sleep(0).</summary>
    [Theory]
    [InlineData(0.1, 1)]
    [InlineData(0.9, 1)]
    [InlineData(1.0, 1)]
    [InlineData(1.2, 2)]
    [InlineData(8.0, 8)]
    public void Without_the_timer_a_wait_sleeps_whole_milliseconds_rounded_up(double milliseconds, int expected)
    {
        Assert.Equal(expected, PreciseWait.FallbackSleepMs(Ms(milliseconds)));
    }

    [Fact]
    public void When_the_pads_go_the_gaps_and_CPU_are_logged_and_the_priority_drops_back()
    {
        _pacing.StartRead(Ms(0), fastWanted: true);
        _pacing.StartRead(Ms(1), fastWanted: true);
        _pacing.StartRead(Ms(3), fastWanted: true);
        // 1 % of 4 cores over 4 s.
        _processorTime = Ms(160);

        Assert.True(_pacing.StartRead(Ms(4000), fastWanted: false));

        Assert.Equal([true, false], _priority);
        Assert.Equal(
        [
            "Controller: Pads werden weitergeleitet, Arcadia liest alle 1 ms mit hoher Priorität",
            "Controller: Abstand der Lesevorgänge n 2, Mittel 1500 µs, p50 1010 µs, p99 2000 µs, max 2000 µs; CPU Arcadia 1.00 % aller Kerne in 0.1 min",
            "Controller: keine Pads mehr weitergeleitet, Arcadia liest wieder alle 8 ms",
        ], _log);
    }

    [Fact]
    public void Every_10_minutes_of_fast_reads_a_summary_is_logged_and_the_next_one_starts_over()
    {
        _pacing.StartRead(TimeSpan.Zero, fastWanted: true);
        _pacing.StartRead(ReadPacing.SummaryEvery, fastWanted: true);
        _pacing.StartRead(ReadPacing.SummaryEvery + Ms(1), fastWanted: true);
        _pacing.Stop(ReadPacing.SummaryEvery + Ms(2));

        var summaries = _log.Where(line => line.Contains("Abstand")).ToList();
        Assert.Equal(2, summaries.Count);
        Assert.StartsWith("Controller: Abstand der Lesevorgänge n 1, ", summaries[0]);
        Assert.Contains("in 10.0 min", summaries[0]);
        Assert.StartsWith("Controller: Abstand der Lesevorgänge n 1, Mittel 1000 µs", summaries[1]);
        Assert.Equal([true, false], _priority);
    }
}
