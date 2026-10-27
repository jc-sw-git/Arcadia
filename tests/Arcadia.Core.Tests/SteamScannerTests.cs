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

    private static string Manifest(string appId, string name, string installDir = "Game", int stateFlags = 4, string buildId = "22479045", string lastPlayed = "1775679163", string lastOwner = "76561197960278073") => $$"""
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
        	"LastOwner"		"{{lastOwner}}"
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

    [Theory]
    [InlineData("380", "Half-Life 2: Episode One", 68)]
    [InlineData("1894430", "Plan B: Terraform", 70)]
    public void Leaves_out_apps_nobody_owns(string appId, string name, int stateFlags)
    {
        Assert.Null(SteamScanner.ParseManifest(Manifest(appId, name, stateFlags: stateFlags, lastOwner: "0"), _root));
    }

    [Fact]
    public void Keeps_an_app_whose_manifest_names_no_owner()
    {
        var manifest = Manifest("220", "Half-Life 2").Replace("\"LastOwner\"\t\t\"76561197960278073\"", "", StringComparison.Ordinal);

        Assert.DoesNotContain("LastOwner", manifest, StringComparison.Ordinal);
        Assert.NotNull(SteamScanner.ParseManifest(manifest, _root));
    }

    [Fact]
    public void Never_played_has_no_last_played_date()
    {
        Assert.Null(SteamScanner.ParseManifest(Manifest("239030", "Papers, Please", lastPlayed: "0"), _root)?.LastPlayedAt);
    }

    [Theory]
    [InlineData("228980", "Steamworks Common Redistributables")]
    [InlineData("1628350", "Steam Linux Runtime 3.0 (sniper)")]
    [InlineData("2348590", "Proton 8.0")]
    public void Leaves_out_runtimes_and_redistributables(string appId, string name)
    {
        Assert.Null(SteamScanner.ParseManifest(Manifest(appId, name), _root));
    }

    [Theory]
    [InlineData("228980", "Steamworks Common Redistributables", "Tool")]
    [InlineData("1628350", "Steam Linux Runtime 3.0 (sniper)", "Tool")]
    public void Leaves_out_runtimes_whatever_type_Steam_gives_them(string appId, string name, string steamType)
    {
        var types = new Dictionary<uint, string> { [uint.Parse(appId)] = steamType };

        Assert.Null(SteamScanner.ParseManifest(Manifest(appId, name), _root, appTypes: types));
    }

    [Theory]
    [InlineData("243730", "Source SDK Base 2013 Singleplayer", GameType.Tool)]
    [InlineData("250820", "SteamVR", GameType.Tool)]
    [InlineData("3637470", "RuneScape: Dragonwilds Early Adopter Soundtrack", GameType.Soundtrack)]
    [InlineData("431960", "Wallpaper Engine", GameType.Game)]
    [InlineData("4353420", "Vampire Hunter: Nightrise Demo", GameType.Game)]
    public void Types_apps_by_id_and_name_without_appinfo(string appId, string name, GameType expected)
    {
        Assert.Equal(expected, SteamScanner.ParseManifest(Manifest(appId, name), _root)?.Type);
    }

    [Theory]
    [InlineData("Game", GameType.Game)]
    [InlineData("game", GameType.Game)]
    [InlineData("Demo", GameType.Game)]
    [InlineData("Application", GameType.Software)]
    [InlineData("Tool", GameType.Tool)]
    [InlineData("Music", GameType.Soundtrack)]
    public void Types_apps_by_their_type_in_appinfo(string steamType, GameType expected)
    {
        var types = new Dictionary<uint, string> { [431960] = steamType };

        Assert.Equal(expected, SteamScanner.ParseManifest(Manifest("431960", "Wallpaper Engine"), _root, appTypes: types)?.Type);
    }

    [Theory]
    [InlineData("DLC")]
    [InlineData("Video")]
    [InlineData("Config")]
    [InlineData("")]
    public void Leaves_out_types_no_library_category_shows(string steamType)
    {
        var types = new Dictionary<uint, string> { [431960] = steamType };

        Assert.Null(SteamScanner.ParseManifest(Manifest("431960", "Something"), _root, appTypes: types));
    }

    [Fact]
    public void Appinfo_wins_over_the_name()
    {
        // A game whose name ends in "Soundtrack" is still a game when Steam says so.
        var types = new Dictionary<uint, string> { [1145360] = "Game" };

        Assert.Equal(GameType.Game, SteamScanner.ParseManifest(Manifest("1145360", "Hades Soundtrack"), _root, appTypes: types)?.Type);
    }

    [Fact]
    public void Finds_a_soundtrack_in_the_music_folder()
    {
        var library = Path.Combine(_root, "Lib");
        var music = Directory.CreateDirectory(Path.Combine(library, "steamapps", "music", "Hades II Soundtrack")).FullName;
        var types = new Dictionary<uint, string> { [1145350] = "Music" };

        var soundtrack = SteamScanner.ParseManifest(Manifest("1145350", "Hades II Original Soundtrack", "Hades II Soundtrack"), library, appTypes: types);

        Assert.Equal(music, soundtrack?.InstallDirectory);
    }

    [Fact]
    public void Keeps_a_soundtrack_in_common_when_it_is_there()
    {
        var library = Path.Combine(_root, "Lib");
        Directory.CreateDirectory(Path.Combine(library, "steamapps", "music", "OST"));
        var common = Directory.CreateDirectory(Path.Combine(library, "steamapps", "common", "OST")).FullName;

        var soundtrack = SteamScanner.ParseManifest(Manifest("1145350", "Game Soundtrack", "OST"), library);

        Assert.Equal(common, soundtrack?.InstallDirectory);
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
        Write(Path.Combine(steam, "userdata", "12345", "config", "localconfig.vdf"), """
            "UserLocalConfigStore" { "Software" { "Valve" { "Steam" { "apps" {
            	"960090" { "LastPlayed" "1775672229" "Playtime" "3466" }
            } } } } }
            """);

        var scanner = new SteamScanner(() => new SteamInstallation(steam, 12345));
        var games = (await scanner.ScanAsync()).OrderBy(g => g.Title).ToList();

        Assert.Equal(["Bloons TD 6", "Papers, Please"], games.Select(g => g.Title));
        var bloons = games[0];
        Assert.True(bloons.UpdateAvailable);
        Assert.Equal(TimeSpan.FromMinutes(3466), bloons.Playtime);

        // The manifest's last start (1775679163) is later than the account's; the later one counts.
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1775679163), bloons.LastPlayedAt);
        Assert.NotNull(bloons.InstalledAt);
        Assert.Null(games[1].Playtime);
        Assert.Null(games[1].InstalledAt);
    }

    [Fact]
    public async Task Types_the_installed_apps_from_appinfo()
    {
        var steam = Path.Combine(_root, "Steam");
        Write(Path.Combine(steam, "steamapps", "appmanifest_239030.acf"), Manifest("239030", "Papers, Please", "PapersPlease"));
        Write(Path.Combine(steam, "steamapps", "appmanifest_431960.acf"), Manifest("431960", "Wallpaper Engine", "wallpaper_engine"));
        Write(Path.Combine(steam, "steamapps", "appmanifest_250820.acf"), Manifest("250820", "SteamVR", "SteamVR"));
        Write(Path.Combine(steam, "steamapps", "appmanifest_1145350.acf"), Manifest("1145350", "Hades II Soundtrack", "Hades II Soundtrack"));
        Write(Path.Combine(steam, "steamapps", "appmanifest_1145351.acf"), Manifest("1145351", "Hades II Artbook", "Hades II Artbook"));
        WriteBytes(Path.Combine(steam, "appcache", "appinfo.vdf"), AppInfoFile.Build(AppInfoFile.V29,
        [
            (239030, "Papers, Please", "Game"),
            (431960, "Wallpaper Engine", "Application"),
            (250820, "SteamVR", "Tool"),
            (1145350, "Hades II Soundtrack", "Music"),
            (1145351, "Hades II Artbook", "Video"),
        ]));

        var games = await new SteamScanner(() => new SteamInstallation(steam, null)).ScanAsync();

        Assert.Equal(
            [("Hades II Soundtrack", GameType.Soundtrack), ("Papers, Please", GameType.Game), ("SteamVR", GameType.Tool), ("Wallpaper Engine", GameType.Software)],
            games.OrderBy(g => g.Title).Select(g => (g.Title, g.Type)));
    }

    [Fact]
    public async Task Without_a_readable_appinfo_types_apps_by_name()
    {
        var steam = Path.Combine(_root, "Steam");
        Write(Path.Combine(steam, "steamapps", "appmanifest_431960.acf"), Manifest("431960", "Wallpaper Engine", "wallpaper_engine"));
        Write(Path.Combine(steam, "steamapps", "appmanifest_1145350.acf"), Manifest("1145350", "Hades II Soundtrack", "Hades II Soundtrack"));
        Write(Path.Combine(steam, "appcache", "appinfo.vdf"), "not a cache");

        var games = await new SteamScanner(() => new SteamInstallation(steam, null)).ScanAsync();

        Assert.Equal(
            [("Hades II Soundtrack", GameType.Soundtrack), ("Wallpaper Engine", GameType.Game)],
            games.OrderBy(g => g.Title).Select(g => (g.Title, g.Type)));
    }

    [Fact]
    public async Task Reads_appinfo_again_for_a_new_install()
    {
        var steam = Path.Combine(_root, "Steam");
        var appInfo = Path.Combine(steam, "appcache", "appinfo.vdf");
        Write(Path.Combine(steam, "steamapps", "appmanifest_239030.acf"), Manifest("239030", "Papers, Please", "PapersPlease"));
        WriteBytes(appInfo, AppInfoFile.Build(AppInfoFile.V29, [(239030, "Papers, Please", "Game"), (431960, "Wallpaper Engine", "Application")]));
        var written = File.GetLastWriteTimeUtc(appInfo);
        var scanner = new SteamScanner(() => new SteamInstallation(steam, null));
        await scanner.ScanAsync();

        // Steam wrote the app's type before the install; the file itself is unchanged.
        Write(Path.Combine(steam, "steamapps", "appmanifest_431960.acf"), Manifest("431960", "Wallpaper Engine", "wallpaper_engine"));
        File.SetLastWriteTimeUtc(appInfo, written);
        var games = await scanner.ScanAsync();

        Assert.Equal(GameType.Software, games.Single(g => g.Id == "431960").Type);
    }

    [Fact]
    public async Task Without_steam_the_library_is_empty()
    {
        Assert.Empty(await new SteamScanner(() => null).ScanAsync());
        Assert.Empty(await new SteamScanner(() => new SteamInstallation(Path.Combine(_root, "missing"), null)).ScanAsync());
    }

    private static void WriteBytes(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static string Escape(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
