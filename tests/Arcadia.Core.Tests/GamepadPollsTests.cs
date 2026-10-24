using Arcadia.Input;

namespace Arcadia.Core.Tests;

public sealed class GamepadPollsTests
{
    private readonly GamepadPolls _polls = new();
    private readonly List<string> _events = [];

    public GamepadPollsTests()
    {
        _polls.ButtonPressed += button => _events.Add($"+{button}");
        _polls.ButtonReleased += button => _events.Add($"-{button}");
    }

    [Fact]
    public void A_button_is_pressed_once_and_released_when_it_is_no_longer_held()
    {
        Poll(0, PadButtons.A);
        Poll(500, PadButtons.A);
        Poll(1000);

        Assert.Equal(["+A", "-A"], _events);
    }

    [Fact]
    public void The_D_pad_repeats_after_the_delay_at_the_interval()
    {
        for (var now = 0; now <= GamepadPolls.RepeatDelayMs + (2 * GamepadPolls.RepeatIntervalMs); now += 10)
        {
            Poll(now, PadButtons.DPadRight);
        }

        Assert.Equal(["+DPadRight", "+DPadRight", "+DPadRight", "+DPadRight"], _events);
    }

    [Fact]
    public void Held_just_short_of_the_delay_the_D_pad_does_not_repeat()
    {
        Poll(0, PadButtons.DPadDown);
        Poll(GamepadPolls.RepeatDelayMs - 1, PadButtons.DPadDown);

        Assert.Equal(["+DPadDown"], _events);
    }

    [Theory]
    [InlineData(0.9, 0, GamepadButton.DPadRight)]
    [InlineData(-0.9, 0, GamepadButton.DPadLeft)]
    [InlineData(0, 0.9, GamepadButton.DPadUp)]
    [InlineData(0, -0.9, GamepadButton.DPadDown)]
    public void The_left_stick_past_half_way_is_the_D_pad(double x, double y, GamepadButton expected)
    {
        Assert.Equal([expected], GamepadPolls.Down([Reading(leftStick: new StickPosition(x, y))]));
    }

    [Fact]
    public void A_stick_short_of_half_way_is_nothing()
    {
        Assert.Empty(GamepadPolls.Down([Reading(leftStick: new StickPosition(0.48, -0.48)), Reading(rightStick: new StickPosition(-0.48, 0.48))]));
    }

    [Fact]
    public void The_right_stick_is_its_own_four_directions_and_buttons_of_several_controllers_add_up()
    {
        var down = GamepadPolls.Down([Reading(PadButtons.A | PadButtons.RightTrigger, rightStick: new StickPosition(0.9, 0.9)), Reading(PadButtons.Menu, slot: 1)]);

        Assert.Equal(
            new HashSet<GamepadButton> { GamepadButton.A, GamepadButton.RightTrigger, GamepadButton.Menu, GamepadButton.RightStickUp, GamepadButton.RightStickRight },
            down);
    }

    private void Poll(long now, PadButtons buttons = PadButtons.None) => _polls.Update([Reading(buttons)], now);

    private static GamepadReading Reading(PadButtons buttons = PadButtons.None, StickPosition leftStick = default, StickPosition rightStick = default, int slot = 0) =>
        new(slot, buttons, leftStick, rightStick);
}
