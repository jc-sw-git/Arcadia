using Arcadia.Input;
using B = Arcadia.Input.SdlPadState.Button;

namespace Arcadia.Core.Tests;

public sealed class SdlPadMappingTests
{
    /// <summary>By position (#72): written out once, so a change of SDL's numbering or of the table shows.</summary>
    [Theory]
    [InlineData(B.South, PadButtons.A)]
    [InlineData(B.East, PadButtons.B)]
    [InlineData(B.West, PadButtons.X)]
    [InlineData(B.North, PadButtons.Y)]
    [InlineData(B.Back, PadButtons.View)]
    [InlineData(B.Start, PadButtons.Menu)]
    [InlineData(B.LeftStick, PadButtons.LeftStickClick)]
    [InlineData(B.RightStick, PadButtons.RightStickClick)]
    [InlineData(B.LeftShoulder, PadButtons.LeftShoulder)]
    [InlineData(B.RightShoulder, PadButtons.RightShoulder)]
    [InlineData(B.DPadUp, PadButtons.DPadUp)]
    [InlineData(B.DPadDown, PadButtons.DPadDown)]
    [InlineData(B.DPadLeft, PadButtons.DPadLeft)]
    [InlineData(B.DPadRight, PadButtons.DPadRight)]
    public void Each_button_maps_by_its_position(B button, PadButtons expected) =>
        Assert.Equal(expected, Map(Held(button)).Buttons);

    [Fact]
    public void SDL_s_button_numbers_are_those_of_SDL_GamepadButton()
    {
        Assert.Equal(0, (int)B.South);
        Assert.Equal(5, (int)B.Guide);
        Assert.Equal(11, (int)B.DPadUp);
        Assert.Equal(15, (int)B.Misc1);
        Assert.Equal(20, (int)B.Touchpad);
    }

    [Theory]
    [InlineData(B.Guide)]
    [InlineData(B.Misc1)]
    [InlineData(B.Touchpad)]
    [InlineData(B.RightPaddle1)]
    [InlineData(B.LeftPaddle1)]
    [InlineData(B.RightPaddle2)]
    [InlineData(B.LeftPaddle2)]
    public void Guide_capture_touchpad_click_and_paddles_do_nothing(B button) =>
        Assert.Equal(PadButtons.None, Map(Held(button)).Buttons);

    [Fact]
    public void Triggers_count_as_held_from_half_way()
    {
        Assert.Equal(PadButtons.None, Map(new SdlPadState(0, 0, 0, 0, 0, 16383, 16383)).Buttons);
        Assert.Equal(PadButtons.LeftTrigger | PadButtons.RightTrigger, Map(new SdlPadState(0, 0, 0, 0, 0, 16384, 32767)).Buttons);
    }

    [Fact]
    public void Sticks_count_up_and_right_as_positive_and_reach_exactly_one()
    {
        var reading = Map(new SdlPadState(0, short.MaxValue, short.MinValue, short.MinValue, short.MaxValue, 0, 0));

        Assert.Equal(new StickPosition(1, 1), reading.LeftStick);
        Assert.Equal(new StickPosition(-1, -1), reading.RightStick);
    }

    [Fact]
    public void The_reading_carries_the_pad_s_id() => Assert.Equal(7, SdlPadMapping.ToReading(7, default).PadId);

    private static SdlPadState Held(B button) => new(SdlPadState.Bit(button), 0, 0, 0, 0, 0, 0);

    private static GamepadReading Map(SdlPadState state) => SdlPadMapping.ToReading(1, state);
}
