using System.Collections.Concurrent;

namespace Arcadia.Input;

/// <summary>
/// One toasted error and how often it came again since (#399), for the toast's entry in "Letzte Meldungen" ("×N",
/// #433). A new streak is a new object, so an older entry keeps its count. Read from any thread without allocating.
/// </summary>
public sealed class ErrorStreak
{
    private long _repeats;

    public long Repeats => Interlocked.Read(ref _repeats);

    internal void Repeat() => Interlocked.Increment(ref _repeats);
}

/// <summary>
/// The unexpected errors of places that repeat them every few seconds or every millisecond (#399): per source, the
/// first error of a streak goes to <c>unexpected</c> (log with stack trace, toast); its repeats reach the log at most
/// once per <see cref="SummaryInterval"/>, as one line with the latest message and the count since the source's last
/// line. A source quiet for <see cref="QuietPeriod"/> starts a new streak with its next error. No timer: a count left
/// when the errors stop is written with the source's next line. Time is the clock's monotonic timestamp, so a changed
/// system clock neither shortens nor prolongs either period. Callable from any thread; a repeat allocates nothing.
/// </summary>
/// <param name="nameOf">The source's name for its summary lines, or null when the context names it already.</param>
public sealed class RepeatedErrors<TKey>(TimeProvider time, Action<string> log, Func<TKey, string?> nameOf)
    where TKey : notnull
{
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan SummaryInterval = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<TKey, Source> _sources = new();

    /// <param name="context">What the source was doing, in German, for the log and the toast.</param>
    /// <param name="unexpected">Logs the error with its stack and shows the toast; a streak's first error only.</param>
    public void Report(TKey key, string context, Exception error, Action<Exception, string, ErrorStreak> unexpected)
    {
        if (error is OperationCanceledException)
        {
            // Shutdown, as in AppErrors.Report: no error of the source, it neither ends nor starts a streak.
            return;
        }

        var source = _sources.GetOrAdd(key, static (key, nameOf) => new Source(nameOf(key)), nameOf);
        var now = time.GetTimestamp();
        ErrorStreak? started = null;
        long unlogged = 0;
        Exception? latest = null;
        lock (source)
        {
            if (source.Streak is null || time.GetElapsedTime(source.LastErrorAt, now) >= QuietPeriod)
            {
                // The repeats of the streak that ended were not logged yet: they come before the new error.
                (unlogged, latest) = (source.Unlogged, source.Latest);
                started = source.Streak = new ErrorStreak();
                (source.Unlogged, source.Latest) = (0, null);

                // The error's own line is the source's line of this minute.
                source.LastLineAt = now;
            }
            else
            {
                source.Streak.Repeat();
                source.Unlogged++;
                source.Latest = error;
                if (time.GetElapsedTime(source.LastLineAt, now) >= SummaryInterval)
                {
                    (unlogged, latest) = (source.Unlogged, error);
                    source.Unlogged = 0;
                    source.LastLineAt = now;
                }
            }

            source.LastErrorAt = now;
        }

        // No lambdas here: their captures would be allocated on every call, also for a repeat that writes nothing.
        if (unlogged > 0 && latest is not null)
        {
            try
            {
                log(SummaryLine(context, source.Name, unlogged, latest));
            }
            catch (Exception)
            {
                // Reporting must not end the poll thread, loop or read that reported; the counts are already kept.
            }
        }

        if (started is not null)
        {
            try
            {
                unexpected(error, context, started);
            }
            catch (Exception)
            {
                // As above; the next summary line still has its count.
            }
        }
    }

    private static string SummaryLine(string context, string? name, long count, Exception latest) =>
        $"Unerwarteter Fehler ({context}{(name is null ? "" : $", {name}")}) wiederholt: {count}× seit der letzten Zeile, "
        + $"zuletzt {latest.GetType().Name}: {latest.Message}";

    /// <summary>One source's state; its own lock, so sources on different threads don't wait for each other.</summary>
    private sealed class Source(string? name)
    {
        public string? Name { get; } = name;
        public ErrorStreak? Streak { get; set; }
        public long LastErrorAt { get; set; }
        public long LastLineAt { get; set; }
        public long Unlogged { get; set; }
        public Exception? Latest { get; set; }
    }
}
