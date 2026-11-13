using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Artwork;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #491: "Bei mehreren Treffern" and the waiting list "Warten auf deine Auswahl" through the view models, with games of a
/// launcher without images of its own found by name at a fake SteamGridDB.
/// </summary>
public sealed class WaitingListTests
{
    private const string Key = FakeSteamGridDb.Key;

    internal static readonly LauncherInfo Other = new("other", "Anderer Launcher", "other", null);

    internal static Game GameOf(string id, string title) => new()
    {
        Id = id,
        LauncherId = Other.Id,
        Title = title,
        Launch = new LaunchTarget.ProtocolUri("other://run/" + id),
    };

    private static readonly Game[] Games = [GameOf("echo", "Echo Drift"), GameOf("crown", "Golden Crown"), GameOf("ember", "Iron Ember")];

    /// <summary>Three games, each with several namesakes at SteamGridDB, a valid key, the first search done.</summary>
    private static async Task<ArcadiaDriver> StartAsync()
    {
        var arcadia = await ArcadiaDriver.StartAsync([Launcher], Settings, imageServer: Images());
        await FindNamesakesAsync(arcadia);
        return arcadia;
    }

    internal static LauncherSpec Launcher => LauncherSpec.With(Other, Games);

    internal static AppSettings Settings => new() { PanEnabled = true, ActiveLaunchers = [Other.Id] };

    /// <summary>A cover and a background for each namesake.</summary>
    internal static FakeImageServer Images()
    {
        var images = new FakeImageServer();
        foreach (var id in HitIds)
        {
            images.Serve($"https://cdn.example/grid/{id}.png", TestImages.Png(600, 900));
            images.Serve($"https://cdn.example/hero/{id}.png", TestImages.Png(1920, 620));
        }

        return images;
    }

    private static readonly int[] HitIds = [501, 502, 601, 602, 701, 702];

    /// <summary>SteamGridDB knows two games of each name; the key is set and the search has run.</summary>
    internal static async Task FindNamesakesAsync(ArcadiaDriver arcadia)
    {
        arcadia.SteamGridDb
            .Finds("Echo Drift", (501, "Echo Drift", 2019), (502, "Echo Drift", 2023))
            .Finds("Golden Crown", (601, "Golden Crown", 2018), (602, "Golden Crown", null))
            .Finds("Iron Ember", (701, "Iron Ember", 2020), (702, "Iron Ember", 2016));
        foreach (var id in HitIds)
        {
            var gameId = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            arcadia.SteamGridDb.Cover("game", gameId, $"https://cdn.example/grid/{id}.png", 600, 900)
                .Hero("game", gameId, $"https://cdn.example/hero/{id}.png", 1920, 620);
        }

        await arcadia.Artwork.SteamGridDb!.SetAsync(Key);
        arcadia.Artwork.SearchAgain(byPlayer: false);
        await SettledAsync(arcadia);
    }

