using Arcadia.App.Controls;
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
/// The detail page's info box and value chip as drawn, by mouse and gamepad (#506): the wheel turning quickly or
/// scrolling a box, the chip with a long value, the value boxes alike on both pages, the fades, and RS only where a
/// row's box is explained.
/// </summary>
public sealed class InfoBoxWindowTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T506-5")]
    [Trait("TestCase", "T506-34")]
    public async Task The_wheel_turns_quickly_one_game_a_notch_and_over_a_box_that_doesnt_fit_it_scrolls_the_box()
    {
        var games = Enumerable.Range(0, 8).Select(i => TestGames.NotInstalled(KnownLaunchers.Gog, $"Spiel {i}")).ToList();
        using var arcadia = await ArcadiaWindow.ShowAsync([LauncherSpec.With(KnownLaunchers.Gog, [.. games])], new AppSettings { ActiveLaunchers = ["gog"] });
        var titles = arcadia.Arcadia.PageGames.Select(tile => tile.Title).ToList();
        OpenByHoldingA(arcadia);
        var detail = arcadia.Arcadia.Detail!;
        var jumps = 0;
        detail.Jumped += _ => jumps++;
        var heading = Part<StackPanel>(arcadia, "Heading");

        // One notch alone jumps as RT does; four quick notches show the fifth game at once, nothing follows.
        arcadia.Wheel(heading, notches: -1);
        Assert.Equal(titles[1], detail.Current.Tile.Title);
        Assert.Equal(1, jumps);
        arcadia.WaitForTransitions();
        arcadia.Arcadia.Time.Advance(TimeSpan.FromSeconds(1));
        for (var notch = 0; notch < 4; notch++)
        {
            arcadia.Wheel(heading, notches: -1);
        }

        Assert.Equal(titles[5], detail.Current.Tile.Title);
        Assert.Equal(2, jumps);
        Assert.True(heading.Opacity > 0.99, $"Titel mit Deckkraft {heading.Opacity}");
        arcadia.Arcadia.Time.Advance(TimeSpan.FromSeconds(2));
        arcadia.WaitForTransitions();
        Assert.Equal(titles[5], detail.Current.Tile.Title);
        Assert.Equal(2, jumps);

        // Over an info box that doesn't fit the wheel scrolls it and the game stays.
        arcadia.Arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.ControllerMode);
        arcadia.WaitForTransitions();
        Assert.True(detail.InfoOverflows, "Die Infobox passt; der Test braucht eine, die überläuft.");
        var scroller = Part<ScrollViewer>(arcadia, "InfoScroller");
        arcadia.Wheel(scroller, notches: -2);
        Assert.True(scroller.Offset.Y > 0, "Das Mausrad scrollt die Infobox");
        Assert.Equal(titles[5], detail.Current.Tile.Title);

        // At its end and at its start the box stops the wheel; it never turns to another game.
        arcadia.Wheel(scroller, notches: -20).Wheel(scroller, notches: -1);
        Assert.Equal(scroller.Extent.Height - scroller.Viewport.Height, scroller.Offset.Y, 1);
        Assert.Equal(titles[5], detail.Current.Tile.Title);
        arcadia.Wheel(scroller, notches: 20).Wheel(scroller, notches: 1);
        Assert.Equal(0, scroller.Offset.Y, 1);
        Assert.Equal(titles[5], detail.Current.Tile.Title);

        // Over a box that fits it turns the carousel again.
        arcadia.Press(UiAction.Back).Press(UiAction.Down).Press(UiAction.Accept).WaitForTransitions();
        Assert.False(detail.InfoOverflows);
        arcadia.Wheel(Part<Border>(arcadia, "InfoBox"), notches: -1);
        Assert.Equal(titles[6], detail.Current.Tile.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-13")]
    [Trait("TestCase", "T506-22")]
    public async Task A_long_value_fits_its_chip_in_one_line_and_the_value_boxes_neither_widen_the_box_nor_react_to_the_mouse()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync([new LauncherSpec(KnownLaunchers.Steam, 3)], new AppSettings { ActiveLaunchers = ["steam"], PanEnabled = true });
        OpenByHoldingA(arcadia);
        var page = arcadia.Arcadia.Detail!.Current;
        var row = arcadia.Arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.ControllerMode);
        arcadia.WaitForTransitions();
        var infoBox = Part<Border>(arcadia, "InfoBox");
        var boxWidth = infoBox.Bounds.Width;
        var rowButtons = DetailRows(arcadia);
        var rowHeight = rowButtons[0].Bounds.Height;

        // "Standard (Steam-Eingabe)" and every other value: one line in the chip, the row keeps its height.
        foreach (var _ in row.ValueBoxes)
        {
            var text = rowButtons[0].GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("valueText"));
            Assert.Equal(row.ValueText, text.Text);
            Assert.True(text.Bounds.Height < 30, $"„{text.Text}“ bricht um: {text.Bounds.Height} hoch");
            Assert.True(text.TextTrimming != Avalonia.Media.TextTrimming.None);
            Assert.Equal(rowHeight, rowButtons[0].Bounds.Height, 1);
            Assert.Equal(boxWidth, infoBox.Bounds.Width, 1);
            arcadia.Press(UiAction.Right).WaitForTransitions();
        }

        // The mouse over a value box: the focus stays on the row, the value stays; a click changes nothing either.
        var value = row.ValueText;
        var valueBox = infoBox.GetVisualDescendants().OfType<Border>().Last(b => b.Classes.Contains("valueBox"));
        arcadia.MouseMove(arcadia.CentreOf(valueBox));
        arcadia.Click(valueBox);
        Assert.Same(row, page.FocusedRow);
        Assert.Equal(value, row.ValueText);
        Assert.Equal(boxWidth, infoBox.Bounds.Width, 1);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-21")]
    [Trait("TestCase", "T506-35")]
    public async Task The_value_boxes_are_one_template_with_the_highlight_from_the_theme_and_the_fade_only_at_a_cut_edge()
    {
        var notInstalled = TestGames.NotInstalled(KnownLaunchers.Gog, "Bravo");
        using var arcadia = await ArcadiaWindow.ShowAsync([LauncherSpec.With(KnownLaunchers.Gog, notInstalled)], new AppSettings { ActiveLaunchers = ["gog"] });
        OpenByHoldingA(arcadia);
        arcadia.Arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.ControllerMode);
        arcadia.WaitForTransitions();
        var infoBox = Part<Border>(arcadia, "InfoBox");
        var detailCurrent = CurrentBox(infoBox);
        var (thickness, surface, border) = (detailCurrent.BorderThickness, detailCurrent.Background, detailCurrent.BorderBrush);
        Assert.Equal(2, detailCurrent.BorderThickness.Top);
        Assert.Equal(ArcadiaWindow.ThemeResource<Avalonia.Media.ISolidColorBrush>("ValueBoxCurrentBorder").Color, ((Avalonia.Media.ISolidColorBrush)detailCurrent.BorderBrush!).Color);

        // The fade: none at the top while at the start, at the bottom while more follows; at the end the other way round.
        var fade = infoBox.GetVisualDescendants().OfType<EdgeFade>().Single();
        var scroller = Part<ScrollViewer>(arcadia, "InfoScroller");
        Assert.True(arcadia.Arcadia.Detail!.InfoOverflows);
        Assert.False(fade.ShowsStart);
        Assert.True(fade.ShowsEnd);
        arcadia.Press(UiAction.SecondaryDown, times: 20).WaitForTransitions();
        Assert.Equal(scroller.Extent.Height - scroller.Viewport.Height, scroller.Offset.Y, 1);
        Assert.True(fade.ShowsStart);
        Assert.False(fade.ShowsEnd);

        // The settings page draws its value boxes with the same view and the same highlight.
        arcadia.Press(UiAction.Back, times: 3).Press(UiAction.Settings).Press(UiAction.NextTab);
        arcadia.Arcadia.FocusRow("mouse-mode");
        arcadia.WaitForTransitions();
        var panel = arcadia.Window.GetVisualDescendants().OfType<ValueBoxesView>().Single(view => view.IsEffectivelyVisible);
        var settingsCurrent = CurrentBox(panel);
        Assert.Equal(thickness, settingsCurrent.BorderThickness);
        Assert.Same(surface, settingsCurrent.Background);
        Assert.Same(border, settingsCurrent.BorderBrush);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-30")]
    public async Task In_the_rail_RS_neither_scrolls_the_box_of_the_open_area_nor_is_named_in_the_footer()
    {
        var notInstalled = TestGames.NotInstalled(KnownLaunchers.Gog, "Bravo");
        using var arcadia = await ArcadiaWindow.ShowAsync([LauncherSpec.With(KnownLaunchers.Gog, notInstalled)], new AppSettings { ActiveLaunchers = ["gog"] });
        OpenByHoldingA(arcadia);
        var page = arcadia.Arcadia.Detail!.Current;
        arcadia.Arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.ControllerMode);
        arcadia.WaitForTransitions();
        Assert.True(arcadia.Arcadia.Detail!.InfoOverflows, "Die Infobox passt; der Test braucht eine, die überläuft.");

        // B back to the rail: "Controller" stays open, but RS belongs to a focused row only.
        arcadia.Press(UiAction.Back).WaitForTransitions();
        Assert.Equal(DetailZone.Rail, page.Zone);
        var scroller = Part<ScrollViewer>(arcadia, "InfoScroller");
        Assert.DoesNotContain(arcadia.FooterHints(), hint => hint.StartsWith("RS", StringComparison.Ordinal));
        arcadia.Press(UiAction.SecondaryDown).WaitForTransitions();
        Assert.Equal(0, scroller.Offset.Y, 1);

        // The same in the button row.
        arcadia.Press(UiAction.Up, times: 3).WaitForTransitions();
        Assert.Equal(DetailZone.Buttons, page.Zone);
        Assert.DoesNotContain(arcadia.FooterHints(), hint => hint.StartsWith("RS", StringComparison.Ordinal));
        arcadia.Press(UiAction.SecondaryDown).WaitForTransitions();
        Assert.Equal(0, scroller.Offset.Y, 1);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-26")]
    public async Task A_running_game_puts_the_reason_of_its_greyed_rows_at_the_top_of_the_box()
    {
        var game = TestGames.Installed(KnownLaunchers.Gog, "Alpha", DateTimeOffset.UnixEpoch);
        using var arcadia = await ArcadiaWindow.ShowAsync([new LauncherSpec(KnownLaunchers.Gog, 1, Library: [game], Running: [])], new AppSettings { ActiveLaunchers = ["gog"] });
        OpenByHoldingA(arcadia);
        arcadia.Arcadia.RunInLauncher(game);
        await arcadia.Arcadia.FollowRunningGamesAsync();
        var row = arcadia.Arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Uninstall);
        arcadia.WaitForTransitions();
        Assert.True(row.IsGreyed, "Deinstallieren während das Spiel läuft");

        var infoBox = Part<Border>(arcadia, "InfoBox");
        var note = Assert.Single(infoBox.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("infoNote") && b.IsEffectivelyVisible);
        var title = Assert.Single(infoBox.GetVisualDescendants().OfType<TextBlock>(), t => t.IsEffectivelyVisible && t.Text == row.Title);
        Assert.True(arcadia.Bounds(note).Bottom <= arcadia.Bounds(title).Top, "Der Hinweis steht über dem Namen.");
        Assert.Contains(note.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == row.Note);
    }

    private static Border CurrentBox(Visual within) =>
        Assert.Single(within.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("valueBox") && b.Classes.Contains("current"));

    private static List<Button> DetailRows(ArcadiaWindow arcadia) =>
        [.. arcadia.Window.GetVisualDescendants().OfType<GameDetailView>().Single().GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("detailRow") && b.IsEffectivelyVisible)];

    private static void OpenByHoldingA(ArcadiaWindow arcadia)
    {
        arcadia.Arcadia.PressDown(UiAction.Accept).Wait(TimeSpan.FromSeconds(1.1)).LetGo(UiAction.Accept);
        arcadia.WaitForTransitions();
        Assert.True(arcadia.Arcadia.Main.IsDetailOpen, "Detailseite offen");
    }

    private static T Part<T>(ArcadiaWindow arcadia, string name)
        where T : Control => arcadia.Window.GetVisualDescendants().OfType<GameDetailView>().Single().GetVisualDescendants().OfType<T>().Single(control => control.Name == name);
}
