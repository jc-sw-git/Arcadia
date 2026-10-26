using Arcadia.Input;

namespace Arcadia.Core.Tests;

public class XboxPadStateTests
{
    [Fact]
    public void SDL_values_reach_the_Xbox_pad_as_XInput_counts_them()
    {
        var reading = SdlPadMapping.ToReading(1, new SdlPadState(
            SdlPadState.Bit(SdlPadState.Button.South) | SdlPadState.Bit(SdlPadState.Button.Back),
            LeftX: short.MinValue, LeftY: short.MinValue, RightX: 16384, RightY: short.MaxValue,
            LeftTrigger: 32767, RightTrigger: 0));

        var state = XboxPadState.From(reading, reading.Buttons);

        // A and Back; the trigger-as-button bit of the left trigger is not an XInput button.
        Assert.Equal(new XboxPadState(0x1020, 255, 0, -32767, 32767, 16384, -32767), state);
    }

    [Fact]
    public void A_trigger_pulled_half_way_is_half_way_on_the_pad_too()
    {
        var reading = SdlPadMapping.ToReading(1, new SdlPadState(0, 0, 0, 0, 0, LeftTrigger: 16384, RightTrigger: 100));

        Assert.Equal((128, 1), (XboxPadState.From(reading, reading.Buttons).LeftTrigger, XboxPadState.From(reading, reading.Buttons).RightTrigger));
    }
}
