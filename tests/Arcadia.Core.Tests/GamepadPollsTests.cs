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

    /// <summary>The feel of holding the D-pad: the first repeat after 400 ms, then one every 90 ms (written out, not taken from the code).</summary>
    [Fact]
    public void The_D_pad_repeats_after_400_ms_and_then_every_90_ms()
    {
        Poll(0, PadButtons.DPadRight);
        Poll(399, PadButtons.DPadRight);
        Assert.Equal(["+DPadRight"], _events);

        Poll(400, PadButtons.DPadRight);
        Assert.Equal(2, _events.Count);

        Poll(489, PadButtons.DPadRight);
        Assert.Equal(2, _events.Count);

        Poll(490, PadButtons.DPadRight);
        Assert.Equal(3, _events.Count);

        Poll(579, PadButtons.DPadRight);
        Poll(580, PadButtons.DPadRight);
        Assert.Equal(["+DPadRight", "+DPadRight", "+DPadRight", "+DPadRight"], _events);
    }

    [Fact]
    public void The_right_stick_repeats_too_but_A_does_not()
    {
        var held = Reading(PadButtons.A, rightStick: new StickPosition(0, -0.9));
        _polls.Update([held], 0);
        _polls.Update([held], 400);

        Assert.Equal(["+A", "+RightStickDown", "+RightStickDown"], _events.Order());
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
