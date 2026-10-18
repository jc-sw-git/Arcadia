using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class RepeatGestureTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(60);

    private readonly FakeTimeProvider _time = new();
    private readonly RepeatGesture _gesture;
    private int _repeats;

    public RepeatGestureTests()
    {
        _gesture = new RepeatGesture(Delay, Interval, _time, action => action());
        _gesture.Repeated += () => _repeats++;
    }

    [Fact]
    public void A_press_acts_at_once_and_a_short_one_only_once()
    {
        _gesture.Press();
        Assert.Equal(1, _repeats);

        _time.Advance(TimeSpan.FromMilliseconds(300));
        _gesture.Release();
        _time.Advance(TimeSpan.FromSeconds(2));

        Assert.Equal(1, _repeats);
    }

    [Fact]
    public void Held_it_repeats_after_the_delay_at_the_interval_until_released()
    {
        _gesture.Press();
        _time.Advance(Delay);
        Assert.Equal(2, _repeats);

        _time.Advance(Interval * 3);
        Assert.Equal(5, _repeats);

        _gesture.Release();
        _time.Advance(Interval * 3);
        Assert.Equal(5, _repeats);
    }

    [Fact]
    public void A_second_press_without_release_does_not_restart_or_double_the_repeat()
    {
        _gesture.Press();
        _time.Advance(TimeSpan.FromMilliseconds(200));
        _gesture.Press();
        _time.Advance(TimeSpan.FromMilliseconds(200));

        Assert.Equal(2, _repeats);
    }

    [Fact]
    public void A_tick_posted_before_the_release_does_nothing_after_it()
    {
        var posted = new List<Action>();
        var gesture = new RepeatGesture(Delay, Interval, _time, posted.Add);
        var repeats = 0;
        gesture.Repeated += () => repeats++;

        gesture.Press();
        _time.Advance(Delay);
        gesture.Release();
        posted.ForEach(action => action());

        Assert.Equal(1, repeats);
    }
}
