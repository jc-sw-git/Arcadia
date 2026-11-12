using Arcadia.App.Input;
using Arcadia.App.Tests.Fixtures;
using Arcadia.Core.Tests.Artwork;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #506 part B as the player meets it, with the real Steam and Epic integrations reading recreated folders: a rescan
/// (window active again, a game ended, an install finished) sends nothing to SteamGridDB, Steam's image server or Epic's
/// image addresses; only a game new to the library is searched, silently. Made-up ids, names and addresses.
/// </summary>
public sealed class ArtworkSearchTriggerAcceptanceTests
{
    private const string Known = "4300001";
    private const string Arriving = "4300002";
    private const string ArrivingCover = "https://cdn.example/grid/4300002.png";

    private static readonly string[] Game = ["games", "applications"];

    private static int NetworkRequests(LauncherFiles files, ArcadiaDriver arcadia)
    {
        lock (files.ImageServer.Requests)
        {
            return files.ImageServer.Requests.Count + arcadia.SteamGridDb.Count;
        }
    }

    private static void ClearRequests(LauncherFiles files, ArcadiaDriver arcadia)
    {
        lock (files.ImageServer.Requests)
        {
            files.ImageServer.Requests.Clear();
        }

        lock (arcadia.SteamGridDb.Requests)
        {
            arcadia.SteamGridDb.Requests.Clear();
        }
    }

