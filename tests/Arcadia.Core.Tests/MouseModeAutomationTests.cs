using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class MouseModeAutomationTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly MouseMode _mode;
    private readonly MouseModeAutomation _automation;
    private bool _endWhenArcadiaInFront = true;
    private bool _startWhenArcadiaLosesFocus = true;

    public MouseModeAutomationTests()
    {
        _mode = new MouseMode(new NullMouseKeyboardOutput(), () => new MouseModeOptions(true, TimeSpan.FromSeconds(1), 5), _time);
        _automation = new MouseModeAutomation(_mode, () => _endWhenArcadiaInFront, () => _startWhenArcadiaLosesFocus);
    }

    [Fact]
    public void Losing_the_focus_to_something_else_than_a_game_switches_on()
    {
        _automation.ArcadiaDeactivated(gameInFront: false);
        Poll();

        Assert.True(_mode.IsOn);
        Assert.True(_mode.IsAutomatic);
        Assert.True(_automation.WatchesForeground);
    }

    [Fact]
    public void Losing_the_focus_to_a_game_keeps_it_off()
    {
        _automation.ArcadiaDeactivated(gameInFront: true);
        Poll();

        Assert.False(_mode.IsOn);
    }

    [Fact]
    public void Without_the_setting_losing_the_focus_does_nothing()
    {
        _startWhenArcadiaLosesFocus = false;

        _automation.ArcadiaDeactivated(gameInFront: false);
        Poll();

        Assert.False(_mode.IsOn);
    }

    [Fact]
    public void Switched_on_automatically_it_ends_when_a_game_comes_to_the_front()
    {
        _automation.ArcadiaDeactivated(gameInFront: false);
        Poll();

        _automation.ForegroundChecked(gameInFront: false);
        Poll();
        Assert.True(_mode.IsOn);

        _automation.ForegroundChecked(gameInFront: true);
        Poll();
        Assert.False(_mode.IsOn);
    }

    [Fact]
    public void Switched_on_by_hand_it_stays_when_a_game_comes_to_the_front()
    {
        SwitchOnByHand();

        Assert.False(_automation.WatchesForeground);
        _automation.ForegroundChecked(gameInFront: true);
        Poll();

        Assert.True(_mode.IsOn);
    }

    [Fact]
    public void Arcadia_coming_to_the_front_ends_it_with_the_setting()
    {
        SwitchOnByHand();

        _automation.ArcadiaActivated();
        Poll();

        Assert.False(_mode.IsOn);
    }

    [Fact]
    public void Arcadia_coming_to_the_front_keeps_it_without_the_setting()
    {
        _endWhenArcadiaInFront = false;
        SwitchOnByHand();

        _automation.ArcadiaActivated();
        Poll();

        Assert.True(_mode.IsOn);
    }

    [Fact]
    public void Switched_off_by_hand_it_stays_off_until_arcadia_has_been_in_front_again()
    {
        _automation.ArcadiaDeactivated(gameInFront: false);
        Poll();
        HoldCombo();
        Poll();
        Assert.False(_mode.IsOn);

        // A game Arcadia doesn't know: nothing switches it back on behind the player's back.
        _automation.ForegroundChecked(gameInFront: false);
        Poll();
        Assert.False(_mode.IsOn);

        _automation.ArcadiaActivated();
        _automation.ArcadiaDeactivated(gameInFront: false);
        Poll();
        Assert.True(_mode.IsOn);
    }

    private void SwitchOnByHand()
    {
        HoldCombo();

        Assert.True(_mode.IsOn);
        Assert.False(_mode.IsAutomatic);
    }

    private void HoldCombo()
    {
        for (var held = TimeSpan.Zero; held <= TimeSpan.FromSeconds(1); held += TimeSpan.FromMilliseconds(8))
        {
            Poll(new GamepadReading(0, PadButtons.View | PadButtons.Menu, default, default));
        }
    }

    private void Poll(params GamepadReading[] pads)
    {
        _time.Advance(TimeSpan.FromMilliseconds(8));
        _mode.Update(pads);
    }
}
