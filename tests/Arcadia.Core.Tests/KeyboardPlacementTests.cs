using Arcadia.Core.Search;

namespace Arcadia.Core.Tests;

public class KeyboardPlacementTests
{
    private static readonly ScreenRect WorkArea = new(0, 0, 1920, 1040);
    private static readonly ScreenRect AtTop = KeyboardPlacement.At(WorkArea, 1646, 384, 24, top: true);
    private static readonly ScreenRect AtBottom = KeyboardPlacement.At(WorkArea, 1646, 384, 24, top: false);

    [Theory]
    [InlineData(1920, 1080, 1.0)]
    [InlineData(3840, 2160, 2.0)]
    [InlineData(2560, 1440, 1.333)]
    [InlineData(3440, 1440, 1.333)]
    [InlineData(1280, 1024, 0.667)]
    public void The_keyboard_grows_with_the_monitor_like_the_big_picture_layout(int width, int height, double scale)
    {
        Assert.Equal(scale, KeyboardPlacement.ScaleFor(new ScreenRect(0, 0, width, height)), 3);
    }

    [Fact]
    public void It_sits_centred_at_the_chosen_edge_of_the_work_area()
    {
        var secondMonitor = new ScreenRect(-1920, 200, 1920, 1040);

        Assert.Equal(new ScreenRect(137, 24, 1646, 384), AtTop);
        Assert.Equal(new ScreenRect(137, 632, 1646, 384), AtBottom);
        Assert.Equal(new ScreenRect(-1783, 832, 1646, 384), KeyboardPlacement.At(secondMonitor, 1646, 384, 24, top: false));
    }

    [Fact]
    public void It_opens_at_the_edge_chosen_last_without_a_caret_or_with_one_it_does_not_cover()
    {
        Assert.True(KeyboardPlacement.OpensAtTop(true, AtTop, AtBottom, caret: null));
        Assert.False(KeyboardPlacement.OpensAtTop(false, AtTop, AtBottom, caret: null));
        Assert.False(KeyboardPlacement.OpensAtTop(false, AtTop, AtBottom, new ScreenRect(500, 500, 2, 20)));
    }

    [Fact]
    public void A_caret_under_the_keyboard_sends_it_to_the_other_edge()
    {
        Assert.True(KeyboardPlacement.OpensAtTop(false, AtTop, AtBottom, new ScreenRect(500, 900, 2, 20)));
        Assert.False(KeyboardPlacement.OpensAtTop(true, AtTop, AtBottom, new ScreenRect(500, 100, 2, 20)));
    }

    [Fact]
    public void A_caret_covered_at_both_edges_leaves_the_last_choice()
    {
        var small = new ScreenRect(0, 0, 1024, 600);
        var top = KeyboardPlacement.At(small, 900, 384, 10, top: true);
        var bottom = KeyboardPlacement.At(small, 900, 384, 10, top: false);

        Assert.False(KeyboardPlacement.OpensAtTop(false, top, bottom, new ScreenRect(300, 300, 2, 20)));
    }
}
