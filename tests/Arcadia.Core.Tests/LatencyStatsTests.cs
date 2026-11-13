using Arcadia.Input;

namespace Arcadia.Core.Tests;

[Trait("TestCase", "T238-11")]
public sealed class LatencyStatsTests
{
    [Fact]
    public void Mean_percentiles_and_maximum_cover_every_sample_since_the_reset()
    {
        var stats = new LatencyStats();
        for (var i = 0; i < 98; i++)
        {
            stats.Add(TimeSpan.FromMicroseconds(100));
        }

        stats.Add(TimeSpan.FromMicroseconds(900));
        stats.Add(TimeSpan.FromMilliseconds(80));

        // p99 is the 99th of 100 samples, in its 10 µs bucket; the maximum beyond the last bucket stays exact.
        Assert.Equal("n 100, Mittel 907 µs, p50 110 µs, p99 910 µs, max 80000 µs", stats.Summary());
    }

    [Fact]
    public void A_percentile_beyond_the_last_bucket_says_it_is_at_least_50_ms()
    {
        var stats = new LatencyStats();
        stats.Add(TimeSpan.FromMicroseconds(49_995));
        stats.Add(TimeSpan.FromMilliseconds(60));
        stats.Add(TimeSpan.FromMilliseconds(70));

        // 49,995 µs is still in the last regular bucket; the other two are only known to be at least 50 ms.
        Assert.Equal("n 3, Mittel 59998 µs, p50 ≥ 50000 µs, p99 ≥ 50000 µs, max 70000 µs", stats.Summary());
    }

    [Fact]
    public void Nothing_measured_says_so_and_a_reset_starts_over()
    {
        var stats = new LatencyStats();
        Assert.Equal("keine Werte", stats.Summary());

        stats.Add(TimeSpan.FromMilliseconds(1));
        stats.Reset();

        Assert.Equal("keine Werte", stats.Summary());
        Assert.Equal(0, stats.Count);
    }
}
