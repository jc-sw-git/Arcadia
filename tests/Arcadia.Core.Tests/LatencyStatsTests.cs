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
