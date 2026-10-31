using System.Diagnostics;

namespace Arcadia.Input;

/// <summary>
/// Delays of a hot path (#238 D9, T242-18) for <c>arcadia.log</c>: count, mean, p50, p99 and maximum over every sample
/// since the last <see cref="Reset"/>, with <see cref="Due"/> saying when the next summary is to be logged. Percentiles
/// come from 10 µs buckets, so adding stays cheap at a thousand samples a second. Thread-safe.
/// </summary>
public sealed class LatencyStats
{
    private const double BucketMicroseconds = 10;

    // Up to 50 ms; anything slower lands in the last bucket (the maximum is kept exactly).
    private const int Buckets = 5000;

    private readonly long[] _counts = new long[Buckets];
    private readonly Lock _lock = new();
    private readonly long _every;
    private long _count;
    private double _sumMicroseconds;
    private double _maxMicroseconds;
    private long _lastSummary = Stopwatch.GetTimestamp();

    /// <param name="every">How often <see cref="Due"/> says yes; 10 minutes by default.</param>
    public LatencyStats(TimeSpan? every = null)
    {
        _every = (long)((every ?? TimeSpan.FromMinutes(10)).TotalSeconds * Stopwatch.Frequency);
    }

    public long Count
    {
        get
        {
            lock (_lock) return _count;
        }
    }

    public void Add(TimeSpan delay)
    {
        var microseconds = Math.Max(0, delay.TotalMicroseconds);
        var bucket = (int)Math.Min(Buckets - 1, microseconds / BucketMicroseconds);
        lock (_lock)
        {
            _counts[bucket]++;
            _count++;
            _sumMicroseconds += microseconds;
            _maxMicroseconds = Math.Max(_maxMicroseconds, microseconds);
        }
    }

    /// <summary>True once per interval given at construction.</summary>
    public bool Due()
    {
        var now = Stopwatch.GetTimestamp();
        var last = Interlocked.Read(ref _lastSummary);
        return now - last >= _every && Interlocked.CompareExchange(ref _lastSummary, now, last) == last;
    }

    /// <summary>"n …, Mittel … µs, p50 … µs, p99 … µs, max … µs", or "keine Werte". A percentile is its bucket's upper edge.</summary>
    public string Summary()
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                return "keine Werte";
            }

            return FormattableString.Invariant(
                $"n {_count}, Mittel {_sumMicroseconds / _count:0} µs, p50 {Percentile(0.50):0} µs, p99 {Percentile(0.99):0} µs, max {_maxMicroseconds:0} µs");
        }
    }

    /// <summary>Starts over, for a new pad or a new stretch of fast reads.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            Array.Clear(_counts);
            _count = 0;
            _sumMicroseconds = 0;
            _maxMicroseconds = 0;
            Interlocked.Exchange(ref _lastSummary, Stopwatch.GetTimestamp());
        }
    }

    /// <summary>Under the lock.</summary>
    private double Percentile(double p)
    {
        var rank = (long)Math.Ceiling(_count * p);
        long seen = 0;
        for (var bucket = 0; bucket < Buckets; bucket++)
        {
            seen += _counts[bucket];
            if (seen >= rank)
            {
                return Math.Min(_maxMicroseconds, (bucket + 1) * BucketMicroseconds);
            }
        }

        return _maxMicroseconds;
    }
}
