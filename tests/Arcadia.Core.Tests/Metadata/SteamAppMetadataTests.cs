using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests.Metadata;

/// <summary>#493: Steam's local metadata from <c>appinfo.vdf</c> and the names of its store tags (T493-3, T493-4).</summary>
public sealed class SteamAppMetadataTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-steam-metadata-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static Game SteamGame(string id) => new()
    {
        Id = id,
        LauncherId = "steam",
        Title = "Made-up Game " + id,
        Launch = new LaunchTarget.ProtocolUri("steam://rungameid/" + id),
    };

    private SteamMetadata WithAppInfo(byte[] appInfo)
    {
        Directory.CreateDirectory(Path.Combine(_root, "appcache"));
        File.WriteAllBytes(Path.Combine(_root, "appcache", "appinfo.vdf"), appInfo);
        return new SteamMetadata(() => new SteamInstallation(_root, null));
    }

    private static Dictionary<string, object> Common(Dictionary<string, object> common, Dictionary<string, object>? extended = null)
    {
        var info = new Dictionary<string, object> { ["common"] = common };
        if (extended is not null)
        {
            info["extended"] = extended;
        }

        return info;
    }

    [Fact]
    [Trait("TestCase", "T493-3")]
    public void Reads_genres_developers_publishers_release_date_and_tags_of_an_app()
    {
        var metadata = WithAppInfo(AppInfoMetadataFile.Build((4000001, Common(new()
        {
            ["name"] = "Made-up Game",
            ["type"] = "Game",
            ["primary_genre"] = "25",
            ["genres"] = AppInfoMetadataFile.List("1", "25", "37"),
            ["store_tags"] = AppInfoMetadataFile.List("19", "21", "999999"),
            ["associations"] = AppInfoMetadataFile.List(
                new Dictionary<string, object> { ["type"] = "developer", ["name"] = "Made-up Studio" },
                new Dictionary<string, object> { ["type"] = "publisher", ["name"] = "Made-up Publishing" }),
            ["steam_release_date"] = "1609459200",
        }))));

        var game = SteamGame("4000001");
        var found = metadata.Look([game]).Of(game);

        Assert.NotNull(found);
        Assert.Equal([Genre.Adventure, Genre.Action], found.Genres);
        Assert.Equal(["Made-up Studio"], found.Developers);
        Assert.Equal(["Made-up Publishing"], found.Publishers);
        Assert.Equal(new DateOnly(2021, 1, 1), found.ReleaseDate);
        Assert.Equal(["Action", "Abenteuer"], found.Tags);
        Assert.Null(found.Description);
        Assert.Empty(found.AgeRatings);
    }

    [Fact]
    [Trait("TestCase", "T493-3")]
    public void Takes_developer_and_publisher_from_extended_when_associations_lack_them_and_leaves_out_empty_fields()
    {
        var metadata = WithAppInfo(AppInfoMetadataFile.Build(
            (4000002, Common(
                new() { ["name"] = "Made-up Two", ["type"] = "Game", ["genres"] = new Dictionary<string, object>(), ["original_release_date"] = 1262304000 },
                new() { ["developer"] = "Made-up Devs", ["publisher"] = " " }))));

        var game = SteamGame("4000002");
        var found = metadata.Look([game]).Of(game)!;

        Assert.Equal(["Made-up Devs"], found.Developers);
        Assert.Empty(found.Publishers);
        Assert.Empty(found.Genres);
        Assert.Equal(new DateOnly(2010, 1, 1), found.ReleaseDate);
    }

    [Fact]
    [Trait("TestCase", "T493-3")]
    public void A_broken_entry_costs_only_that_games_data()
    {
        var apps = new[]
        {
            (4000003u, Common(new() { ["name"] = "Broken", ["type"] = "Game", ["genres"] = AppInfoMetadataFile.List("1") })),
            (4000004u, Common(new() { ["name"] = "Fine", ["type"] = "Game", ["genres"] = AppInfoMetadataFile.List("2") })),
        };
        var metadata = WithAppInfo(AppInfoMetadataFile.Build(apps, brokenApp: 4000003));

        var broken = SteamGame("4000003");
        var fine = SteamGame("4000004");
        var lookup = metadata.Look([broken, fine]);

        Assert.Null(lookup.Of(broken));
        Assert.Equal([Genre.Strategy], lookup.Of(fine)!.Genres);
    }

    [Fact]
    [Trait("TestCase", "T493-3")]
    public void A_truncated_or_missing_file_gives_no_data_and_never_throws()
    {
        var truncated = AppInfoMetadataFile.Build((4000005, Common(new() { ["name"] = "X", ["type"] = "Game" })));
        var metadata = WithAppInfo(truncated[..20]);
        var game = SteamGame("4000005");

        Assert.Null(metadata.Look([game]).Of(game));
        Assert.Null(new SteamMetadata(() => null).Look([game]).Of(game));
        Assert.Null(new SteamMetadata(() => new SteamInstallation(Path.Combine(_root, "missing"), null)).Look([game]).Of(game));
    }

    [Fact]
    [Trait("TestCase", "T493-4")]
    public void Store_tags_are_named_from_the_german_list_in_steams_order_and_unknown_ids_are_dropped()
    {
        var metadata = WithAppInfo(AppInfoMetadataFile.Build((4000006, Common(new()
        {
            ["name"] = "Tagged",
            ["type"] = "Game",
            ["store_tags"] = AppInfoMetadataFile.List("1664", "123456789", "1774", "1664"),
        }))));

        var game = SteamGame("4000006");

        Assert.Equal(["Rätsel", "Shooter"], metadata.Look([game]).Of(game)!.Tags);
    }

    [Fact]
    [Trait("TestCase", "T493-3")]
    public void A_game_of_another_launcher_or_with_an_id_that_is_no_appid_gets_nothing()
    {
        var metadata = WithAppInfo(AppInfoMetadataFile.Build((4000007, Common(new() { ["name"] = "X", ["type"] = "Game", ["genres"] = AppInfoMetadataFile.List("1") }))));

        Assert.Null(metadata.Look([SteamGame("04000007")]).Of(SteamGame("04000007")));
        var epic = SteamGame("4000007") with { LauncherId = "epic" };
        Assert.Null(metadata.Look([epic]).Of(epic));
    }
}