    internal static async Task SettledAsync(ArcadiaDriver arcadia)
    {
        await arcadia.Artwork.WhenIdleAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Artwork.Search.Running is null, "Suche beendet");
        Dispatcher.UIThread.RunJobs();
    }

    internal static void OpenDataSources(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings);
        for (var i = 0; i < 4 && arcadia.Main.Settings!.Selected is not DataSourcesSettingsViewModel; i++)
        {
            arcadia.Press(UiAction.NextTab);
        }
    }

    private static SettingsRowViewModel Row(ArcadiaDriver arcadia, string key) => arcadia.Main.Settings!.Selected.Rows.Single(row => row.Key == key);

    internal static WaitingListViewModel OpenList(ArcadiaDriver arcadia)
    {
        OpenDataSources(arcadia);
        arcadia.FocusRow(SeveralHitsRows.WaitingKey(WaitingKind.Artwork));
        arcadia.Press(UiAction.Accept);
        return arcadia.Main.WaitingList ?? throw new InvalidOperationException("Die Warteliste ist nicht offen.");
    }

    private static string? CoverOrigin(ArcadiaDriver arcadia, Game game) => new ArtworkCache(Path.Combine(arcadia.Folder, "artwork")).Read(game.Key)?.Cover?.Stamp;

    [AvaloniaFact]
    [Trait("TestCase", "T491-9")]
    [Trait("TestCase", "T506-117")]
    public async Task The_rows_show_how_many_games_wait_and_grey_out_with_ersten_nehmen()
    {
        using var arcadia = await StartAsync();
        OpenDataSources(arcadia);

        var several = Assert.IsType<ChoiceRowViewModel>(Row(arcadia, SeveralHitsRows.SeveralHitsKey(WaitingKind.Artwork)));
        var waiting = Assert.IsType<ActionRowViewModel>(Row(arcadia, SeveralHitsRows.WaitingKey(WaitingKind.Artwork)));
        Assert.Equal((AppTexts.DataSourcesSeveralHitsAsk, AppTexts.DataSourcesSeveralHitsSubtitle), (several.ValueText, several.Subtitle));
        Assert.Equal(("3", AppTexts.DataSourcesWaitingSubtitle, true, false, 1), (waiting.CountBadge, waiting.Subtitle, waiting.CanActivate, waiting.IsDimmed, waiting.Indent));
        var facts = Assert.Single(waiting.Info.OfType<InfoFacts>());
        Assert.Equal(AppTexts.WaitingGames(3), facts.Title);
        Assert.Equal(("Echo Drift", "Anderer Launcher"), (facts.Facts[0].Label, facts.Facts[0].Value));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-12")]
    public async Task Switching_to_ersten_nehmen_takes_the_first_hit_of_every_waiting_game_with_one_toast()
    {
        using var arcadia = await StartAsync();
        OpenDataSources(arcadia);
        arcadia.FocusRow(SeveralHitsRows.SeveralHitsKey(WaitingKind.Artwork));

        arcadia.Press(UiAction.Accept);
        await SettledAsync(arcadia);

        Assert.Equal(AppTexts.DataSourcesTookFirstHits(3), arcadia.Main.Toast.Text);
        var waiting = Assert.IsType<ActionRowViewModel>(Row(arcadia, SeveralHitsRows.WaitingKey(WaitingKind.Artwork)));
        Assert.Equal((null, AppTexts.DataSourcesWaitingOnlyAsk, true, false), (waiting.CountBadge, waiting.Subtitle, waiting.IsDimmed, waiting.CanActivate));
        Assert.Equal("https://cdn.example/grid/501.png", CoverOrigin(arcadia, Games[0]));

        // Back to "Selbst auswählen": none of them waits again (D3).
        arcadia.Press(UiAction.Accept);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(arcadia.Artwork.Waiting);
        Assert.Equal(AppTexts.DataSourcesWaitingNone, Row(arcadia, SeveralHitsRows.WaitingKey(WaitingKind.Artwork)).Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-15")]
    [Trait("TestCase", "T491-16")]
    [Trait("TestCase", "T491-17")]
    [Trait("TestCase", "T506-84")]
    [Trait("TestCase", "T506-87")]
    public async Task A_takes_the_hit_Y_skips_and_after_the_last_game_B_comes_back_to_the_row()
    {
        using var arcadia = await StartAsync();
        var list = OpenList(arcadia);

        arcadia.Press(UiAction.Accept).Press(UiAction.Right).Press(UiAction.Accept);
        await list.Loading;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(("Golden Crown", AppTexts.WaitingPosition(number: 1, count: 2), false), (list.Hits!.Title, list.Position, list.InHits));
        Assert.Equal("https://cdn.example/grid/502.png", CoverOrigin(arcadia, Games[0]));

        arcadia.Press(UiAction.Status);
        Assert.Equal("Iron Ember", list.Hits!.Title);
        Assert.Null(CoverOrigin(arcadia, Games[1]));
        arcadia.Press(UiAction.Accept).Press(UiAction.Accept);
        await list.Loading;
        Dispatcher.UIThread.RunJobs();

        Assert.True(list.IsDone);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.WaitingList);
        var row = arcadia.Main.Settings!.Selected.Rows.Single(r => r.IsFocused);
        Assert.Equal((SeveralHitsRows.WaitingKey(WaitingKind.Artwork), AppTexts.DataSourcesWaitingNone, true), (row.Key, row.Subtitle, row.IsDimmed));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-18")]
    public async Task B_closes_and_the_undecided_games_wait_in_their_order()
    {
        using var arcadia = await StartAsync();
        var list = OpenList(arcadia);
        arcadia.Press(UiAction.Down);

        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.WaitingList);
        arcadia.Press(UiAction.Accept);

        var again = arcadia.Main.WaitingList!;
        Assert.Equal(["Echo Drift", "Golden Crown", "Iron Ember"], again.Games.Select(g => g.Title));
        Assert.Equal("Echo Drift", again.Hits!.Title);
        Assert.True(list != again);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-19")]
    public async Task The_hit_covers_of_the_game_shown_load_in_the_background()
    {
        using var arcadia = await StartAsync();
        var list = OpenList(arcadia);

        Assert.All(list.Hits!.Hits, hit => Assert.Null(hit.Image));
        await ShownAsync(list);

        Assert.All(list.Hits!.Hits, hit => Assert.NotNull(hit.Image));
    }

    /// <summary>Real time passes, with the UI thread's jobs run, until the condition holds; fails after 5 s.</summary>
    internal static async Task WaitUntilAsync(Func<bool> condition)
    {
        var limit = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < limit, "Die Bedingung trat nicht innerhalb von 5 s ein.");
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
    }

    /// <summary>The focus rested on the current game: its hits checked and their tiles loaded.</summary>
    internal static async Task ShownAsync(WaitingListViewModel list)
    {
        await list.Hits!.Ready.WaitAsync(TimeSpan.FromSeconds(10));
        await list.Hits.Images.WaitAsync(TimeSpan.FromSeconds(10));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-21")]
    public async Task A_game_leaving_the_library_leaves_the_open_list_and_the_next_one_is_current()
    {
        using var arcadia = await StartAsync();
        var list = OpenList(arcadia);

        arcadia.Artwork.Update([new LibraryArtwork(new Arcadia.Core.Library.LauncherLibrary(Other, [Games[1], Games[2]]), null)]);
        await SettledAsync(arcadia);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Golden Crown", "Iron Ember"], list.Games.Select(g => g.Title));
        Assert.Equal("Golden Crown", list.Hits!.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-26")]
    [Trait("TestCase", "T506-86")]
    public async Task The_whole_way_works_with_the_controller_alone_and_b_goes_back()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([Launcher], Settings, imageServer: Images());
        OpenDataSources(arcadia);
        arcadia.FocusRow(SeveralHitsRows.SeveralHitsKey(WaitingKind.Artwork));
        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(SeveralHits.Ask, arcadia.Settings.Current.Artwork.SeveralHits);
        await FindNamesakesAsync(arcadia);

        arcadia.FocusRow(SeveralHitsRows.WaitingKey(WaitingKind.Artwork));
        arcadia.Press(UiAction.Accept);
        var list = arcadia.Main.WaitingList!;
        arcadia.Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Accept);
        await list.Loading;
        Dispatcher.UIThread.RunJobs();

        // Y in the hits skips and comes back to the games list, as A does.
        arcadia.Press(UiAction.Accept).Press(UiAction.Status);
        Assert.False(list.InHits);
        arcadia.Press(UiAction.Back);

        Assert.Null(arcadia.Main.WaitingList);
        Assert.Equal(["Echo Drift"], arcadia.Artwork.Waiting.Select(w => w.Game.Title));
        Assert.Equal("https://cdn.example/grid/601.png", CoverOrigin(arcadia, Games[1]));
        Assert.Null(CoverOrigin(arcadia, Games[2]));
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Settings);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-21")]
    public async Task Y_and_a_change_of_the_libraries_keep_the_rows_of_the_other_games()
    {
        using var arcadia = await StartAsync();
        var list = OpenList(arcadia);
        var crown = list.Games[1];
        var ember = list.Games[2];

        arcadia.Press(UiAction.Status);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([crown, ember], list.Games);
        Assert.True(crown.IsCurrent);
        arcadia.Artwork.Update([new LibraryArtwork(new Arcadia.Core.Library.LauncherLibrary(Other, [.. Games]), null)]);
        await SettledAsync(arcadia);
        Assert.Equal([crown, ember], list.Games);
    }
}
