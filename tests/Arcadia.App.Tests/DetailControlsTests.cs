using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Input;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The detail page's controls after the owner's hand test of #43 (#530): the jumps of a held LT/RT, the glyph in the
/// focus ring of an area, "Infos" scrolled without entering it, and the arrows in "Übersicht anpassen".
/// </summary>
public sealed class DetailControlsTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T530-2")]
    public async Task Held_RT_jumps_at_the_first_turn_and_again_on_release_but_never_in_between()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!;
        var jumps = 0;
        detail.Jumped += _ => jumps++;

        arcadia.HoldPad(TimeSpan.FromMilliseconds(1000), PadButtons.RightTrigger, letGo: false);
        Assert.Equal(1, jumps);

        arcadia.HoldPad(TimeSpan.FromMilliseconds(20));
        Assert.Equal(detail.Covers.Single(cover => cover.Offset == 0).Tile, detail.Current.Tile);
        Assert.Equal(2, jumps);
        arcadia.Wait(TimeSpan.FromSeconds(2));
        Assert.Equal(2, jumps);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T530-1")]
    public async Task In_Uebersicht_anpassen_left_and_right_move_between_the_columns_at_the_same_height()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        arcadia.Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Up).Press(UiAction.Accept);
        var dialog = Assert.IsType<CustomizeOverviewViewModel>(arcadia.Main.CustomizeOverview);
        string Focused() => dialog.Entries.Single(entry => entry.IsFocused).Title;

        // Left "Controller" (2 entries), right "Verwalten"; five ticked, so the two title entries are greyed and passed over.
        Assert.True(dialog.MovesBothWays);
        Assert.False(dialog.MovesInOneColumn);
        Assert.Equal(AppTexts.DetailControllerModeTitle, Focused());
        arcadia.Press(UiAction.Right);
        Assert.Equal(AppTexts.ActionChangeArtwork, Focused());
        arcadia.Press(UiAction.Down);
        Assert.Equal(AppTexts.ActionTags, Focused());
        arcadia.Press(UiAction.Left);
        Assert.Equal(AppTexts.DetailMouseModeTitle, Focused());

        // From the last entry on the right, below the left column's end: the left column's nearest, its last.
        arcadia.Press(UiAction.Right).Press(UiAction.Down);
        Assert.Equal(AppTexts.ActionUninstallMore, Focused());
        arcadia.Press(UiAction.Left);
        Assert.Equal(AppTexts.DetailMouseModeTitle, Focused());

        // A fresh ◀ on the left goes round to the right; a held ▶ on the right stops there.
        arcadia.Press(UiAction.Left);
        Assert.Equal(AppTexts.ActionTags, Focused());
        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.DPadRight);
        Assert.Equal(AppTexts.ActionTags, Focused());

        // "Für alle Spiele übernehmen" stands below both: ◀ ▶ do nothing there; the question keeps its own ◀ ▶.
        arcadia.Press(UiAction.Down, times: 2);
        Assert.True(dialog.IsApplyFocused);
        arcadia.Press(UiAction.Left).Press(UiAction.Right);
        Assert.True(dialog.IsApplyFocused);
        arcadia.Press(UiAction.Accept);
        Assert.True(dialog.IsAsking);
        Assert.False(dialog.MovesBothWays);
        arcadia.Press(UiAction.Right);
        Assert.True(dialog.IsReplaceFocused);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T530-3")]
    [Trait("TestCase", "T530-4")]
    public async Task Areas_with_rows_show_As_glyph_in_their_ring_and_Infos_scrolls_with_RS_without_A()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!;
        var page = detail.Current;
        Assert.DoesNotContain(page.Areas, area => area.ShowsEnterHint);

        // Down the rail: every area but "Infos" shows A's glyph while it has the focus, and only that one.
        arcadia.Press(UiAction.Down);
        foreach (var _ in page.Areas)
        {
            var focused = Assert.Single(page.Areas, area => area.IsFocused);
            Assert.Equal(focused.IsInfos ? [] : [focused], page.Areas.Where(area => area.ShowsEnterHint));
            Assert.Equal(focused.IsInfos ? null : AppTexts.Select, page.AcceptHint);
            arcadia.Press(UiAction.Down);
        }

        // In the rows the ring is on a row, so no area shows the glyph.
        arcadia.Press(UiAction.Accept);
        Assert.Equal(DetailZone.Rows, page.Zone);
        Assert.DoesNotContain(page.Areas, area => area.ShowsEnterHint);
        arcadia.Press(UiAction.Back);

        // "Infos": RS scrolls its text from the rail while it is longer than it shows (the view says so); A does nothing.
        while (!page.OpenArea.IsInfos)
        {
            arcadia.Press(UiAction.Down);
        }

        var scrolls = new List<int>();
        detail.InfoScrollRequested += scrolls.Add;
        Assert.True(page.IsReadingInfos);
        arcadia.Press(UiAction.SecondaryDown);
        Assert.Empty(scrolls);
        detail.InfoOverflows = true;
        arcadia.Press(UiAction.SecondaryDown).Press(UiAction.SecondaryUp);
        Assert.Equal([1, -1], scrolls);
        arcadia.Press(UiAction.Accept);
        Assert.Equal(DetailZone.Rail, page.Zone);
        Assert.True(page.OpenArea.IsInfos);

        // The page keeps reading "Infos" from the rail across a turn: a focus carried in its rows (an older page) can't be.
        arcadia.Press(UiAction.NextPage);
        Assert.True(detail.Current.IsReadingInfos);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T530-8")]
    [Trait("TestCase", "T530-9")]
    public async Task R3_filters_the_tab_from_the_detail_page_and_a_game_that_drops_out_gives_way_to_the_next_hit()
    {
        var gog = KnownLaunchers.Gog;
        var day = ArcadiaDriver.Now.AddDays(-10);
        Game[] games =
        [
            TestGames.Installed(gog, "Alpha", day),
            TestGames.NotInstalled(gog, "Bravo"),
            TestGames.NotInstalled(gog, "Charlie"),
            TestGames.Installed(gog, "Delta", day),
            TestGames.Installed(gog, "Echo", day),
        ];
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(gog, games)]);
        arcadia.Press(UiAction.Right, times: 2);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!;
        Assert.Equal("Charlie", detail.Current.Tile.Title);

        // R3 opens the tab's filter over the page; "Nur installierte", B on the values, B closes and applies.
        arcadia.Press(UiAction.KeyboardLayout);
        Assert.NotNull(arcadia.Main.FilterModal);
        arcadia.Press(UiAction.Right).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Back).Press(UiAction.Back);
        Assert.Null(arcadia.Main.FilterModal);

        // Charlie dropped out: the page shows the next hit after it, Delta, and turns through the hits only.
        Assert.Same(detail, arcadia.Detail);
        Assert.Equal("Delta", detail.Current.Tile.Title);
        Assert.Equal(["Alpha", "Delta", "Echo"], detail.Games.Select(game => game.Title));
        Assert.True(arcadia.Main.Library.IsFiltered);
        arcadia.Press(UiAction.NextPage, times: 2);
        Assert.Equal("Alpha", detail.Current.Tile.Title);

        // "Alle Filter zurücksetzen" above the categories, R3 closes: the shown game stays, all games are back.
        arcadia.Press(UiAction.KeyboardLayout).Press(UiAction.Up).Press(UiAction.Accept).Press(UiAction.KeyboardLayout);
        Assert.Null(arcadia.Main.FilterModal);

        Assert.Equal("Alpha", detail.Current.Tile.Title);
        Assert.Equal(5, detail.Games.Count);

        // B returns to the library on the game shown last, with the tab's filter as set here.
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsDetailOpen);
        Assert.Equal("Alpha", arcadia.Main.Library.FocusedGame?.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T530-9")]
    public async Task A_filter_from_the_detail_page_that_leaves_no_hit_closes_the_page_to_the_library()
    {
        var gog = KnownLaunchers.Gog;
        var day = ArcadiaDriver.Now.AddDays(-10);
        Game[] games =
        [
            TestGames.Installed(gog, "Alpha", day),
            TestGames.NotInstalled(gog, "Bravo"),
            TestGames.Installed(gog, "Charlie", day) with { Type = GameType.Soundtrack },
        ];
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(gog, games)]);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.NotNull(arcadia.Detail);

        // Uninstalled soundtracks: there are none, so no game of the tab passes.
        arcadia.Press(UiAction.KeyboardLayout);
        Assert.NotNull(arcadia.Main.FilterModal);
        arcadia.Press(UiAction.Accept).Press(UiAction.Down, times: 2).Press(UiAction.Accept)
            .Press(UiAction.Back).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Right, times: 2).Press(UiAction.Accept);
        Assert.Equal("0", arcadia.Main.FilterModal!.HitsText);
        arcadia.Press(UiAction.KeyboardLayout);

        // The page closes, the library shows that the filter hides every game, and its "Filter ändern" takes A.
        Assert.Null(arcadia.Main.FilterModal);
        Assert.False(arcadia.Main.IsDetailOpen);
        Assert.Null(arcadia.Detail);
        Assert.True(arcadia.Main.IsNoHitsShown);
        arcadia.Press(UiAction.Accept);
        Assert.NotNull(arcadia.Main.FilterModal);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T560-2")]
    public async Task The_play_button_shows_As_glyph_only_while_it_has_the_focus()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var play = Assert.Single(arcadia.Detail!.Current.Buttons, button => button.IsPrimary);
        Assert.True(play.IsFocused);
        Assert.Equal((true, false), (play.ShowsPressGlyph, play.ShowsIcon));

        // Down into the rail: A presses the rail now, so the button shows its icon instead.
        arcadia.Press(UiAction.Down);
        Assert.False(play.IsFocused);
        Assert.Equal((false, true), (play.ShowsPressGlyph, play.ShowsIcon));
        Assert.All(arcadia.Detail.Current.Buttons, button => Assert.False(button.ShowsPressGlyph));
    }
}
