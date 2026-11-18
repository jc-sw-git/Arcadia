using Arcadia.App.Controls;
using Arcadia.Core.Artwork;
using Avalonia;

namespace Arcadia.App.Tests;

/// <summary>
/// #565: how a background fits its frame. At most 10 % of the image is cut, the rest of the frame shows the blurred copy;
/// the detail page shows a flat image at full width; "Bild ansehen" shows it whole. Never stretched.
/// </summary>
public sealed class BackgroundFitTests
{
    private static readonly Size Card = new(400, 180);
    private static readonly Size Screen = new(1920, 1080);
    private static readonly Size Flat = new(1920, 620);
    private static readonly Size SixteenByNine = new(2560, 1440);

    [Fact]
    [Trait("TestCase", "T565-1")]
    public void A_flat_background_on_the_preview_card_loses_ten_percent_at_the_sides_and_gets_a_narrow_margin_above_and_below()
    {
        var placement = ArtworkPlacement.Of(Flat, Card, ArtworkFit.AlmostWhole);

        AssertCut(Flat, placement, 0.10);
        Assert.Equal(Card.Width, placement.Target.Width, 3);
        var margin = (Card.Height - placement.Target.Height) / 2;
        Assert.InRange(margin, 15, 25);
        Assert.Equal(margin, placement.Target.Y, 3);
        Assert.True(placement.HasMargins(Card));
        AssertNotStretched(placement);
    }

    [Fact]
    [Trait("TestCase", "T565-2")]
    public void A_sixteen_by_nine_background_on_the_preview_card_loses_ten_percent_above_and_below_and_gets_narrow_side_margins()
    {
        var placement = ArtworkPlacement.Of(SixteenByNine, Card, ArtworkFit.AlmostWhole);

        AssertCut(SixteenByNine, placement, 0.10);
        Assert.Equal(Card.Height, placement.Target.Height, 3);
        Assert.InRange((Card.Width - placement.Target.Width) / 2, 15, 25);
        AssertNotStretched(placement);
    }

    [Fact]
    [Trait("TestCase", "T565-3")]
    public void On_the_detail_page_a_flat_background_runs_full_width_uncut_and_fades_at_its_lower_edge()
    {
        var placement = ArtworkPlacement.Of(Flat, Screen, ArtworkFit.DetailPage);

        Assert.Equal(new Rect(Flat), placement.Source);
        Assert.Equal(new Rect(0, 0, 1920, 620), placement.Target);
        Assert.Equal(FadingEdges.Bottom, placement.Fading);
        Assert.True(placement.HasMargins(Screen));
    }

    [Fact]
    [Trait("TestCase", "T565-4")]
    public void On_the_detail_page_a_sixteen_by_nine_background_fills_the_screen_uncut()
    {
        var placement = ArtworkPlacement.Of(SixteenByNine, Screen, ArtworkFit.DetailPage);

        Assert.Equal(new Rect(SixteenByNine), placement.Source);
        Assert.Equal(new Rect(Screen), placement.Target);
        Assert.Equal(FadingEdges.None, placement.Fading);
        Assert.False(placement.HasMargins(Screen));
    }

    [Theory]
    [Trait("TestCase", "T565-5")]
    [InlineData(2560, 1080)]   // 21 : 9
    [InlineData(1600, 1200)]   // 4 : 3
    [InlineData(1300, 620)]    // between 16 : 9 and flat
    [InlineData(600, 900)]     // a cover by mistake
    public void Any_other_format_loses_at_most_ten_percent_anywhere_and_is_never_stretched(int width, int height)
    {
        var image = new Size(width, height);
        foreach (var (box, fit) in new[] { (Card, ArtworkFit.AlmostWhole), (Screen, ArtworkFit.DetailPage), (new Size(280, 158), ArtworkFit.AlmostWhole) })
        {
            var placement = ArtworkPlacement.Of(image, box, fit);

            AssertCut(image, placement, atMost: 0.10);
            AssertNotStretched(placement);
            Assert.True(new Rect(box).Contains(placement.Target), $"{width} × {height} in {box}: ragt aus dem Rahmen.");
        }
    }

    [Theory]
    [Trait("TestCase", "T565-6")]
    [InlineData(1920, 620)]
    [InlineData(2560, 1440)]
    [InlineData(1600, 1200)]
    public void Bild_ansehen_shows_every_image_whole_and_centred_its_edges_fading_into_the_margins(int width, int height)
    {
        var image = new Size(width, height);

        var placement = ArtworkPlacement.Of(image, Screen, ArtworkFit.Whole);

        Assert.Equal(new Rect(image), placement.Source);
        Assert.Equal(Screen.Width / 2, placement.Target.Center.X, 3);
        Assert.Equal(Screen.Height / 2, placement.Target.Center.Y, 3);
        AssertNotStretched(placement);
        Assert.Equal(placement.HasMargins(Screen), placement.Fading != FadingEdges.None);
    }

    [Fact]
    public void A_cover_still_fills_its_tile_cropped_centred()
    {
        var placement = ArtworkPlacement.Of(new Size(1200, 1600), new Size(200, 300), ArtworkFit.Fill);

        Assert.Equal(new Rect(0, 0, 200, 300), placement.Target);
        Assert.Equal(1066.67, placement.Source.Width, 2);
        Assert.Equal(66.67, placement.Source.X, 2);
        Assert.Equal(FadingEdges.None, placement.Fading);
    }

    [Fact]
    public void A_background_is_decoded_only_as_large_as_it_is_shown()
    {
        // Whole on the screen needs the flat image 1920 wide, not the 3345 that covering the screen would.
        Assert.Equal(1920, ArtworkImage.DecodeWidth(new ImageSize(3840, 1240), 1920, 1080, ArtworkFit.Whole));
        Assert.Equal(3345, ArtworkImage.DecodeWidth(new ImageSize(3840, 1240), 1920, 1080, ArtworkFit.Fill));
        Assert.Equal(445, ArtworkImage.DecodeWidth(new ImageSize(1920, 620), 400, 180, ArtworkFit.AlmostWhole));
    }

    [Fact]
    public void The_blurred_copy_spreads_a_bright_spot_and_keeps_an_even_area_even()
    {
        const int width = 9;
        const int height = 9;
        var pixels = new byte[width * height * 4];
        Array.Fill(pixels, (byte)40);
        var centre = ((4 * width) + 4) * 4;
        pixels[centre] = 255;

        BlurredCopy.Blur(pixels, width, height);

        Assert.InRange(pixels[centre], 41, 120);
        Assert.True(pixels[(((4 * width) + 6) * 4)] > 40, "Der helle Punkt breitet sich nicht aus.");
        Assert.Equal(40, pixels[1]);
        Assert.Equal(40, pixels[^1]);
    }

    /// <summary>The share of the image left out, along the axis that overflows.</summary>
    private static void AssertCut(Size image, ArtworkPlacement placement, double atMost)
    {
        var shown = placement.Source.Width * placement.Source.Height / (image.Width * image.Height);
        Assert.True(1 - shown <= atMost + 0.001, $"{image}: {1 - shown:P1} abgeschnitten.");
    }

    private static void AssertNotStretched(ArtworkPlacement placement) =>
        Assert.Equal(placement.Target.Width / placement.Source.Width, placement.Target.Height / placement.Source.Height, 6);
}
