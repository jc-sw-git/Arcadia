using System.Text;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Epic;

namespace Arcadia.Core.Tests;

public class EpicCatalogTests
{
    // Shapes taken from a real catcache.bin, trimmed to the fields Arcadia reads.
    private const string Catalog = """
        [
          {
            "id": "item456", "namespace": "ns123", "title": "Loop Hero",
            "categories": [{ "path": "applications" }, { "path": "games" }],
            "mainGameItem": { "namespace": "", "id": "" },
            "releaseInfo": [{ "appId": "f7a0ebb44f93430fb1c4388a395eba96", "platform": ["Windows"] }]
          },
          {
            "id": "crabItem", "namespace": "crab", "title": "Satisfactory",
            "categories": [{ "path": "applications" }, { "path": "games" }, { "path": "public" }],
            "mainGameItem": { "namespace": "", "id": "" },
            "releaseInfo": [
              { "appId": "CrabMac", "platform": ["Mac"] },
              { "appId": "CrabEA", "platform": ["Windows"] }
            ]
          },
          {
            "id": "dlc1", "namespace": "ctrl", "title": "Control Expansion 2 AWE",
            "categories": [{ "path": "applications" }, { "path": "games" }],
            "mainGameItem": { "namespace": "ctrl", "id": "controlBase" },
            "releaseInfo": [{ "appId": "ControlAwe", "platform": ["Windows"] }]
          },
          {
            "id": "addon1", "namespace": "ns9", "title": "Some Add-on",
            "categories": [{ "path": "addons" }, { "path": "games" }],
            "releaseInfo": [{ "appId": "Addon", "platform": ["Windows"] }]
          },
          {
            "id": "engine", "namespace": "ue", "title": "Unreal Engine",
            "categories": [{ "path": "applications" }, { "path": "software" }],
            "releaseInfo": [{ "appId": "UE_5", "platform": ["Windows"] }]
          },
          {
            "id": "noApp", "namespace": "ns7", "title": "Bundle Without App",
            "categories": [{ "path": "games" }],
            "releaseInfo": []
          }
        ]
        """;

    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Reads_base_games_and_skips_dlc_addons_and_software()
    {
        var items = EpicCatalog.Parse(Encode(Catalog));

        Assert.Equal(["Loop Hero", "Satisfactory"], items.Select(i => i.Title));
    }

    [Fact]
    public void Prefers_the_windows_release()
    {
        var satisfactory = EpicCatalog.Parse(Encode(Catalog)).Single(i => i.Title == "Satisfactory");

        Assert.Equal(new EpicCatalogItem("crab", "crabItem", "CrabEA", "Satisfactory"), satisfactory);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64 !")]
    [InlineData("e30=")] // "{}" – not an array
    public void Unreadable_cache_yields_nothing(string content) => Assert.Empty(EpicCatalog.Parse(content));

    [Fact]
    public void Missing_cache_file_yields_nothing() =>
        Assert.Empty(EpicCatalog.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "catcache.bin")));

    [Theory]
    [InlineData("Rocket League?", "Rocket League")]
    [InlineData("Batman? Arkham Knight", "Batman Arkham Knight")]
    [InlineData("Fallout New Vegas?: Gun Runners? Arsenal?", "Fallout New Vegas: Gun Runners Arsenal")]
    [InlineData("Deus Ex?? Mankind Divided", "Deus Ex Mankind Divided")]
    [InlineData("Shadowrun: Dragonfall - Director?s Cut", "Shadowrun: Dragonfall - Director’s Cut")]
    [InlineData("Train Sim World? 4 Compatible: Bakerloo Line", "Train Sim World 4 Compatible: Bakerloo Line")]
    [InlineData("Schnellfahrstrecke K?ln-Aachen", "Schnellfahrstrecke K?ln-Aachen")]
    [InlineData("Astro Duel?2", "Astro Duel 2")]
    [InlineData("Loop Hero", "Loop Hero")]
    public void Repairs_titles_mangled_by_the_ascii_cache(string cached, string expected) =>
        Assert.Equal(expected, EpicCatalog.RepairTitle(cached));

    [Fact]
    public void Merge_adds_owned_games_as_not_installed()
    {
        var installed = EpicScanner.ParseManifest("""
            { "DisplayName": "Loop Hero", "AppName": "f7a0ebb44f93430fb1c4388a395eba96",
              "CatalogNamespace": "ns123", "CatalogItemId": "item456", "InstallLocation": "D:\\Epic Games\\LoopHero" }
            """)!;

        var games = EpicScanner.Merge([installed], EpicCatalog.Parse(Encode(Catalog)));

        Assert.Equal(2, games.Count);
        Assert.True(games.Single(g => g.Title == "Loop Hero").IsInstalled);

        var owned = games.Single(g => g.Title == "Satisfactory");
        Assert.False(owned.IsInstalled);
        Assert.Equal("CrabEA", owned.Id);
        Assert.Null(owned.InstallDirectory);
        Assert.Equal(new LaunchTarget.ProtocolUri("com.epicgames.launcher://apps/crab%3AcrabItem%3ACrabEA?action=install"), owned.Install);
    }

    [Fact]
    public void Merge_matches_installed_games_by_app_name_when_the_catalog_id_differs()
    {
        var installed = EpicScanner.ParseManifest("""
            { "DisplayName": "Satisfactory", "AppName": "CrabEA", "CatalogNamespace": "crab", "CatalogItemId": "otherItem" }
            """)!;

        var games = EpicScanner.Merge([installed], EpicCatalog.Parse(Encode(Catalog)));

        Assert.Single(games, g => g.Title == "Satisfactory");
    }

    [Fact]
    public void Not_installed_games_sort_last_by_install_date()
    {
        var installedAt = new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero);
        var installed = EpicScanner.ParseManifest("""
            { "DisplayName": "Zork", "AppName": "zork", "CatalogNamespace": "z", "CatalogItemId": "z1", "InstallLocation": "D:\\Zork" }
            """, _ => installedAt)!;
        var games = EpicScanner.Merge([installed], EpicCatalog.Parse(Encode(Catalog)));

        var sorted = GameSorter.Sort(games, new SortOrder(SortField.InstalledAt, SortDirection.Descending));

        Assert.Equal("Zork", sorted[0].Title);
        Assert.All(sorted.Skip(1), g => Assert.False(g.IsInstalled));
    }
}
