using Arcadia.App.ViewModels;
using Arcadia.Core.Metadata;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Tests;

/// <summary>#541: an age rating's border takes its board's colour, the highest rating red.</summary>
public sealed class AgeRatingToneTests
{
    [Theory]
    [Trait("TestCase", "T541-3")]
    [InlineData(AgeRatingBoard.Usk, "0", AgeRatingTone.White)]
    [InlineData(AgeRatingBoard.Usk, "6", AgeRatingTone.Yellow)]
    [InlineData(AgeRatingBoard.Usk, "12", AgeRatingTone.Green)]
    [InlineData(AgeRatingBoard.Usk, "16", AgeRatingTone.Blue)]
    [InlineData(AgeRatingBoard.Usk, "18", AgeRatingTone.Red)]
    [InlineData(AgeRatingBoard.Pegi, "3", AgeRatingTone.Green)]
    [InlineData(AgeRatingBoard.Pegi, "7", AgeRatingTone.Green)]
    [InlineData(AgeRatingBoard.Pegi, "12", AgeRatingTone.Orange)]
    [InlineData(AgeRatingBoard.Pegi, "16", AgeRatingTone.Orange)]
    [InlineData(AgeRatingBoard.Pegi, "18", AgeRatingTone.Red)]
    [InlineData(AgeRatingBoard.Esrb, "E", AgeRatingTone.Green)]
    [InlineData(AgeRatingBoard.Esrb, "E10+", AgeRatingTone.Green)]
    [InlineData(AgeRatingBoard.Esrb, "T", AgeRatingTone.Orange)]
    [InlineData(AgeRatingBoard.Esrb, "M", AgeRatingTone.Red)]
    [InlineData(AgeRatingBoard.Esrb, "AO", AgeRatingTone.Red)]
    public void Each_rating_takes_its_boards_colour(AgeRatingBoard board, string rating, AgeRatingTone tone) =>
        Assert.Equal(tone, AgeRatingTones.Of(new AgeRating(board, rating)));

    [Fact]
    [Trait("TestCase", "T541-3")]
    public void The_info_line_and_every_rating_in_infos_carry_their_colour()
    {
        var info = new DetailMetadata(new MergedMetadata
        {
            AgeRatings =
            [
                new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Usk, "18"), "Steam"),
                new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Pegi, "12"), "IGDB"),
            ],
        });

        Assert.Equal(AgeRatingTone.Red, info.BadgeTone);
        Assert.Equal([AgeRatingTone.Red, AgeRatingTone.Orange], info.Facts.Where(fact => fact.IsBadge).Select(fact => fact.Tone));
    }
}
