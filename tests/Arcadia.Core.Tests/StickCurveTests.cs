using Arcadia.Input;

namespace Arcadia.Core.Tests;

public sealed class StickCurveTests
{
    [Fact]
    public void A_stick_resting_in_the_dead_zone_does_not_move_the_pointer()
    {
        Assert.Equal((0.0, 0.0), StickCurve.PointerVelocity(new StickPosition(0.2, -0.1), speed: 5, slow: false));
    }

    [Fact]
    public void Up_on_the_stick_moves_the_pointer_up_the_screen()
    {
        var (x, y) = StickCurve.PointerVelocity(new StickPosition(0, 1), speed: 5, slow: false);

        Assert.Equal(0, x);
        Assert.Equal(-1500, y, precision: 6);
    }

    [Fact]
    public void A_half_push_is_much_slower_than_a_full_one()
    {
        var (half, _) = StickCurve.PointerVelocity(new StickPosition(0.62, 0), speed: 5, slow: false);

        // Halfway between the dead zone and the edge, squared: a quarter of the top speed.
        Assert.Equal(1500 / 4.0, half, precision: 6);
    }

    [Theory]
    [InlineData(1, 500)]
    [InlineData(5, 1500)]
    [InlineData(9, 2500)]
    [InlineData(0, 500)]
    [InlineData(12, 2500)]
    public void The_speed_setting_sets_the_top_speed(int speed, double pixelsPerSecond)
    {
        Assert.Equal(pixelsPerSecond, StickCurve.TopSpeed(speed));
    }

    [Fact]
    public void The_slow_pointer_moves_at_a_third()
    {
        var (x, _) = StickCurve.PointerVelocity(new StickPosition(1, 0), speed: 5, slow: true);

        Assert.Equal(500, x, precision: 6);
    }

    [Fact]
    public void A_diagonal_push_is_as_fast_as_a_straight_one()
    {
        var (x, y) = StickCurve.PointerVelocity(new StickPosition(Math.Sqrt(0.5), Math.Sqrt(0.5)), speed: 5, slow: false);

        Assert.Equal(1500, Math.Sqrt((x * x) + (y * y)), precision: 6);
    }

    [Fact]
    public void The_wheel_turns_up_and_right_with_the_stick()
    {
        var (vertical, horizontal) = StickCurve.WheelVelocity(new StickPosition(1, 0));
        Assert.Equal(0, vertical);
        Assert.True(horizontal > 0);

        (vertical, horizontal) = StickCurve.WheelVelocity(new StickPosition(0, 1));
        Assert.True(vertical > 0);
        Assert.Equal(0, horizontal);
    }
}
