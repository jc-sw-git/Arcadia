using System.Text;
using Arcadia.Core.Metadata;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests.Metadata;

/// <summary>#493: Steam's store page, its address and its answer (T493-5, T493-15).</summary>
public sealed class SteamAppDetailsTests
{
    private static readonly IMetadataStore Store = new SteamMetadata(() => null).Store!;

    private static Game SteamGame(string id) => new()
    {
        Id = id,
        LauncherId = "steam",
        Title = "Made-up Game " + id,
        Launch = new LaunchTarget.ProtocolUri("steam://rungameid/" + id),
    };

    private static GameMetadata? Parse(string id, string json) => Store.Parse(SteamGame(id), Encoding.UTF8.GetBytes(json));

    private const string Answer = """
        {"4000001":{"success":true,"data":{"type":"game","name":"Made-up Game","steam_appid":4000001,
          "about_the_game":"<h2>Über das Spiel</h2>Du baust eine <b>Stadt</b> und die Bürger sind froh.<br><img src=\"https://x.example/a.png\"><a href=\"https://x.example\">Mehr</a>",
          "short_description":"Eine erfundene Stadt &amp; ihre Bürger.",
          "genres":[{"id":"1","description":"Action"},{"id":"28","description":"Simulation"},{"id":"70","description":"Early Access"}],
          "developers":["Made-up Studio"],"publishers":["Made-up Publishing"],
          "ratings":{"usk":{"rating":"16"},"pegi":{"rating":"12"},"esrb":{"rating":"t"},"steam_germany":{"rating":"18"},"dejus":{"rating":"16"}}}}}
        """;

    [Fact]
    [Trait("TestCase", "T493-5")]
    public void Reads_description_short_description_genres_companies_and_ratings()
    {
        var found = MetadataCheck.Checked(Parse("4000001", Answer));

        Assert.Equal("Über das Spiel\n\nDu baust eine Stadt und die Bürger sind froh.\nMehr", found.Description);
        Assert.Equal("Eine erfundene Stadt & ihre Bürger.", found.ShortDescription);
        Assert.Equal([Genre.Action, Genre.Simulation], found.Genres);
        Assert.Equal(["Made-up Studio"], found.Developers);
        Assert.Equal(["Made-up Publishing"], found.Publishers);
        Assert.Equal(
            [new AgeRating(AgeRatingBoard.Usk, "16"), new AgeRating(AgeRatingBoard.Usk, "18", ByStore: true), new AgeRating(AgeRatingBoard.Pegi, "12"), new AgeRating(AgeRatingBoard.Esrb, "T")],
            found.AgeRatings);
    }

    [Fact]
    [Trait("TestCase", "T493-5")]
    public void Steam_saying_it_has_no_page_is_an_empty_answer()
    {
        Assert.Same(GameMetadata.Empty, Parse("4000001", """{"4000001":{"success":false}}"""));
    }

    [Theory]
    [Trait("TestCase", "T493-5")]
    [InlineData("""{"4000001":{"success":true}}""")]
    [InlineData("""{"4000001":{"success":true,"data":[]}}""")]
    [InlineData("""{"4000001":{"success":"yes","data":{}}}""")]
    [InlineData("""{"4000009":{"success":true,"data":{"steam_appid":4000009}}}""")]
    [InlineData("""{"4000001":{"success":true,"data":{"steam_appid":4000002,"short_description":"Another game"}}}""")]
    [InlineData("""{"4000001":{"success":true,"data":{"steam_appid":"4000001"}}}""")]
    [InlineData("""{"4000001":{"success":true,"data":{"steam_appid":4000001""")]
    [InlineData("[]")]
    [InlineData("not json")]
    public void A_broken_answer_or_one_for_another_app_is_no_answer(string json)
    {
        Assert.Null(Parse("4000001", json));
    }

    [Fact]
    [Trait("TestCase", "T493-5")]
    public void Values_of_the_wrong_type_are_left_out()
    {
        var found = Parse("4000001", """
            {"4000001":{"success":true,"data":{"steam_appid":4000001,"about_the_game":42,"short_description":["x"],
              "genres":"Action","developers":[1,"Made-up Studio",null],"ratings":{"usk":"16","pegi":{"rating":12}}}}}
            """)!;

        Assert.Null(found.Description);
        Assert.Null(found.ShortDescription);
        Assert.Empty(found.Genres);
        Assert.Equal(["Made-up Studio"], found.Developers);
        Assert.Empty(found.AgeRatings);
    }

    [Fact]
    [Trait("TestCase", "T493-15")]
    public void The_address_is_https_to_the_store_host_with_only_the_appid_and_the_language()
    {
        var address = Store.AddressOf(SteamGame("4000001"))!;

        Assert.Equal("https", address.Scheme);
        Assert.Equal("store.steampowered.com", address.Host);
        Assert.Equal("/api/appdetails", address.AbsolutePath);
        Assert.Equal("?l=german&appids=4000001", address.Query);
        Assert.Empty(address.UserInfo);
    }

    [Theory]
    [Trait("TestCase", "T493-15")]
    [InlineData("0")]
    [InlineData("04000001")]
    [InlineData("4000001&l=english")]
    [InlineData("../4000001")]
    [InlineData("-5")]
    [InlineData("99999999999")]
    [InlineData("")]
    public void An_id_that_is_no_plain_appid_never_gets_an_address(string id)
    {
        Assert.Null(Store.AddressOf(SteamGame(id)));
        Assert.Null(Store.Parse(SteamGame(id), Encoding.UTF8.GetBytes(Answer)));
    }

    [Fact]
    [Trait("TestCase", "T493-15")]
    public void A_game_of_another_launcher_never_gets_an_address()
    {
        Assert.Null(Store.AddressOf(SteamGame("4000001") with { LauncherId = "epic" }));
    }
}
