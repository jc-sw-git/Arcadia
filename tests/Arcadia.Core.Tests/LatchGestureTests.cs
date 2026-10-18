using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class LatchGestureTests
{
    private static readonly TimeSpan HoldTime = TimeSpan.FromMilliseconds(500);

    private readonly FakeTimeProvider _time = new();
    private readonly LatchGesture _shift;

    public LatchGestureTests() => _shift = new LatchGesture(HoldTime, _time);

    [Fact]
    public void A_tap_switches_it_on_and_the_next_tap_off()
    {
        Tap();
        Assert.True(_shift.IsOn);

        Tap();
        Assert.False(_shift.IsOn);
    }

    [Fact]
    public void It_is_on_while_held_and_off_after_a_long_hold()
    {
        _shift.Press();
        Assert.True(_shift.IsOn);

        _time.Advance(HoldTime);
        _shift.Release();

        Assert.False(_shift.IsOn);
    }

    [Fact]
    public void Typing_while_held_makes_even_a_short_press_a_hold()
    {
        _shift.Press();
        _shift.Use();
        _time.Advance(TimeSpan.FromMilliseconds(100));
        _shift.Release();

        Assert.False(_shift.IsOn);
    }

    [Fact]
    public void Held_while_switched_on_it_is_off_afterwards()
    {
        Tap();
        _shift.Press();
        _time.Advance(HoldTime);
        _shift.Release();

        Assert.False(_shift.IsOn);
    }

    [Fact]
    public void Typing_while_switched_on_keeps_it_on()
    {
        Tap();
        _shift.Use();

        Assert.True(_shift.IsOn);
    }

    [Fact]
    public void A_cancelled_press_restores_the_state_before_it()
    {
        _shift.Press();
        _shift.Cancel();
        Assert.False(_shift.IsOn);

        Tap();
        _shift.Press();
        _shift.Cancel();
        _shift.Release();
        Assert.True(_shift.IsOn);
    }

    [Fact]
    public void Changes_are_reported_once_each()
    {
        var changes = new List<bool>();
        _shift.Changed += changes.Add;

        _shift.Press();
        _shift.Press();
        _shift.Release();
        _shift.Toggle();
        _shift.Reset();
        _shift.Release();

        Assert.Equal([true, false], changes);
    }

    private void Tap()
    {
        _shift.Press();
        _time.Advance(TimeSpan.FromMilliseconds(100));
        _shift.Release();
    }
}
