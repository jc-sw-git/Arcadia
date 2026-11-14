using System.Diagnostics;
using System.Text.Json;
using Arcadia.Core.Library;
using Arcadia.Core.Metadata;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

/// <summary>
/// The library's filter by metadata (#466): genre, tags (own and the providers'), developer, publisher, release year and
/// age rating, "Ohne Angabe einbeziehen", one value per name, and what survives a rename, a restart or a tab without it.
/// </summary>
public sealed class GameFilterMetadataTests
{
    private static Game Entry(string id, bool installed = true) => new()
    {
        Id = id,
        LauncherId = "steam",
        Title = id,
        Type = GameType.Game,
        Launch = new LaunchTarget.None(),
        IsInstalled = installed,
    };

    private static MergedMetadata Data(
        Genre[]? genres = null,
        string[]? tags = null,
        string[]? developers = null,
        string[]? publishers = null,
        int? year = null,
        params AgeRating[] ratings) => new()
    {
        Genres = genres is null ? null : new Sourced<IReadOnlyList<Genre>>(genres, "Steam"),
        Tags = tags is null ? null : new Sourced<IReadOnlyList<string>>(tags, "IGDB"),
        Developers = developers is null ? null : new Sourced<IReadOnlyList<string>>(developers, "Steam"),
        Publishers = publishers is null ? null : new Sourced<IReadOnlyList<string>>(publishers, "Steam"),
        ReleaseDate = year is null ? null : new Sourced<DateOnly>(new DateOnly(year.Value, 6, 1), "Steam"),
        AgeRatings = [.. ratings.Select(rating => new Sourced<AgeRating>(rating, "Steam"))],
    };

    /// <summary>
    /// A tab of six: two shooters by "Nordlicht Studios" (once spelled in capitals), a role-playing game by "Kiesel", an
    /// entry without any metadata, one with only an own tag, one with only a PEGI rating.
    /// </summary>
    private static (IReadOnlyList<Game> Games, FilterFacts Facts) Tab()
    {
        var games = new[] { Entry("shooter-a"), Entry("shooter-b", installed: false), Entry("rpg"), Entry("bare"), Entry("own"), Entry("pegi") };
        var facts = new Dictionary<string, GameFacts>
        {
            [games[0].Key] = GameFacts.Of(Data([Genre.Shooter, Genre.Action], ["Koop", "Horror"], ["Nordlicht Studios"], ["Fernweh"], 2024, new AgeRating(AgeRatingBoard.Usk, "18"))),
            [games[1].Key] = GameFacts.Of(Data([Genre.Shooter], ["Koop"], ["NORDLICHT STUDIOS "], ["Fernweh"], 2016, new AgeRating(AgeRatingBoard.Usk, "16"))),
            [games[2].Key] = GameFacts.Of(Data([Genre.RolePlaying], ["Fantasy"], ["Kiesel"], null, 1998, new AgeRating(AgeRatingBoard.Usk, "12"), new AgeRating(AgeRatingBoard.Pegi, "12"))),
            [games[4].Key] = GameFacts.Of(MergedMetadata.None, ["Horror"]),
            [games[5].Key] = GameFacts.Of(Data(ratings: new AgeRating(AgeRatingBoard.Pegi, "16"))),
        };
        return (games, new FilterFacts(facts));
    }

    private static IEnumerable<string> Ids(IEnumerable<Game> games) => games.Select(game => game.Id);

    private static GameFilter Names(FilterList list, params string[] names) => GameFilter.None.WithNames(list, NameChoice.Of(names));

    [Fact]
    public void A_genre_lets_through_its_entries_and_two_genres_either()
    {
        var (games, facts) = Tab();

        Assert.Equal(["shooter-a", "shooter-b"], Ids(Names(FilterList.Genre, "Shooter").Apply(games, facts)));
        Assert.Equal(["shooter-a", "shooter-b", "rpg"], Ids(Names(FilterList.Genre, "Shooter", "RolePlaying").Apply(games, facts)));
    }

