using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Avalonia.Headless.XUnit;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>What the controller does with the search's hits (#12, #127): start, details, menu, filter and sort.</summary>
public sealed class SearchHitTests
{
    private static readonly TimeSpan Tap = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    /// <summary>"river" hits three games at the title's start; every sort puts them in another order.</summary>
    private static readonly IReadOnlyList<LauncherSpec> RiverLibrary =
    [
        LauncherSpec.With(
            KnownLaunchers.Gog,
            Installed(KnownLaunchers.Gog, "River Crossing", ArcadiaDriver.Now.AddDays(-30), lastPlayedAt: ArcadiaDriver.Now.AddDays(-2)),
            NotInstalled(KnownLaunchers.Gog, "River Raid"),
            Installed(KnownLaunchers.Gog, "Iron Harbor", ArcadiaDriver.Now.AddDays(-30))),
        LauncherSpec.With(
            KnownLaunchers.Xbox,
            Installed(KnownLaunchers.Xbox, "Riverbend", ArcadiaDriver.Now.AddDays(-5), lastPlayedAt: ArcadiaDriver.Now.AddDays(-10)),
            Installed(KnownLaunchers.Xbox, "Silent Tides", ArcadiaDriver.Now.AddDays(-5))),
    ];

    [AvaloniaFact]
    [Trait("TestCase", "T12-7")]
    public async Task Start_tapped_on_a_hit_starts_it_on_the_release_or_installs_it_held_opens_its_details_and_View_its_menu()
    {
        using var arcadia = await SearchAsync("river");
        var search = arcadia.Search!;
        Assert.Equal("River Crossing", search.FocusedResult?.Title);

        // The start is requested synchronously (IsStarting); the launcher only hears of it later, on the thread pool.
        var tile = search.FocusedResult!;
        arcadia.PressDown(UiAction.Settings).Wait(Tap);
        Assert.False(tile.IsRunningOrStarting);
        Assert.Empty(arcadia.Starts.Keys);
        arcadia.LetGo(UiAction.Settings);
        Assert.True(tile.IsStarting);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "der Launcher soll das Spiel starten");
        Assert.Equal([search.FocusedResult!.Game.Key], arcadia.Starts.Keys);

        arcadia.Press(UiAction.SecondaryRight);
        Assert.Equal("River Raid", search.FocusedResult?.Title);
        arcadia.Hold(UiAction.Settings, Tap);
        Assert.Equal("Demo-Spiel: Hier würde jetzt „River Raid“ installiert.", arcadia.Main.Toast.Text);
        Assert.Single(arcadia.Starts.Keys);

