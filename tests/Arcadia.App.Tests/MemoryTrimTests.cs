using Arcadia.App.Services;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.App.Tests;

/// <summary>#636: what a burst of work left behind goes back once enough was allocated and the player is quiet.</summary>
public sealed class MemoryTrimTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private DateTimeOffset _lastInput;
    private long _allocated;
    private int _collections;

    private MemoryTrim Trim() => new(_time, () => _lastInput, () => _allocated, () => _collections++);

    [Fact]
    [Trait("TestCase", "T636-4")]
    public void After_a_burst_of_work_it_trims_once_the_player_is_quiet()
    {
        var trim = Trim();
        _lastInput = _time.GetUtcNow();
        _allocated = MemoryTrim.WorthTrimming;

        Assert.False(trim.TrimIfDue(mayPause: true));

        _time.Advance(MemoryTrim.QuietTime);
        Assert.True(trim.TrimIfDue(mayPause: true));
        Assert.Equal(1, _collections);
    }

    [Fact]
    [Trait("TestCase", "T636-4")]
    public void Idle_alone_isnt_worth_a_pause()
    {
        var trim = Trim();
        _time.Advance(TimeSpan.FromHours(1));
        _allocated = MemoryTrim.WorthTrimming - 1;

        Assert.False(trim.TrimIfDue(mayPause: true));
        Assert.Equal(0, _collections);
    }

    [Fact]
    [Trait("TestCase", "T636-4")]
    public void The_next_trim_counts_from_the_last_one()
    {
        var trim = Trim();
        _time.Advance(MemoryTrim.QuietTime);
        _allocated = MemoryTrim.WorthTrimming;
        Assert.True(trim.TrimIfDue(mayPause: true));

        _allocated += MemoryTrim.WorthTrimming / 2;
        Assert.False(trim.TrimIfDue(mayPause: true));

        _allocated += MemoryTrim.WorthTrimming / 2;
        Assert.True(trim.TrimIfDue(mayPause: true));
        Assert.Equal(2, _collections);
    }

    [Fact]
    [Trait("TestCase", "T636-4")]
    public void A_press_during_the_quiet_time_puts_it_off()
    {
        var trim = Trim();
        _allocated = MemoryTrim.WorthTrimming;
        _time.Advance(MemoryTrim.QuietTime - TimeSpan.FromSeconds(1));
        _lastInput = _time.GetUtcNow();
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.False(trim.TrimIfDue(mayPause: true));
        Assert.Equal(0, _collections);
    }

    [Fact]
    [Trait("TestCase", "T636-7")]
    public void Never_while_a_game_runs_or_Arcadia_isnt_in_front_and_it_follows_once_back()
    {
        var trim = Trim();
        _time.Advance(TimeSpan.FromHours(1));
        _allocated = 4 * MemoryTrim.WorthTrimming;

        Assert.False(trim.TrimIfDue(mayPause: false));
        Assert.Equal(0, _collections);

        Assert.True(trim.TrimIfDue(mayPause: true));
        Assert.Equal(1, _collections);
    }
}