    [Fact]
    public void Categories_are_anded_also_with_the_installed_state()
    {
        var (games, facts) = Tab();
        var filter = Names(FilterList.Genre, "Shooter").WithNames(FilterList.Developer, NameChoice.Of(["Nordlicht Studios"]));

        Assert.Equal(["shooter-a", "shooter-b"], Ids(filter.Apply(games, facts)));
        Assert.Equal(["shooter-a"], Ids(filter.WithInstalled(InstalledChoice.Installed).Apply(games, facts)));
        Assert.Empty(filter.WithNames(FilterList.Developer, NameChoice.Of(["Kiesel"])).Apply(games, facts));
    }

    [Theory]
    [InlineData(FilterList.Genre, "Shooter", "shooter-a,shooter-b,bare,own,pegi")]
    [InlineData(FilterList.Tag, "Koop", "shooter-a,shooter-b,bare,pegi")]
    [InlineData(FilterList.Developer, "Kiesel", "rpg,bare,own,pegi")]
    [InlineData(FilterList.Publisher, "Fernweh", "shooter-a,shooter-b,rpg,bare,own,pegi")]
    public void Ohne_Angabe_adds_the_entries_without_a_value_in_that_category(FilterList list, string name, string expected)
    {
        var (games, facts) = Tab();
        var filter = GameFilter.None.WithNames(list, NameChoice.Of([name], withUnknown: true));

        Assert.Equal(expected.Split(','), Ids(filter.Apply(games, facts)));
    }

    [Fact]
    public void The_switch_is_off_once_nothing_is_chosen()
    {
        var choice = NameChoice.Of(["Shooter"], withUnknown: true).With("Shooter", isOwn: false, chosen: false);

        Assert.False(choice.IsActive);
        Assert.False(choice.WithUnknown);
        Assert.False(NameChoice.None.WithUnknownEntries(true).WithUnknown);
    }

    [Fact]
    public void Names_differing_only_in_case_or_whitespace_are_one_value_in_their_most_frequent_spelling()
    {
        var games = new[] { Entry("a"), Entry("b"), Entry("c"), Entry("d") };
        var facts = new FilterFacts(new Dictionary<string, GameFacts>
        {
            [games[0].Key] = GameFacts.Of(Data(developers: ["CD PROJEKT RED"])),
            [games[1].Key] = GameFacts.Of(Data(developers: ["CD PROJEKT RED"])),
            [games[2].Key] = GameFacts.Of(Data(developers: ["CD Projekt Red "])),
            [games[3].Key] = GameFacts.Of(Data(developers: ["Ubisoft Montreal", "Ubisoft"])),
        });

        var offer = FilterOffer.Of(games, facts);
        var counts = GameFilter.None.Count(games, facts);

        Assert.Equal(["CD PROJEKT RED 3", "Ubisoft 1", "Ubisoft Montreal 1"], offer.NamesOf(FilterList.Developer).Select(name => $"{name.Key} {name.Entries}"));
        Assert.Equal(3, counts.Of(FilterList.Developer, "cd projekt red"));
        Assert.Equal(3, Names(FilterList.Developer, "Cd Projekt Red").Apply(games, facts).Count);
    }

    [Fact]
    public void Genres_are_held_by_key_and_read_by_their_shown_name()
    {
        var (games, facts) = Tab();

        var genres = FilterOffer.Of(games, facts).NamesOf(FilterList.Genre);

        Assert.Equal(["Shooter", "Action", "RolePlaying"], genres.Select(genre => genre.Key));
        Assert.Equal(GenreNames.Of(Genre.RolePlaying), genres[2].Label);
    }

