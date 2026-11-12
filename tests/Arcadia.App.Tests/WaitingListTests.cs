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

    private static readonly LauncherInfo Other = new("other", "Anderer Launcher", "other", null);

    private static Game GameOf(string id, string title) => new()
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
        arcadia.FocusRow(ArtworkNameRows.WaitingKey);
        arcadia.Press(UiAction.Accept);
        return arcadia.Main.WaitingList ?? throw new InvalidOperationException("Die Warteliste ist nicht offen.");
    }

    private static string? CoverOrigin(ArcadiaDriver arcadia, Game game) => new ArtworkCache(Path.Combine(arcadia.Folder, "artwork")).Read(game.Key)?.Cover?.Stamp;

    [AvaloniaFact]
    [Trait("TestCase", "T491-9")]
    public async Task The_rows_show_how_many_games_wait_and_grey_out_with_ersten_nehmen()
    {
        using var arcadia = await StartAsync();
        OpenDataSources(arcadia);

        var several = Assert.IsType<ChoiceRowViewModel>(Row(arcadia, ArtworkNameRows.SeveralHitsKey));
        var waiting = Assert.IsType<ActionRowViewModel>(Row(arcadia, ArtworkNameRows.WaitingKey));
        Assert.Equal((AppTexts.DataSourcesSeveralHitsAsk, AppTexts.DataSourcesSeveralHitsSubtitle), (several.ValueText, several.Subtitle));
        Assert.Equal(("3", AppTexts.DataSourcesWaitingSubtitle, true, false, 1), (waiting.CountBadge, waiting.Subtitle, waiting.CanActivate, waiting.IsDimmed, waiting.Indent));
        var facts = Assert.Single(waiting.Info.OfType<InfoFacts>());
        Assert.Equal(AppTexts.WaitingGames(3), facts.Title);
        Assert.Equal(("Echo Drift", "Anderer Launcher · 2 Treffer"), (facts.Facts[0].Label, facts.Facts[0].Value));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-12")]
    public async Task Switching_to_ersten_nehmen_takes_the_first_hit_of_every_waiting_game_with_one_toast()
    {
        using var arcadia = await StartAsync();
        OpenDataSources(arcadia);
        arcadia.FocusRow(ArtworkNameRows.SeveralHitsKey);

        arcadia.Press(UiAction.Accept);
        await SettledAsync(arcadia);

        Assert.Equal(AppTexts.DataSourcesTookFirstHits(3), arcadia.Main.Toast.Text);
        var waiting = Assert.IsType<ActionRowViewModel>(Row(arcadia, ArtworkNameRows.WaitingKey));
        Assert.Equal((null, AppTexts.DataSourcesWaitingOnlyAsk, true, false), (waiting.CountBadge, waiting.Subtitle, waiting.IsDimmed, waiting.CanActivate));
        Assert.Equal("https://cdn.example/grid/501.png", CoverOrigin(arcadia, Games[0]));

        // Back to "Selbst auswählen": none of them waits again (D3).
        arcadia.Press(UiAction.Accept);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(arcadia.Artwork.Waiting);
        Assert.Equal(AppTexts.DataSourcesWaitingNone, Row(arcadia, ArtworkNameRows.WaitingKey).Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-14")]
    public async Task Left_and_right_move_through_the_hits_up_and_down_through_the_games_round_at_the_ends()
    {
        using var arcadia = await StartAsync();
        var list = OpenList(arcadia);

        Assert.Equal(("Echo Drift", AppTexts.WaitingPosition(number: 1, count: 3)), (list.CurrentTitle, list.Position));
        Assert.True(list.Hits[0].IsFocused);
        arcadia.Press(UiAction.Right);
        Assert.True(list.Hits[1].IsFocused);
        arcadia.Press(UiAction.Right);
        Assert.True(list.Hits[0].IsFocused);
        arcadia.Press(UiAction.Left);
        Assert.True(list.Hits[1].IsFocused);

        arcadia.Press(UiAction.Up);
        Assert.Equal("Iron Ember", list.CurrentTitle);
        Assert.True(list.Hits[0].IsFocused);
        arcadia.Press(UiAction.Down);
        Assert.Equal("Echo Drift", list.CurrentTitle);
        arcadia.Press(UiAction.Down);
        Assert.Equal(("Golden Crown", true), (list.CurrentTitle, list.Games[1].IsCurrent));
        Assert.Equal((string.Empty, false), (list.Hits[1].YearText, list.Hits[1].HasYear));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-15")]
    [Trait("TestCase", "T491-16")]
    [Trait("TestCase", "T491-17")]
    public async Task A_takes_the_hit_Y_skips_and_after_the_last_game_B_comes_back_to_the_row()
    {
        using var arcadia = await StartAsync();
        var list = OpenList(arcadia);

        arcadia.Press(UiAction.Right);
        arcadia.Press(UiAction.Accept);
        await list.Loading;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(("Golden Crown", AppTexts.WaitingPosition(number: 1, count: 2)), (list.CurrentTitle, list.Position));
        Assert.Equal("https://cdn.example/grid/502.png", CoverOrigin(arcadia, Games[0]));

        arcadia.Press(UiAction.Status);
        Assert.Equal("Iron Ember", list.CurrentTitle);
        Assert.Null(CoverOrigin(arcadia, Games[1]));
        arcadia.Press(UiAction.Accept);
        await list.Loading;
        Dispatcher.UIThread.RunJobs();

        Assert.True(list.IsDone);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.WaitingList);
        var row = arcadia.Main.Settings!.Selected.Rows.Single(r => r.IsFocused);
        Assert.Equal((ArtworkNameRows.WaitingKey, AppTexts.DataSourcesWaitingNone, true), (row.Key, row.Subtitle, row.IsDimmed));
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
        Assert.Equal("Echo Drift", again.CurrentTitle);
        Assert.True(list != again);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-19")]
    public async Task The_hit_covers_of_the_game_shown_load_in_the_background()
    {
        using var arcadia = await StartAsync();
        var list = OpenList(arcadia);

        Assert.All(list.Hits, hit => Assert.Null(hit.Cover));
        await list.Covers.WaitAsync(TimeSpan.FromSeconds(10));
        Dispatcher.UIThread.RunJobs();

        Assert.All(list.Hits, hit => Assert.NotNull(hit.Cover));
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
        Assert.Equal("Golden Crown", list.CurrentTitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-26")]
    public async Task The_whole_way_works_with_the_controller_alone_and_b_goes_back()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([Launcher], Settings, imageServer: Images());
        OpenDataSources(arcadia);
        arcadia.FocusRow(ArtworkNameRows.SeveralHitsKey);
        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(SeveralHits.Ask, arcadia.Settings.Current.Artwork.SeveralHits);
        await FindNamesakesAsync(arcadia);

        arcadia.FocusRow(ArtworkNameRows.WaitingKey);
        arcadia.Press(UiAction.Accept);
        var list = arcadia.Main.WaitingList!;
        arcadia.Press(UiAction.Right);
        arcadia.Press(UiAction.Down);
        arcadia.Press(UiAction.Accept);
        await list.Loading;
        Dispatcher.UIThread.RunJobs();
        arcadia.Press(UiAction.Status);
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
