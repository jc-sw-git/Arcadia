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
        await list.Covers.WaitAsync(TimeSpan.FromSeconds(10));
        window.Settle();
        return (window, list);
    }

    private static Button HitButton(ArcadiaWindow window, WaitingHitViewModel hit) => Assert.Single(
        window.Window.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("waitingHit") && button.DataContext == hit);

    private static Button GameButton(ArcadiaWindow window, WaitingGameViewModel game) => Assert.Single(
        window.Window.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("waitingGame") && button.DataContext == game);

    private static Border Frame(ArcadiaWindow window, WaitingHitViewModel hit) => Assert.Single(
        HitButton(window, hit).GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("artworkFrame"));

    [AvaloniaFact]
    [Trait("TestCase", "T491-13")]
    public async Task The_screen_shows_the_games_the_hits_and_the_hints_with_the_ring_on_the_focused_hit()
    {
        var (window, list) = await OpenAsync();
        using var _window = window;

        foreach (var text in new[]
        {
            AppTexts.WaitingTitle, AppTexts.WaitingQuestion, AppTexts.WaitingPosition(number: 1, count: 3), AppTexts.WaitingGames(3),
            AppTexts.WaitingIntro(2), AppTexts.WaitingNote, AppTexts.WaitingChooseHint, AppTexts.WaitingSkipHint, AppTexts.WaitingTakeHint, AppTexts.Close,
            "Golden Crown", "Iron Ember", "2019", "2023",
        })
        {
            Assert.NotEmpty(window.WithText(text));
        }

        Assert.Equal(3, window.WithText(AppTexts.WaitingHitCount(2)).Count);
        var first = Frame(window, list.Hits[0]);
        Assert.Equal(2.0 / 3, first.Bounds.Width / first.Bounds.Height, 2);
        window.AssertRingAround(first, "erster Treffer");
        window.Press(UiAction.Right);
        window.AssertRingAround(Frame(window, list.Hits[1]), "zweiter Treffer");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T491-20")]
    [Trait("TestCase", "T491-17")]
    public async Task Clicks_and_keys_pick_skip_take_and_close_and_after_the_last_game_only_b_is_left()
    {
        var (window, list) = await OpenAsync();
        using var _window = window;

        window.Click(GameButton(window, list.Games[2]));
        Assert.Equal("Iron Ember", list.CurrentTitle);
        window.Click(HitButton(window, list.Hits[1]));
        Assert.True(list.Hits[1].IsFocused);
        window.Click(window.OneWithText(AppTexts.WaitingSkipHint));
        Assert.Equal(2, list.Games.Count);

        // Keyboard: V skips, Space takes.
        window.Key(Key.V);
        Assert.Single(list.Games);
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
