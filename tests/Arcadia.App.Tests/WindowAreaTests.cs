using Arcadia.App.Controls;
using Avalonia;

namespace Arcadia.App.Tests;

/// <summary>
/// #612: the detail page's background fills the whole window, also the bars a window of another shape leaves beside or
/// above and below the scaled 1920 × 1080 layout.
/// </summary>
public sealed class WindowAreaTests
{
    [Fact]
    [Trait("TestCase", "T612-3")]
    public void A_window_wider_than_the_layout_reaches_past_its_left_and_right_edges_by_the_bars()
    {
        // 2547 × 1360: the layout is scaled by 1360 / 1080 and centred, bars of 63.5 px at both sides.
        var scale = 1360.0 / 1080;
        var bar = (2547 - (1920 * scale)) / 2;
        var layoutToWindow = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(bar, 0);

        var area = WindowArea.Of(layoutToWindow, new Size(2547, 1360))!.Value;

        Assert.Equal(-bar / scale, area.X, 3);
        Assert.Equal(0, area.Y, 3);
        Assert.Equal(2547 / scale, area.Width, 3);
        Assert.Equal(1080, area.Height, 3);
    }

    [Fact]
    [Trait("TestCase", "T612-3")]
    public void A_window_taller_than_the_layout_reaches_past_its_top_and_bottom_edges_by_the_bars()
    {
        var scale = 1200.0 / 1920;
        var bar = (1000 - (1080 * scale)) / 2;
        var layoutToWindow = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(0, bar);

        var area = WindowArea.Of(layoutToWindow, new Size(1200, 1000))!.Value;

        Assert.Equal(0, area.X, 3);
        Assert.Equal(-bar / scale, area.Y, 3);
        Assert.Equal(1920, area.Width, 3);
        Assert.Equal(1000 / scale, area.Height, 3);
    }

    [Fact]
    [Trait("TestCase", "T612-3")]
    public void A_sixteen_by_nine_window_is_exactly_the_layout()
    {
        var area = WindowArea.Of(Matrix.CreateScale(2, 2), new Size(3840, 2160))!.Value;

        Assert.Equal(new Rect(0, 0, 1920, 1080), area);
    }

    [Fact]
    public void A_layout_scaled_to_nothing_has_no_area()
    {
        Assert.Null(WindowArea.Of(Matrix.CreateScale(0, 0), new Size(800, 600)));
    }
}