    [Fact]
    public void An_own_tag_hits_only_the_entries_the_player_gave_it_and_a_providers_tag_of_the_same_name_only_its_own()
    {
        var (games, facts) = Tab();
        var own = GameFilter.None.WithNames(FilterList.Tag, NameChoice.Of([], ["Horror"]));
        var providers = Names(FilterList.Tag, "Horror");

        Assert.Equal(["own"], Ids(own.Apply(games, facts)));
        Assert.Equal(["shooter-a"], Ids(providers.Apply(games, facts)));
        var offer = FilterOffer.Of(games, facts);
        Assert.Equal(["Horror"], offer.OwnTags.Select(tag => tag.Key));
        Assert.True(offer.Has(FilterList.Tag));
    }

    [Fact]
    public void A_providers_tag_the_player_also_gave_as_an_own_one_counts_as_the_own_one_as_on_the_detail_page()
    {
        var facts = GameFacts.Of(Data(tags: ["Horror", "Koop"]), ["horror"]);

        Assert.Equal(["Koop"], facts.Tags);
        Assert.Equal(["horror"], facts.OwnTags);
    }

    [Fact]
    public void Counts_beside_values_leave_their_own_category_out()
    {
        var (games, facts) = Tab();
        var filter = Names(FilterList.Genre, "Shooter").WithNames(FilterList.Tag, NameChoice.Of(["Koop"]));

        var counts = filter.Count(games, facts);

        Assert.Equal(2, counts.Hits);
        Assert.Equal(6, counts.Total);
        // Genres counted with the tag "Koop": both shooters; role-playing has no "Koop".
        Assert.Equal((2, 2, 0), (counts.Of(FilterList.Genre, "Shooter"), counts.AllOf(FilterCategory.Genre), counts.Of(FilterList.Genre, "RolePlaying")));
        // Entries without a genre among those with "Koop": none.
        Assert.Equal(0, counts.Lacking(FilterList.Genre));
        // Tags counted with "Shooter": "Koop" on both, "Horror" on one.
        Assert.Equal((2, 1), (counts.Of(FilterList.Tag, "Koop"), counts.Of(FilterList.Tag, "Horror")));
        Assert.Equal(2, counts.Of(FilterList.Developer, "Nordlicht Studios"));
        Assert.Equal((1, 1), (counts.OfYear(2024), counts.OfRating(AgeRatingBoard.Usk, "18")));
    }

    [Fact]
    public void A_year_range_includes_both_ends_and_ohne_angabe_adds_entries_without_a_year()
    {
        var (games, facts) = Tab();

        Assert.Equal(["shooter-a", "shooter-b"], Ids(GameFilter.None.WithYears(new YearChoice(2016, 2024)).Apply(games, facts)));
        Assert.Equal(["rpg"], Ids(GameFilter.None.WithYears(new YearChoice(1950, 1999)).Apply(games, facts)));
        Assert.Equal(["shooter-a", "bare", "own", "pegi"], Ids(GameFilter.None.WithYears(new YearChoice(2020, 2029, withUnknown: true)).Apply(games, facts)));
        Assert.Equal((2024, 2016), (new YearChoice(2024, 2016).To, new YearChoice(2024, 2016).From));
    }

    [Fact]
    public void Quick_picks_mean_this_year_and_the_two_before_the_decades_and_everything_before_2000()
    {
        Assert.Equal((2024, 2026), YearPicks.RangeOf(YearPick.LastThreeYears, 2026));
        Assert.Equal((2020, 2029), YearPicks.RangeOf(YearPick.Twenties, 2026));
        Assert.Equal((2010, 2019), YearPicks.RangeOf(YearPick.Tens, 2026));
        Assert.Equal((2000, 2009), YearPicks.RangeOf(YearPick.Noughties, 2026));
        Assert.Equal((YearPicks.Earliest, 1999), YearPicks.RangeOf(YearPick.Before2000, 2026));
        Assert.Null(YearPicks.RangeOf(YearPick.All, 2026));
        Assert.Equal(YearPick.Tens, YearPicks.Of(new YearChoice(2010, 2019, withUnknown: true), 2026));
        Assert.Equal(YearPick.All, YearPicks.Of(null, 2026));
        Assert.Null(YearPicks.Of(new YearChoice(2011, 2019), 2026));
    }

