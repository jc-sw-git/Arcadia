using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>#399: a source's first error toasts, its repeats reach the log at most once a minute, ten quiet minutes reset it.</summary>
public sealed class RepeatedErrorsTests
{
    private const string Context = "Heartbeat an Pan";

    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly List<string> _toasts = [];
    private readonly List<ErrorStreak> _streaks = [];
    private readonly RepeatedErrors<string> _errors;

    public RepeatedErrorsTests()
    {
        _errors = new(_time, line => { lock (_log) _log.Add(line); }, static _ => null);
    }

    private void Unexpected(Exception error, string context, ErrorStreak streak)
    {
        lock (_toasts)
        {
            _toasts.Add($"{context}: {error.Message}");
            _streaks.Add(streak);
        }
    }

    private void Fail(string message = "boom", string source = Context) =>
        _errors.Report(source, source, new InvalidOperationException(message), Unexpected);

    /// <summary>The counts of the summary lines ("wiederholt: N×").</summary>
    private List<long> Counts() =>
        [.. _log.Select(line => long.Parse(line[(line.IndexOf("wiederholt: ", StringComparison.Ordinal) + 12)..line.IndexOf('×')]))];

    [Fact]
    [Trait("TestCase", "T399-1")]
    public void After_ten_quiet_minutes_the_next_error_toasts_again()
    {
        Fail();
        _time.Advance(RepeatedErrors<string>.QuietPeriod);
        Fail();

        Assert.Equal([$"{Context}: boom", $"{Context}: boom"], _toasts);
        Assert.Empty(_log);
    }

    [Fact]
    [Trait("TestCase", "T399-2")]
    public void The_quiet_period_counts_from_the_last_error_and_is_at_least_ten_minutes()
    {
        Fail();
        _time.Advance(TimeSpan.FromSeconds(599));
        Fail();
        Assert.Single(_toasts);

        _time.Advance(TimeSpan.FromSeconds(600));
        Fail();

        Assert.Equal(2, _toasts.Count);
        Assert.Equal([1], Counts());
    }

    [Fact]
    [Trait("TestCase", "T399-3")]
    public void A_steady_error_toasts_once_and_logs_at_most_once_a_minute_with_every_repeat_counted()
    {
        for (var i = 0; i < 720; i++)
        {
            Fail($"timeout {i}");
            _time.Advance(TimeSpan.FromSeconds(5));
        }

        Assert.Single(_toasts);
        Assert.InRange(_log.Count, 1, 60);
        Assert.All(_log, line => Assert.StartsWith($"Unerwarteter Fehler ({Context}) wiederholt: ", line));
        Assert.EndsWith("InvalidOperationException: timeout 708", _log[^1]);

        // The repeats after the last line come with the source's next one.
        _time.Advance(RepeatedErrors<string>.QuietPeriod);
        Fail();
        Assert.Equal(719, Counts().Sum());
        Assert.EndsWith("timeout 719", _log[^1]);
    }

