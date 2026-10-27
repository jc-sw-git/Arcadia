using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Epic;

namespace Arcadia.Core.Tests;

public class EpicScannerTests
{
    private const string Manifest = """
        {
          "FormatVersion": 0,
          "bIsIncompleteInstall": false,
          "DisplayName": "Loop Hero",
          "InstallLocation": "D:\\Epic Games\\LoopHero",
          "InstallSize": 219175976,
          "LaunchExecutable": "Loop Hero.exe",
          "CatalogNamespace": "ns123",
          "CatalogItemId": "item456",
          "AppName": "f7a0ebb44f93430fb1c4388a395eba96",
          "MainGameAppName": ""
        }
        """;

    [Fact]
    public void Parses_a_game_manifest()
    {
        var installedAt = new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero);

        var game = EpicScanner.ParseManifest(Manifest, _ => installedAt);

        Assert.NotNull(game);
        Assert.Equal("Loop Hero", game.Title);
        Assert.Equal("epic", game.LauncherId);
        Assert.Equal("f7a0ebb44f93430fb1c4388a395eba96", game.Id);
        Assert.Equal(219175976, game.InstallSizeBytes);
        Assert.Equal(@"D:\Epic Games\LoopHero", game.InstallDirectory);
        Assert.Equal(Path.Combine(@"D:\Epic Games\LoopHero", "Loop Hero.exe"), game.ExecutablePath);
        Assert.Equal(installedAt, game.InstalledAt);
        Assert.Equal(
            new LaunchTarget.ProtocolUri("com.epicgames.launcher://apps/ns123%3Aitem456%3Af7a0ebb44f93430fb1c4388a395eba96?action=launch&silent=true"),
            game.Launch);

