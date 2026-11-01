using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers;
using Avalonia.Headless.XUnit;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>
/// The ways between library, detail page and search, and what a held A survives (#155). Written while one view model
/// still held all pages, so splitting it up keeps today's behaviour. The return paths of the detail page and the search
/// are in <see cref="DetailPageTests"/>, <see cref="SearchHitTests"/> and <see cref="SearchTests"/>.
/// </summary>
public sealed class PageTransitionTests
{
    private static readonly TimeSpan Tap = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PastFeedback = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T155-1")]
    public async Task A_held_in_the_library_opens_the_detail_page_in_the_tabs_sort_order_with_the_tab_strip()
    {
        var (_, gog) = Changeable(KnownLaunchers.Gog, "Alpha", "Bravo", "Charlie", "Delta", "Echo");
        using var arcadia = await ArcadiaDriver.StartAsync([gog], new AppSettings
        {
            ActiveLaunchers = [KnownLaunchers.Gog.Id],
            LibrarySort = new SortOrder(SortField.Name, SortDirection.Descending),
        });

        // Z → A: Echo, Delta, Charlie, Bravo, Alpha.
        arcadia.Press(UiAction.Right).Hold(UiAction.Accept, PastHoldTime);

        var detail = Assert.IsType<GameDetailViewModel>(arcadia.Detail);
        Assert.True(detail.IsFromLibrary);
        Assert.Equal("Delta", detail.Current.Tile.Title);
        Assert.Equal("2 von 5", detail.PositionText);
        Assert.Equal("Echo", Side(detail, -1));
        Assert.Equal("Charlie", Side(detail, 1));
        Assert.Equal("Name Z–A", detail.SortLabel);
        Assert.True(arcadia.Main.IsTabStripShown);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T155-2")]
    public async Task A_rescan_keeps_the_library_detail_page_on_its_game_or_the_one_in_its_place_closes_it_with_the_tab_empty_and_B_finds_the_library_up_to_date()
    {
        var (games, gog) = Changeable(KnownLaunchers.Gog, "Alpha", "Bravo", "Charlie", "Delta", "Echo");
        using var arcadia = await ArcadiaDriver.StartAsync([gog]);
        arcadia.Press(UiAction.Right, times: 2).Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!;
        Assert.Equal(("Charlie", "3 von 5"), (detail.Current.Tile.Title, detail.PositionText));

        Remove(games, "Alpha");
        await arcadia.RescanAsync();
        Assert.Same(detail, arcadia.Detail);
        Assert.Equal(("Charlie", "2 von 4"), (detail.Current.Tile.Title, detail.PositionText));

        // The library under the detail page followed the rescan too.
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Detail);
        Assert.Equal(["Bravo", "Charlie", "Delta", "Echo"], arcadia.PageGames.Select(tile => tile.Title));
        Assert.Equal("Charlie", arcadia.FocusedGame?.Title);

        // Its game gone, the page shows the game now in its place.
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        detail = arcadia.Detail!;
        Assert.Equal("Charlie", detail.Current.Tile.Title);
        Remove(games, "Charlie");
        await arcadia.RescanAsync();
        Assert.Same(detail, arcadia.Detail);
        Assert.Equal(("Delta", "2 von 3"), (detail.Current.Tile.Title, detail.PositionText));

        games.Clear();
        await arcadia.RescanAsync();
        Assert.Null(arcadia.Detail);
        Assert.True(arcadia.Main.IsEmptyTabShown);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T155-3")]
    public async Task B_in_the_search_closes_the_layout_menu_first_then_puts_the_keyboard_away_then_closes_the_search()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.NextTab).Press(UiAction.Right, times: 3);
        var focused = arcadia.FocusedGame!.Game.Key;
        arcadia.Press(UiAction.Search);
        var search = arcadia.Search!;
        arcadia.Main.TypeInSearch("iron");

        arcadia.Press(UiAction.KeyboardLayout);
        Assert.True(search.Keyboard.LayoutMenu.IsOpen);

