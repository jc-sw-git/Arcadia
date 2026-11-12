using Arcadia.App.Input;
using Arcadia.App.Tests.Fixtures;
using Arcadia.App.ViewModels;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #488: Epic games show the cover and background of Epic's catalog in the library tile, the preview, the detail page and
/// the search, read from recreated Epic folders with made-up ids and image addresses; Epic's image servers are the test's
/// fake one.
/// </summary>
public sealed class EpicArtworkTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly string[] Game = ["games", "applications"];

    private static EpicApp WithImages(EpicApp app) => app with
    {
        KeyImages = [("DieselGameBoxTall", TallUrl(app)), ("DieselGameBox", WideUrl(app))],
    };

    private static string TallUrl(EpicApp app) => $"https://cdn1.epicgames.com/offer/{app.Namespace}/tall-{app.AppName}_1200x1600.jpg";

    private static string WideUrl(EpicApp app) => $"https://cdn1.epicgames.com/offer/{app.Namespace}/wide-{app.AppName}_2560x1440.jpg";

    private static void Serve(LauncherFiles files, EpicApp app)
    {
        files.ImageServer.Serve(TallUrl(app), TestImages.Png(1200, 1600));
        files.ImageServer.Serve(WideUrl(app), TestImages.Png(2560, 1440));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T488-1")]
    public async Task Epics_catalog_images_show_at_all_four_places_for_installed_and_owned_games()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        var installed = WithImages(new EpicApp("ee01", "Leuchtturm"));
        var owned = WithImages(new EpicApp("ee02", "Salzwiesen"));
        epic.WriteInstalled(installed);
        epic.WriteCatalog((installed, Game, null), (owned, Game, null));
        Serve(files, installed);
        Serve(files, owned);

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await ArtworkSettledAsync(arcadia);

        foreach (var (app, search) in new[] { (installed, "Leucht"), (owned, "Salzw") })
        {
            var tile = Assert.Single(arcadia.PageGames, t => t.Game.Id == app.AppName);
            AssertShows(tile, app.AppName, "Bibliothek");
            Assert.Equal((1200, 1600), (tile.Cover!.Size.Width, tile.Cover.Size.Height));
            Assert.Equal((2560, 1440), (tile.Hero!.Size.Width, tile.Hero.Size.Height));

            while (arcadia.Main.Library.FocusedGame != tile)
            {
                arcadia.Press(UiAction.Right);
            }

            Assert.Equal(tile.Game.Key, arcadia.Main.Library.FocusedGame?.Hero?.GameKey);
            arcadia.Hold(UiAction.Accept, PastHoldTime);
            AssertShows(arcadia.Detail!.Current.Tile, app.AppName, "Detailseite");
            arcadia.Press(UiAction.Back).Press(UiAction.Search);
            arcadia.Main.TypeInSearch(search);
            AssertShows(Assert.Single(arcadia.Search!.Results), app.AppName, "Suche");
            arcadia.Press(UiAction.Back, times: 2);
            Assert.Null(arcadia.Search);
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T488-8")]
    public async Task Epics_images_are_downloaded_once_and_still_shown_offline_after_a_restart()
    {
        using var files = new LauncherFiles();
        var app = WithImages(new EpicApp("ee03", "Nebelbank"));
        files.Epic.Install().WriteCatalog((app, Game, null));
        Serve(files, app);

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await ArtworkSettledAsync(arcadia);
        AssertShows(Assert.Single(arcadia.PageGames), app.AppName, "Bibliothek");
        await arcadia.Main.Session.RescanAsync(null);
        await ArtworkSettledAsync(arcadia);
        Assert.Equal(2, files.ImageServer.Requests.Count);
        Assert.All(files.ImageServer.Requests, request =>
        {
            Assert.Empty(request.RequestUri!.Query);
            Assert.False(request.Headers.Contains("Cookie"), "Ein Cookie geht an Epics Bildserver.");
            Assert.Null(request.Headers.Authorization);
        });

        files.ImageServer.Offline = true;
        using var restarted = await arcadia.RestartAsync();
        await ArtworkSettledAsync(restarted);

        AssertShows(Assert.Single(restarted.PageGames), app.AppName, "Bibliothek nach dem Neustart");
        Assert.Equal(2, files.ImageServer.Requests.Count);
    }

    /// <summary>T488-13 and T488-9 as the player meets them: started without network, the images come with the next search (#506).</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T488-13")]
    public async Task The_placeholder_shows_until_the_download_arrives_and_the_focus_stays()
    {
        using var files = new LauncherFiles();
        var first = WithImages(new EpicApp("ee04", "Dünenmeer"));
        var second = WithImages(new EpicApp("ee05", "Kreidefelsen"));
        files.Epic.Install().WriteCatalog((first, Game, null), (second, Game, null));
        Serve(files, first);
        Serve(files, second);
        files.ImageServer.Offline = true;
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await ArtworkSettledAsync(arcadia);
        arcadia.Press(UiAction.Right);
        var focused = arcadia.Main.Library.FocusedGame!;
        Assert.All(arcadia.PageGames, tile => Assert.Null(tile.Cover));
        Assert.Null(focused.Hero);

        files.ImageServer.Offline = false;
        await arcadia.Main.Session.RescanAsync(null);
        await ArtworkSettledAsync(arcadia);

        // A rescan goes online for no known game (#506): the next "Jetzt suchen" brings the images.
        Assert.All(arcadia.PageGames, tile => Assert.Null(tile.Cover));
        Assert.True(arcadia.Artwork.SearchAgain(byPlayer: true));
        await ArtworkSettledAsync(arcadia);

        Assert.Same(focused, arcadia.Main.Library.FocusedGame);
        Assert.True(focused.IsFocused);
        Assert.All(arcadia.PageGames, tile => AssertShows(tile, tile.Game.Id, "Bibliothek nach dem Neueinlesen"));
    }

    private static void AssertShows(GameTileViewModel tile, string appName, string where)
    {
        Assert.True(tile.Cover is { } cover && cover.GameKey == tile.Game.Key && tile.Game.Id == appName, $"{where}: kein Cover von {appName}.");
        Assert.True(File.Exists(tile.Cover!.Path), $"{where}: das Cover liegt nicht im Cache.");
        Assert.True(tile.Hero is { } hero && hero.GameKey == tile.Game.Key, $"{where}: kein Hintergrund von {appName}.");
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
