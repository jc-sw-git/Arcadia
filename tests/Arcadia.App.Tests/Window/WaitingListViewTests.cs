using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// "Warten auf deine Auswahl" drawn (#491): its texts and tiles, the focus ring on a hit, the mouse (a click on a game, a
/// hit, a hint) and the keyboard, and the screen after the last game.
/// </summary>
public sealed class WaitingListViewTests
{
    private static async Task<(ArcadiaWindow Window, WaitingListViewModel List)> OpenAsync()
    {
        var window = await ArcadiaWindow.ShowAsync([WaitingListTests.Launcher], WaitingListTests.Settings, imageServer: WaitingListTests.Images());
        await WaitingListTests.FindNamesakesAsync(window.Arcadia);
        var list = WaitingListTests.OpenList(window.Arcadia);
        await WaitingListTests.ShownAsync(list);
        window.Settle();
        return (window, list);
    }

    private static Button HitButton(ArcadiaWindow window, WaitingHitViewModel hit) => Assert.Single(
        window.Window.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("waitingHit") && button.DataContext == hit);

    private static Button GameButton(ArcadiaWindow window, WaitingGameViewModel game) => Assert.Single(
        window.Window.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("waitingGame") && button.DataContext == game);

    private static Border Frame(ArcadiaWindow window, WaitingHitViewModel hit) => Assert.Single(
        HitButton(window, hit).GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("artworkFrame"));

    private static Border TermField(ArcadiaWindow window) => Assert.Single(
        window.Window.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("waitingTerm"));

    private static EdgeFade Fade(ArcadiaWindow window, string name) =>
        Assert.Single(window.Window.GetVisualDescendants().OfType<EdgeFade>(), fade => fade.Name == name);

