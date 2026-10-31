using Arcadia.Input;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class ControllerActivityTests
{
    private static readonly GamepadReading Resting = new(1, PadButtons.None, default, default);

    /// <summary>
    /// #212: Steam's desktop layout moves the mouse with the Steam Controller's trackpads, so a touch is use and the
    /// desktop filter drops that mouse.
    /// </summary>
    [Fact]
    public void A_touched_or_clicked_trackpad_is_use_of_the_controller()
    {
        Assert.False(ControllerActivity.IsInUse(Resting));
        Assert.True(ControllerActivity.IsInUse(Resting with { LeftTouchpad = new TouchpadReading(true, 0, 0, false) }));
        Assert.True(ControllerActivity.IsInUse(Resting with { RightTouchpad = new TouchpadReading(true, 0.3, -0.2, false) }));
        Assert.True(ControllerActivity.IsInUse(Resting with { RightTouchpad = new TouchpadReading(false, 0, 0, true) }));
    }

    /// <summary>Gyro and accelerometer always move a little; a controller lying on the table isn't in use.</summary>
    [Fact]
    public void Motion_alone_is_not_use()
    {
        var lying = Resting with { Motion = new MotionReading(true, new SensorAxes(0.02f, 0, 0), new SensorAxes(0, 9.81f, 0)) };

        Assert.False(ControllerActivity.IsInUse(lying));
    }

    /// <summary>#212: Steam's trackpad pointer glides on for up to about a second after the finger left the pad.</summary>
    [Fact]
    public void A_trackpad_let_go_stays_in_use_for_a_second_a_button_only_for_the_short_grace()
    {
        var time = new FakeTimeProvider();
        var activity = new ControllerActivity(time);

        activity.Note([Resting with { RightTouchpad = new TouchpadReading(true, 0, 0, false) }]);
        activity.Note([Resting]);
        time.Advance(TimeSpan.FromMilliseconds(900));
        Assert.True(activity.IsInUse());
        time.Advance(ControllerActivity.TrackpadGrace - TimeSpan.FromMilliseconds(899));
        Assert.False(activity.IsInUse());

        activity.Note([Resting with { Buttons = PadButtons.A }]);
        activity.Note([Resting]);
        time.Advance(ControllerActivity.Grace + TimeSpan.FromMilliseconds(1));
        Assert.False(activity.IsInUse());
    }
}
