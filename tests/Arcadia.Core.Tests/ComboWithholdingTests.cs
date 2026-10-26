using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public class ComboWithholdingTests
{
    private static readonly ComboWithholdingOptions Both = new(BackAndStart: true, ReturnHold: TimeSpan.FromSeconds(1));
    private readonly FakeTimeProvider _time = new();
    private readonly ComboWithholding _filter;

    public ComboWithholdingTests() => _filter = new ComboWithholding(_time);

    private PadButtons Poll(PadButtons buttons, ComboWithholdingOptions? options = null, int afterMs = 8)
    {
        _time.Advance(TimeSpan.FromMilliseconds(afterMs));
        return _filter.Filter(buttons, options ?? Both);
    }

    [Fact]
    public void Other_buttons_go_through_at_once()
    {
        Assert.Equal(PadButtons.A | PadButtons.LeftTrigger, Poll(PadButtons.A | PadButtons.LeftTrigger));
    }

    [Fact]
    public void Back_and_Start_together_never_reach_the_game_in_either_order()
    {
        Assert.Equal(PadButtons.None, Poll(PadButtons.View));
        Assert.Equal(PadButtons.A, Poll(PadButtons.View | PadButtons.Menu | PadButtons.A));
        Assert.Equal(PadButtons.None, Poll(PadButtons.View | PadButtons.Menu, afterMs: 3000));
        Assert.Equal(PadButtons.None, Poll(PadButtons.Menu));
        Assert.Equal(PadButtons.None, Poll(PadButtons.None));

        Assert.Equal(PadButtons.None, Poll(PadButtons.Menu));
        Assert.Equal(PadButtons.None, Poll(PadButtons.Menu | PadButtons.View));
        Assert.Equal(PadButtons.None, Poll(PadButtons.View));
        Assert.Equal(PadButtons.None, Poll(PadButtons.None));
    }

    [Fact]
    public void A_short_press_of_View_or_Menu_reaches_the_game_as_a_tap_on_release()
    {
        foreach (var button in new[] { PadButtons.View, PadButtons.Menu })
        {
            Assert.Equal(PadButtons.None, Poll(button));
            Assert.Equal(PadButtons.None, Poll(button, afterMs: 100));
            Assert.Equal(button, Poll(PadButtons.None));
            Assert.Equal(button, Poll(PadButtons.None, afterMs: 40));
            Assert.Equal(PadButtons.None, Poll(PadButtons.None, afterMs: 20));
        }
    }

    [Fact]
    public void Menu_held_alone_a_long_time_still_reaches_the_game_only_as_a_tap_on_release()
    {
        Assert.Equal(PadButtons.None, Poll(PadButtons.Menu));
        Assert.Equal(PadButtons.None, Poll(PadButtons.Menu, afterMs: 2000));
        Assert.Equal(PadButtons.Menu, Poll(PadButtons.None));
    }

    /// <summary>Codex review of #231: the mouse mode counts Back + Start however late the second button joins.</summary>
    [Theory]
    [InlineData(PadButtons.Menu, PadButtons.View, true)]
    [InlineData(PadButtons.View, PadButtons.Menu, false)]
    public void A_second_button_joining_late_still_makes_it_a_combo_the_game_never_sees(PadButtons first, PadButtons second, bool returnOn)
    {
        var options = new ComboWithholdingOptions(BackAndStart: true, ReturnHold: returnOn ? TimeSpan.FromSeconds(5) : null);
        Assert.Equal(PadButtons.None, Poll(first, options));
        Assert.Equal(PadButtons.None, Poll(first, options, afterMs: 250));
        Assert.Equal(PadButtons.None, Poll(first | second, options, afterMs: 250));
        Assert.Equal(PadButtons.None, Poll(first | second, options, afterMs: 1500));
        Assert.Equal(PadButtons.None, Poll(PadButtons.None, options));
        Assert.Equal(PadButtons.None, Poll(PadButtons.None, options, afterMs: 100));
    }

    [Fact]
    public void View_held_for_the_way_back_never_reaches_the_game()
    {
        Assert.Equal(PadButtons.None, Poll(PadButtons.View));
        Assert.Equal(PadButtons.None, Poll(PadButtons.View, afterMs: 500));
        Assert.Equal(PadButtons.None, Poll(PadButtons.View, afterMs: 600));
        Assert.Equal(PadButtons.None, Poll(PadButtons.None));
        Assert.Equal(PadButtons.None, Poll(PadButtons.None, afterMs: 100));
    }

    [Fact]
    public void View_held_shorter_than_the_way_back_reaches_the_game_as_a_tap()
    {
        Assert.Equal(PadButtons.None, Poll(PadButtons.View));
        Assert.Equal(PadButtons.None, Poll(PadButtons.View, afterMs: 600));
        Assert.Equal(PadButtons.View, Poll(PadButtons.None));
    }

    [Fact]
    public void With_both_combos_off_View_and_Menu_go_through_at_once()
    {
        var off = new ComboWithholdingOptions(BackAndStart: false, ReturnHold: null);
        Assert.Equal(PadButtons.View | PadButtons.Menu, Poll(PadButtons.View | PadButtons.Menu, off));
    }

    [Fact]
    public void Without_the_way_back_View_held_alone_reaches_the_game_as_a_tap_on_release()
    {
        var backAndStartOnly = new ComboWithholdingOptions(BackAndStart: true, ReturnHold: null);
        Assert.Equal(PadButtons.None, Poll(PadButtons.View, backAndStartOnly));
        Assert.Equal(PadButtons.None, Poll(PadButtons.View, backAndStartOnly, afterMs: 3000));
        Assert.Equal(PadButtons.View, Poll(PadButtons.None, backAndStartOnly));
    }

    [Fact]
    public void Without_the_mouse_mode_Menu_goes_through_at_once_and_only_View_waits()
    {
        var returnOnly = new ComboWithholdingOptions(BackAndStart: false, ReturnHold: TimeSpan.FromSeconds(1));
        Assert.Equal(PadButtons.Menu, Poll(PadButtons.Menu, returnOnly));
        Assert.Equal(PadButtons.Menu, Poll(PadButtons.Menu | PadButtons.View, returnOnly));
    }

    [Fact]
    public void Reset_forgets_what_was_held()
    {
        Poll(PadButtons.View);
        _filter.Reset();
        Assert.Equal(PadButtons.None, Poll(PadButtons.None));
    }
}
