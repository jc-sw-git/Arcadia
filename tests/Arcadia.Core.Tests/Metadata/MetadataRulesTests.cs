using Arcadia.Core.Metadata;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests.Metadata;

/// <summary>#493: genres, the language rule, age ratings, the merge per field and plain text (T493-5 to T493-9, T493-36).</summary>
public sealed class MetadataRulesTests
{
    private const string German = "Du baust eine Stadt und die Bürger sind froh, wenn der Winter nicht zu lang ist.";
    private const string English = "You build a city and the people are happy with the winter that is not too long.";
    private const string BrokenGerman = "Du baust eine Stadt in K?ln und die B?rger sind froh, wenn der Winter nicht lang ist.";

    private static ProviderMetadata Provider(string source, GameMetadata data) => new(source, MetadataCheck.Checked(data));

    [Fact]
    [Trait("TestCase", "T493-6")]
    public void Genres_of_appinfo_and_the_store_name_the_same_entry_by_id_each_once_and_unmapped_ones_go()
    {
        var store = new Arcadia.Launchers.Steam.SteamMetadata(() => null).Store!;
        var game = new Game { Id = "4000001", LauncherId = "steam", Title = "X", Launch = new LaunchTarget.ProtocolUri("steam://rungameid/4000001") };
        var parsed = store.Parse(game, """
            {"4000001":{"success":true,"data":{"steam_appid":4000001,"genres":[{"id":"3","description":"Rollenspiel"},{"id":"3","description":"RPG"},{"id":"37","description":"Kostenlos spielbar"}]}}}
            """u8)!;

        Assert.Equal([Genre.RolePlaying], parsed.Genres);
    }

    [Fact]
    [Trait("TestCase", "T493-6")]
    public void A_game_with_only_unmapped_genres_shows_none()
    {
        var store = new Arcadia.Launchers.Steam.SteamMetadata(() => null).Store!;
        var game = new Game { Id = "4000001", LauncherId = "steam", Title = "X", Launch = new LaunchTarget.ProtocolUri("steam://rungameid/4000001") };
        var parsed = store.Parse(game, """{"4000001":{"success":true,"data":{"steam_appid":4000001,"genres":[{"id":"70","description":"Early Access"}]}}}"""u8)!;

        Assert.Null(MergedMetadata.Of([Provider("Steam", parsed)]).Genres);
    }

    [Theory]
    [Trait("TestCase", "T493-7")]
    [InlineData(German, null, German)]
    [InlineData(null, English, English)]
    [InlineData(BrokenGerman, English, English)]
    [InlineData(English, German, German)]
    [InlineData(German, "Ein anderes deutsches Spiel, das nicht zu lang ist und dir gefällt.", German)]
    [InlineData("   ", English, English)]
    [InlineData(BrokenGerman, null, BrokenGerman)]
    public void A_description_that_is_not_good_german_counts_only_without_a_german_one(string? first, string? second, string expected)
    {
        var merged = MergedMetadata.Of([
            Provider("Steam", new GameMetadata { Description = first }),
            Provider("Other", new GameMetadata { Description = second }),
        ]);

        Assert.Equal(expected, merged.Description?.Value);
    }

    [Fact]
    [Trait("TestCase", "T493-7")]
    public void Empty_descriptions_everywhere_give_none()
    {
        Assert.Null(MergedMetadata.Of([Provider("Steam", new GameMetadata { Description = " \n " })]).Description);
    }

