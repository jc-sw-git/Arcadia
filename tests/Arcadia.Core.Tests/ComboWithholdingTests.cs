using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public class ComboWithholdingTests
{
    private static readonly ComboWithholdingOptions On = new(BackAndStart: true);
    private readonly FakeTimeProvider _time = new();
    private readonly ComboWithholding _filter;

    public ComboWithholdingTests() => _filter = new ComboWithholding(_time);

    private PadButtons Poll(PadButtons buttons, ComboWithholdingOptions? options = null, int afterMs = 8)
    {
        _time.Advance(TimeSpan.FromMilliseconds(afterMs));
        return _filter.Filter(buttons, options ?? On);
    }

    [Fact]
    public void Other_buttons_go_through_at_once()
    {
        Assert.Equal(PadButtons.A | PadButtons.LeftTrigger, Poll(PadButtons.A | PadButtons.LeftTrigger));
    }

    [Theory]
    [InlineData(PadButtons.View, PadButtons.Menu)]
    [InlineData(PadButtons.Menu, PadButtons.View)]
    public void Back_and_Start_pressed_together_never_reach_the_game(PadButtons first, PadButtons second)
    {
        Assert.Equal(PadButtons.None, Poll(first));
        Assert.Equal(PadButtons.A, Poll(first | second | PadButtons.A, afterMs: 60));
        Assert.Equal(PadButtons.None, Poll(first | second, afterMs: 3000));
        Assert.Equal(PadButtons.None, Poll(second));
        Assert.Equal(PadButtons.None, Poll(PadButtons.None));
        Assert.Equal(PadButtons.None, Poll(PadButtons.None, afterMs: 100));
    }

    [Fact]
    public void Both_down_in_the_same_poll_is_the_combo_too()
    {
        Assert.Equal(PadButtons.None, Poll(PadButtons.View | PadButtons.Menu));
        Assert.Equal(PadButtons.None, Poll(PadButtons.None));
    }

    [Theory]
    [InlineData(PadButtons.View)]
    [InlineData(PadButtons.Menu)]
    public void A_short_press_reaches_the_game_as_a_tap_on_release(PadButtons button)
    {
        Assert.Equal(PadButtons.None, Poll(button));
        Assert.Equal(PadButtons.None, Poll(button, afterMs: 50));
        Assert.Equal(button, Poll(PadButtons.None));
        Assert.Equal(button, Poll(PadButtons.None, afterMs: 40));
        Assert.Equal(PadButtons.None, Poll(PadButtons.None, afterMs: 20));
    }

    /// <summary>Owner, 2026-10-26: held alone, View and Menu reach the game as held; View also while it brings Arcadia back.</summary>
    [Theory]
    [InlineData(PadButtons.View)]
    [InlineData(PadButtons.Menu)]
    public void Held_alone_past_the_combo_window_it_goes_through_as_held(PadButtons button)
    {
        Assert.Equal(PadButtons.None, Poll(button));
        Assert.Equal(PadButtons.None, Poll(button, afterMs: 100));
        Assert.Equal(button, Poll(button, afterMs: 10));
        Assert.Equal(button, Poll(button, afterMs: 3000));
        Assert.Equal(PadButtons.None, Poll(PadButtons.None));
    }

    /// <summary>Codex review of #231: the second button joining late is no combo, neither here nor in the mouse mode.</summary>
    [Theory]
    [InlineData(PadButtons.Menu, PadButtons.View)]
    [InlineData(PadButtons.View, PadButtons.Menu)]
    public void A_second_button_joining_late_goes_through_too(PadButtons first, PadButtons second)
    {
        Poll(first);
        Assert.Equal(first, Poll(first, afterMs: 250));
        Assert.Equal(first | second, Poll(first | second));
        Assert.Equal(second, Poll(second));
        Assert.Equal(PadButtons.None, Poll(PadButtons.None));
    }

    [Fact]
    public void With_the_mouse_mode_off_View_and_Menu_go_through_at_once()
    {
        var off = new ComboWithholdingOptions(BackAndStart: false);
        Assert.Equal(PadButtons.View | PadButtons.Menu, Poll(PadButtons.View | PadButtons.Menu, off));
    }

    [Fact]
    public void Reset_forgets_what_was_held()
    {
        Poll(PadButtons.View);
        _filter.Reset();
        Assert.Equal(PadButtons.None, Poll(PadButtons.None));
    }
}
