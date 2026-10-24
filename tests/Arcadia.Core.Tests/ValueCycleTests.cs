using Arcadia.Core.Navigation;

namespace Arcadia.Core.Tests;

public class ValueCycleTests
{
    [Theory]
    [InlineData(1, -1, 9)]
    [InlineData(9, 1, 1)]
    [InlineData(5, 1, 6)]
    [InlineData(5, -1, 4)]
    [InlineData(2, -3, 8)]
    public void A_step_past_one_end_comes_in_at_the_other(int value, int delta, int expected)
    {
        Assert.Equal(expected, ValueCycle.Step(value, delta, 1, 9));
    }

    [Fact]
    public void A_switch_flips_in_both_directions()
    {
        Assert.Equal(1, ValueCycle.Step(0, -1, 0, 1));
        Assert.Equal(1, ValueCycle.Step(0, 1, 0, 1));
        Assert.Equal(0, ValueCycle.Step(1, 1, 0, 1));
    }

    [Fact]
    public void A_single_value_and_an_empty_range_stay_put()
    {
        Assert.Equal(3, ValueCycle.Step(3, 1, 3, 3));
        Assert.Equal(4, ValueCycle.Step(4, -1, 5, 3));
    }
}