    /// <summary>A Steam game without cached images (Steam's server answers 404 for it) and an Epic game whose catalog image doesn't load.</summary>
    private static async Task<ArcadiaDriver> StartWithGamesMissingImagesAsync(LauncherFiles files)
    {
        var steam = files.Steam.Install();
        steam.Write(new SteamApp(Known, "Leuchtturm"));
        var epic = new EpicApp("ee41", "Salzwiesen")
        {
            KeyImages = [("DieselGameBoxTall", "https://cdn1.epicgames.com/offer/nsee41/tall-ee41_1200x1600.jpg")],
        };
        files.Epic.Install().WriteCatalog((epic, Game, null));

        var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await ArtworkSettledAsync(arcadia);
        await arcadia.Artwork.SteamGridDb!.SetAsync(FakeSteamGridDb.Key);
        Assert.True(arcadia.Artwork.SteamGridDb.IsUsable);
        await ArtworkSettledAsync(arcadia);
        ClearRequests(files, arcadia);
        return arcadia;
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-41")]
    public async Task Rescans_without_a_new_game_send_nothing_to_steamgriddb_or_the_launchers_image_servers()
    {
        using var files = new LauncherFiles();
        using var arcadia = await StartWithGamesMissingImagesAsync(files);
        arcadia.SteamGridDb.Cover("steam", Known, "https://cdn.example/grid/4300001.png", 600, 900);
        files.ImageServer.Serve("https://cdn.example/grid/4300001.png", TestImages.Png(600, 900));
        Assert.All(arcadia.PageGames, tile => Assert.Null(tile.Cover));

        // Window active again, a game ended / an install finished (both a forced refresh), "Neu einlesen".
        await arcadia.ActivateWindowAsync();
        await ArtworkSettledAsync(arcadia);
        await arcadia.RescanAsync();
        await ArtworkSettledAsync(arcadia);
        Assert.Null(arcadia.Main.Toast.Text);
        Assert.Null(arcadia.Artwork.Search.Running);
        await arcadia.Main.Session.RescanAsync(null);
        await ArtworkSettledAsync(arcadia);

        Assert.Equal(0, NetworkRequests(files, arcadia));

        // Local launcher files are still read at every scan: an image Steam cached meanwhile shows at once.
        TestImages.Write(Path.Combine(files.Steam.Root, "appcache", "librarycache", Known, "library_600x900.jpg"), TestImages.Png(600, 900));
        await arcadia.RescanAsync();
        await ArtworkSettledAsync(arcadia);
        Assert.NotNull(Assert.Single(arcadia.PageGames, tile => tile.Game.Id == Known).Cover);
        Assert.Equal(0, NetworkRequests(files, arcadia));

        // The gate is what kept the network quiet: "Jetzt suchen" asks for the same games.
        Assert.True(arcadia.Artwork.SearchAgain(byPlayer: true));
        await ArtworkSettledAsync(arcadia);
        Assert.NotEqual(0, arcadia.SteamGridDb.Count);
        Assert.NotEmpty(files.ImageServer.Requests);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-40")]
    public async Task A_game_new_to_the_library_is_searched_alone_and_silently_without_moving_the_focus()
    {
        using var files = new LauncherFiles();
        using var arcadia = await StartWithGamesMissingImagesAsync(files);
        arcadia.SteamGridDb.Cover("steam", Arriving, ArrivingCover, 600, 900).Knows("heroes", "steam", Arriving);
        files.ImageServer.Serve(ArrivingCover, TestImages.Png(600, 900));
        arcadia.Press(UiAction.Right);
        var focused = arcadia.FocusedGame!;

        files.Steam.Write(new SteamApp(Arriving, "Nebelbank"));
        await arcadia.RescanAsync();
        await ArtworkSettledAsync(arcadia);

        Assert.NotEmpty(arcadia.SteamGridDb.Asked());
        Assert.All(arcadia.SteamGridDb.Asked(), path => Assert.EndsWith("/steam/" + Arriving, path, StringComparison.Ordinal));
        lock (files.ImageServer.Requests)
        {
            Assert.All(files.ImageServer.Requests, request => Assert.Contains(Arriving, request.RequestUri!.AbsoluteUri, StringComparison.Ordinal));
        }

        var arrived = Assert.Single(arcadia.PageGames, tile => tile.Game.Id == Arriving);
        Assert.Equal((600, 900), (arrived.Cover!.Size.Width, arrived.Cover.Size.Height));
        Assert.Equal(focused.Game.Key, arcadia.FocusedGame!.Game.Key);
        Assert.Null(arcadia.Main.Toast.Text);
        Assert.Null(arcadia.Artwork.Search.Running);

        // Searched once: the next rescans leave it alone.
        ClearRequests(files, arcadia);
        await arcadia.ActivateWindowAsync();
        await ArtworkSettledAsync(arcadia);
        Assert.Equal(0, NetworkRequests(files, arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-70")]
    public async Task The_first_start_of_this_version_on_an_existing_library_searches_nothing()
    {
        using var files = new LauncherFiles();
        using (var before = await StartWithGamesMissingImagesAsync(files))
        {
            // An earlier version: its cache, but no list of known games.
            TestImages.Write(Path.Combine(files.Steam.Root, "appcache", "librarycache", Known, "library_hero.jpg"), TestImages.Png(3840, 1240));
            await before.RescanAsync();
            await ArtworkSettledAsync(before);
            var list = Path.Combine(before.Folder, "artwork", "known-games.json");
            Assert.True(File.Exists(list));
            File.Delete(list);
            ClearRequests(files, before);

            using var upgraded = await before.RestartAsync();
            await ArtworkSettledAsync(upgraded);

            Assert.Equal(0, NetworkRequests(files, upgraded));
            Assert.True(File.Exists(list));
            Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(list)!, "*.tmp"));

            // A damaged list: one line in the log, nothing searched, Arcadia keeps running.
            File.WriteAllText(list, "{ kaputt");
            using var damaged = await upgraded.RestartAsync();
            await ArtworkSettledAsync(damaged);
            Assert.Equal(0, NetworkRequests(files, damaged));
            Assert.NotEmpty(damaged.PageGames);
        }
    }

    private static async Task ArtworkSettledAsync(ArcadiaDriver arcadia)
    {
        for (var round = 0; round < 3; round++)
        {
            Dispatcher.UIThread.RunJobs();
            await arcadia.Artwork.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Dispatcher.UIThread.RunJobs();
        }
    }
}
