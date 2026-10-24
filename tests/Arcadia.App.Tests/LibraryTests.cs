using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The library with the controller alone: focus, tabs, pages and "Ansicht".</summary>
public sealed class LibraryTests
{
    [AvaloniaFact]
    [Trait("Testfall", "G-6")]
    public async Task The_D_pad_moves_the_focus_through_the_grid_and_stops_at_its_edges()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var main = arcadia.Main;

        arcadia.Press(UiAction.Left).Press(UiAction.Up);
        Assert.Equal(0, FocusedIndex(arcadia));

        arcadia.Press(UiAction.Right);
        Assert.Equal(1, FocusedIndex(arcadia));

        arcadia.Press(UiAction.Down);
        Assert.Equal(1 + Arcadia.App.ViewModels.MainViewModel.GridColumns, FocusedIndex(arcadia));

        arcadia.Press(UiAction.Up);
        Assert.Equal(1, FocusedIndex(arcadia));

        // Down from the last full row lands on the last tile of the page (25 per page: 3 rows of 8 and one tile).
        arcadia.Press(UiAction.Down, times: 5);
        Assert.Equal(main.PageGames.Count - 1, FocusedIndex(arcadia));
        Assert.Single(main.PageGames, tile => tile.IsFocused);
    }

    [AvaloniaFact]
    [Trait("Testfall", "G-7")]
    public async Task LB_and_RB_switch_the_launcher_tab_and_wrap_around()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();

        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Xbox", arcadia.Main.SelectedTab?.Name);
        Assert.Contains(arcadia.Main.PageGames, tile => tile.Game.LauncherId == "xbox");
        Assert.DoesNotContain(arcadia.Main.PageGames, tile => tile.Game.LauncherId != "xbox");

        arcadia.Press(UiAction.PreviousTab, times: 2);
        Assert.Equal("Amazon Games", arcadia.Main.SelectedTab?.Name);
        Assert.Single(arcadia.Main.Tabs, tab => tab.IsSelected);
    }

    [AvaloniaFact]
    [Trait("Testfall", "G-8")]
    public async Task LT_and_RT_turn_the_pages_and_the_page_bar_shows_the_current_page()
    {
        // GOG has 31 games: 25 on page 1, 6 on page 2.
        using var arcadia = await ArcadiaDriver.StartAsync();
        var main = arcadia.Main;
        Assert.Equal(25, main.PageGames.Count);
        Assert.Equal("1", CurrentPage(arcadia));

        arcadia.Press(UiAction.Right).Press(UiAction.NextPage);
        Assert.Equal(6, main.PageGames.Count);
        Assert.Equal("2", CurrentPage(arcadia));
        Assert.Equal(0, FocusedIndex(arcadia));

        // Past the last page nothing happens.
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("2", CurrentPage(arcadia));

        arcadia.Press(UiAction.PreviousPage);
        Assert.Equal(25, main.PageGames.Count);
        Assert.Equal("1", CurrentPage(arcadia));
    }

    [AvaloniaFact]
    [Trait("Testfall", "G-12")]
    [Trait("Testfall", "G-14")]
    public async Task View_options_change_the_page_size_at_once_and_offer_only_sizes_the_tab_can_fill()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var main = arcadia.Main;

        arcadia.Press(UiAction.ViewOptions);
        var options = Assert.IsType<Arcadia.App.ViewModels.ViewOptionsViewModel>(main.ViewOptions);

        // GOG has 31 games: 10, 25 and 50 can be chosen, 100 would show nothing more.
        Assert.Equal(["10", "25", "50", "100"], options.PageSizeOptions.Select(o => o.Label));
        Assert.Equal([true, true, true, false], options.PageSizeOptions.Select(o => o.IsEnabled));
        Assert.True(options.PageSizeOptions.Single(o => o.Label == "25").IsSelected);

        // From the selected sort (first row) down to the page sizes, onto 10, and choose it.
        arcadia.Press(UiAction.Down, times: 3).Press(UiAction.Left, times: 3).Press(UiAction.Accept);
        Assert.True(main.IsViewOptionsOpen);
        Assert.Equal(10, main.PageGames.Count);

        arcadia.Press(UiAction.Back);
        Assert.False(main.IsViewOptionsOpen);
        Assert.Equal(10, main.PageGames.Count);

        // A tab with few games offers only 10 and shows all of them.
        arcadia.Press(UiAction.PreviousTab).Press(UiAction.ViewOptions);
        Assert.Equal([true, false, false, false], main.ViewOptions!.PageSizeOptions.Select(o => o.IsEnabled));
        Assert.Equal(6, main.PageGames.Count);
    }

    /// <summary>Five games whose order under every sort is written out by hand, so the expectation is not Arcadia's own sorter.</summary>
    private static readonly LauncherSpec SortLibrary = LauncherSpec.With(
        KnownLaunchers.Gog,
        TestGames.Installed(KnownLaunchers.Gog, "Echo", installedAt: Day(2024, 3, 3), lastPlayedAt: Day(2025, 12, 24)),
        TestGames.NotInstalled(KnownLaunchers.Gog, "Delta"),
        TestGames.Installed(KnownLaunchers.Gog, "Charlie", installedAt: Day(2026, 9, 1), lastPlayedAt: Day(2026, 10, 20)),
        TestGames.Installed(KnownLaunchers.Gog, "bravo", installedAt: Day(2025, 5, 1)),
        TestGames.Installed(KnownLaunchers.Gog, "Alpha", installedAt: Day(2026, 1, 10), lastPlayedAt: Day(2026, 10, 1)));

    /// <summary>The rows and columns of "Ansicht": what the user picks and the order the tab must show then.</summary>
    public static TheoryData<int, int, string, string[]> Sorts => new()
    {
        { 0, 0, "Name A → Z", ["Alpha", "bravo", "Charlie", "Delta", "Echo"] },
        { 0, 1, "Name Z → A", ["Echo", "Delta", "Charlie", "bravo", "Alpha"] },
        { 1, 0, "Installationsdatum, neueste zuerst", ["Charlie", "Alpha", "bravo", "Echo", "Delta"] },
        { 1, 1, "Installationsdatum, älteste zuerst", ["Echo", "bravo", "Alpha", "Charlie", "Delta"] },
        { 2, 0, "Zuletzt gespielt, neueste zuerst", ["Charlie", "Alpha", "Echo", "bravo", "Delta"] },
        { 2, 1, "Zuletzt gespielt, älteste zuerst", ["Echo", "Alpha", "Charlie", "bravo", "Delta"] },
    };

    [AvaloniaTheory]
    [MemberData(nameof(Sorts))]
    [Trait("Testfall", "G-13")]
    public async Task Each_sort_in_view_options_orders_the_tab_with_games_without_a_date_last(int row, int column, string sort, string[] expected)
    {
        using var arcadia = await ArcadiaDriver.StartAsync([SortLibrary]);

        // From wherever the focus starts up to "Name A → Z", then to the wanted cell.
        arcadia.Press(UiAction.ViewOptions)
            .Press(UiAction.Up, times: 3).Press(UiAction.Left)
            .Press(UiAction.Down, times: row).Press(UiAction.Right, times: column)
            .Press(UiAction.Accept).Press(UiAction.Back);

        Assert.True(expected.SequenceEqual(arcadia.Main.PageGames.Select(tile => tile.Title)), $"{sort}: {string.Join(", ", arcadia.Main.PageGames.Select(tile => tile.Title))}");
    }

    [AvaloniaFact]
    [Trait("Testfall", "T8-1")]
    public async Task Sort_page_size_and_tab_are_the_same_after_a_restart_with_the_focus_on_page_one()
    {
        using var first = await ArcadiaDriver.StartAsync();
        first.Press(UiAction.ViewOptions)
            .Press(UiAction.Right).Press(UiAction.Accept)                                         // Name Z → A
            .Press(UiAction.Down, times: 3).Press(UiAction.Left, times: 3).Press(UiAction.Accept) // 10 per page
            .Press(UiAction.Back)
            .Press(UiAction.NextTab);
        var pageOne = first.Main.PageGames.Select(tile => tile.Title).ToList();

        // Arcadia is closed elsewhere than page one and its first game.
        first.Press(UiAction.NextPage).Press(UiAction.Right, times: 3);
        Assert.Equal("2", CurrentPage(first));
        Assert.Equal(3, FocusedIndex(first));

        using var restarted = await first.RestartAsync();
        var main = restarted.Main;

        Assert.Equal("Xbox", main.SelectedTab?.Name);
        Assert.Equal(10, main.PageGames.Count);
        Assert.Equal("1", CurrentPage(restarted));
        Assert.Equal(0, FocusedIndex(restarted));
        Assert.Equal(new SortOrder(SortField.Name, SortDirection.Descending), restarted.Settings.Current.LibrarySort);
        Assert.Equal(pageOne, main.PageGames.Select(tile => tile.Title));
    }

    private static int FocusedIndex(ArcadiaDriver arcadia) =>
        arcadia.Main.FocusedGame is { } focused ? arcadia.Main.PageGames.IndexOf(focused) : -1;

    private static string? CurrentPage(ArcadiaDriver arcadia) =>
        arcadia.Main.PageButtons.SingleOrDefault(button => button.IsCurrent)?.Label;

    private static DateTimeOffset Day(int year, int month, int day) => new(year, month, day, 18, 0, 0, TimeSpan.Zero);
}