        arcadia.Press(UiAction.Options);
        var menu = Assert.IsType<GameMenuViewModel>(arcadia.Main.GameMenu);
        Assert.True(menu.AtCover);
        Assert.Equal("RIVER RAID", menu.Title);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.GameMenu);

        arcadia.PressDown(UiAction.Settings).Wait(TimeSpan.FromMilliseconds(300));
        Assert.True(search.FocusedResult!.IsHolding);
        Assert.Null(arcadia.Detail);
        arcadia.Wait(PastHoldTime - TimeSpan.FromMilliseconds(300));
        var detail = Assert.IsType<GameDetailViewModel>(arcadia.Detail);
        Assert.True(detail.IsFromSearch);
        Assert.Equal("River Raid", detail.Current.Tile.Title);
        arcadia.LetGo(UiAction.Settings);
        Assert.Single(arcadia.Starts.Keys);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T12-8")]
    public async Task The_details_of_a_hit_turn_through_the_hits_in_their_order_without_tabs_and_LB_RB_and_Y_do_nothing()
    {
        using var arcadia = await SearchAsync("river");
        var main = arcadia.Main;
        arcadia.Press(UiAction.SecondaryRight);
        arcadia.PressDown(UiAction.Settings).Wait(PastHoldTime).LetGo(UiAction.Settings);

        var detail = Assert.IsType<GameDetailViewModel>(arcadia.Detail);
        Assert.True(detail.IsFromSearch);
        Assert.Null(detail.TabName);
        Assert.False(main.IsTabStripShown);
        Assert.Equal("River Raid", detail.Current.Tile.Title);
        Assert.Equal("2 von 3", detail.PositionText);

        arcadia.Press(UiAction.NextPage);
        Assert.Equal("Riverbend", detail.Current.Tile.Title);
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("River Crossing", detail.Current.Tile.Title);
        Assert.Equal("1 von 3", detail.PositionText);
        arcadia.Press(UiAction.PreviousPage);
        Assert.Equal("Riverbend", detail.Current.Tile.Title);

        var tab = arcadia.SelectedTab;
        arcadia.Press(UiAction.NextTab).Press(UiAction.PreviousTab).Press(UiAction.ViewOptions);
        Assert.Same(detail, arcadia.Detail);
        Assert.Equal("Riverbend", detail.Current.Tile.Title);
        Assert.Same(tab, arcadia.SelectedTab);
        Assert.Null(main.ViewOptions);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T127-4")]
    public async Task From_the_details_of_a_hit_X_returns_to_the_search_as_it_was_and_B_goes_to_the_game_in_its_launchers_listing()
    {
        // Thirty games before it in the Xbox tab, so it sits on page two.
        var fillers = Enumerable.Range(1, 30).Select(i => Installed(KnownLaunchers.Xbox, $"Filler {i:00}", ArcadiaDriver.Now.AddDays(-10)));
        var zephyr = Installed(KnownLaunchers.Xbox, "Zephyr Run", ArcadiaDriver.Now.AddDays(-60));
        IReadOnlyList<LauncherSpec> library =
        [
            LauncherSpec.With(KnownLaunchers.Gog, Installed(KnownLaunchers.Gog, "Zephyr Gate", ArcadiaDriver.Now.AddDays(-10))),
            LauncherSpec.With(KnownLaunchers.Xbox, [.. fillers, zephyr]),
        ];
        using var arcadia = await ArcadiaDriver.StartAsync(library);
        var main = arcadia.Main;
        arcadia.Press(UiAction.Search);
        main.TypeInSearch("zephyr");
        var search = arcadia.Search!;
        Assert.Equal(["Zephyr Gate", "Zephyr Run"], search.Hits.Select(game => game.Title));

        arcadia.PressDown(UiAction.Settings).Wait(PastHoldTime).LetGo(UiAction.Settings);
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("Zephyr Run", arcadia.Detail?.Current.Tile.Title);

        arcadia.Press(UiAction.Search);
        Assert.Null(arcadia.Detail);
        Assert.Same(search, arcadia.Search);
        Assert.Equal("zephyr", search.Text);
        Assert.Equal("Zephyr Run", search.FocusedResult?.Title);

        arcadia.PressDown(UiAction.Settings).Wait(PastHoldTime).LetGo(UiAction.Settings);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Detail);
        Assert.Null(arcadia.Search);
        Assert.Equal("Xbox", arcadia.SelectedTab?.Name);
        Assert.Equal("2", arcadia.CurrentPage);
        Assert.Equal(zephyr.Key, arcadia.FocusedGame?.Game.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T127-3")]
    public async Task X_on_a_detail_page_of_the_library_opens_the_search_and_B_there_comes_back_to_the_same_page()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(RiverLibrary);
        var main = arcadia.Main;
        arcadia.Press(UiAction.Right);
        arcadia.PressDown(UiAction.Accept).Wait(PastHoldTime).LetGo(UiAction.Accept);
        var detail = Assert.IsType<GameDetailViewModel>(arcadia.Detail);
        var game = detail.Current.Tile.Game.Key;

        arcadia.Press(UiAction.Search);
        Assert.True(main.IsSearchShown);
        Assert.Null(arcadia.Detail);

        // The first B puts the on-screen keyboard away, the second leaves the search.
        arcadia.Press(UiAction.Back);
        Assert.True(main.IsSearchShown);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Search);
        Assert.Same(detail, arcadia.Detail);
        Assert.True(detail.IsFromLibrary);
        Assert.Equal(game, detail.Current.Tile.Game.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T12-15")]
    public async Task L3_opens_filter_and_sort_whose_every_choice_counts_at_once_and_a_new_search_starts_by_relevance()
    {
        using var arcadia = await SearchAsync("river");
        var main = arcadia.Main;
        var search = arcadia.Search!;

        arcadia.Press(UiAction.Filter);
        var filter = Assert.IsType<ViewOptionsViewModel>(main.ViewOptions);
        Assert.Equal("Filter und Sortierung", filter.Title);
        Assert.Equal(["Alle Launcher", "GOG Galaxy", "Xbox"], filter.LauncherRows.Single().Select(option => option.Label));
        Assert.Equal(["Relevanz", "Name", "Installationsdatum", "Zuletzt gespielt"], filter.SortRows.Select(row => row.Label));
        Assert.All(filter.SortRows.Skip(1), row => Assert.Equal(2, row.Options.Count));
        Assert.True(filter.SortRows[0].Options.Single().IsSelected);
        Assert.Equal("Sortiert nach Relevanz", search.SortText);

        // Several launchers at once: GOG off leaves Xbox, GOG on again both.
        Choose(arcadia, row: 0, column: 1);
        Assert.Equal(["Riverbend"], search.Hits.Select(game => game.Title));
        Assert.Equal([false, false, true], main.ViewOptions!.LauncherRows.Single().Select(option => option.IsSelected));
        Choose(arcadia, row: 0, column: 1);
        Assert.Equal(3, search.Hits.Count);
        Assert.Equal([true, true, true], main.ViewOptions!.LauncherRows.Single().Select(option => option.IsSelected));

        (int Row, int Column, string Label, string[] Order)[] sorts =
        [
            (2, 0, "Name A–Z", ["River Crossing", "River Raid", "Riverbend"]),
            (2, 1, "Name Z–A", ["Riverbend", "River Raid", "River Crossing"]),
            (3, 0, "Installiert · neueste zuerst", ["Riverbend", "River Crossing", "River Raid"]),
            (3, 1, "Installiert · älteste zuerst", ["River Crossing", "Riverbend", "River Raid"]),
            (4, 0, "Zuletzt gespielt · neueste zuerst", ["River Crossing", "Riverbend", "River Raid"]),
            (4, 1, "Zuletzt gespielt · älteste zuerst", ["Riverbend", "River Crossing", "River Raid"]),
        ];
        foreach (var (row, column, label, order) in sorts)
        {
            Choose(arcadia, row, column);
            Assert.True(main.IsViewOptionsOpen);
            Assert.Equal($"Sortiert nach {label}", search.SortText);
            Assert.Equal(order, search.Hits.Select(game => game.Title));
        }

        arcadia.Press(UiAction.Back);
        Assert.False(main.IsViewOptionsOpen);
        arcadia.Press(UiAction.Filter).Press(UiAction.Filter);
        Assert.False(main.IsViewOptionsOpen);
        Assert.Equal("Sortiert nach Zuletzt gespielt · älteste zuerst", search.SortText);

        // Leave the search (B puts the keyboard away, B again closes) and search anew.
        arcadia.Press(UiAction.Back, times: 2);
        Assert.Null(arcadia.Search);
        arcadia.Press(UiAction.Search);
        Assert.Equal("Sortiert nach Relevanz", arcadia.Search!.SortText);
    }

    /// <summary>X with the controller, then <paramref name="term"/> typed (the on-screen keyboard has tests of its own).</summary>
    private static async Task<ArcadiaDriver> SearchAsync(string term)
    {
        var arcadia = await ArcadiaDriver.StartAsync(RiverLibrary);
        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch(term);
        return arcadia;
    }

    /// <summary>The D-pad to the dialog's top left, then down and right to the option, and A.</summary>
    private static void Choose(ArcadiaDriver arcadia, int row, int column)
    {
        arcadia.Press(UiAction.Up, times: 6).Press(UiAction.Left, times: 4).Press(UiAction.Down, times: row).Press(UiAction.Right, times: column);
        Assert.True(arcadia.Main.ViewOptions!.FocusPosition == (row, column), $"Fokus auf {arcadia.Main.ViewOptions.FocusPosition}, nicht auf ({row}, {column})");
        arcadia.Press(UiAction.Accept);
    }
}