    [Fact]
    public void The_offer_holds_the_years_with_pre_orders_and_only_the_boards_the_tab_has()
    {
        var (games, facts) = Tab();
        var preOrder = Entry("pre-order");
        var withPreOrder = new FilterFacts(new Dictionary<string, GameFacts> { [preOrder.Key] = GameFacts.Of(Data(year: 2031)) });

        var offer = FilterOffer.Of(games, facts);

        Assert.Equal((1998, 2024), (offer.EarliestYear, offer.LatestYear));
        Assert.Equal(2031, FilterOffer.Of([preOrder], withPreOrder).LatestYear);
        Assert.Equal([AgeRatingBoard.Usk, AgeRatingBoard.Pegi], offer.Boards);
        Assert.Equal(AgeRatingBoard.Usk, offer.DefaultBoard());
        Assert.Equal(AgeRatingBoard.Usk, offer.DefaultBoard(AgeRatingBoard.Esrb));
        Assert.Equal(AgeRatingBoard.Pegi, offer.DefaultBoard(AgeRatingBoard.Pegi));
    }

    [Fact]
    public void A_tab_with_only_pegi_starts_on_pegi()
    {
        var game = Entry("only-pegi");
        var facts = new FilterFacts(new Dictionary<string, GameFacts> { [game.Key] = GameFacts.Of(Data(ratings: new AgeRating(AgeRatingBoard.Pegi, "7"))) });

        var offer = FilterOffer.Of([game], facts);

        Assert.Equal([AgeRatingBoard.Pegi], offer.Boards);
        Assert.Equal(AgeRatingBoard.Pegi, offer.DefaultBoard());
    }

    [Fact]
    public void Ratings_of_one_board_are_ored_and_an_entry_with_only_another_boards_rating_is_ohne_angabe()
    {
        var (games, facts) = Tab();
        var usk = RatingChoice.Of(AgeRatingBoard.Usk, ["16", "18"]);

        Assert.Equal(["shooter-a", "shooter-b"], Ids(GameFilter.None.WithRating(usk).Apply(games, facts)));
        Assert.Equal(["shooter-a", "shooter-b", "bare", "own", "pegi"], Ids(GameFilter.None.WithRating(usk.WithUnknownEntries(true)).Apply(games, facts)));
        Assert.Equal(["rpg", "pegi"], Ids(GameFilter.None.WithRating(RatingChoice.Of(AgeRatingBoard.Pegi, ["12", "16"])).Apply(games, facts)));
        Assert.Equal(4, GameFilter.None.WithRating(usk).Count(games, facts).LackingRating(AgeRatingBoard.Usk) + 1);
    }

    [Fact]
    public void A_board_without_a_chosen_rating_filters_nothing_and_ratings_keep_the_boards_order()
    {
        var (games, facts) = Tab();
        var board = RatingChoice.Of(AgeRatingBoard.Esrb, []);

        Assert.False(GameFilter.None.WithRating(board).IsActive);
        Assert.Same(games, GameFilter.None.WithRating(board).Apply(games, facts));
        Assert.Equal(["E", "T", "AO"], RatingChoice.Of(AgeRatingBoard.Esrb, ["AO", "E", "T", "X"]).Ratings);
    }

