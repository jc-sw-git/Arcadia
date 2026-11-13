using System.Globalization;
using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.Tests.Fixtures;
using Arcadia.App.ViewModels;
using Arcadia.Core.Artwork;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #491's acceptance tests as the player meets them: an Epic game asked by its AppName on <c>egs</c>, a game found by its
/// name taken without a question, several matching games waiting or the first taken, and the choices kept across a
/// restart. Made-up games, ids and images; SteamGridDB and the image servers are the tests' fakes.
/// </summary>
public sealed class FindArtworkByNameAcceptanceTests
{
    private static readonly string[] EpicGame = ["games", "applications"];

    private static readonly LauncherInfo Other = new("other", "Anderer Launcher", "other", null);

    private static Game GameOf(string id, string title) => new()
    {
        Id = id,
        LauncherId = Other.Id,
        Title = title,
        Launch = new LaunchTarget.ProtocolUri("other://run/" + id),
    };

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

    private static string GridUrl(int id) => $"https://cdn.example/grid/{id}.png";

    private static string HeroUrl(int id) => $"https://cdn.example/hero/{id}.png";

    /// <summary>SteamGridDB knows the game <paramref name="id"/> with a cover and a background, served by <paramref name="images"/>.</summary>
    private static void Knows(ArcadiaDriver arcadia, FakeImageServer images, int id)
    {
        arcadia.SteamGridDb.Cover("game", Id(id), GridUrl(id), 600, 900).Hero("game", Id(id), HeroUrl(id), 1920, 620);
        images.Serve(GridUrl(id), TestImages.Png(600, 900));
        images.Serve(HeroUrl(id), TestImages.Png(1920, 620));
    }

    private static AppSettings SettingsWith(SeveralHits severalHits) =>
        new() { PanEnabled = true, ActiveLaunchers = [Other.Id], Artwork = new ArtworkSettings { SeveralHits = severalHits } };

    /// <summary>The key set (its check is one request) and a search run; what the search asked SteamGridDB for.</summary>
    private static async Task<IReadOnlyList<string>> SearchWithKeyAsync(ArcadiaDriver arcadia)
    {
        await arcadia.Artwork.SteamGridDb!.SetAsync(FakeSteamGridDb.Key);
        var checkedKey = arcadia.SteamGridDb.Count;
        arcadia.Artwork.SearchAgain(byPlayer: false);
        await SettledAsync(arcadia);
        return [.. arcadia.SteamGridDb.Asked().Skip(checkedKey)];
    }

