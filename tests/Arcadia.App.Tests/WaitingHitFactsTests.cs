using Arcadia.App.ViewModels;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Tests;

/// <summary>The info box of a metadata hit (#539): what IGDB has, checked, and nothing it hasn't.</summary>
public sealed class WaitingHitFactsTests
{
    [Fact]
    [Trait("TestCase", "T539-1")]
    public void The_box_shows_what_igdb_has_checked_in_the_detail_pages_order()
    {
        var facts = new WaitingHitFacts(new GameMetadata
        {
            ReleaseDate = new DateOnly(2015, 3, 4),
            Genres = [Genre.RolePlaying, (Genre)999],
            Developers = ["Nordkai Studio"],
            AgeRatings = [new AgeRating(AgeRatingBoard.Usk, "16"), new AgeRating(AgeRatingBoard.Pegi, "17")],
            Description = "<p>Ein <b>gutes</b> Spiel.</p>",
        });

        Assert.Equal(
            [
                (AppTexts.DetailReleasedCaption, "04.03.2015"),
                (AppTexts.DetailGenresCaption, "Rollenspiel"),
                (AppTexts.DetailDevelopersCaption, "Nordkai Studio"),
                (AppTexts.DetailAgeRatingCaption, "USK 16"),
            ],
            facts.Facts.Select(f => (f.Caption, f.Value)));
        Assert.Equal("Ein gutes Spiel.", facts.Description);
        Assert.Equal("Rollenspiel · Nordkai Studio", facts.Line);
        Assert.False(facts.IsEmpty);
    }

    [Fact]
    [Trait("TestCase", "T539-1")]
    public void A_long_description_is_cut_at_a_word_and_one_without_spaces_at_the_limit()
    {
        var words = new WaitingHitFacts(new GameMetadata { Description = string.Join(' ', Enumerable.Repeat("Wort", 300)) });
        var block = new WaitingHitFacts(new GameMetadata { Description = new string('x', 2000) });

        Assert.EndsWith("Wort …", words.Description, StringComparison.Ordinal);
        Assert.True(words.Description!.Length <= WaitingHitFacts.MaxDescription + 2);
        Assert.Equal(WaitingHitFacts.MaxDescription + 2, block.Description!.Length);
    }

    [Fact]
    [Trait("TestCase", "T539-2")]
    public void Without_any_fact_the_box_says_so()
    {
        var facts = new WaitingHitFacts(GameMetadata.Empty);

        Assert.True(facts.IsEmpty);
        Assert.Equal(string.Empty, facts.Line);
    }
}
