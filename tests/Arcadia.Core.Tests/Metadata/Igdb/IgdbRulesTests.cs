using System.Text;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests.Metadata.Igdb;

/// <summary>#494: the access values, the queries, IGDB's answer and the matching by name, with made-up games.</summary>
public sealed class IgdbRulesTests
{
    private static IgdbGame Hit(long id, string name, int? type = 0) => new(id, name, type, GameMetadata.Empty, null, null);

    [Theory]
    [Trait("TestCase", "T494-4")]
    [InlineData("  madeupclientid0000000001 \r\n", "madeupclientid0000000001")]
    [InlineData("madeupclientid00000", null)]
    [InlineData("madeupclientid0000000000000000000000000000000000000000000000000000001", null)]
    [InlineData("madeup client id 000000001", null)]
    [InlineData("madeupclientid000000\r\n01", null)]
    [InlineData("madeupclientid\"0000000001", null)]
    [InlineData("madeupclientid&0000000001", null)]
    [InlineData("madeupclientid=0000000001", null)]
    [InlineData("madeupclientid\u00000000000001", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void A_value_is_20_to_64_letters_and_digits_and_nothing_else(string? text, string? expected) =>
        Assert.Equal(expected, IgdbAccess.Normalize(text));

    [Fact]
    [Trait("TestCase", "T494-13")]
    public void A_steam_game_is_asked_by_its_appid_at_source_1_and_nothing_but_digits_goes()
    {
        var query = IgdbQuery.BySteamAppId("4711")!;
        Assert.Equal("external_games", query.Endpoint);
        Assert.Contains("where external_game_source = 1 & uid = \"4711\";", query.Body);
        Assert.Null(IgdbQuery.BySteamAppId("47\"11"));
        Assert.Null(IgdbQuery.BySteamAppId("12345678901"));
        Assert.Null(IgdbQuery.BySteamAppId(""));
    }

    [Theory]
    [Trait("TestCase", "T494-11")]
    [InlineData("Nordwind\"; fields *; where id = 1", "Nordwind fields where id 1")]
    [InlineData("Nord\\wind*\r\nII", "Nord wind II")]
    [InlineData("Nordwind™ Remastered®", "Nordwind Remastered")]
    [InlineData("K?ln Stories", "K ln Stories")]
    [InlineData("Nordwind 🚀 Saga", "Nordwind Saga")]
    [InlineData("™ ® ©", null)]
    [InlineData("  ", null)]
    public void A_name_never_changes_the_query_beyond_the_name(string name, string? term)
    {
        Assert.Equal(term, IgdbQuery.SearchTerm(name));
        if (IgdbQuery.ByName(name) is { } query)
        {
            Assert.StartsWith($"search \"{term}\"; fields ", query.Body);
            Assert.Equal(1, query.Body.Count(c => c == '"') / 2);
        }
    }

    [Fact]
    [Trait("TestCase", "T494-11")]
    public void A_name_of_300_characters_is_cut()
    {
        var term = IgdbQuery.SearchTerm(new string('a', 300))!;
        Assert.Equal(IgdbQuery.MaxTerm, term.Length);
    }

    [Fact]
    [Trait("TestCase", "T494-14")]
    public void Dlc_mods_and_bundles_are_dropped_before_hits_are_counted()
    {
        var match = IgdbMatching.Of("Nordwind", [Hit(2, "Nordwind – Season Pass", 1), Hit(3, "Nordwind Remix", 5), Hit(4, "Nordwind Bundle", 3), Hit(1, "Nordwind")]);
        Assert.Equal(1, match.Taken?.Id);
        Assert.False(match.IsSeveral);
    }

    [Theory]
    [Trait("TestCase", "T494-14")]
    [InlineData(0, true)]
    [InlineData(4, true)]
    [InlineData(8, true)]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    [InlineData(null, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(5, false)]
    [InlineData(6, false)]
    [InlineData(7, false)]
    [InlineData(12, false)]
    [InlineData(13, false)]
    [InlineData(14, false)]
    public void Main_games_are_the_types_of_d4(int? type, bool main) => Assert.Equal(main, IgdbGameType.IsMainGame(type));

    [Fact]
    [Trait("TestCase", "T494-15")]
    public void A_question_mark_stands_for_one_character_and_case_and_punctuation_are_ignored()
    {
        Assert.Equal(1, IgdbMatching.Of("K?ln", [Hit(2, "Köln Stories"), Hit(1, "Köln")]).Taken?.Id);
        Assert.Equal(1, IgdbMatching.Of("nordwind: das spiel™", [Hit(2, "Nordwind 2"), Hit(1, "Nordwind – Das Spiel")]).Taken?.Id);
        Assert.Null(IgdbMatching.Of("K?ln", [Hit(1, "Kln"), Hit(2, "Köölln")]).Taken);
    }

    [Fact]
    [Trait("TestCase", "T494-15")]
    public void A_question_mark_also_stands_for_a_dash_an_ellipsis_or_a_curly_quote_of_the_hit()
    {
        Assert.Equal(1, IgdbMatching.Of("Nordwind ? Die R?ckkehr", [Hit(2, "Nordwind"), Hit(1, "Nordwind – Die Rückkehr")]).Taken?.Id);
        Assert.Equal(1, IgdbMatching.Of("Warte?", [Hit(2, "Warten"), Hit(1, "Warte…"), Hit(3, "Warte")]).Taken?.Id);
        Assert.Equal(1, IgdbMatching.Of("Jonas? Reise", [Hit(2, "Jonas Reise 2"), Hit(1, "Jonas’ Reise")]).Taken?.Id);
    }

    [Fact]
    [Trait("TestCase", "T494-15")]
    public void One_main_game_of_any_name_is_taken_two_equal_or_several_others_are_several_hits_and_none_is_nothing()
    {
        Assert.Equal(5, IgdbMatching.Of("Nordwind", [Hit(5, "Ganz anders"), Hit(6, "Nordwind DLC", 1)]).Taken?.Id);

        var twoEqual = IgdbMatching.Of("Nordwind", [Hit(1, "Nordwind"), Hit(2, "Nordwind"), Hit(3, "Nordwind 2")]);
        Assert.Null(twoEqual.Taken);
        Assert.Equal([1L, 2L], twoEqual.Hits.Select(h => h.Id));

        var several = IgdbMatching.Of("Nordwind", [.. Enumerable.Range(1, 12).Select(i => Hit(i, "Anders " + i))]);
        Assert.True(several.IsSeveral);
        Assert.Equal(IgdbMatching.MaxHits, several.Hits.Count);
        Assert.Equal(1, several.Hits[0].Id);

        Assert.Same(IgdbMatch.None, IgdbMatching.Of("Nordwind", [Hit(7, "Nordwind DLC", 1)]));
    }

    [Fact]
    [Trait("TestCase", "T494-27")]
    public void Igdbs_fields_map_onto_arcadias_genres_ratings_tags_and_companies()
    {
        var json = """
            [{"id":7,"name":"Köln","game_type":0,"summary":"A made-up city game.","first_release_date":1609459200,
              "genres":[{"id":12,"name":"Role-playing (RPG)"},{"id":31,"name":"Adventure"},{"id":36,"name":"MOBA"}],
              "themes":[{"name":"Action"},{"name":"Fantasy"}],"keywords":[{"name":"city"}],
              "age_ratings":[{"organization":4,"rating_category":{"id":21,"rating":"16"}},{"organization":2,"rating_category":{"id":10,"rating":"12"}},
                             {"organization":1,"rating_category":{"id":6,"rating":"M"}},{"organization":3,"rating_category":{"id":15,"rating":"C"}},
                             {"organization":4,"rating_category":{"id":99,"rating":"99"}}],
              "involved_companies":[{"company":{"name":"Made-up Studio"},"developer":true,"publisher":false},
                                    {"company":{"name":"Made-up Publisher"},"developer":false,"publisher":true}],
              "cover":{"image_id":"co1abc"}}]
            """;
        var game = Assert.Single(IgdbAnswer.Read(Encoding.UTF8.GetBytes(json))!);

        Assert.Equal((7L, "Köln", 0, 2021, "co1abc"), (game.Id, game.Name, game.Type!.Value, game.Year!.Value, game.CoverId!));
        Assert.Equal([Genre.Action, Genre.RolePlaying, Genre.Adventure], game.Data.Genres);
        Assert.Equal(
            [new AgeRating(AgeRatingBoard.Usk, "16"), new AgeRating(AgeRatingBoard.Pegi, "12"), new AgeRating(AgeRatingBoard.Esrb, "M")],
            game.Data.AgeRatings);
        Assert.Equal(["Action", "Fantasy", "city"], game.Data.Tags);
        Assert.Equal(["Made-up Studio"], game.Data.Developers);
        Assert.Equal(["Made-up Publisher"], game.Data.Publishers);
        Assert.Equal(new DateOnly(2021, 1, 1), game.Data.ReleaseDate);
        Assert.Equal("A made-up city game.", game.Data.Description);
    }

    [Theory]
    [Trait("TestCase", "T541-2")]
    [InlineData("""{"organization":2,"rating_category":{"id":10,"rating":"Twelve"}}""", AgeRatingBoard.Pegi, "12")]
    [InlineData("""{"organization":2,"rating_category":{"id":8,"rating":"PEGI 3+"}}""", AgeRatingBoard.Pegi, "3")]
    [InlineData("""{"organization":1,"rating_category":{"id":4,"rating":"E10"}}""", AgeRatingBoard.Esrb, "E10+")]
    [InlineData("""{"organization":4,"rating_category":{"id":20,"rating":"USK_12"}}""", AgeRatingBoard.Usk, "12")]
    [InlineData("""{"category":2,"rating":3}""", AgeRatingBoard.Pegi, "12")]
    [InlineData("""{"category":1,"rating":9}""", AgeRatingBoard.Esrb, "E10+")]
    [InlineData("""{"organization":4,"category":4,"rating":22}""", AgeRatingBoard.Usk, "18")]
    public void A_rating_is_read_from_its_categorys_text_or_the_deprecated_enum(string rating, AgeRatingBoard board, string value)
    {
        var json = "[{\"id\":7,\"name\":\"Nordwind\",\"age_ratings\":[" + rating + "]}]";

        Assert.Equal([new AgeRating(board, value)], Assert.Single(IgdbAnswer.Read(Encoding.UTF8.GetBytes(json))!).Data.AgeRatings);
    }

    [Theory]
    [Trait("TestCase", "T541-2")]
    [InlineData("""{"organization":2,"rating_category":10}""")]
    [InlineData("""{"organization":3,"rating_category":{"id":14,"rating":"B"}}""")]
    [InlineData("""{"organization":1,"rating_category":{"id":1,"rating":"RP"}}""")]
    [InlineData("""{"rating_category":{"id":10,"rating":"12"}}""")]
    public void A_rating_without_a_known_board_or_value_is_dropped_never_guessed_from_an_id(string rating)
    {
        var json = "[{\"id\":7,\"name\":\"Nordwind\",\"age_ratings\":[" + rating + "]}]";

        Assert.Empty(Assert.Single(IgdbAnswer.Read(Encoding.UTF8.GetBytes(json))!).Data.AgeRatings);
    }

    [Theory]
    [Trait("TestCase", "T494-28")]
    [InlineData("{not json")]
    [InlineData("{\"id\":1}")]
    [InlineData("\"text\"")]
    public void A_broken_answer_counts_as_none(string answer) => Assert.Null(IgdbAnswer.Read(Encoding.UTF8.GetBytes(answer)));

    [Fact]
    [Trait("TestCase", "T494-28")]
    public void A_game_without_an_id_or_with_an_overlong_name_is_left_out_and_a_cover_comes_only_from_igdbs_image_host()
    {
        var json = "[{\"name\":\"No id\"},{\"id\":2,\"name\":\"" + new string('x', 300) + "\"},{\"id\":3,\"name\":\"Fine\",\"cover\":{\"image_id\":\"../../evil\"}}]";
        var game = Assert.Single(IgdbAnswer.Read(Encoding.UTF8.GetBytes(json))!);
        Assert.Equal(3, game.Id);
        Assert.Null(game.CoverId);
    }
}