    [Fact]
    public void Values_the_tab_no_longer_holds_drop_out_and_a_choice_keeps_them_from_the_saved_one()
    {
        var (games, facts) = Tab();
        var saved = Names(FilterList.Tag, "Koop", "Weltraum")
            .WithNames(FilterList.Publisher, NameChoice.Of(["Hochland"], withUnknown: true))
            .WithYears(new YearChoice(2000, 2009))
            .WithRating(RatingChoice.Of(AgeRatingBoard.Esrb, ["M"]));
        var offer = FilterOffer.Of(games, facts);

        var within = saved.Within(offer);

        Assert.Equal(["Koop"], within.NamesOf(FilterList.Tag).Names);
        Assert.False(within.NamesOf(FilterList.Publisher).IsActive);
        Assert.Equal(new YearChoice(2000, 2009), within.Years);
        Assert.Null(within.Rating);

        // Closed again with a change in "Tags": what the tab didn't hold comes back from the saved choice.
        var over = within.WithNames(FilterList.Tag, NameChoice.Of(["Fantasy"])).Over(saved, offer);
        Assert.Equal(["Fantasy", "Weltraum"], over.NamesOf(FilterList.Tag).Names);
        Assert.Equal(NameChoice.Of(["Hochland"], withUnknown: true), over.NamesOf(FilterList.Publisher));
        Assert.Equal(RatingChoice.Of(AgeRatingBoard.Esrb, ["M"]), over.Rating);
    }

    [Fact]
    public void Categories_a_tab_without_metadata_lacks_keep_their_saved_choice()
    {
        var bare = new[] { Entry("a"), Entry("b", installed: false) };
        var saved = Names(FilterList.Genre, "Shooter").WithYears(new YearChoice(2010, 2019)).WithRating(RatingChoice.Of(AgeRatingBoard.Usk, ["12"]));
        var offer = FilterOffer.Of(bare);

        Assert.False(offer.Has(FilterCategory.Genre) || offer.HasYears || offer.HasRatings);
        Assert.Equal(GameFilter.None, saved.Within(offer));
        Assert.Equal(saved, GameFilter.None.Over(saved, offer));
    }

    [Fact]
    public void A_renamed_or_merged_own_tag_follows_into_the_filter_and_a_deleted_one_drops_out()
    {
        var filter = GameFilter.None.WithNames(FilterList.Tag, NameChoice.Of(["Koop"], ["Grusel"]));

        Assert.Equal(["Horror"], filter.WithOwnTagRenamed("grusel", "Horror").NamesOf(FilterList.Tag).Own);
        Assert.Equal(["Koop"], filter.WithOwnTagRenamed("Grusel", null).NamesOf(FilterList.Tag).Names);
        Assert.Empty(filter.WithOwnTagRenamed("Grusel", null).NamesOf(FilterList.Tag).Own);
        Assert.Same(filter.NamesOf(FilterList.Tag), filter.WithOwnTagRenamed("Koop", "x").NamesOf(FilterList.Tag));

        var onlyOwn = GameFilter.None.WithNames(FilterList.Tag, NameChoice.Of([], ["Grusel"]));
        Assert.False(onlyOwn.WithOwnTagRenamed("Grusel", null).IsActive);
    }

    [Fact]
    public void Settings_follow_a_renamed_own_tag_in_every_tab()
    {
        var settings = new AppSettings()
            .WithFilter("steam", GameFilter.None.WithNames(FilterList.Tag, NameChoice.Of([], ["Grusel"])))
            .WithFilter("epic", GameFilter.None.WithNames(FilterList.Tag, NameChoice.Of([], ["Grusel", "Koop"])))
            .WithFilter("gog", Names(FilterList.Genre, "Shooter"));

        var renamed = settings.WithOwnTagRenamed("Grusel", "Horror");
        var deleted = settings.WithOwnTagRenamed("Grusel", null);

        Assert.Equal(["Horror"], renamed.FilterOf("steam").NamesOf(FilterList.Tag).Own);
        Assert.Equal(["Koop", "Horror"], renamed.FilterOf("epic").NamesOf(FilterList.Tag).Own);
        Assert.False(deleted.LibraryFilters.ContainsKey("steam"));
        Assert.Equal(["Koop"], deleted.FilterOf("epic").NamesOf(FilterList.Tag).Own);
        Assert.Equal(settings.FilterOf("gog"), renamed.FilterOf("gog"));
    }

