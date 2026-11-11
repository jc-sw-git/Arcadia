using Arcadia.Input;

namespace Arcadia.Core.Tests;

public sealed class GamepadPollsTests
{
    private readonly GamepadPolls _polls = new();
    private readonly List<string> _events = [];

    public GamepadPollsTests()
    {
        _polls.ButtonPressed += button => _events.Add($"+{button}");
        _polls.ButtonRepeated += button => _events.Add($"*{button}");
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

    /// <summary>#367: the A that started a game, still held when the controllers come back, must not act again.</summary>
    [Fact]
    public void After_a_let_go_a_button_still_held_is_no_new_press_until_it_was_up_once()
    {
        Poll(0, PadButtons.A | PadButtons.X);
        _events.Clear();
        _polls.LetGo();
        Assert.Equal(["-A", "-X"], _events.Order());

        _events.Clear();
        Poll(1000, PadButtons.A);
        Poll(1100, PadButtons.A | PadButtons.B);
        Poll(1200, PadButtons.B);
        Poll(1300, PadButtons.A | PadButtons.B);

        Assert.Equal(["+B", "+A"], _events);
    }

    [Fact]
    public void A_held_D_pad_doesnt_repeat_after_a_let_go()
    {
        Poll(0, PadButtons.DPadRight);
        _polls.LetGo();
        Poll(1000, PadButtons.DPadRight);
        Poll(2000, PadButtons.DPadRight);

        Assert.Equal(["+DPadRight", "-DPadRight"], _events);
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
        Assert.Equal(["+DPadRight", "*DPadRight", "*DPadRight", "*DPadRight"], _events);
    }

    [Fact]
    public void The_right_stick_repeats_too_but_A_does_not()
    {
        var held = Reading(PadButtons.A, rightStick: new StickPosition(0, -0.9));
        _polls.Update([held], 0);
        _polls.Update([held], 400);

        Assert.Equal(["*RightStickDown", "+A", "+RightStickDown"], _events.Order());
    }

    /// <summary>#476: held LT/RT and LB/RB turn on, first after 400 ms, then every 150 ms (written out, not taken from the code).</summary>
    [Theory]
    [Trait("TestCase", "T476-8")]
    [InlineData(PadButtons.LeftTrigger, GamepadButton.LeftTrigger)]
    [InlineData(PadButtons.RightTrigger, GamepadButton.RightTrigger)]
    [InlineData(PadButtons.LeftShoulder, GamepadButton.LeftShoulder)]
    [InlineData(PadButtons.RightShoulder, GamepadButton.RightShoulder)]
    public void LT_RT_LB_and_RB_repeat_after_400_ms_and_then_every_150_ms(PadButtons held, GamepadButton button)
    {
        Poll(0, held);
        Poll(399, held);
        Assert.Equal([$"+{button}"], _events);

        Poll(400, held);
        Poll(549, held);
        Assert.Equal([$"+{button}", $"*{button}"], _events);

        Poll(550, held);
        Poll(699, held);
        Poll(700, held);
        Poll(800);
        Assert.Equal([$"+{button}", $"*{button}", $"*{button}", $"*{button}", $"-{button}"], _events);
    }

    [Theory]
    [Trait("TestCase", "T476-8")]
    [InlineData(PadButtons.A)]
    [InlineData(PadButtons.B)]
    [InlineData(PadButtons.X)]
    [InlineData(PadButtons.Y)]
    [InlineData(PadButtons.View)]
    [InlineData(PadButtons.Menu)]
    [InlineData(PadButtons.LeftStickClick)]
    [InlineData(PadButtons.RightStickClick)]
    public void The_other_buttons_dont_repeat(PadButtons held)
    {
        Poll(0, held);
        Poll(400, held);
        Poll(2000, held);

        Assert.Single(_events);
    }

    /// <summary>#476 with #367: Pan hiding or showing a controller while RT is held releases it, and no repeat follows.</summary>
    [Fact]
    [Trait("TestCase", "T476-12")]
    public void A_held_RT_doesnt_repeat_after_a_let_go()
    {
        Poll(0, PadButtons.RightTrigger);
        _polls.LetGo();
        Poll(1000, PadButtons.RightTrigger);
        Poll(2000, PadButtons.RightTrigger);

        Assert.Equal(["+RightTrigger", "-RightTrigger"], _events);
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

    /// <summary>#476: a half-pressed trigger wobbling across the press point gives no fresh press, which would wrap round.</summary>
    [Fact]
    [Trait("TestCase", "T476-14")]
    public void A_held_trigger_lets_go_only_below_a_quarter()
    {
        var trigger = (short value, long now) => _polls.Update([SdlPadMapping.ToReading(0, new SdlPadState(0, 0, 0, 0, 0, 0, value))], now);

        trigger(17000, 0);
        trigger(15000, 10);
        trigger(16500, 20);
        trigger(8500, 30);
        Assert.Equal(["+RightTrigger"], _events);

        trigger(8000, 40);
        trigger(15000, 50);
        Assert.Equal(["+RightTrigger", "-RightTrigger"], _events);

        trigger(16384, 60);
        Assert.Equal(["+RightTrigger", "-RightTrigger", "+RightTrigger"], _events);
    }

    [Fact]
    [Trait("TestCase", "T476-14")]
    public void A_held_stick_lets_go_only_below_three_tenths()
    {
        var tilt = (double x, double y, long now) => _polls.Update([Reading(leftStick: new StickPosition(x, y), rightStick: new StickPosition(-x, -y))], now);

        tilt(0.6, 0, 0);
        tilt(0.4, 0, 10);
        tilt(0.31, 0, 20);
        Assert.Equal(["+DPadRight", "+RightStickLeft"], _events.Order(StringComparer.Ordinal));

        tilt(0.29, 0, 30);
        tilt(0.45, 0, 40);
        Assert.Equal(["+DPadRight", "+RightStickLeft", "-DPadRight", "-RightStickLeft"], _events.Order(StringComparer.Ordinal));

        // The other way is a press of its own, at half way.
        _events.Clear();
        tilt(-0.45, 0, 50);
        Assert.Empty(_events);
        tilt(-0.6, 0, 60);
        Assert.Equal(["+DPadLeft", "+RightStickRight"], _events.Order(StringComparer.Ordinal));
    }

    private void Poll(long now, PadButtons buttons = PadButtons.None) => _polls.Update([Reading(buttons)], now);

    private static GamepadReading Reading(PadButtons buttons = PadButtons.None, StickPosition leftStick = default, StickPosition rightStick = default, int slot = 0) =>
        new(slot, buttons, leftStick, rightStick);
}
