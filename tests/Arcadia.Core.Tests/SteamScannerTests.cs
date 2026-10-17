using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

public sealed class SteamScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-steam-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static string Manifest(string appId, string name, string installDir = "Game", int stateFlags = 4, string buildId = "22479045", string lastPlayed = "1775679163") => $$"""
        "AppState"
        {
        	"appid"		"{{appId}}"
        	"Universe"		"1"
        	"name"		"{{name}}"
        	"StateFlags"		"{{stateFlags}}"
        	"installdir"		"{{installDir}}"
        	"LastUpdated"		"1775526912"
        	"LastPlayed"		"{{lastPlayed}}"
        	"SizeOnDisk"		"8207380004"
        	"buildid"		"{{buildId}}"
        	"UserConfig"
        	{
        		"language"		"english"
        	}
        }
        """;

    [Fact]
    public void Parses_an_app_manifest()
    {
        var library = Path.Combine(_root, "Lib");
        var installedAt = new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero);

        var game = SteamScanner.ParseManifest(Manifest("1030300", "Hollow Knight: Silksong", "Hollow Knight Silksong"), library, _ => installedAt);

        Assert.NotNull(game);
        Assert.Equal("1030300", game.Id);
        Assert.Equal("steam", game.LauncherId);
        Assert.Equal("Hollow Knight: Silksong", game.Title);
        Assert.Equal(new LaunchTarget.ProtocolUri("steam://rungameid/1030300"), game.Launch);
        Assert.Equal(new LaunchTarget.ProtocolUri("steam://nav/games/details/1030300"), game.OpenInLauncher);
        Assert.Equal(new LaunchTarget.ProtocolUri("steam://uninstall/1030300"), game.Uninstall);
        Assert.Equal(new LaunchTarget.ProtocolUri("steam://controllerconfig/1030300"), game.ControllerLayout);
        Assert.Equal(Path.Combine(library, "steamapps", "common", "Hollow Knight Silksong"), game.InstallDirectory);
        Assert.Equal(8207380004, game.InstallSizeBytes);
        Assert.Equal(installedAt, game.InstalledAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1775679163), game.LastPlayedAt);
        Assert.False(game.UpdateAvailable);
        Assert.True(game.IsInstalled);
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(6, true)]
    [InlineData(68, false)]
    [InlineData(70, true)]
    public void Pending_update_is_state_flag_two(int stateFlags, bool updateAvailable)
    {
        var game = SteamScanner.ParseManifest(Manifest("960090", "Bloons TD 6", stateFlags: stateFlags), _root);

        Assert.Equal(updateAvailable, game?.UpdateAvailable);
    }

    [Fact]
    public void Keeps_a_game_while_it_updates_but_not_a_first_install()
    {
        Assert.NotNull(SteamScanner.ParseManifest(Manifest("960090", "Bloons TD 6", stateFlags: 1026), _root));
        Assert.Null(SteamScanner.ParseManifest(Manifest("960090", "Bloons TD 6", stateFlags: 1026, buildId: "0"), _root));
    }

    [Fact]
    public void Never_played_has_no_last_played_date()
    {
        Assert.Null(SteamScanner.ParseManifest(Manifest("239030", "Papers, Please", lastPlayed: "0"), _root)?.LastPlayedAt);
    }

    [Theory]
    [InlineData("228980", "Steamworks Common Redistributables")]
    [InlineData("243730", "Source SDK Base 2013 Singleplayer")]
    [InlineData("1628350", "Steam Linux Runtime 3.0 (sniper)")]
    [InlineData("2348590", "Proton 8.0")]
    [InlineData("3637470", "RuneScape: Dragonwilds Early Adopter Soundtrack")]
    public void Leaves_out_tools(string appId, string name)
    {
        Assert.Null(SteamScanner.ParseManifest(Manifest(appId, name), _root));
    }

    [Theory]
    [InlineData("Vampire Hunter: Nightrise Demo")]
    [InlineData("Half-Life 2")]
    [InlineData("Protonaut")]
    public void Keeps_games_and_demos(string name)
    {
        Assert.NotNull(SteamScanner.ParseManifest(Manifest("4353420", name), _root));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("..\\\\..\\\\Windows")]
    [InlineData("../../Windows")]
    [InlineData("C:\\\\Windows")]
    [InlineData("")]
    public void Rejects_install_dirs_outside_the_library(string installDir)
    {
        Assert.Null(SteamScanner.ParseManifest(Manifest("220", "Half-Life 2", installDir), _root));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("220/../x")]
    public void Rejects_app_ids_that_are_no_numbers(string appId)
    {
        Assert.Null(SteamScanner.ParseManifest(Manifest(appId, "Half-Life 2"), _root));
    }

    [Fact]
    public void Ignores_broken_manifests()
    {
        Assert.Null(SteamScanner.ParseManifest("\"AppState\" { \"appid\" ", _root));
        Assert.Null(SteamScanner.ParseManifest(null, _root));
        Assert.Null(SteamScanner.ParseManifest("\"Other\" { }", _root));
    }

    [Fact]
    public void Reads_all_libraries_without_duplicates()
    {
        var steam = Path.Combine(_root, "Steam");
        var second = Path.Combine(_root, "SteamLibrary");
        var vdf = $$"""
            "libraryfolders"
            {
            	"0"
            	{
            		"path"		"{{Escape(steam)}}"
            		"apps" { "228980" "591490404" }
            	}
            	"1"
            	{
            		"path"		"{{Escape(second.Replace('\\', '/') + "/")}}"
            	}
            	"2"
            	{
            		"path"		"{{Escape(second)}}"
            	}
            }
            """;

        Assert.Equal([steam, second], SteamScanner.LibraryPaths(steam, vdf));
        Assert.Equal([steam], SteamScanner.LibraryPaths(steam, null));
        Assert.Equal([steam], SteamScanner.LibraryPaths(steam, "\"libraryfolders\" {"));
    }

    [Fact]
    public void Reads_playtime_and_last_played_from_localconfig()
    {
        var stats = SteamScanner.ReadPlayStats("""
            "UserLocalConfigStore"
            {
            	"Software"
            	{
            		"valve"
            		{
            			"Steam"
            			{
            				"apps"
            				{
            					"960090"
            					{
            						"LastPlayed"		"1775672229"
            						"Playtime"		"3466"
            					}
            					"220"
            					{
            						"cloud" { "last_sync_state" "synchronized" }
            					}
            				}
            			}
            		}
            	}
            	"apps"
            	{
            		"960090" "0123456789abcdef"
            	}
            }
            """);

        Assert.Equal(new SteamPlayStats(DateTimeOffset.FromUnixTimeSeconds(1775672229), TimeSpan.FromMinutes(3466)), stats[960090]);
        Assert.Equal(new SteamPlayStats(null, null), stats[220]);
        Assert.Empty(SteamScanner.ReadPlayStats(null));
        Assert.Empty(SteamScanner.ReadPlayStats("broken {"));
    }

    [Fact]
    public async Task Scans_every_library_and_adds_play_stats()
    {
        var steam = Path.Combine(_root, "Steam");
        var second = Path.Combine(_root, "SteamLibrary");
        Write(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), $$"""
            "libraryfolders"
            {
            	"0" { "path" "{{Escape(steam)}}" }
            	"1" { "path" "{{Escape(second)}}" }
            	"2" { "path" "{{Escape(Path.Combine(_root, "Unplugged"))}}" }
            }
            """);
        Write(Path.Combine(steam, "steamapps", "appmanifest_228980.acf"), Manifest("228980", "Steamworks Common Redistributables"));
        Write(Path.Combine(steam, "steamapps", "appmanifest_239030.acf"), Manifest("239030", "Papers, Please", "PapersPlease"));
        Write(Path.Combine(second, "steamapps", "appmanifest_960090.acf"), Manifest("960090", "Bloons TD 6", "BloonsTD6", stateFlags: 6));
        Write(Path.Combine(second, "steamapps", "appmanifest_broken.acf"), "\"AppState\" {");
        Directory.CreateDirectory(Path.Combine(second, "steamapps", "common", "BloonsTD6"));
        Write(Path.Combine(steam, "userdata", "7266204", "config", "localconfig.vdf"), """
            "UserLocalConfigStore" { "Software" { "Valve" { "Steam" { "apps" {
            	"960090" { "LastPlayed" "1775672229" "Playtime" "3466" }
            } } } } }
            """);

        var scanner = new SteamScanner(() => new SteamInstallation(steam, 7266204));
        var games = (await scanner.ScanAsync()).OrderBy(g => g.Title).ToList();

        Assert.Equal(["Bloons TD 6", "Papers, Please"], games.Select(g => g.Title));
        var bloons = games[0];
        Assert.True(bloons.UpdateAvailable);
        Assert.Equal(TimeSpan.FromMinutes(3466), bloons.Playtime);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1775672229), bloons.LastPlayedAt);
        Assert.NotNull(bloons.InstalledAt);
        Assert.Null(games[1].Playtime);
        Assert.Null(games[1].InstalledAt);
    }

    [Fact]
    public async Task Without_steam_the_library_is_empty()
    {
        Assert.Empty(await new SteamScanner(() => null).ScanAsync());
        Assert.Empty(await new SteamScanner(() => new SteamInstallation(Path.Combine(_root, "missing"), null)).ScanAsync());
    }

    private static string Escape(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
