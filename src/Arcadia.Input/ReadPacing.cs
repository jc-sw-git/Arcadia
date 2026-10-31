namespace Arcadia.Input;

/// <summary>
/// How often <see cref="SdlGamepadSource"/> reads the controllers (#238 D9). As a rule every
/// <see cref="SdlGamepadSource.PollIntervalMs"/>; while emulated pads are forwarded every <see cref="FastInterval"/> at
/// high priority, so a controller's report waits about a millisecond for its read instead of up to a whole poll. Not
/// while every pad rests (Arcadia in front, the mouse mode): <c>FastReads</c> asks for it only while states reach a game.
/// <see cref="IGamepadSource.Polled"/> (Arcadia's own input, the mouse mode) keeps its 8 ms clock either way; only
/// <see cref="IGamepadSource.Sampled"/> comes with every read.
/// </summary>
/// <remarks>
/// SDL has no call that waits for a controller's next report: it reads what arrived when asked, so a read every
/// millisecond is the nearest to forwarding each report as it comes. While fast, the time between reads (the most a
/// report waits for its read) and Arcadia's CPU over the same stretch are logged every <see cref="SummaryEvery"/> and
/// when it ends.
/// </remarks>
public sealed class ReadPacing
{
    public static readonly TimeSpan FastInterval = TimeSpan.FromMilliseconds(1);
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(SdlGamepadSource.PollIntervalMs);
    public static readonly TimeSpan SummaryEvery = TimeSpan.FromMinutes(10);

    /// <summary>Also after a read that took longer than <see cref="FastInterval"/>: a thread at high priority always yields.</summary>
    public static readonly TimeSpan MinimumWait = TimeSpan.FromMilliseconds(0.1);

    // Reads come about every millisecond, a little early or late: a poll a read early beats one a read late.
    private static readonly TimeSpan PollTolerance = FastInterval / 2;

    private readonly Action<string> _log;
    private readonly Action<bool> _highPriority;
    private readonly Func<TimeSpan> _processorTime;
    private readonly int _processorCount;
    private readonly LatencyStats _gaps = new();
    private bool _fast;
    private TimeSpan _lastRead;
    private TimeSpan _lastPoll = TimeSpan.MinValue;
    private TimeSpan _summaryAt;
    private TimeSpan _processorTimeAtSummary;

    /// <param name="log">For <c>arcadia.log</c>.</param>
    /// <param name="highPriority">Raises (true) or restores (false) the reading thread's priority.</param>
    /// <param name="processorTime">Arcadia's CPU time so far, all threads.</param>
    /// <param name="processorCount">Logical processors: the CPU share is of all of them, as Task Manager shows it.</param>
    public ReadPacing(Action<string> log, Action<bool> highPriority, Func<TimeSpan> processorTime, int processorCount)
    {
        _log = log;
        _highPriority = highPriority;
        _processorTime = processorTime;
        _processorCount = Math.Max(1, processorCount);
    }

    public bool IsFast => _fast;

    /// <summary>At the start of each read.</summary>
    /// <param name="now">A steady clock; only differences count.</param>
    /// <param name="fastWanted">Pads are forwarded now (<see cref="IGamepadSource.ReadFast"/>).</param>
    /// <returns>Whether this read is also a poll (<see cref="IGamepadSource.Polled"/> and the button events).</returns>
    public bool StartRead(TimeSpan now, bool fastWanted)
    {
        if (fastWanted != _fast)
        {
            Switch(now, fastWanted);
        }
        else if (_fast)
        {
            _gaps.Add(now - _lastRead);
            if (now - _summaryAt >= SummaryEvery)
            {
                Summarise(now);
            }
        }

        _lastRead = now;
        var pollDue = !_fast || _lastPoll == TimeSpan.MinValue || now - _lastPoll >= PollInterval - PollTolerance;
        if (pollDue)
        {
            _lastPoll = now;
        }

        return pollDue;
    }

    /// <summary>
    /// How long to wait at <paramref name="now"/> before the next read: while fast until a millisecond after the last read
    /// began, at least <see cref="MinimumWait"/>.
    /// </summary>
    public TimeSpan Wait(TimeSpan now) => _fast ? TimeSpan.FromTicks(Math.Max(MinimumWait.Ticks, (_lastRead + FastInterval - now).Ticks)) : PollInterval;

    /// <summary>The reading thread ends: a fast stretch is summed up.</summary>
    public void Stop(TimeSpan now)
    {
        if (_fast)
        {
            Switch(now, false);
        }
    }

    private void Switch(TimeSpan now, bool fast)
    {
        if (fast)
        {
            _fast = true;
            _summaryAt = now;
            _processorTimeAtSummary = _processorTime();
            _highPriority(true);
            _log($"Controller: Pads werden weitergeleitet, Arcadia liest alle {FastInterval.TotalMilliseconds:0} ms mit hoher Priorität");
            return;
        }

        Summarise(now);
        _fast = false;
        _highPriority(false);
        _log($"Controller: keine Pads mehr weitergeleitet, Arcadia liest wieder alle {PollInterval.TotalMilliseconds:0} ms");
    }

    private void Summarise(TimeSpan now)
    {
        var processorTime = _processorTime();
        var wall = now - _summaryAt;
        var share = wall > TimeSpan.Zero
            ? (processorTime - _processorTimeAtSummary).TotalMilliseconds / (wall.TotalMilliseconds * _processorCount) * 100
            : 0;
        _log(FormattableString.Invariant(
            $"Controller: Abstand der Lesevorgänge {_gaps.Summary()}; CPU Arcadia {share:0.00} % aller Kerne in {wall.TotalMinutes:0.0} min"));
        _gaps.Reset();
        _summaryAt = now;
        _processorTimeAtSummary = processorTime;
    }
}