    [Fact]
    [Trait("TestCase", "T399-5")]
    public void A_pending_count_is_written_with_the_sources_next_line_never_by_itself()
    {
        // The first error and 30 repeats.
        for (var i = 0; i <= 30; i++)
        {
            Fail();
            _time.Advance(TimeSpan.FromSeconds(40.0 / 31));
        }

        Assert.Empty(_log);
        for (var minute = 0; minute < 60; minute++)
        {
            _time.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Empty(_log);

        Fail("back");

        Assert.Equal([30], Counts());
        Assert.Equal([$"{Context}: boom", $"{Context}: back"], _toasts);
    }

    [Fact]
    [Trait("TestCase", "T399-5")]
    public void A_source_that_stops_without_repeats_writes_no_line()
    {
        Fail();
        _time.Advance(TimeSpan.FromHours(1));
        Fail();

        Assert.Empty(_log);
        Assert.Equal(2, _toasts.Count);
    }

    [Fact]
    [Trait("TestCase", "T399-6")]
    public void A_different_exception_at_the_same_source_is_a_repeat_and_its_message_is_logged()
    {
        _errors.Report(Context, Context, new TimeoutException("zu langsam"), Unexpected);
        _time.Advance(TimeSpan.FromMinutes(3));
        _errors.Report(Context, Context, new IOException("Pipe zu"), Unexpected);

        Assert.Equal([$"{Context}: zu langsam"], _toasts);
        Assert.EndsWith("zuletzt IOException: Pipe zu", Assert.Single(_log));
    }

    [Fact]
    [Trait("TestCase", "T399-7")]
    public void Two_sources_each_toast_and_keep_their_own_counts()
    {
        Fail();
        _time.Advance(TimeSpan.FromSeconds(5));
        Fail();
        Fail("keeper", "Verbindung zu Pan halten");
        _time.Advance(TimeSpan.FromMinutes(1));
        Fail();
        Fail("keeper", "Verbindung zu Pan halten");

        Assert.Equal([$"{Context}: boom", "Verbindung zu Pan halten: keeper"], _toasts);
        Assert.Equal(2, _log.Count);
        Assert.Contains(_log, line => line.StartsWith($"Unerwarteter Fehler ({Context}) wiederholt: 2×", StringComparison.Ordinal));
        Assert.Contains(_log, line => line.StartsWith("Unerwarteter Fehler (Verbindung zu Pan halten) wiederholt: 1×", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T399-8")]
    public void A_source_with_a_name_names_it_in_its_summary_line()
    {
        var errors = new RepeatedErrors<string>(_time, _log.Add, static name => name);
        errors.Report("LayoutOf", "Controller für Spiele", new InvalidOperationException("a"), Unexpected);
        _time.Advance(TimeSpan.FromMinutes(1));
        errors.Report("LayoutOf", "Controller für Spiele", new InvalidOperationException("b"), Unexpected);

        Assert.StartsWith("Unerwarteter Fehler (Controller für Spiele, LayoutOf) wiederholt: 1×", Assert.Single(_log));
    }

    [Fact]
    [Trait("TestCase", "T399-10")]
    public void A_new_instance_starts_without_history()
    {
        Fail();
        var restarted = new RepeatedErrors<string>(_time, _log.Add, static _ => null);
        restarted.Report(Context, Context, new InvalidOperationException("again"), Unexpected);

        Assert.Equal([$"{Context}: boom", $"{Context}: again"], _toasts);
    }

    [Fact]
    [Trait("TestCase", "T399-11")]
    public void A_jumping_wall_clock_changes_neither_the_quiet_period_nor_the_lines()
    {
        var clock = new JumpingClock();
        var errors = new RepeatedErrors<string>(clock, _log.Add, static _ => null);
        void Report() => errors.Report(Context, Context, new InvalidOperationException("boom"), Unexpected);

        Report();
        clock.Wall -= TimeSpan.FromHours(1);
        clock.Advance(TimeSpan.FromSeconds(30));
        Report();
        clock.Wall += TimeSpan.FromHours(2);
        clock.Advance(TimeSpan.FromSeconds(20));
        Report();

        Assert.Single(_toasts);
        Assert.Empty(_log);

        clock.Advance(TimeSpan.FromSeconds(10));
        Report();
        Assert.Equal([3], Counts());
    }

    [Fact]
    [Trait("TestCase", "T399-12")]
    public void A_throwing_toast_or_log_is_swallowed_and_the_count_is_kept()
    {
        var log = new List<string>();
        var logBroken = false;
        var errors = new RepeatedErrors<string>(_time, line =>
        {
            if (logBroken)
            {
                throw new IOException("log broke");
            }

            log.Add(line);
        }, static _ => null);

        errors.Report(Context, Context, new InvalidOperationException("first"), (_, _, _) => throw new InvalidOperationException("toast broke"));
        _time.Advance(TimeSpan.FromSeconds(30));
        errors.Report(Context, Context, new InvalidOperationException("second"), Unexpected);
        _time.Advance(TimeSpan.FromSeconds(30));
        errors.Report(Context, Context, new InvalidOperationException("third"), Unexpected);

        Assert.Empty(_toasts);
        Assert.StartsWith($"Unerwarteter Fehler ({Context}) wiederholt: 2×", Assert.Single(log));

        logBroken = true;
        _time.Advance(TimeSpan.FromMinutes(1));
        errors.Report(Context, Context, new InvalidOperationException("fourth"), Unexpected);
        _time.Advance(RepeatedErrors<string>.QuietPeriod);
        errors.Report(Context, Context, new InvalidOperationException("fifth"), Unexpected);

        Assert.Equal([$"{Context}: fifth"], _toasts);
    }

    [Fact]
    [Trait("TestCase", "T399-13")]
    public void Concurrent_reports_toast_once_and_lose_or_double_no_repeat()
    {
        const int Threads = 8;
        const int Each = 10_000;
        using var start = new Barrier(Threads);
        var workers = Enumerable.Range(0, Threads).Select(i => new Thread(() =>
        {
            start.SignalAndWait();
            for (var n = 0; n < Each; n++)
            {
                Fail();
                if (n % 1000 == 0)
                {
                    lock (_time)
                    {
                        _time.Advance(TimeSpan.FromSeconds(7));
                    }
                }
            }
        })).ToList();
        workers.ForEach(t => t.Start());
        workers.ForEach(t => t.Join());

        // The rest comes with the next line: after the quiet period, with the new streak.
        _time.Advance(RepeatedErrors<string>.QuietPeriod);
        Fail();

        Assert.Equal(2, _toasts.Count);
        Assert.Equal(Threads * Each - 1, Counts().Sum());
        Assert.Equal(Threads * Each - 1, _streaks[0].Repeats);
    }

    [Fact]
    [Trait("TestCase", "T399-13")]
    public void Two_sources_on_different_threads_each_toast_once()
    {
        var a = new Thread(() => { for (var i = 0; i < 10_000; i++) Fail("a", "A"); });
        var b = new Thread(() => { for (var i = 0; i < 10_000; i++) Fail("b", "B"); });
        a.Start();
        b.Start();
        a.Join();
        b.Join();

        Assert.Equal(["A: a", "B: b"], _toasts.Order());
    }

    [Fact]
    [Trait("TestCase", "T399-14")]
    public void A_cancellation_is_no_error_of_the_source()
    {
        _errors.Report(Context, Context, new OperationCanceledException(), Unexpected);
        _errors.Report(Context, Context, new TaskCanceledException(), Unexpected);
        Assert.Empty(_toasts);

        Fail();
        _time.Advance(TimeSpan.FromMinutes(1));
        _errors.Report(Context, Context, new OperationCanceledException(), Unexpected);
        Assert.Equal(0, _streaks[0].Repeats);
        Assert.Empty(_log);

        // A cancellation doesn't keep the streak going either.
        _time.Advance(RepeatedErrors<string>.QuietPeriod - TimeSpan.FromMinutes(1));
        Fail();
        Assert.Equal(2, _toasts.Count);
    }

    [Fact]
    [Trait("TestCase", "T399-15")]
    public void A_repeat_between_two_lines_allocates_nothing()
    {
        var error = new InvalidOperationException("boom");
        Action<Exception, string, ErrorStreak> unexpected = Unexpected;
        _errors.Report(Context, Context, error, unexpected);
        _errors.Report(Context, Context, error, unexpected);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100_000; i++)
        {
            _errors.Report(Context, Context, error, unexpected);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Empty(_log);
    }

    [Fact]
    [Trait("TestCase", "T399-17")]
    public void The_streak_counts_its_repeats_and_a_new_streak_starts_at_zero()
    {
        Fail();
        for (var i = 0; i < 37; i++)
        {
            _time.Advance(TimeSpan.FromSeconds(5));
            Fail();
        }

        var first = Assert.Single(_streaks);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var repeats = first.Repeats;
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(37, repeats);

        _time.Advance(RepeatedErrors<string>.QuietPeriod);
        Fail();

        Assert.Equal(0, _streaks[1].Repeats);
        Assert.Equal(37, first.Repeats);
    }

    /// <summary>A clock whose wall time jumps while its monotonic timestamp goes on.</summary>
    private sealed class JumpingClock : TimeProvider
    {
        private long _timestamp;

        public DateTimeOffset Wall { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => Wall;

        public void Advance(TimeSpan by)
        {
            _timestamp += by.Ticks;
            Wall += by;
        }
    }
}