    private static async Task SettledAsync(ArcadiaDriver arcadia)
    {
        for (var round = 0; round < 3; round++)
        {
            Dispatcher.UIThread.RunJobs();
            await arcadia.Artwork.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await arcadia.WaitUntilAsync(() => arcadia.Artwork.Search.Running is null, "Suche beendet");
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static CachedArtwork? Cached(ArcadiaDriver arcadia, string key) => new ArtworkCache(Path.Combine(arcadia.Folder, "artwork")).Read(key);

    private static IReadOnlyList<string> Searches(ArcadiaDriver arcadia) =>
        [.. arcadia.SteamGridDb.Asked().Where(a => a.StartsWith("search/", StringComparison.Ordinal))];

    private static ActionRowViewModel WaitingRow(ArcadiaDriver arcadia)
    {
        WaitingListTests.OpenDataSources(arcadia);
        var row = Assert.IsType<ActionRowViewModel>(arcadia.Main.Settings!.Selected.Rows.Single(r => r.Key == SeveralHitsRows.WaitingKey(WaitingKind.Artwork)));
        arcadia.Press(UiAction.Back);
        return row;
    }

    /// <summary>The tile, the detail page and the search hit show the game's images, cover and background.</summary>
    private static void AssertShownEverywhere(ArcadiaDriver arcadia, Game game, string searchTerm, (int Width, int Height) hero)
    {
        var tile = Assert.Single(arcadia.PageGames, t => t.Game.Key == game.Key);
        AssertTile(tile, game, hero, "Bibliothek");
        while (arcadia.Main.Library.FocusedGame != tile)
        {
            arcadia.Press(UiAction.Right);
        }

        Assert.Equal(game.Key, arcadia.Main.Library.FocusedGame?.Hero?.GameKey);
        arcadia.Hold(UiAction.Accept, TimeSpan.FromMilliseconds(1100));
        AssertTile(arcadia.Detail!.Current.Tile, game, hero, "Detailseite");
        arcadia.Press(UiAction.Back).Press(UiAction.Search);
        arcadia.Main.TypeInSearch(searchTerm);
        AssertTile(Assert.Single(arcadia.Search!.Results), game, hero, "Suche");
        arcadia.Press(UiAction.Back, times: 2);
    }

    private static void AssertTile(GameTileViewModel tile, Game game, (int Width, int Height) hero, string where)
    {
        Assert.True(tile.Cover is { } cover && cover.GameKey == game.Key && File.Exists(cover.Path), $"{where}: kein Cover von {game.Title}.");
        Assert.True(tile.Hero is { } shown && shown.GameKey == game.Key, $"{where}: kein Hintergrund von {game.Title}.");
        Assert.Equal(hero, (tile.Hero!.Size.Width, tile.Hero.Size.Height));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-1")]
    public async Task An_epic_game_gets_steamgriddbs_background_asked_by_its_app_name_on_egs_and_keeps_epics_cover()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        var app = new EpicApp("ab12cd34", "Leuchtturm") with
        {
            KeyImages = [("DieselGameBoxTall", "https://cdn1.epicgames.com/offer/nsab/tall_1200x1600.jpg"), ("DieselGameBox", "https://cdn1.epicgames.com/offer/nsab/wide_2560x1440.jpg")],
        };
        epic.WriteInstalled(app);
        epic.WriteCatalog((app, EpicGame, null));
        files.ImageServer.Serve("https://cdn1.epicgames.com/offer/nsab/tall_1200x1600.jpg", TestImages.Png(1200, 1600));
        files.ImageServer.Serve("https://cdn1.epicgames.com/offer/nsab/wide_2560x1440.jpg", TestImages.Png(2560, 1440));
        files.ImageServer.Serve("https://cdn.example/hero/egs.png", TestImages.Png(1920, 620));

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        arcadia.SteamGridDb.Hero("egs", app.AppName, "https://cdn.example/hero/egs.png", 1920, 620);
        var asked = await SearchWithKeyAsync(arcadia);

        // Epic's 3 : 4 cover fits by cropping, so only the background is asked for, by the AppName on egs, never by name.
        Assert.Equal(["heroes/egs/" + app.AppName], asked);
        Assert.All(arcadia.SteamGridDb.Requests, request =>
        {
            Assert.Equal(("https", FakeSteamGridDb.Host), (request.RequestUri!.Scheme, request.RequestUri.Host));
            Assert.Equal(("Bearer", FakeSteamGridDb.Key), (request.Headers.Authorization!.Scheme, request.Headers.Authorization.Parameter));
            Assert.DoesNotContain(FakeSteamGridDb.Key, request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
        });

        var game = Assert.Single(arcadia.PageGames).Game;
        var cached = Cached(arcadia, game.Key)!;
        Assert.Equal(ArtworkService.SteamGridDbOrigin, cached.Hero!.Origin);
        Assert.NotEqual(ArtworkService.SteamGridDbOrigin, cached.Cover!.Origin);
        AssertShownEverywhere(arcadia, game, "Leucht", (1920, 620));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-3")]
    public async Task A_game_without_a_launcher_id_and_one_matching_game_takes_its_images_without_a_question()
    {
        var images = new FakeImageServer();
        var game = GameOf("echo", "Echo Drift™");
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(Other, [game])], SettingsWith(SeveralHits.Ask), imageServer: images);
        arcadia.SteamGridDb.Finds("Echo Drift", (501, "Echo Drift", 2019), (503, "Echo Drifter", 2015));
        Knows(arcadia, images, 501);
        Knows(arcadia, images, 503);

        var asked = await SearchWithKeyAsync(arcadia);

        // Both hits are checked for a cover first (#506); the check's answer for 501 is its cover, not asked twice.
        Assert.Equal(["search/autocomplete/Echo Drift", "grids/game/501", "grids/game/503", "heroes/game/501"], asked);
        Assert.Equal((GridUrl(501), HeroUrl(501)), (Cached(arcadia, game.Key)!.Cover!.Stamp, Cached(arcadia, game.Key)!.Hero!.Stamp));
        Assert.Empty(arcadia.Artwork.Waiting);
        Assert.True(WaitingRow(arcadia).IsDimmed);
        AssertShownEverywhere(arcadia, game, "Echo", (1920, 620));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-3")]
    public async Task An_epic_game_steamgriddb_doesnt_know_by_its_app_name_is_found_by_its_name()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        var app = new EpicApp("ef56ab78", "Salzwiesen");
        epic.WriteInstalled(app);
        epic.WriteCatalog((app, EpicGame, null));

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        arcadia.SteamGridDb.Finds("Salzwiesen", (601, "Salzwiesen", 2021));
        Knows(arcadia, files.ImageServer, 601);
        var asked = await SearchWithKeyAsync(arcadia);

        Assert.Equal(["grids/egs/" + app.AppName, "search/autocomplete/Salzwiesen", "grids/game/601", "heroes/game/601"], asked);
        var game = Assert.Single(arcadia.PageGames).Game;
        Assert.Equal((GridUrl(601), HeroUrl(601)), (Cached(arcadia, game.Key)!.Cover!.Stamp, Cached(arcadia, game.Key)!.Hero!.Stamp));
        AssertShownEverywhere(arcadia, game, "Salzw", (1920, 620));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-10")]
    public async Task With_selbst_auswaehlen_a_game_with_three_namesakes_waits_once_without_a_hits_image_and_no_new_search()
    {
        var images = new FakeImageServer();
        var game = GameOf("crown", "Golden Crown");
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(Other, [game])], SettingsWith(SeveralHits.Ask), imageServer: images);
        arcadia.SteamGridDb.Finds("Golden Crown", (701, "Golden Crown", 2018), (702, "Golden Crown", 2020), (703, "Golden Crown", null));
        foreach (var id in new[] { 701, 702, 703 })
        {
            Knows(arcadia, images, id);
        }

        await SearchWithKeyAsync(arcadia);

        AssertWaitsWithoutImages(arcadia, game);
        Assert.Equal("1", WaitingRow(arcadia).CountBadge);

        // A further scan, "Jetzt suchen" and a restart neither add it twice nor search its name again.
        await arcadia.Main.Session.RescanAsync(null);
        await SettledAsync(arcadia);
        arcadia.Artwork.SearchAgain(byPlayer: true);
        await SettledAsync(arcadia);
        using var restarted = await arcadia.RestartAsync();
        await SettledAsync(restarted);
        restarted.Artwork.SearchAgain(byPlayer: true);
        await SettledAsync(restarted);

        AssertWaitsWithoutImages(restarted, game);
        Assert.Equal("1", WaitingRow(restarted).CountBadge);
        Assert.Empty(images.Requests);
    }

    private static void AssertWaitsWithoutImages(ArcadiaDriver arcadia, Game game)
    {
        var waiting = Assert.Single(arcadia.Artwork.Waiting);
        Assert.Equal((game.Key, 3), (waiting.Game.Key, waiting.Hits.Count));
        Assert.Single(Searches(arcadia));
        // Each hit was checked for a cover once (#506), none of their images downloaded.
        Assert.All(arcadia.SteamGridDb.Asked().Where(a => a.Contains("/game/", StringComparison.Ordinal)), a => Assert.StartsWith("grids/game/70", a, StringComparison.Ordinal));
        Assert.True(arcadia.SteamGridDb.Asked().Count(a => a.Contains("/game/", StringComparison.Ordinal)) <= 3);
        var tile = Assert.Single(arcadia.PageGames, t => t.Game.Key == game.Key);
        Assert.Null(tile.Cover);
        Assert.Null(tile.Hero);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-11")]
    public async Task With_ersten_nehmen_twenty_games_with_namesakes_take_their_first_hit_and_nothing_waits()
    {
        var images = new FakeImageServer();
        var games = Enumerable.Range(1, 20).Select(i => GameOf($"g{i}", $"Namensvetter {i}")).ToArray();
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(Other, games)], SettingsWith(SeveralHits.TakeFirst), imageServer: images);
        for (var i = 1; i <= 20; i++)
        {
            arcadia.SteamGridDb.Finds($"Namensvetter {i}", (1000 + i, $"Namensvetter {i}", 2010), (2000 + i, $"Namensvetter {i}", 2020), (3000 + i, $"Namensvetter {i}", null));
            Knows(arcadia, images, 1000 + i);
            Knows(arcadia, images, 2000 + i);
        }

        await SearchWithKeyAsync(arcadia);

        Assert.All(games.Select((game, i) => (game, first: 1001 + i)), e =>
            Assert.Equal((GridUrl(e.first), HeroUrl(e.first)), (Cached(arcadia, e.game.Key)!.Cover!.Stamp, Cached(arcadia, e.game.Key)!.Hero!.Stamp)));
        Assert.DoesNotContain(images.Requests, r => r.RequestUri!.AbsoluteUri.Contains("/grid/2", StringComparison.Ordinal));
        Assert.Empty(arcadia.Artwork.Waiting);
        Assert.True(WaitingRow(arcadia).IsDimmed);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-22")]
    public async Task A_chosen_a_skipped_and_a_waiting_game_keep_their_state_across_a_restart_and_a_reload()
    {
        var images = new FakeImageServer();
        var chosen = GameOf("echo", "Echo Drift");
        var skipped = GameOf("crown", "Golden Crown");
        var waiting = GameOf("ember", "Iron Ember");
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(Other, [chosen, skipped, waiting])], SettingsWith(SeveralHits.Ask), imageServer: images);
        arcadia.SteamGridDb
            .Finds("Echo Drift", (501, "Echo Drift", 2019), (502, "Echo Drift", 2023))
            .Finds("Golden Crown", (601, "Golden Crown", 2018), (602, "Golden Crown", null))
            .Finds("Iron Ember", (701, "Iron Ember", 2020), (702, "Iron Ember", 2016));
        foreach (var id in new[] { 501, 502, 601, 602, 701, 702 })
        {
            Knows(arcadia, images, id);
        }

