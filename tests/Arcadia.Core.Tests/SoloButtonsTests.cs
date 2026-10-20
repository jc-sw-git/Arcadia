using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class SoloButtonsTests
{
    private static readonly TimeSpan SoloDelay = TimeSpan.FromMilliseconds(200);

    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _events = [];

    private SoloButtons Create(TimeSpan? soloDelay)
    {
        var buttons = new SoloButtons(GamepadButton.View, GamepadButton.Menu, soloDelay, _time);
        buttons.Pressed += b => _events.Add($"{b} pressed");
        buttons.Released += b => _events.Add($"{b} released");
        buttons.Cancelled += b => _events.Add($"{b} cancelled");
        return buttons;
    }

    [Fact]
    public void A_tap_acts_on_release()
    {
        var buttons = Create(SoloDelay);

        buttons.Press(GamepadButton.Menu);
        buttons.Tick();
        Assert.Empty(_events);

        buttons.Release(GamepadButton.Menu);
        Assert.Equal(["Menu pressed", "Menu released"], _events);
    }

    [Fact]
    public void Pressed_together_neither_acts()
    {
        var buttons = Create(SoloDelay);

        buttons.Press(GamepadButton.View);
        _time.Advance(TimeSpan.FromMilliseconds(100));
        buttons.Press(GamepadButton.Menu);
        _time.Advance(TimeSpan.FromSeconds(3));
        buttons.Tick();
        buttons.Release(GamepadButton.View);
        buttons.Release(GamepadButton.Menu);

        Assert.Empty(_events);
    }

    [Fact]
    public void Held_alone_past_the_delay_it_acts_without_waiting_for_the_release()
    {
        var buttons = Create(SoloDelay);

        buttons.Press(GamepadButton.Menu);
        _time.Advance(SoloDelay);
        buttons.Tick();
        Assert.Equal(["Menu pressed"], _events);

        buttons.Release(GamepadButton.Menu);
        Assert.Equal(["Menu pressed", "Menu released"], _events);
    }

    [Fact]
    public void A_button_already_acting_is_cancelled_when_the_other_joins()
    {
        var buttons = Create(SoloDelay);

        buttons.Press(GamepadButton.Menu);
        _time.Advance(SoloDelay);
        buttons.Tick();
        buttons.Press(GamepadButton.View);
        buttons.Release(GamepadButton.Menu);
        buttons.Release(GamepadButton.View);

        Assert.Equal(["Menu pressed", "Menu cancelled"], _events);
    }

    [Fact]
    public void Without_a_delay_only_the_release_acts()
    {
        var buttons = Create(soloDelay: null);

        buttons.Press(GamepadButton.View);
        _time.Advance(TimeSpan.FromSeconds(5));
        buttons.Tick();
        Assert.Empty(_events);

        buttons.Release(GamepadButton.View);
        Assert.Equal(["View pressed", "View released"], _events);
    }

    [Fact]
    public void After_a_reset_the_release_of_a_held_button_does_nothing()
    {
        var buttons = Create(SoloDelay);

        buttons.Press(GamepadButton.View);
        buttons.Reset();
        buttons.Release(GamepadButton.View);

        Assert.Empty(_events);
    }

    [Fact]
    public void The_second_button_acts_alone_once_the_first_was_let_go_before()
    {
        var buttons = Create(SoloDelay);

        buttons.Press(GamepadButton.View);
        buttons.Release(GamepadButton.View);
        buttons.Press(GamepadButton.Menu);
        buttons.Release(GamepadButton.Menu);

        Assert.Equal(["View pressed", "View released", "Menu pressed", "Menu released"], _events);
    }
}
