using Arcadia.Core.Artwork;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>#487: the fitting-format rule with the tolerance "Normal" (15 %), T487-8 to T487-10.</summary>
public sealed class ArtworkFormatTests
{
    [Fact]
    public void Steams_own_formats_fit()
    {
        Assert.True(ArtworkFormat.Fits(new ImageSize(600, 900), ArtworkKind.Cover));
        Assert.True(ArtworkFormat.Fits(new ImageSize(1920, 620), ArtworkKind.Hero));
        Assert.True(ArtworkFormat.Fits(new ImageSize(3840, 1240), ArtworkKind.Hero));
    }

    [Fact]
    public void A_square_cover_and_a_2_to_1_hero_cut_too_much()
    {
        Assert.Equal(1.0 / 3, ArtworkFormat.CutAway(new ImageSize(900, 900), ArtworkKind.Cover), 3);
        Assert.False(ArtworkFormat.Fits(new ImageSize(900, 900), ArtworkKind.Cover));
        Assert.Equal(0.35, ArtworkFormat.CutAway(new ImageSize(2000, 1000), ArtworkKind.Hero), 2);
        Assert.False(ArtworkFormat.Fits(new ImageSize(2000, 1000), ArtworkKind.Hero));
    }

    [Fact]
    public void Exactly_15_percent_fits_16_percent_does_not()
    {
        // Narrower than 2 : 3: the crop cuts away 15 % and 16 % of the height.
        Assert.True(ArtworkFormat.Fits(new ImageSize(850, 1500), ArtworkKind.Cover));
        Assert.False(ArtworkFormat.Fits(new ImageSize(840, 1500), ArtworkKind.Cover));
        Assert.Equal(0.15, ArtworkFormat.CutAway(new ImageSize(850, 1500), ArtworkKind.Cover), 6);
        Assert.Equal(0.16, ArtworkFormat.CutAway(new ImageSize(840, 1500), ArtworkKind.Cover), 6);
    }
}