    [Fact]
    [Trait("TestCase", "T493-8")]
    public void A_real_usk_wins_over_steams_own_rating_for_germany()
    {
        var merged = MergedMetadata.Of([Provider("Steam", new GameMetadata
        {
            AgeRatings = [new AgeRating(AgeRatingBoard.Usk, "18", ByStore: true), new AgeRating(AgeRatingBoard.Usk, "16")],
        })]);

        Assert.Equal([new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Usk, "16"), "Steam")], merged.AgeRatings);
    }

    [Fact]
    [Trait("TestCase", "T493-8")]
    public void Without_a_real_usk_steams_own_rating_counts_as_usk_and_pegi_and_esrb_come_as_delivered()
    {
        var merged = MergedMetadata.Of([Provider("Steam", new GameMetadata
        {
            AgeRatings = [new AgeRating(AgeRatingBoard.Esrb, "m"), new AgeRating(AgeRatingBoard.Pegi, "16"), new AgeRating(AgeRatingBoard.Usk, "12", ByStore: true)],
        })]);

        Assert.Equal(
            [
                new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Usk, "12", ByStore: true), "Steam"),
                new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Pegi, "16"), "Steam"),
                new Sourced<AgeRating>(new AgeRating(AgeRatingBoard.Esrb, "M"), "Steam"),
            ],
            merged.AgeRatings);
    }

    [Theory]
    [Trait("TestCase", "T493-8")]
    [InlineData(AgeRatingBoard.Usk, "17")]
    [InlineData(AgeRatingBoard.Usk, "ab 16")]
    [InlineData(AgeRatingBoard.Pegi, "6")]
    [InlineData(AgeRatingBoard.Esrb, "rp")]
    [InlineData(AgeRatingBoard.Esrb, "<b>M</b>")]
    [InlineData((AgeRatingBoard)7, "16")]
    public void Unknown_rating_values_are_dropped(AgeRatingBoard board, string value)
    {
        Assert.Null(MetadataCheck.CheckedRating(new AgeRating(board, value)));
        Assert.Empty(MergedMetadata.Of([Provider("Steam", new GameMetadata { AgeRatings = [new AgeRating(board, value)] })]).AgeRatings);
    }

    [Fact]
    [Trait("TestCase", "T493-8")]
    public void No_rating_at_all_gives_none()
    {
        Assert.Empty(MergedMetadata.Of([Provider("Steam", new GameMetadata { Developers = ["Made-up Studio"] })]).AgeRatings);
    }

    [Fact]
    [Trait("TestCase", "T493-9")]
    public void Each_field_comes_from_the_first_provider_that_has_it_and_keeps_its_source()
    {
        var merged = MergedMetadata.Of([
            Provider("First", new GameMetadata { Developers = ["Made-up Studio"], Genres = [Genre.Action] }),
            Provider("Nothing", GameMetadata.Empty),
            Provider("Second", new GameMetadata
            {
                Developers = ["Other Studio"],
                Publishers = ["Made-up Publishing"],
                ReleaseDate = new DateOnly(2021, 3, 4),
                Tags = ["Koop"],
                Genres = [Genre.Puzzle],
            }),
        ]);

        Assert.Equal(new Sourced<IReadOnlyList<string>>(["Made-up Studio"], "First"), merged.Developers, SourcedListComparer.Instance);
        Assert.Equal("First", merged.Genres!.Source);
        Assert.Equal([Genre.Action], merged.Genres.Value);
        Assert.Equal("Second", merged.Publishers!.Source);
        Assert.Equal(new Sourced<DateOnly>(new DateOnly(2021, 3, 4), "Second"), merged.ReleaseDate);
        Assert.Equal("Second", merged.Tags!.Source);
    }

    [Fact]
    [Trait("TestCase", "T493-9")]
    public void Providers_that_deliver_nothing_change_nothing()
    {
        Assert.Same(MergedMetadata.None, MergedMetadata.Of([Provider("A", GameMetadata.Empty), Provider("B", GameMetadata.Empty)]));
        Assert.True(MergedMetadata.Of([]).IsEmpty);
    }

    [Fact]
    [Trait("TestCase", "T493-36")]
    public void Html_becomes_plain_text_without_scripts_images_links_or_entities()
    {
        var text = PlainText.Of("""
            <p>Erste&nbsp;Zeile &amp; mehr</p><script>alert('x')</script><style>p{}</style><!-- note -->
            <ul class="bb_ul"><li><strong>Punkt</strong> eins</li><li>Punkt zwei 🎮</li></ul>
            <img src="javascript:alert(1)"><a href="https://x.example" onclick="x()">Link</a>
            """);

        Assert.Equal("Erste Zeile & mehr\n\n• Punkt eins\n• Punkt zwei 🎮\n\nLink", text);
    }

    [Fact]
    [Trait("TestCase", "T493-36")]
    public void A_very_long_text_ends_with_an_ellipsis_and_control_characters_go()
    {
        var text = PlainText.Of("Wort\u0007 " + new string('a', 20000))!;

        Assert.Equal(PlainText.MaxLength, text.Length);
        Assert.EndsWith("…", text);
        Assert.StartsWith("Wort a", text);
        Assert.Null(PlainText.Of("<br><p></p>"));
    }

    [Fact]
    [Trait("TestCase", "T493-5")]
    public void Names_and_tags_keep_one_line_without_markup_and_a_sane_length()
    {
        var checkedData = MetadataCheck.Checked(new GameMetadata
        {
            Developers = ["<b>Made-up</b>\nStudio", new string('x', 500), "  "],
            Tags = ["Koop", "Koop", new string('t', 100)],
            ReleaseDate = new DateOnly(1800, 1, 1),
        });

        Assert.Equal(["Made-up Studio"], checkedData.Developers);
        Assert.Equal(["Koop"], checkedData.Tags);
        Assert.Null(checkedData.ReleaseDate);
    }

    private sealed class SourcedListComparer : IEqualityComparer<Sourced<IReadOnlyList<string>>?>
    {
        public static SourcedListComparer Instance { get; } = new();

        public bool Equals(Sourced<IReadOnlyList<string>>? x, Sourced<IReadOnlyList<string>>? y) =>
            x is not null && y is not null && x.Source == y.Source && x.Value.SequenceEqual(y.Value);

        public int GetHashCode(Sourced<IReadOnlyList<string>>? obj) => 0;
    }
}
