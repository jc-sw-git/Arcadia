using System.Text;
using System.Text.Json;
using Arcadia.Core.Metadata;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Epic;

namespace Arcadia.Core.Tests.Metadata;

/// <summary>#493: Epic's catalog delivers only the developer; its description and dates are never used (T493-10).</summary>
public sealed class EpicMetadataTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-epic-metadata-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string CatalogPath => Path.Combine(_root, "catcache.bin");

    private void WriteCatalog(params object[] entries) =>
        File.WriteAllText(CatalogPath, Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entries))));

    private static object Entry(string id, string appName, string? developer, string? mainGame = null) => new Dictionary<string, object?>
    {
        ["id"] = id,
        ["namespace"] = "ns-" + id,
        ["title"] = "Made-up Game " + id,
        ["description"] = "Ein erfundenes Spiel aus K?ln",
        ["developer"] = developer,
        ["dateAdded"] = "2021-01-01T00:00:00.000Z",
        ["mainGameItem"] = mainGame is null ? null : new Dictionary<string, object> { ["id"] = mainGame },
        ["categories"] = new[] { new Dictionary<string, object> { ["path"] = "games" } },
        ["releaseInfo"] = new[] { new Dictionary<string, object> { ["appId"] = appName, ["platform"] = new[] { "Windows" } } },
    };

    private static Game GameOf(string appName) => new()
    {
        Id = appName,
        LauncherId = EpicIntegration.Info.Id,
        Title = "Made-up Game",
        Launch = new LaunchTarget.ProtocolUri($"com.epicgames.launcher://apps/{appName}?action=launch&silent=true"),
    };

    [Fact]
    [Trait("TestCase", "T493-10")]
    public void An_epic_game_gets_only_its_developer()
    {
        WriteCatalog(Entry("item1", "MadeUpApp", "Made-up Studio"));
        var metadata = new EpicMetadata(CatalogPath);

        var found = metadata.Look([GameOf("MadeUpApp")]).Of(GameOf("MadeUpApp"));

        Assert.Equal(new GameMetadata { Developers = ["Made-up Studio"] }.Developers, found!.Developers);
        Assert.Null(found.Description);
        Assert.Null(found.ShortDescription);
        Assert.Null(found.ReleaseDate);
        Assert.Empty(found.Genres);
        Assert.Empty(found.Tags);
        Assert.Empty(found.AgeRatings);
        Assert.Null(((ILauncherMetadata)metadata).Store);
        Assert.Equal([MetadataField.Developers], metadata.Fields);

        var merged = MergedMetadata.Of([new ProviderMetadata(EpicIntegration.Info.DisplayName, MetadataCheck.Checked(found))]);
        Assert.Equal(EpicIntegration.Info.DisplayName, merged.Developers!.Source);
        Assert.Null(merged.Description);
    }

    [Fact]
    [Trait("TestCase", "T493-10")]
    public void A_base_game_wins_over_its_dlc_and_a_game_without_developer_or_catalog_gets_nothing()
    {
        WriteCatalog(
            Entry("dlc1", "MadeUpApp", "DLC Studio", mainGame: "item1"),
            Entry("item1", "MadeUpApp", "Made-up Studio"),
            Entry("item2", "OtherApp", null));
        var lookup = new EpicMetadata(CatalogPath).Look([GameOf("MadeUpApp"), GameOf("OtherApp")]);

        Assert.Equal(["Made-up Studio"], lookup.Of(GameOf("MadeUpApp"))!.Developers);
        Assert.Null(lookup.Of(GameOf("OtherApp")));
        Assert.Null(new EpicMetadata(Path.Combine(_root, "missing.bin")).Look([GameOf("MadeUpApp")]).Of(GameOf("MadeUpApp")));
        File.WriteAllText(CatalogPath, "not base64 !!");
        Assert.Null(new EpicMetadata(CatalogPath).Look([GameOf("MadeUpApp")]).Of(GameOf("MadeUpApp")));
    }
}