        await SearchWithKeyAsync(arcadia);
        var list = WaitingListTests.OpenList(arcadia);
        await WaitingListTests.ShownAsync(list);
        Assert.Equal(chosen.Title, list.Hits!.Title);
        arcadia.Press(UiAction.Accept).Press(UiAction.Right);
        await WaitingListTests.TakeCoverAndItsBackgroundAsync(arcadia, list);
        Assert.Equal(skipped.Title, list.Hits!.Title);
        arcadia.Press(UiAction.Status).Press(UiAction.Back);
        await SettledAsync(arcadia);
        Assert.Equal(GridUrl(502), Cached(arcadia, chosen.Key)!.Cover!.Stamp);
        var searchesBefore = Searches(arcadia).Count;

        using var restarted = await arcadia.RestartAsync();
        await SettledAsync(restarted);
        var asked = restarted.SteamGridDb.Count;
        Assert.True(restarted.Artwork.ReloadAll(overwriteSetByHand: false));
        await SettledAsync(restarted);
        var askedAfterRestart = restarted.SteamGridDb.Asked().Skip(asked).ToList();

        // The chosen game is asked by its SteamGridDB game, the skipped one never, and nobody by name.
        Assert.Equal(searchesBefore, Searches(restarted).Count);
        Assert.Equal(["grids/game/502", "heroes/game/502"], askedAfterRestart);
        Assert.Equal(GridUrl(502), Cached(restarted, chosen.Key)!.Cover!.Stamp);
        Assert.Null(Cached(restarted, skipped.Key)?.Cover);

        // The waiting one is still on the list with the same hits.
        var stillWaiting = Assert.Single(restarted.Artwork.Waiting);
        Assert.Equal(waiting.Key, stillWaiting.Game.Key);
        Assert.Equal([701, 702], stillWaiting.Hits.Select(h => h.Id));
        Assert.Equal("1", WaitingRow(restarted).CountBadge);
    }
}
