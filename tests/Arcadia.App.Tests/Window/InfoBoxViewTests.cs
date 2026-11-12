using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.App.Views;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// The detail page's turning and its info box as drawn (#506, scenarios 1 to 5): quick turns show the newest game at once,
/// the value chip, the note on top, the value boxes, and RS scrolling a box that doesn't fit, back to its top on a change.
/// </summary>
public sealed class InfoBoxViewTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T506-1")]
    [Trait("TestCase", "T506-2")]
    [Trait("TestCase", "T506-3")]
    public async Task A_turn_on_its_own_jumps_and_quick_turns_show_the_newest_game_at_once_with_nothing_replayed()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var games = arcadia.Arcadia.PageGames.Select(tile => tile.Title).ToList();
        OpenByHoldingA(arcadia);
        var heading = Part<StackPanel>(arcadia, "Heading");

        var jumps = 0;
        arcadia.Arcadia.Detail!.Jumped += _ => jumps++;

        var pressed = System.Diagnostics.Stopwatch.StartNew();
        arcadia.Press(UiAction.NextPage);
        Assert.Equal(1, jumps);

        // The animation runs on real time: on a loaded machine its first frame may come after its end.
        if (pressed.Elapsed < GameDetailViewModel.JumpTime / 2)
        {
            Assert.True(heading.Opacity < 1, "Ein einzelner Druck springt.");
        }

        arcadia.WaitForTransitions();
        Assert.Equal(1, heading.Opacity, 2);

        // Four quick presses: the first jumps, each next one stops the running jump and shows the newest game at once;
        // when they end, nothing is replayed.
        arcadia.Arcadia.Time.Advance(TimeSpan.FromSeconds(1));
        arcadia.Press(UiAction.NextPage).Press(UiAction.NextPage).Press(UiAction.NextPage).Press(UiAction.NextPage).Settle();
        AssertDetailOf(arcadia, games[5]);
        Assert.True(heading.Opacity > 0.99, $"Titel mit Deckkraft {heading.Opacity}, Y {((Avalonia.Media.TranslateTransform)heading.RenderTransform!).Y}");
        var content = Part<Panel>(arcadia, "AreaContent");
        Assert.True(content.Opacity > 0.99, $"Inhalt mit Deckkraft {content.Opacity}");
        Assert.Equal(2, jumps);
        arcadia.Arcadia.Time.Advance(TimeSpan.FromSeconds(2));
        arcadia.WaitForTransitions();
        AssertDetailOf(arcadia, games[5]);
        Assert.Equal(1, heading.Opacity, 2);
        Assert.Equal(2, jumps);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-11")]
    [Trait("TestCase", "T506-14")]
    [Trait("TestCase", "T506-21")]
    [Trait("TestCase", "T506-23")]
    [Trait("TestCase", "T506-27")]
    public async Task The_value_rows_show_a_chip_and_the_info_box_puts_the_reason_on_top_then_name_boxes_and_text()
    {
        var notInstalled = TestGames.NotInstalled(KnownLaunchers.Gog, "Bravo");
        using var arcadia = await ArcadiaWindow.ShowAsync([LauncherSpec.With(KnownLaunchers.Gog, notInstalled)], new AppSettings { ActiveLaunchers = ["gog"] });
        OpenByHoldingA(arcadia);
        arcadia.Arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.ControllerMode);
        arcadia.WaitForTransitions();

        // The chip of the settings' value rows, on both rows of "Controller".
        Assert.Equal(2, Visible<Border>(arcadia, "valuePill").Count);

        var infoBox = Part<Border>(arcadia, "InfoBox");
        var note = Assert.Single(Within<Border>(infoBox, "infoNote"));
        var title = Assert.Single(infoBox.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text == AppTexts.DetailControllerModeTitle);
        var boxes = Within<Border>(infoBox, "valueBox");
        Assert.Equal(3, boxes.Count);
        Assert.Single(boxes, box => box.Classes.Contains("current"));
        Assert.True(arcadia.Bounds(note).Bottom <= arcadia.Bounds(title).Top, "Der Hinweis steht über dem Namen.");
        Assert.True(arcadia.Bounds(title).Bottom <= arcadia.Bounds(boxes[0]).Top, "Die Wertboxen stehen unter dem Namen.");
        Assert.Contains("dimmed", infoBox.GetVisualDescendants().OfType<ValueBoxesView>().Single().Classes);

        // The highlight has a frame, not only a colour of its own.
        var current = boxes.Single(box => box.Classes.Contains("current"));
        Assert.True(current.BorderThickness.Top > 0 && current.BorderBrush is not null);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-27")]
    public async Task A_row_without_a_note_shows_none()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        OpenByHoldingA(arcadia);
        arcadia.Arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Artwork);
        arcadia.WaitForTransitions();

        Assert.Empty(Within<Border>(Part<Border>(arcadia, "InfoBox"), "infoNote"));
        Assert.Empty(Within<Border>(Part<Border>(arcadia, "InfoBox"), "valueBox"));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-28")]
    [Trait("TestCase", "T506-29")]
    [Trait("TestCase", "T506-30")]
    [Trait("TestCase", "T506-32")]
    public async Task RS_scrolls_an_info_box_that_doesnt_fit_the_footer_names_it_and_another_row_starts_at_the_top()
    {
        var notInstalled = TestGames.NotInstalled(KnownLaunchers.Gog, "Bravo");
        using var arcadia = await ArcadiaWindow.ShowAsync([LauncherSpec.With(KnownLaunchers.Gog, notInstalled)], new AppSettings { ActiveLaunchers = ["gog"] });
        OpenByHoldingA(arcadia);
        var page = arcadia.Arcadia.Detail!;
        var scroller = Part<ScrollViewer>(arcadia, "InfoScroller");

        // "Übersicht" has no info box: RS does nothing and the footer doesn't name it.
        arcadia.Press(UiAction.SecondaryDown);
        Assert.False(page.InfoOverflows);
        Assert.DoesNotContain(arcadia.FooterHints(), hint => hint.StartsWith("RS", StringComparison.Ordinal));

        arcadia.Arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.ControllerMode);
        arcadia.WaitForTransitions();
        Assert.True(page.InfoOverflows, "Die Infobox passt; der Test braucht eine, die überläuft.");
        Assert.Contains(arcadia.FooterHints(), hint => hint.StartsWith("RS", StringComparison.Ordinal));

        // RS down scrolls it to its end and stops there; the focus stays on the row.
        arcadia.Press(UiAction.SecondaryDown);
        Assert.True(scroller.Offset.Y > 0);
        arcadia.Press(UiAction.SecondaryDown, times: 10);
        var end = scroller.Extent.Height - scroller.Viewport.Height;
        Assert.Equal(end, scroller.Offset.Y, 1);
        Assert.Equal(GameActionKind.ControllerMode, page.Current.FocusedRow?.Kind);
        arcadia.Press(UiAction.SecondaryUp, times: 10);
        Assert.Equal(0, scroller.Offset.Y, 1);

        // Scrolled, then ▼ to the next row: its box starts at the top.
        arcadia.Press(UiAction.SecondaryDown).Press(UiAction.Down).WaitForTransitions();
        Assert.Equal(GameActionKind.MouseMode, page.Current.FocusedRow?.Kind);
        Assert.Equal(0, scroller.Offset.Y, 1);

        // A box that fits ignores RS: "Artwork ändern".
        arcadia.Press(UiAction.Back).Press(UiAction.Down).Press(UiAction.Accept).WaitForTransitions();
        Assert.Equal(GameActionKind.Artwork, page.Current.FocusedRow?.Kind);
        Assert.False(page.InfoOverflows);
        Assert.DoesNotContain(arcadia.FooterHints(), hint => hint.StartsWith("RS", StringComparison.Ordinal));
        arcadia.Press(UiAction.SecondaryDown);
        Assert.Equal(0, scroller.Offset.Y, 1);
        Assert.Equal(GameActionKind.Artwork, page.Current.FocusedRow?.Kind);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-31")]
    [Trait("TestCase", "T506-33")]
    public async Task A_value_change_brings_the_settings_info_panel_back_to_its_top()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        arcadia.Arcadia.FocusRow("mouse-mode");
        arcadia.WaitForTransitions();
        var scroller = arcadia.Named<ScrollViewer>("InfoScroller");
        Assert.True(arcadia.Arcadia.Main.Settings!.InfoOverflows, "Die Infobox von „Mausmodus“ passt; der Test braucht eine, die überläuft.");

        arcadia.Press(UiAction.SecondaryDown).WaitForTransitions();
        Assert.True(scroller.Offset.Y > 0);
        arcadia.Press(UiAction.Right).WaitForTransitions();
        Assert.Equal(0, scroller.Offset.Y, 1);
        arcadia.Press(UiAction.Left).WaitForTransitions();
    }

    private static void OpenByHoldingA(ArcadiaWindow arcadia)
    {
        arcadia.Arcadia.PressDown(UiAction.Accept).Wait(TimeSpan.FromSeconds(1.1)).LetGo(UiAction.Accept);
        arcadia.WaitForTransitions();
        Assert.True(arcadia.Arcadia.Main.IsDetailOpen, "Detailseite offen");
    }

    private static void AssertDetailOf(ArcadiaWindow arcadia, string title)
    {
        Assert.Equal(title, arcadia.Arcadia.Detail?.Current.Tile.Title);
        Assert.Equal(title, Part<TextBlock>(arcadia, "Title").Text);
    }

    private static T Part<T>(ArcadiaWindow arcadia, string name)
        where T : Control => arcadia.Window.GetVisualDescendants().OfType<GameDetailView>().Single().GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static List<T> Visible<T>(ArcadiaWindow arcadia, string style)
        where T : Control => Within<T>(arcadia.Window, style);

    private static List<T> Within<T>(Visual within, string style)
        where T : Control => [.. within.GetVisualDescendants().OfType<T>().Where(control => control.Classes.Contains(style) && control.IsEffectivelyVisible)];
}