    [AvaloniaFact]
    [Trait("TestCase", "T506-116")]
    public async Task Long_hit_names_take_two_lines_the_focused_hit_shows_its_whole_name_and_a_hover_shows_it_too()
    {
        const string longName = "Quiet Harbour: The Complete Adventure of the Long Forgotten Kingdom";
        var game = WaitingListTests.GameOf("n1", "Quiet Harbour");
        using var window = await ArcadiaWindow.ShowAsync([LauncherSpec.With(WaitingListTests.Other, [game])], WaitingListTests.Settings, imageServer: WaitingListTests.Images());
        window.Arcadia.SteamGridDb.Finds(game.Title, (501, longName, 2019), (502, longName + " II", 2023));
        await WaitingListTests.FindNamesakesAsync(window.Arcadia);
        var list = WaitingListTests.OpenList(window.Arcadia);
        await WaitingListTests.ShownAsync(list);
        window.Settle();
        window.Press(UiAction.Accept).Settle();
        var (focused, other) = (list.Hits!.Hits[0], list.Hits.Hits[1]);

        TextBlock NameOf(WaitingHitViewModel hit) => Assert.Single(
            HitButton(window, hit).GetVisualDescendants().OfType<TextBlock>(), text => text.Classes.Contains("waitingHitName"));
        var line = NameOf(other).FontSize * 1.25;
        Assert.InRange(NameOf(other).Bounds.Height, 1.5 * line, 2.5 * line);
        Assert.True(NameOf(focused).Bounds.Height > 2.5 * line, "Der fokussierte Treffer zeigt seinen ganzen Namen");
        Assert.Equal(longName + " II", ToolTip.GetTip(HitButton(window, other)));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-13")]
    [Trait("TestCase", "T506-80")]
    [Trait("TestCase", "T506-82")]
    [Trait("TestCase", "T506-98")]
    [Trait("TestCase", "T506-117")]
    public async Task The_screen_shows_two_levels_what_is_sought_and_the_own_term_with_the_ring_where_the_focus_is()
    {
        var (window, list) = await OpenAsync();
        using var _window = window;

        foreach (var text in new[]
        {
            AppTexts.WaitingTitle, AppTexts.WaitingQuestion, AppTexts.WaitingPosition(number: 1, count: 3), AppTexts.WaitingGames(3),
            AppTexts.WaitingIntro(count: 2, loads: AppTexts.WaitingLoadsBoth), AppTexts.WaitingSoughtBoth, AppTexts.WaitingNote,
            AppTexts.WaitingChooseHint, AppTexts.WaitingSkipHint, AppTexts.WaitingOpenHitsHint, AppTexts.Close,
            AppTexts.WaitingTerm, AppTexts.WaitingReset, "Golden Crown", "Iron Ember", "2019", "2023",
        })
        {
            Assert.NotEmpty(window.WithText(text));
        }

        Assert.Empty(window.WithText("2 Treffer"));
        var first = Frame(window, list.Hits!.Hits[0]);
        Assert.Equal(2.0 / 3, first.Bounds.Width / first.Bounds.Height, 2);

        // In the games list no hit has the ring; A brings it to the first hit, and the footer to the hits.
        Assert.DoesNotContain(list.Hits.Hits, hit => hit.IsFocused);
        window.Press(UiAction.Accept);
        window.AssertRingAround(first, "erster Treffer");
        foreach (var text in new[] { AppTexts.WaitingChooseHitHint, AppTexts.WaitingTakeHint, AppTexts.Back })
        {
            Assert.NotEmpty(window.WithText(text));
        }

        window.Press(UiAction.Right);
        window.AssertRingAround(Frame(window, list.Hits.Hits[1]), "zweiter Treffer");

        // ▲ from the top row: the field "Eigener Suchbegriff" with the game's search name, above the grid.
        window.Press(UiAction.Up);
        window.AssertRingAround(TermField(window), "Eigener Suchbegriff");
        Assert.True(window.Bounds(TermField(window)).Bottom < window.Bounds(Frame(window, list.Hits.Hits[0])).Top);
        Assert.Contains(TermField(window).GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Echo Drift" && text.IsEffectivelyVisible);
        Assert.NotEmpty(window.WithText(AppTexts.WaitingSearchHint));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-20")]
    [Trait("TestCase", "T491-17")]
    [Trait("TestCase", "T506-88")]
    public async Task Clicks_and_keys_pick_skip_take_and_close_and_after_the_last_game_only_b_is_left()
    {
        var (window, list) = await OpenAsync();
        using var _window = window;

        window.Click(GameButton(window, list.Games[2]));
        Assert.Equal("Iron Ember", list.Hits!.Title);
        await WaitingListTests.ShownAsync(list);
        window.Settle();

        // A click on a hit takes it; the next waiting game is the first.
        window.Click(HitButton(window, list.Hits.Hits[1]));
        await list.Loading;
        window.Settle();
        Assert.Equal((2, "Echo Drift"), (list.Games.Count, list.Hits!.Title));
        window.Click(window.OneWithText(AppTexts.WaitingSkipHint));
        Assert.Single(list.Games);

        // Keyboard: Space goes into the hits and takes.
        window.Key(Key.Space);
        Assert.True(list.InHits);
        window.Key(Key.Space);
        await list.Loading;
        window.Settle();

        Assert.True(list.IsDone);
        Assert.NotEmpty(window.WithText(AppTexts.WaitingDoneTitle));
        Assert.NotEmpty(window.WithText(AppTexts.WaitingDoneText));
        Assert.Empty(window.WithText(AppTexts.WaitingSkipHint));
        Assert.Empty(window.WithText(AppTexts.WaitingTakeHint));

        window.Click(window.OneWithText(AppTexts.Close));
        Assert.Null(window.Arcadia.Main.WaitingList);
        Assert.Equal(ArtworkNameRows.WaitingKey, window.Arcadia.Main.Settings!.FocusedRow!.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-106")]
    public async Task A_long_games_list_shows_a_scroll_bar_and_fades_where_more_follow_and_a_short_one_doesnt()
    {
        var (window, _) = await OpenAsync();
        using (window)
        {
            Assert.False(Fade(window, "GameFade").ShowsEnd);
            Assert.False(Fade(window, "HitFade").ShowsEnd);
        }

        var games = Enumerable.Range(1, 14).Select(i => WaitingListTests.GameOf($"saga{i}", $"Saga {i}")).ToArray();
        using var many = await ArcadiaWindow.ShowAsync([LauncherSpec.With(WaitingListTests.Other, games)], WaitingListTests.Settings, imageServer: WaitingListTests.Images());
        for (var i = 0; i < games.Length; i++)
        {
            many.Arcadia.SteamGridDb.Finds(games[i].Title, (501, games[i].Title, 2019), (502, games[i].Title, 2023));
        }

        await WaitingListTests.FindNamesakesAsync(many.Arcadia);
        var longList = WaitingListTests.OpenList(many.Arcadia);
        await WaitingListTests.ShownAsync(longList);
        many.Settle();

        var scroller = Assert.Single(many.Window.GetVisualDescendants().OfType<ScrollViewer>(), s => s.Name == "GameScroller");
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height);
        Assert.Contains(scroller.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>(), bar => bar.IsEffectivelyVisible && bar.Orientation == Avalonia.Layout.Orientation.Vertical);
        Assert.True(Fade(many, "GameFade").ShowsEnd);

        // Round to the last game: it is scrolled into sight, the fade now at the top.
        many.Press(UiAction.Up);
        many.Settle();
        Assert.True(Fade(many, "GameFade").ShowsStart);
        Assert.True(many.Bounds(GameButton(many, longList.Games[^1])).Bottom <= many.Bounds(scroller).Bottom + 1);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-9")]
    public async Task The_waiting_row_is_indented_below_bei_mehreren_treffern_with_its_badge_and_greys_out_with_ersten_nehmen()
    {
        using var window = await ArcadiaWindow.ShowAsync([WaitingListTests.Launcher], WaitingListTests.Settings, imageServer: WaitingListTests.Images());
        await WaitingListTests.FindNamesakesAsync(window.Arcadia);
        WaitingListTests.OpenDataSources(window.Arcadia);
        window.Arcadia.FocusRow(ArtworkNameRows.WaitingKey);
        window.Settle();

        var several = window.RowButton(window.Row(ArtworkNameRows.SeveralHitsKey));
        var waiting = window.RowButton(window.Row(ArtworkNameRows.WaitingKey));
        Assert.True(window.Bounds(waiting).Left > window.Bounds(several).Left + 10, "Warten auf deine Auswahl ist nicht eingerückt.");
        Assert.True(window.Bounds(waiting).Top > window.Bounds(several).Top);
        var badge = Assert.Single(waiting.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "3" && text.IsEffectivelyVisible);
        Assert.True(window.Bounds(badge).Width > 0);
        window.AssertRingAround(waiting, "Warten auf deine Auswahl");

        window.Arcadia.FocusRow(ArtworkNameRows.SeveralHitsKey);
        window.Press(UiAction.Accept);
        await WaitingListTests.SettledAsync(window.Arcadia);
        window.Settle();

        Assert.Contains("unavailable", window.RowButton(window.Row(ArtworkNameRows.WaitingKey)).Classes);
        Assert.DoesNotContain(window.RowButton(window.Row(ArtworkNameRows.WaitingKey)).GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "3" && text.IsEffectivelyVisible);
    }
}
