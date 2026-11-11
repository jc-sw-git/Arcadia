using Arcadia.App.Input;
using Arcadia.App.Tests.Fixtures;
using Arcadia.App.ViewModels;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #487: Steam games show their real cover and background in the library tile, the preview, the detail page and the search,
/// read from recreated Steam folders with made-up AppIDs and images; Steam's image server is the fake one of the test.
/// </summary>
public sealed class ArtworkTests
{
    private const string ServerHost = "https://shared.steamstatic.com/store_item_assets/steam/apps/";

    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T487-1")]
    public async Task Steams_cached_images_show_at_all_four_places_without_a_request()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(new SteamApp("4100001", "Leuchtturm"));
        steam.Write(new SteamApp("4100002", "Salzwiesen"));
        TestImages.Write(Path.Combine(steam.Root, "appcache", "librarycache", "4100001", "library_600x900.jpg"), TestImages.Png(600, 900));
        TestImages.Write(Path.Combine(steam.Root, "appcache", "librarycache", "4100001", "library_hero.jpg"), TestImages.Png(3840, 1240));
        files.ImageServer.Offline = true;

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await ArtworkSettledAsync(arcadia);

        var tile = Assert.Single(arcadia.PageGames, t => t.Game.Id == "4100001");
        AssertShows(tile, "4100001", "Bibliothek");
        // The preview shows the focused game's background; the first by name is focused.
        Assert.Same(tile, arcadia.Main.Library.FocusedGame);
        Assert.Equal(tile.Game.Key, arcadia.Main.Library.FocusedGame?.Hero?.GameKey);

        arcadia.Hold(UiAction.Accept, PastHoldTime);
        AssertShows(arcadia.Detail!.Current.Tile, "4100001", "Detailseite");

        arcadia.Press(UiAction.Back).Press(UiAction.Search);
        arcadia.Main.TypeInSearch("Leucht");
        AssertShows(Assert.Single(arcadia.Search!.Results), "4100001", "Suche");

        // The other game has none: its placeholder stays. Nothing asked Steam's image server for the first one.
        Assert.Null(arcadia.Artwork.Of(Assert.Single(arcadia.PageGames, t => t.Game.Id == "4100002").Game.Key)?.Cover);
        Assert.DoesNotContain(files.ImageServer.Requests, request => request.RequestUri!.AbsoluteUri.Contains("/4100001/", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T487-4")]
    public async Task Images_from_steams_server_are_cached_and_still_shown_offline_after_a_restart()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(new SteamApp("4100003", "Nebelbank"));
        files.ImageServer.Serve(ServerHost + "4100003/library_600x900.jpg", TestImages.Png(600, 900));
        files.ImageServer.Serve(ServerHost + "4100003/library_hero.jpg", TestImages.Png(1920, 620));

        using (var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files))
        {
            await ArtworkSettledAsync(arcadia);
            AssertShows(Assert.Single(arcadia.PageGames), "4100003", "Bibliothek");

            // Only the image's address: no key, no cookie, no account.
            Assert.All(files.ImageServer.Requests, request =>
            {
                Assert.Empty(request.RequestUri!.Query);
                Assert.False(request.Headers.Contains("Cookie"), "Ein Cookie geht an den Bildserver.");
                Assert.Null(request.Headers.Authorization);
            });

            files.ImageServer.Offline = true;
            var asked = files.ImageServer.Requests.Count;
            using var restarted = await arcadia.RestartAsync();
            await ArtworkSettledAsync(restarted);

            AssertShows(Assert.Single(restarted.PageGames), "4100003", "Bibliothek nach dem Neustart");
            Assert.Equal(asked, files.ImageServer.Requests.Count);
        }
    }

    /// <summary>
    /// T487-17 and T487-25 as the player does it: an own cover set in Steam shows after "Neu einlesen", although the library
    /// itself didn't change.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T487-17")]
    public async Task An_own_cover_set_in_steam_shows_after_a_rescan()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(new SteamApp("4100004", "Kreidefelsen"));
        TestImages.Write(Path.Combine(steam.Root, "appcache", "librarycache", "4100004", "library_600x900.jpg"), TestImages.Png(600, 900));
        files.ImageServer.Offline = true;
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await ArtworkSettledAsync(arcadia);
        var before = Assert.Single(arcadia.PageGames).Cover;
        Assert.NotNull(before);

        TestImages.Write(Path.Combine(steam.UserData(SteamFolder.Account), "config", "grid", "4100004p.png"), TestImages.Png(400, 600));
        await arcadia.Main.Session.RescanAsync(null);
        await ArtworkSettledAsync(arcadia);

        var after = Assert.Single(arcadia.PageGames).Cover;
        Assert.NotNull(after);
        Assert.Equal((400, 600), (after.Size.Width, after.Size.Height));
    }

    /// <summary>T487-5 as the player meets it: Arcadia started before the network was up gets the images at the next rescan.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T487-5")]
    public async Task Images_missed_without_network_come_with_the_next_rescan()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(new SteamApp("4100005", "Dünenmeer"));
        files.ImageServer.Serve(ServerHost + "4100005/library_600x900.jpg", TestImages.Png(600, 900));
        files.ImageServer.Offline = true;
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await ArtworkSettledAsync(arcadia);
        Assert.Null(Assert.Single(arcadia.PageGames).Cover);

        files.ImageServer.Offline = false;
        await arcadia.Main.Session.RescanAsync(null);
        await ArtworkSettledAsync(arcadia);

        Assert.NotNull(Assert.Single(arcadia.PageGames).Cover);
    }

    private static void AssertShows(GameTileViewModel tile, string appId, string where)
    {
        Assert.True(tile.Cover is { } cover && cover.GameKey == tile.Game.Key && tile.Game.Id == appId, $"{where}: kein Cover von {appId}.");
        Assert.True(File.Exists(tile.Cover!.Path), $"{where}: das Cover liegt nicht im Cache.");
        Assert.True(tile.Hero is { } hero && hero.GameKey == tile.Game.Key, $"{where}: kein Hintergrund von {appId}.");
    }

    /// <summary>The artwork run after the scan has ended and the views took what it changed.</summary>
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