    [Fact]
    public void Every_metadata_choice_comes_back_from_settings_json_with_genres_by_key()
    {
        var filter = Names(FilterList.Genre, "Shooter", "RolePlaying")
            .WithNames(FilterList.Tag, NameChoice.Of(["Koop"], ["Grusel"], withUnknown: true))
            .WithNames(FilterList.Developer, NameChoice.Of(["CD PROJEKT RED"]))
            .WithNames(FilterList.Publisher, NameChoice.Of(["Fernweh"]))
            .WithYears(new YearChoice(2010, 2019, withUnknown: true))
            .WithRating(RatingChoice.Of(AgeRatingBoard.Pegi, ["12", "16"]));
        var settings = new AppSettings().WithFilter("steam", filter);

        var json = JsonSerializer.Serialize(settings);
        var read = JsonSerializer.Deserialize<AppSettings>(json)!;

        Assert.Contains("\"RolePlaying\"", json, StringComparison.Ordinal);
        Assert.Equal(filter, read.FilterOf("steam"));
    }

    [Theory]
    [InlineData("""{"Genres":{"Names":["Shooter","Rollenspiel","7"]}}""", "Genre:Shooter")]
    [InlineData("""{"Years":{"From":2019,"To":2010}}""", "")]
    [InlineData("""{"Years":{"From":1800,"To":2010}}""", "")]
    [InlineData("""{"AgeRating":{"Board":"Usk","Ratings":["16","17"]}}""", "Usk:16")]
    [InlineData("""{"AgeRating":{"Board":"Bbfc","Ratings":["15"]}}""", "")]
    [InlineData("""{"AgeRating":{"Board":"Usk","Ratings":["17"]}}""", "")]
    [InlineData("""{"Tags":{"Names":["Koop",""],"Own":["Ein sehr langer eigener Tag mit mehr als vierzig Zeichen"]}}""", "Tag:Koop")]
    [InlineData("""{"Developers":{"Names":null,"WithUnknown":true}}""", "")]
    public void Metadata_values_this_version_does_not_accept_drop_out_silently(string saved, string expected)
    {
        var filter = JsonSerializer.Deserialize<SavedFilter>(saved)!.ToFilter();

        var left = Enum.GetValues<FilterList>()
            .SelectMany(list => filter.NamesOf(list).Names.Concat(filter.NamesOf(list).Own).Select(name => $"{list}:{name}"))
            .Concat(filter.Rating is { } rating ? rating.Ratings.Select(value => $"{rating.Board}:{value}") : [])
            .Concat(filter.Years is { } years ? [$"Years:{years.From}"] : []);
        Assert.Equal(expected, string.Join(",", left));
    }

    [Fact]
    public void Counting_five_thousand_entries_with_thousands_of_tags_stays_well_within_a_press()
    {
        var random = new Random(466);
        var games = Enumerable.Range(0, 5000).Select(i => Entry($"game-{i}")).ToList();
        var facts = new FilterFacts(games.ToDictionary(game => game.Key, game => GameFacts.Of(Data(
            [(Genre)random.Next(20), (Genre)random.Next(20)],
            [.. Enumerable.Range(0, 30).Select(_ => $"Tag {random.Next(3000)}")],
            [$"Studio {random.Next(1500)}"],
            [$"Publisher {random.Next(600)}"],
            1990 + random.Next(37),
            new AgeRating(AgeRatingBoard.Usk, "12")))));
        var filter = Names(FilterList.Genre, "Shooter", "Action").WithNames(FilterList.Tag, NameChoice.Of(["Tag 1", "Tag 2"], withUnknown: true));
        filter.Count(games, facts);
        FilterOffer.Of(games, facts);

        var watch = Stopwatch.StartNew();
        var counts = filter.Count(games, facts);
        var offer = FilterOffer.Of(games, facts);

        Assert.InRange(counts.AllOf(FilterCategory.Tag), 1, 5000);
        Assert.InRange(offer.NamesOf(FilterList.Tag).Count, 2500, 3000);
        // Off the UI thread in the modal; this only guards against a slip to quadratic work (a CI runner is slow).
        Assert.InRange(watch.ElapsedMilliseconds, 0, 400);
    }
}