        // Epic has no link to one game's page or to uninstalling it: both go through its library.
        Assert.Equal(new LaunchTarget.ProtocolUri("com.epicgames.launcher://store/library"), game.OpenInLauncher);
        Assert.Null(game.Uninstall);
    }

    [Fact]
    public void Skips_dlc_manifests()
    {
        var dlc = Manifest.Replace("\"MainGameAppName\": \"\"", "\"MainGameAppName\": \"basegame\"", StringComparison.Ordinal);

        Assert.Null(EpicScanner.ParseManifest(dlc));
    }

    [Fact]
    public void Skips_incomplete_installs()
    {
        var incomplete = Manifest.Replace("\"bIsIncompleteInstall\": false", "\"bIsIncompleteInstall\": true", StringComparison.Ordinal);

        Assert.Null(EpicScanner.ParseManifest(incomplete));
    }

    [Fact]
    public void Escapes_ids_in_launch_and_install_uris()
    {
        Assert.Equal(
            "com.epicgames.launcher://apps/ns%2F..%3Aitem%3Fx%3Dy%26z%3Aapp%23?action=launch&silent=true",
            EpicScanner.LaunchUri("ns/..", "item?x=y&z", "app#"));
        Assert.Equal(
            "com.epicgames.launcher://apps/ns%3Aitem%20456%3Aapp?action=install",
            EpicScanner.InstallUri("ns", "item 456", "app"));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{ \"AppName\": \"x\" }")]
    public void Rejects_invalid_manifests(string json)
    {
        Assert.Null(EpicScanner.ParseManifest(json));
    }

    [Theory]
    [InlineData("\"C:\\Program Files (x86)\\Epic Games\\Launcher\\Portal\\Binaries\\Win64\\EpicGamesLauncher.exe\" %1",
        @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe")]
    [InlineData(@"C:\Epic\EpicGamesLauncher.exe %1", @"C:\Epic\EpicGamesLauncher.exe")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Reads_launcher_path_from_protocol_command(string? command, string? expected)
    {
        Assert.Equal(expected, EpicLauncherClient.ParseExecutable(command));
    }

    [Fact]
    public async Task Missing_manifest_directory_and_catalog_yield_no_games()
    {
        var missing = Path.Combine(Path.GetTempPath(), "arcadia-does-not-exist-" + Guid.NewGuid());
        var scanner = new EpicScanner(missing, Path.Combine(missing, "catcache.bin"), missing, new EpicLastPlayedStore(Path.Combine(missing, "last-played.json")));

        Assert.Empty(await scanner.ScanAsync());
    }
    private static string EngineManifest(string appName, string version) => $$"""
        { "DisplayName": "Unreal Engine", "AppName": "{{appName}}", "AppVersionString": "{{version}}",
          "CatalogNamespace": "ue", "CatalogItemId": "engine{{appName}}", "InstallLocation": "C:\\UE\\{{appName}}",
          "LaunchExecutable": "Engine/Binaries/Win64/UnrealEditor.exe", "AppCategories": ["engines", "applications"] }
        """;

    [Fact]
    public void Reads_an_engine_version_as_the_unreal_engine_tool()
    {
        var game = EpicScanner.ParseManifest(EngineManifest("UE_5.4", "5.4.4-36161435+++UE5+Release-5.4"));

        Assert.NotNull(game);
        Assert.Equal("Unreal Engine", game.Title);
        Assert.Equal(GameType.Tool, game.Type);
    }

    [Fact]
    public void Keeps_a_launchable_addon_that_names_its_game()
    {
        var uefn = """
            { "DisplayName": "Unreal Editor for Fortnite", "AppName": "Fortnite_Studio", "MainGameAppName": "Fortnite",
              "CatalogNamespace": "fn", "CatalogItemId": "uefn", "AppCategories": ["addons", "addons/launchable", "applications"] }
            """;

        var game = EpicScanner.ParseManifest(uefn);

        Assert.NotNull(game);
        Assert.Equal(GameType.Tool, game.Type);
        Assert.Equal("Unreal Editor for Fortnite", game.Title);
    }

    [Fact]
    public void A_game_manifest_is_a_game()
    {
        Assert.Equal(GameType.Game, EpicScanner.ParseManifest(Manifest)!.Type);
    }

    [Theory]
    [InlineData("UE_5.4", "5.4.4-36161435+++UE5+Release-5.4", "5.4")]
    [InlineData("UE_4.27", null, "4.27")]
    [InlineData("UE_5", null, "5.0")]
    [InlineData("SomeEngineApp", "5.3.2-29314046+++UE5+Release-5.3", "5.3.2")]
    [InlineData("SomeEngineApp", "build", "0.0")]
    [InlineData("SomeEngineApp", null, "0.0")]
    public void Reads_the_engine_version_from_the_app_name_or_the_version_string(string appName, string? versionString, string expected)
    {
        Assert.Equal(Version.Parse(expected), EpicScanner.EngineVersionOf(appName, versionString));
    }

    [Fact]
    public async Task Shows_all_installed_engine_versions_as_one_entry_that_starts_the_newest()
    {
        var folder = Directory.CreateTempSubdirectory("arcadia-epic-").FullName;
        try
        {
            var manifests = Directory.CreateDirectory(Path.Combine(folder, "Manifests")).FullName;
            File.WriteAllText(Path.Combine(manifests, "a.item"), EngineManifest("UE_4.27", "4.27.2-18319896+++UE4+Release-4.27"));
            File.WriteAllText(Path.Combine(manifests, "b.item"), EngineManifest("UE_5.4", "5.4.4-36161435+++UE5+Release-5.4"));
            File.WriteAllText(Path.Combine(manifests, "c.item"), EngineManifest("UE_5.3", "5.3.2-29314046+++UE5+Release-5.3"));
            File.WriteAllText(Path.Combine(manifests, "d.item"), Manifest);
            var scanner = new EpicScanner(manifests, Path.Combine(folder, "catcache.bin"), folder, new EpicLastPlayedStore(Path.Combine(folder, "last-played.json")));

            var games = await scanner.ScanAsync();

            var engine = Assert.Single(games, g => g.Type == GameType.Tool);
            Assert.Equal("UE_5.4", engine.Id);
            Assert.Equal("Unreal Engine", engine.Title);
            Assert.Single(games, g => g.Title == "Loop Hero");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Merge_adds_no_catalog_engine_beside_an_installed_one()
    {
        var installed = EpicScanner.ParseManifest(EngineManifest("UE_5.4", "5.4.4"))!;
        EpicCatalogItem[] catalog =
        [
            new("ue", "engineA", "UE_5.3", "Unreal Engine", GameType.Tool, IsEngine: true),
            new("ue", "engineB", "UE_5.5", "Unreal Engine", GameType.Tool, IsEngine: true),
        ];

        var games = EpicScanner.Merge([installed], catalog);

        Assert.Equal(["UE_5.4"], games.Select(g => g.Id));
    }

    [Fact]
    public void Merge_offers_the_newest_catalog_engine_when_none_is_installed()
    {
        EpicCatalogItem[] catalog =
        [
            new("ue", "engineA", "UE_5.3", "Unreal Engine", GameType.Tool, IsEngine: true),
            new("ue", "engineB", "UE_5.5", "Unreal Engine", GameType.Tool, IsEngine: true),
            new("fn", "uefn", "Fortnite_Studio", "Unreal Editor for Fortnite", GameType.Tool),
        ];

        var games = EpicScanner.Merge([], catalog);

        Assert.Equal(2, games.Count);
        Assert.All(games, g => Assert.Equal(GameType.Tool, g.Type));
        Assert.All(games, g => Assert.False(g.IsInstalled));
        Assert.Equal("UE_5.5", Assert.Single(games, g => g.Title == "Unreal Engine").Id);
    }
}
