using Arcadia.Core.Artwork;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>#487, #562, #566: the fitting-format rule (15 % for covers and backgrounds), T487-8 to T487-10, T562-1, T562-2, T566-2.</summary>
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
    public void A_background_fits_in_16_to_9_first_and_flat_second()
    {
        Assert.Equal(0, ArtworkFormat.RankOf(new ImageSize(2560, 1440), ArtworkKind.Hero));
        Assert.Equal(0, ArtworkFormat.CutAway(new ImageSize(1920, 1080), ArtworkKind.Hero), 6);
        Assert.Equal(1, ArtworkFormat.RankOf(new ImageSize(1920, 620), ArtworkKind.Hero));
        Assert.Equal(2, ArtworkFormat.RankOf(new ImageSize(2400, 1000), ArtworkKind.Hero));
    }

    [Fact]
    public void Epics_three_by_four_cover_fits_and_one_wider_than_fifteen_percent_doesnt()
    {
        Assert.True(ArtworkFormat.Fits(new ImageSize(1200, 1600), ArtworkKind.Cover));
        Assert.False(ArtworkFormat.Fits(new ImageSize(750, 900), ArtworkKind.Cover));
    }

    [Fact]
    public void A_square_cover_and_a_square_hero_cut_too_much()
    {
        Assert.Equal(1.0 / 3, ArtworkFormat.CutAway(new ImageSize(900, 900), ArtworkKind.Cover), 3);
        Assert.False(ArtworkFormat.Fits(new ImageSize(900, 900), ArtworkKind.Cover));
        Assert.Equal(0.44, ArtworkFormat.CutAway(new ImageSize(1000, 1000), ArtworkKind.Hero), 2);
        Assert.False(ArtworkFormat.Fits(new ImageSize(1000, 1000), ArtworkKind.Hero));
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
