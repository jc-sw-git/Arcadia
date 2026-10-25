using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Input;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The library with the controller alone: focus, tabs, pages and "Ansicht".</summary>
public sealed class LibraryTests
{
    [AvaloniaFact]
    [Trait("TestCase", "G-6")]
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
        Assert.Equal(arcadia.PageGames.Count - 1, FocusedIndex(arcadia));
        Assert.Single(arcadia.PageGames, tile => tile.IsFocused);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-6")]
    public async Task The_D_pad_held_repeats_the_move_after_a_moment_and_stops_at_the_edge_of_the_page()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();

        // Shorter than the repeat delay: one step, as a tap.
        arcadia.HoldPad(TimeSpan.FromMilliseconds(300), PadButtons.DPadRight);
        Assert.Equal(1, FocusedIndex(arcadia));

        // Held a second: it goes on by itself, several steps further.
        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.DPadRight);
        Assert.InRange(FocusedIndex(arcadia), 5, 24);

        // Held long, down: row by row to the last tile of the page (25 per page: 3 rows of 8 and one tile), no further.
        arcadia.HoldPad(TimeSpan.FromSeconds(3), PadButtons.DPadDown);
        Assert.Equal(arcadia.PageGames.Count - 1, FocusedIndex(arcadia));
        Assert.Equal("1", CurrentPage(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-6")]
    public async Task The_left_stick_moves_the_focus_like_the_D_pad_but_not_when_only_nudged()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var tap = TimeSpan.FromMilliseconds(100);

        arcadia.HoldPad(tap, leftStick: new StickPosition(0.9, 0));
        Assert.Equal(1, FocusedIndex(arcadia));

        // Up on the stick is positive.
        arcadia.HoldPad(tap, leftStick: new StickPosition(0, -0.9));
        Assert.Equal(1 + Arcadia.App.ViewModels.MainViewModel.GridColumns, FocusedIndex(arcadia));

        arcadia.HoldPad(tap, leftStick: new StickPosition(0, 0.9)).HoldPad(tap, leftStick: new StickPosition(-0.9, 0));
        Assert.Equal(0, FocusedIndex(arcadia));

        // A stick that isn't quite at rest, or brushed in passing, doesn't navigate.
        arcadia.HoldPad(TimeSpan.FromSeconds(1), leftStick: new StickPosition(0.3, -0.3));
        Assert.Equal(0, FocusedIndex(arcadia));

        // Held, it repeats like the D-pad.
        arcadia.HoldPad(TimeSpan.FromSeconds(1), leftStick: new StickPosition(1, 0));
        Assert.InRange(FocusedIndex(arcadia), 5, 24);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-15")]
    public async Task The_preview_shows_last_played_playtime_size_launcher_and_location_of_the_focused_game()
    {
        var folder = Path.GetTempPath();
        var played = TestGames.Installed(KnownLaunchers.Gog, "Alpha", installedAt: Day(2026, 1, 10), lastPlayedAt: new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero)) with
        {
            Playtime = TimeSpan.FromHours(12.5),
            InstallSizeBytes = 38_118_773_964, // 35.5 GiB
            InstallDirectory = folder,
        };
        var never = TestGames.Installed(KnownLaunchers.Gog, "Bravo", installedAt: Day(2026, 2, 1)) with { InstallDirectory = folder };
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, played, never)]);

        var preview = arcadia.FocusedGame!;
        Assert.Equal("Alpha", preview.Title);
        Assert.Equal("Gestern", preview.LastPlayedText);
        Assert.Equal("12 Std.", preview.PlaytimeText);
        Assert.Equal("35,5 GB", preview.SizeText);
        Assert.Equal("GOG Galaxy", preview.LauncherName);
        Assert.Equal(folder, preview.LocationText);

        // The preview follows the focus; what the launcher doesn't report shows as such.
        arcadia.Press(UiAction.Right);
        preview = arcadia.FocusedGame!;
        Assert.Equal("Bravo", preview.Title);
        Assert.Equal("Nie gespielt", preview.LastPlayedText);
        Assert.Equal("–", preview.PlaytimeText);
        Assert.Equal("–", preview.SizeText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-7")]
    public async Task LB_and_RB_switch_the_launcher_tab_and_wrap_around()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();

        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Xbox", arcadia.SelectedTab?.Name);
        Assert.Contains(arcadia.PageGames, tile => tile.Game.LauncherId == "xbox");
        Assert.DoesNotContain(arcadia.PageGames, tile => tile.Game.LauncherId != "xbox");

        arcadia.Press(UiAction.PreviousTab, times: 2);
        Assert.Equal("Amazon Games", arcadia.SelectedTab?.Name);
        Assert.Single(arcadia.Main.Tabs, tab => tab.IsSelected);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-8")]
    public async Task LT_and_RT_turn_the_pages_and_the_page_bar_shows_the_current_page()
    {
        // GOG has 31 games: 25 on page 1, 6 on page 2.
        using var arcadia = await ArcadiaDriver.StartAsync();
        var main = arcadia.Main;
        Assert.Equal(25, arcadia.PageGames.Count);
        Assert.Equal("1", CurrentPage(arcadia));

        arcadia.Press(UiAction.Right).Press(UiAction.NextPage);
        Assert.Equal(6, arcadia.PageGames.Count);
        Assert.Equal("2", CurrentPage(arcadia));
        Assert.Equal(0, FocusedIndex(arcadia));

        // Past the last page nothing happens.
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("2", CurrentPage(arcadia));

        arcadia.Press(UiAction.PreviousPage);
        Assert.Equal(25, arcadia.PageGames.Count);
        Assert.Equal("1", CurrentPage(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-12")]
    [Trait("TestCase", "G-14")]
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
        Assert.Equal(10, arcadia.PageGames.Count);

        arcadia.Press(UiAction.Back);
        Assert.False(main.IsViewOptionsOpen);
        Assert.Equal(10, arcadia.PageGames.Count);

        // A tab with few games offers only 10 and shows all of them.
        arcadia.Press(UiAction.PreviousTab).Press(UiAction.ViewOptions);
        Assert.Equal([true, false, false, false], main.ViewOptions!.PageSizeOptions.Select(o => o.IsEnabled));
        Assert.Equal(6, arcadia.PageGames.Count);
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
    [Trait("TestCase", "G-13")]
    public async Task Each_sort_in_view_options_orders_the_tab_with_games_without_a_date_last(int row, int column, string sort, string[] expected)
    {
        using var arcadia = await ArcadiaDriver.StartAsync([SortLibrary]);

        // From wherever the focus starts up to "Name A → Z", then to the wanted cell.
        arcadia.Press(UiAction.ViewOptions)
            .Press(UiAction.Up, times: 3).Press(UiAction.Left)
            .Press(UiAction.Down, times: row).Press(UiAction.Right, times: column)
            .Press(UiAction.Accept).Press(UiAction.Back);

        Assert.True(expected.SequenceEqual(arcadia.PageGames.Select(tile => tile.Title)), $"{sort}: {string.Join(", ", arcadia.PageGames.Select(tile => tile.Title))}");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T8-1")]
    public async Task Sort_page_size_and_tab_are_the_same_after_a_restart_with_the_focus_on_page_one()
    {
        using var first = await ArcadiaDriver.StartAsync();
        first.Press(UiAction.ViewOptions)
            .Press(UiAction.Right).Press(UiAction.Accept)                                         // Name Z → A
            .Press(UiAction.Down, times: 3).Press(UiAction.Left, times: 3).Press(UiAction.Accept) // 10 per page
            .Press(UiAction.Back)
            .Press(UiAction.NextTab);
        var pageOne = first.PageGames.Select(tile => tile.Title).ToList();

        // Arcadia is closed elsewhere than page one and its first game.
        first.Press(UiAction.NextPage).Press(UiAction.Right, times: 3);
        Assert.Equal("2", CurrentPage(first));
        Assert.Equal(3, FocusedIndex(first));

        using var restarted = await first.RestartAsync();
        var main = restarted.Main;

        Assert.Equal("Xbox", restarted.SelectedTab?.Name);
        Assert.Equal(10, restarted.PageGames.Count);
        Assert.Equal("1", CurrentPage(restarted));
        Assert.Equal(0, FocusedIndex(restarted));
        Assert.Equal(new SortOrder(SortField.Name, SortDirection.Descending), restarted.Settings.Current.LibrarySort);
        Assert.Equal(pageOne, restarted.PageGames.Select(tile => tile.Title));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T8-2")]
    public async Task Sort_and_page_size_apply_to_every_tab_and_a_small_tab_shows_the_next_smaller_size_only_for_itself()
    {
        // GOG 31, Xbox 62, Amazon 6 games.
        using var arcadia = await ArcadiaDriver.StartAsync();
        var main = arcadia.Main;

        arcadia.Press(UiAction.ViewOptions);
        Assert.Equal("Sortierung und Einträge pro Seite, gültig für alle Launcher", main.ViewOptions!.Subtitle);
        arcadia.Press(UiAction.Right).Press(UiAction.Accept)                                                            // Name Z → A
            .Press(UiAction.Down, times: 3).Press(UiAction.Left, times: 3).Press(UiAction.Right, times: 2).Press(UiAction.Accept) // 50
            .Press(UiAction.Back);
        Assert.Equal(31, arcadia.PageGames.Count);
        AssertNameZtoA(arcadia);

        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Xbox", arcadia.SelectedTab?.Name);
        Assert.Equal(50, arcadia.PageGames.Count);
        Assert.Equal("Name Z–A  ·  50 pro Seite", main.ViewSummary);
        AssertNameZtoA(arcadia);

        // Six games: 50 isn't offered there, so 10, the largest size it allows.
        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Amazon Games", arcadia.SelectedTab?.Name);
        Assert.Equal(6, arcadia.PageGames.Count);
        Assert.Equal("Name Z–A  ·  10 pro Seite", main.ViewSummary);
        AssertNameZtoA(arcadia);

        // The choice itself is kept: the big tab shows 50 again.
        arcadia.Press(UiAction.PreviousTab);
        Assert.Equal("Xbox", arcadia.SelectedTab?.Name);
        Assert.Equal(50, arcadia.PageGames.Count);
        Assert.Equal("Name Z–A  ·  50 pro Seite", main.ViewSummary);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T8-3")]
    public async Task A_last_tab_whose_launcher_is_gone_starts_in_the_first_tab_and_in_its_own_again_once_it_is_back()
    {
        using var first = await ArcadiaDriver.StartAsync();
        first.Press(UiAction.NextTab);
        Assert.Equal("Xbox", first.SelectedTab?.Name);

        // Xbox is gone from Arcadia, e.g. its integration was removed.
        using var withoutXbox = await first.RestartAsync([ArcadiaDriver.DefaultLaunchers[0], ArcadiaDriver.DefaultLaunchers[2]]);
        Assert.Equal(["GOG Galaxy", "Amazon Games"], withoutXbox.Main.Tabs.Select(tab => tab.Name));
        Assert.Equal("GOG Galaxy", withoutXbox.SelectedTab?.Name);
        Assert.Same(withoutXbox.PageGames[0], withoutXbox.FocusedGame);
        Assert.False(withoutXbox.Main.Toast.IsVisible, withoutXbox.Main.Toast.Text);

        using var withXbox = await withoutXbox.RestartAsync(ArcadiaDriver.DefaultLaunchers);
        Assert.Equal("Xbox", withXbox.SelectedTab?.Name);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T41-7")]
    public async Task Sorted_by_install_date_games_not_installed_come_last_in_both_directions()
    {
        var library = LauncherSpec.With(
            KnownLaunchers.Gog,
            TestGames.NotInstalled(KnownLaunchers.Gog, "Aaron"),
            TestGames.Installed(KnownLaunchers.Gog, "Mitte", installedAt: Day(2025, 6, 1)),
            TestGames.NotInstalled(KnownLaunchers.Gog, "Zora"),
            TestGames.Installed(KnownLaunchers.Gog, "Neu", installedAt: Day(2026, 9, 1)),
            TestGames.Installed(KnownLaunchers.Gog, "Alt", installedAt: Day(2024, 2, 1)));
        using var arcadia = await ArcadiaDriver.StartAsync([library]);

        ChooseSort(arcadia, row: 1, column: 0);
        Assert.Equal(["Neu", "Mitte", "Alt"], Titles(arcadia).Take(3));
        Assert.Equal(["Aaron", "Zora"], Titles(arcadia).Skip(3).Order());

        ChooseSort(arcadia, row: 1, column: 1);
        Assert.Equal(["Alt", "Mitte", "Neu"], Titles(arcadia).Take(3));
        Assert.Equal(["Aaron", "Zora"], Titles(arcadia).Skip(3).Order());
    }

    /// <summary>"Ansicht", from wherever the focus starts up to "Name A → Z", then to the sort in that cell.</summary>
    private static void ChooseSort(ArcadiaDriver arcadia, int row, int column) =>
        arcadia.Press(UiAction.ViewOptions)
            .Press(UiAction.Up, times: 3).Press(UiAction.Left)
            .Press(UiAction.Down, times: row).Press(UiAction.Right, times: column)
            .Press(UiAction.Accept).Press(UiAction.Back);

    private static List<string> Titles(ArcadiaDriver arcadia) => arcadia.PageGames.Select(tile => tile.Title).ToList();

    private static void AssertNameZtoA(ArcadiaDriver arcadia)
    {
        var titles = Titles(arcadia);
        var descending = titles.OrderDescending(StringComparer.Create(Formatting.Culture, ignoreCase: true)).ToList();
        Assert.True(descending.SequenceEqual(titles), string.Join(", ", titles));
    }

    private static int FocusedIndex(ArcadiaDriver arcadia) =>
        arcadia.FocusedGame is { } focused ? arcadia.PageGames.ToList().IndexOf(focused) : -1;

    private static string? CurrentPage(ArcadiaDriver arcadia) =>
        arcadia.Main.PageButtons.SingleOrDefault(button => button.IsCurrent)?.Label;

    private static DateTimeOffset Day(int year, int month, int day) => new(year, month, day, 18, 0, 0, TimeSpan.Zero);
}