        arcadia.Press(UiAction.Back);
        Assert.False(search.Keyboard.LayoutMenu.IsOpen);
        Assert.True(search.IsKeyboardOut);
        Assert.Same(search, arcadia.Search);

        arcadia.Press(UiAction.Back);
        Assert.False(search.IsKeyboardOut);
        Assert.Same(search, arcadia.Search);

        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Search);
        Assert.Equal("Xbox", arcadia.SelectedTab?.Name);
        Assert.Equal(focused, arcadia.FocusedGame?.Game.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T155-4")]
    public async Task A_held_on_a_hit_with_the_keyboard_in_opens_its_details_among_the_hits_without_the_tab_strip()
    {
        using var arcadia = await SearchAsync(RiverLibrary(), "river");
        var search = arcadia.Search!;
        arcadia.Press(UiAction.Back);
        Assert.False(search.IsKeyboardOut);
        arcadia.Press(UiAction.Right);
        var hit = search.FocusedResult!;
        Assert.Equal("River Raid", hit.Title);

        arcadia.PressDown(UiAction.Accept).Wait(PastFeedback);
        Assert.True(hit.IsHolding);
        Assert.Null(arcadia.Detail);
        arcadia.Wait(PastHoldTime - PastFeedback);

        var detail = Assert.IsType<GameDetailViewModel>(arcadia.Detail);
        Assert.True(detail.IsFromSearch);
        Assert.Equal(("River Raid", "2 von 3"), (detail.Current.Tile.Title, detail.PositionText));
        Assert.False(arcadia.Main.IsTabStripShown);
        Assert.False(arcadia.Main.IsSearchShown);
        arcadia.LetGo(UiAction.Accept);
        Assert.Empty(arcadia.Starts.Keys);
        Assert.Null(arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T155-5")]
    public async Task A_search_over_a_library_detail_page_returns_to_it_after_a_rescan_and_to_the_library_once_the_tab_is_empty()
    {
        var (games, gog) = Changeable(KnownLaunchers.Gog, "Alpha", "Bravo", "Charlie", "Delta", "Echo");
        var (_, xbox) = Changeable(KnownLaunchers.Xbox, "Riverbend");
        using var arcadia = await ArcadiaDriver.StartAsync([gog, xbox]);
        arcadia.Press(UiAction.Right, times: 2).Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!;
        Assert.Equal("Charlie", detail.Current.Tile.Title);

        // Its game gone meanwhile: back to the page, on the game now in its place.
        arcadia.Press(UiAction.Search);
        Assert.Null(arcadia.Detail);
        Remove(games, "Charlie");
        await arcadia.RescanAsync();
        Assert.Null(arcadia.Detail);

        // The first B puts the keyboard away, the second leaves the search.
        arcadia.Press(UiAction.Back, times: 2);
        Assert.Null(arcadia.Search);
        Assert.Same(detail, arcadia.Detail);
        Assert.Equal(("Delta", "3 von 4"), (detail.Current.Tile.Title, detail.PositionText));

        // The tab emptied meanwhile: back to the library, which says so.
        arcadia.Press(UiAction.Search);
        games.Clear();
        await arcadia.RescanAsync();
        Assert.NotNull(arcadia.Search);
        arcadia.Press(UiAction.Back, times: 2);
        Assert.Null(arcadia.Search);
        Assert.Null(arcadia.Detail);
        Assert.Equal("GOG Galaxy", arcadia.SelectedTab?.Name);
        Assert.True(arcadia.Main.IsEmptyTabShown);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T155-6")]
    public async Task A_rescan_keeps_the_search_text_with_fresh_hits_and_the_details_of_a_hit_on_their_game_or_the_one_in_its_place()
    {
        var (gogGames, gog) = Changeable(KnownLaunchers.Gog, "River Crossing", "River Raid", "Iron Harbor");
        var (xboxGames, xbox) = Changeable(KnownLaunchers.Xbox, "Riverbend", "Silent Tides");
        using var arcadia = await SearchAsync([gog, xbox], "river");
        var search = arcadia.Search!;
        Assert.Equal(3, search.Hits.Count);

        gogGames.Add(Game(KnownLaunchers.Gog, "River Delta"));
        await arcadia.RescanAsync();
        Assert.Same(search, arcadia.Search);
        Assert.Equal("river", search.Text);
        Assert.Contains("River Delta", search.Hits.Select(game => game.Title));
        Assert.Equal(4, search.Hits.Count);

        var shown = search.FocusedResult!.Title;
        arcadia.PressDown(UiAction.Settings).Wait(PastHoldTime).LetGo(UiAction.Settings);
        var detail = arcadia.Detail!;
        Assert.Equal(shown, detail.Current.Tile.Title);

        Remove(gogGames, search.Hits.Select(game => game.Title).First(title => title != shown && title != "Riverbend"));
        await arcadia.RescanAsync();
        Assert.Same(detail, arcadia.Detail);
        Assert.Equal(shown, detail.Current.Tile.Title);
        Assert.EndsWith("von 3", detail.PositionText);

        // Its game gone: the hit now in its place.
        Remove(gogGames, shown);
        await arcadia.RescanAsync();
        Assert.Same(detail, arcadia.Detail);
        Assert.NotEqual(shown, detail.Current.Tile.Title);
        Assert.EndsWith("von 2", detail.PositionText);
        Assert.True(detail.IsFromSearch);

        // No hit left: the search shows again, with its text.
        gogGames.RemoveAll(game => game.Title.StartsWith("River", StringComparison.Ordinal));
        Remove(xboxGames, "Riverbend");
        await arcadia.RescanAsync();
        Assert.Null(arcadia.Detail);
        Assert.True(arcadia.Main.IsSearchShown);
        Assert.Equal("river", search.Text);
        Assert.Empty(search.Hits);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T155-7")]
    public async Task A_rescan_that_leaves_no_game_closes_the_search_also_under_the_details_of_a_hit()
    {
        var (games, gog) = Changeable(KnownLaunchers.Gog, "River Crossing", "River Raid");
        using var arcadia = await SearchAsync([gog], "river");
        arcadia.PressDown(UiAction.Settings).Wait(PastHoldTime).LetGo(UiAction.Settings);
        Assert.True(arcadia.Detail?.IsFromSearch);

        games.Clear();
        await arcadia.RescanAsync();
        Assert.Null(arcadia.Detail);
        Assert.Null(arcadia.Search);
        Assert.True(arcadia.Main.IsEmptyTabShown);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T209-1")]
    public async Task The_search_filter_stays_open_with_the_searchs_options_after_a_rescan_and_closes_with_the_search_once_no_game_is_left()
    {
        var (games, gog) = Changeable(KnownLaunchers.Gog, "River Crossing", "River Raid");
        using var arcadia = await SearchAsync([gog], "river");
        arcadia.Press(UiAction.Filter);
        Assert.True(arcadia.Main.ViewOptions?.HasLaunchers);

        games.Add(Game(KnownLaunchers.Gog, "River Delta"));
        await arcadia.RescanAsync();
        Assert.True(arcadia.Main.ViewOptions?.HasLaunchers);
        Assert.Equal(3, arcadia.Search!.Hits.Count);

        games.Clear();
        await arcadia.RescanAsync();
        Assert.Null(arcadia.Search);
        Assert.False(arcadia.Main.IsViewOptionsOpen);
        Assert.True(arcadia.Main.IsEmptyTabShown);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T155-8")]
    public async Task B_that_closes_a_dialog_leaves_the_page_below_as_it_was()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(RiverLibrary());

        // The view options over a detail page: B closes them, not the detail page.
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!;
        arcadia.Press(UiAction.ViewOptions);
        Assert.True(arcadia.Main.IsViewOptionsOpen);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsViewOptionsOpen);
        Assert.Same(detail, arcadia.Detail);

        // A hit's menu and the filter over the search: B closes them and leaves the keyboard out.
        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("river");
        var search = arcadia.Search!;
        Assert.True(search.IsKeyboardOut);
        arcadia.Press(UiAction.Options);
        Assert.True(arcadia.Main.IsGameMenuOpen);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsGameMenuOpen);
        Assert.True(search.IsKeyboardOut);
        Assert.Same(search, arcadia.Search);

        arcadia.Press(UiAction.Filter);
        Assert.True(arcadia.Main.IsViewOptionsOpen);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsViewOptionsOpen);
        Assert.True(search.IsKeyboardOut);
        Assert.Equal("river", search.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T155-9")]
    public async Task Another_button_or_a_page_change_while_A_is_held_drops_the_hold_without_a_late_start_or_details()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var tile = arcadia.FocusedGame!;
        arcadia.PressDown(UiAction.Accept).Wait(PastFeedback);
        Assert.True(tile.IsHolding);

        arcadia.Press(UiAction.NextTab);
        Assert.False(tile.IsHolding);
        arcadia.Wait(TimeSpan.FromSeconds(2)).LetGo(UiAction.Accept);
        Assert.Null(arcadia.Detail);
        Assert.Empty(arcadia.Starts.Keys);
        Assert.DoesNotContain(arcadia.PageGames, t => t.IsRunningOrStarting);

        // X opens the search while A is still down.
        tile = arcadia.FocusedGame!;
        arcadia.PressDown(UiAction.Accept).Wait(PastFeedback);
        arcadia.Press(UiAction.Search);
        Assert.False(tile.IsHolding);
        arcadia.Wait(TimeSpan.FromSeconds(2)).LetGo(UiAction.Accept);
        Assert.True(arcadia.Main.IsSearchShown);
        Assert.Null(arcadia.Detail);
        Assert.Empty(arcadia.Starts.Keys);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T155-10")]
    public async Task Losing_the_window_while_A_or_Menu_is_held_drops_the_hold_without_a_late_start_or_details()
    {
        using var arcadia = await SearchAsync(RiverLibrary(), "river");
        var hit = arcadia.Search!.FocusedResult!;
        arcadia.PressDown(UiAction.Settings).Wait(PastFeedback);
        Assert.True(hit.IsHolding);
        arcadia.LoseFocus();
        Assert.False(hit.IsHolding);
        arcadia.Wait(TimeSpan.FromSeconds(2)).LetGo(UiAction.Settings);
        Assert.Null(arcadia.Detail);
        Assert.Empty(arcadia.Starts.Keys);

        arcadia.Press(UiAction.Back, times: 2);
        var tile = arcadia.FocusedGame!;
        arcadia.PressDown(UiAction.Accept).Wait(Tap);
        arcadia.LoseFocus();
        arcadia.LetGo(UiAction.Accept).Wait(TimeSpan.FromSeconds(2));
        Assert.Null(arcadia.Detail);
        Assert.False(tile.IsRunningOrStarting);
        Assert.Empty(arcadia.Starts.Keys);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T155-11")]
    public async Task A_rescan_while_A_is_held_moves_the_press_to_the_games_new_cover_and_drops_it_if_the_game_is_gone()
    {
        var (games, gog) = Changeable(KnownLaunchers.Gog, "Bravo", "Charlie", "Delta");
        using var arcadia = await ArcadiaDriver.StartAsync([gog]);
        arcadia.Press(UiAction.Right);

        // Held: the ring goes on on the new cover and the details open.
        var before = arcadia.FocusedGame!;
        arcadia.PressDown(UiAction.Accept).Wait(PastFeedback);
        games.Add(Game(KnownLaunchers.Gog, "Alpha"));
        await arcadia.RescanAsync();
        var after = arcadia.FocusedGame!;
        Assert.NotSame(before, after);
        Assert.Equal("Charlie", after.Title);
        Assert.True(after.IsHolding);
        arcadia.Wait(PastHoldTime - PastFeedback);
        Assert.Equal("Charlie", arcadia.Detail?.Current.Tile.Title);
        arcadia.LetGo(UiAction.Accept).Press(UiAction.Back);
        Assert.Empty(arcadia.Starts.Keys);

        // Tapped: the game starts from its new cover.
        arcadia.PressDown(UiAction.Accept).Wait(Tap);
        games.Add(Game(KnownLaunchers.Gog, "Aaron"));
        await arcadia.RescanAsync();
        arcadia.LetGo(UiAction.Accept);
        Assert.True(arcadia.FocusedGame!.IsStarting);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "der Launcher soll das Spiel starten");
        Assert.Equal([arcadia.FocusedGame.Game.Key], arcadia.Starts.Keys);
        Assert.Equal("Charlie", arcadia.FocusedGame.Title);

        // Gone: nothing is held any more, nothing starts, no details open.
        arcadia.Press(UiAction.Right);
        var delta = arcadia.FocusedGame!;
        arcadia.PressDown(UiAction.Accept).Wait(PastFeedback);
        Remove(games, "Delta");
        await arcadia.RescanAsync();
        Assert.DoesNotContain(arcadia.PageGames, tile => tile.IsHolding);
        arcadia.Wait(TimeSpan.FromSeconds(2)).LetGo(UiAction.Accept);
        Assert.Null(arcadia.Detail);
        Assert.Single(arcadia.Starts.Keys);
        Assert.False(delta.IsRunningOrStarting);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T155-12")]
    public async Task A_rescan_while_Menu_is_held_on_a_hit_moves_the_press_to_its_new_hit_and_opens_its_details()
    {
        var (gogGames, gog) = Changeable(KnownLaunchers.Gog, "River Crossing", "River Raid");
        using var arcadia = await SearchAsync([gog], "river");
        var search = arcadia.Search!;
        var before = search.FocusedResult!;

        // The game ended meanwhile and has a new "zuletzt gespielt": its hit is built anew, as is the library under the
        // search. The press stays on the hit.
        arcadia.PressDown(UiAction.Settings).Wait(PastFeedback);
        var index = gogGames.FindIndex(game => game.Key == before.Game.Key);
        gogGames[index] = gogGames[index] with { LastPlayedAt = ArcadiaDriver.Now };
        await arcadia.RescanAsync();
        var after = search.FocusedResult!;
        Assert.NotSame(before, after);
        Assert.Equal(before.Game.Key, after.Game.Key);
        Assert.True(after.IsHolding);
        Assert.DoesNotContain(arcadia.PageGames, tile => tile.IsHolding);

        arcadia.Wait(PastHoldTime - PastFeedback);
        Assert.Equal(before.Title, arcadia.Detail?.Current.Tile.Title);
        Assert.True(arcadia.Detail?.IsFromSearch);
        arcadia.LetGo(UiAction.Settings);
        Assert.Empty(arcadia.Starts.Keys);
    }

    /// <summary>"river" hits three games at the title's start, in two launchers.</summary>
    private static IReadOnlyList<LauncherSpec> RiverLibrary() =>
    [
        Changeable(KnownLaunchers.Gog, "River Crossing", "River Raid", "Iron Harbor").Spec,
        Changeable(KnownLaunchers.Xbox, "Riverbend", "Silent Tides").Spec,
    ];

    /// <summary>X with the controller, then <paramref name="term"/> typed.</summary>
    private static async Task<ArcadiaDriver> SearchAsync(IReadOnlyList<LauncherSpec> launchers, string term)
    {
        var arcadia = await ArcadiaDriver.StartAsync(launchers);
        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch(term);
        return arcadia;
    }

    /// <summary>A launcher whose library the test changes before a rescan, as the user installs or removes games.</summary>
    private static (List<Game> Games, LauncherSpec Spec) Changeable(LauncherInfo launcher, params string[] titles)
    {
        var games = titles.Select(title => Game(launcher, title)).ToList();
        return (games, new LauncherSpec(launcher, games.Count, Library: games));
    }

    private static Game Game(LauncherInfo launcher, string title) => Installed(launcher, title, ArcadiaDriver.Now.AddDays(-30));

    private static void Remove(List<Game> games, string title) => Assert.Equal(1, games.RemoveAll(game => game.Title == title));

    /// <summary>The title shown beside the middle: -1 left, 1 right.</summary>
    private static string Side(GameDetailViewModel detail, int offset) => detail.Pages.Single(page => page.Offset == offset).Tile.Title;
}
