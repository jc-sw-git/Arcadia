using Arcadia.Core.Models;
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
    public async Task Missing_manifest_directory_yields_no_games()
    {
        var scanner = new EpicScanner(Path.Combine(Path.GetTempPath(), "arcadia-does-not-exist-" + Guid.NewGuid()));

        Assert.Empty(await scanner.ScanAsync());
    }
}
