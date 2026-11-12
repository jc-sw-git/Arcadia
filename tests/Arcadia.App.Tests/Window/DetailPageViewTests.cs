using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.App.Views;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// The detail page (#11, #127, #499) as drawn: the button row on the artwork, the stacks of the carousel, the panel with
/// the rail of areas, the rows and the info box.
/// </summary>
public sealed class DetailPageViewTests
{
    private static readonly LauncherInfo Gog = KnownLaunchers.Gog;

    [AvaloniaFact]
    [Trait("TestCase", "T11-1")]
    [Trait("TestCase", "T499-1")]
    public async Task Holding_A_or_Details_in_the_menu_opens_the_page_with_its_buttons_rail_stacks_and_footer()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        arcadia.Press(UiAction.Right, times: 2);
        var games = arcadia.Arcadia.PageGames.Select(tile => tile.Title).ToList();

        OpenByHoldingA(arcadia);
        AssertDetailOf(arcadia, games[2]);
        Assert.Single(arcadia.WithText("3 von 31"));

        // The button row on the artwork, "Spielen" focused; the rail with its three areas, "Übersicht" open with the
        // game's figures and no info box.
        Assert.Equal(["Spielen", AppTexts.ActionToFavorites, "In GOG Galaxy öffnen"], DetailButtons(arcadia).Select(Label));
        Assert.Equal("Spielen", Label(Focused(arcadia)));
        Assert.Equal([AppTexts.DetailAreaOverview, AppTexts.DetailAreaController, AppTexts.DetailAreaManage], RailButtons(arcadia).Select(RailName));
        Assert.Contains("open", RailButtons(arcadia)[0].Classes);
        Assert.Equal(4, Visible<Border>(arcadia, "detailStat").Count);
        Assert.False(Part<Border>(arcadia, "InfoBox").IsEffectivelyVisible, "„Übersicht“ hat keine Infobox.");

        // The stacks name the games before and after; the front cover stands beside the title, left of it.
        Assert.Equal([games[1], games[3]], Stacks(arcadia).Select(stack => StackTitle(stack)));
        var front = arcadia.Bounds(Cover(arcadia, offset: 0));
        Assert.True(front.Right < arcadia.Bounds(Part<TextBlock>(arcadia, "Title")).Left, "Das Cover steht links vom Titel.");

        Assert.Equal(["L3", "A Spielen", "▼ Bereiche", "LT RT Spiel", "LB RB Launcher", "B Zurück"], arcadia.FooterHints());

        // ⧉ → "Details anzeigen" opens the same page.
        arcadia.Press(UiAction.Back).Press(UiAction.Options).Press(UiAction.Down).Press(UiAction.Accept);
        AssertDetailOf(arcadia, games[2]);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T11-2")]
    [Trait("TestCase", "T499-23")]
    public async Task LT_and_RT_turn_the_carousel_and_clicks_on_the_stacks_wheel_and_keys_turn_it_too()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var games = arcadia.Arcadia.PageGames.Select(tile => tile.Title).ToList();
        OpenByHoldingA(arcadia);

        arcadia.Press(UiAction.NextPage);
        AssertDetailOf(arcadia, games[1]);
        arcadia.Press(UiAction.PreviousPage);
        AssertDetailOf(arcadia, games[0]);

        // Before the first stands the last, after the last the first.
        arcadia.Press(UiAction.PreviousPage);
        Assert.Equal("31 von 31", arcadia.Arcadia.Detail!.PositionText);
        arcadia.Press(UiAction.NextPage);
        AssertDetailOf(arcadia, games[0]);

        // Pressed quickly three times, three games on.
        arcadia.Press(UiAction.NextPage, times: 3);
        AssertDetailOf(arcadia, games[3]);

        // A click on a stack.
        arcadia.Click(Stacks(arcadia)[1]);
        AssertDetailOf(arcadia, games[4]);
        arcadia.Click(Stacks(arcadia)[0]);
        AssertDetailOf(arcadia, games[3]);

        // The wheel, one notch one game, down the next; Page Up/Down and Shift+arrows.
        var view = arcadia.Window.GetVisualDescendants().OfType<GameDetailView>().Single();
        arcadia.Wheel(view, notches: -1);
        AssertDetailOf(arcadia, games[4]);
        arcadia.Wheel(view, notches: 1);
        AssertDetailOf(arcadia, games[3]);
        arcadia.Key(Key.PageDown);
        AssertDetailOf(arcadia, games[4]);
        arcadia.Key(Key.PageUp);
        AssertDetailOf(arcadia, games[3]);
        arcadia.Key(Key.Right, KeyModifiers.Shift);
        AssertDetailOf(arcadia, games[4]);
        arcadia.Key(Key.Left, KeyModifiers.Shift);
        AssertDetailOf(arcadia, games[3]);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-23")]
    [Trait("TestCase", "T499-16")]
    public async Task A_click_opens_an_area_the_mouse_over_a_row_focuses_it_and_the_arrows_change_its_value()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        OpenByHoldingA(arcadia);
        var page = arcadia.Arcadia.Detail!.Current;

        arcadia.Click(RailButtons(arcadia)[2]);
        Assert.Equal(DetailAreaViewModel.ManageKey, page.OpenArea.Key);
        Assert.True(Part<Border>(arcadia, "InfoBox").IsEffectivelyVisible);

        arcadia.Click(RailButtons(arcadia)[1]);
        Assert.Equal(DetailAreaViewModel.ControllerKey, page.OpenArea.Key);
        var rows = Rows(arcadia);
        Assert.Equal([AppTexts.DetailControllerModeTitle, AppTexts.DetailMouseModeTitle], rows.Select(RowTitle));

        // The mouse over the second row: focused, and the info box explains it.
        arcadia.MouseMove(arcadia.CentreOf(rows[1]));
        Assert.Equal(GameActionKind.MouseMode, page.FocusedRow?.Kind);
        Assert.Single(ValueBoxTexts(arcadia), text => text == AppTexts.DetailMouseModeAllowedInfo);
        arcadia.MouseMove(arcadia.CentreOf(rows[0]));
        Assert.Equal(GameActionKind.ControllerMode, page.FocusedRow?.Kind);
        Assert.Single(arcadia.WithText(AppTexts.DetailControllerModeInfo));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-23")]
    [Trait("TestCase", "T499-8")]
    public async Task The_arrows_beside_a_value_change_it_both_ways_and_the_rail_shows_the_new_value()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(
            [new(KnownLaunchers.Steam, 3), new(Gog, 3)], new Core.Settings.AppSettings { ActiveLaunchers = ["steam", "gog"], PanEnabled = true });
        arcadia.Arcadia.Sdl.Plug(Arcadia.Tests.Sdl.KnownPads.SteamController);
        arcadia.Arcadia.StartControllers();
        await arcadia.Arcadia.TickSteamControllerAsync();
        arcadia.Settle();
        OpenByHoldingA(arcadia);
        arcadia.Click(RailButtons(arcadia)[1]);
        var page = arcadia.Arcadia.Detail!.Current;
        var mode = page.Row(GameActionKind.ControllerMode);
        Assert.False(mode.IsGreyed);
        var arrows = Rows(arcadia)[0].GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("valueArrow")).ToList();
        Assert.Equal(2, arrows.Count);

        arcadia.Click(arrows[1]);
        Assert.Equal(ControllerModes.Arcadia, mode.ValueText);
        Assert.Equal(AppTexts.DetailControllerLine(ControllerModes.Arcadia), page.OpenArea.ValueLine);
        Assert.Single(arcadia.WithText(AppTexts.DetailControllerLine(ControllerModes.Arcadia)), text => text.Classes.Contains("railValue"));

        arcadia.Click(arrows[0]).Click(arrows[0]);
        Assert.Equal(ControllerModes.Steam, mode.ValueText);
        Assert.Equal(AppTexts.DetailControllerLine(ControllerModes.Steam), page.OpenArea.ValueLine);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-2")]
    [Trait("TestCase", "T499-3")]
    public async Task The_buttons_stand_in_one_row_on_the_artwork_the_D_pad_skips_a_disabled_one_and_its_tooltip_says_why()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(
        [
            LauncherSpec.With(Gog, TestGames.Installed(Gog, "Alpha Strike", ArcadiaDriver.Now.AddDays(-3)) with
            {
                UpdateAvailable = true,
                OpenInLauncher = new LaunchTarget.ProtocolUri("goggalaxy://openGameView/1"),
            }),
        ]);
        OpenByHoldingA(arcadia);
        var buttons = DetailButtons(arcadia);

        Assert.Equal(["Spielen", AppTexts.ActionToFavorites, "In GOG Galaxy öffnen"], buttons.Select(Label));
        Assert.Equal([true, false, true], buttons.Select(button => button.IsEffectivelyEnabled));
        var centres = buttons.Select(button => arcadia.Bounds(button).Center.Y).ToList();
        Assert.All(centres, centre => Assert.Equal(centres[0], centre, tolerance: 4));
        Assert.All(buttons, button => Assert.True(arcadia.Bounds(button).Bottom < arcadia.Bounds(Part<Border>(arcadia, "Panel")).Top, $"{Label(button)} reicht in das Panel."));

        // The quiet note beside them: no surface; the focus never lands on it.
        Assert.Single(arcadia.WithText(AppTexts.DetailUpdateAvailable));
        Assert.Single(arcadia.WithText("GOG Galaxy installiert es eventuell vor dem Start."));
        Assert.True(arcadia.Bounds(Part<StackPanel>(arcadia, "Note")).Left > arcadia.Bounds(buttons[^1]).Right, "Der Hinweis steht rechts neben den Knöpfen.");

        arcadia.Press(UiAction.Right);
        Assert.Equal("In GOG Galaxy öffnen", Label(Focused(arcadia)));
        arcadia.Press(UiAction.Right);
        Assert.Equal("Spielen", Label(Focused(arcadia)));
        Assert.Equal(AppTexts.ActionNotYetAvailable, ToolTip.GetTip(buttons[1]));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T127-1")]
    [Trait("TestCase", "T499-25")]
    public async Task The_focus_ring_shows_whole_on_buttons_areas_and_rows_and_there_is_only_one()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(
        [
            LauncherSpec.With(Gog, TestGames.Installed(Gog, "Alpha Strike", ArcadiaDriver.Now.AddDays(-3)) with
            {
                OpenInLauncher = new LaunchTarget.ProtocolUri("goggalaxy://openGameView/1"),
                Uninstall = new LaunchTarget.ProtocolUri("goggalaxy://uninstall/1"),
            }),
        ]);
        OpenByHoldingA(arcadia);

        AssertOneRing(arcadia, Focused(arcadia), "„Spielen“", "FocusRingGap");
        arcadia.Press(UiAction.Left);
        AssertOneRing(arcadia, Focused(arcadia), "letzter Knopf", "FocusRingGap");

        arcadia.Press(UiAction.Down);
        AssertOneRing(arcadia, RailButtons(arcadia)[0], "„Übersicht“", "FocusRing");
        arcadia.Press(UiAction.Down, times: 2);
        AssertOneRing(arcadia, RailButtons(arcadia)[^1], "„Verwalten“", "FocusRing");

        // The rows of "Controller", then of "Verwalten": the first, and ▲ round to the last.
        arcadia.Press(UiAction.Up);
        foreach (var area in new[] { AppTexts.DetailAreaController, AppTexts.DetailAreaManage })
        {
            arcadia.Press(UiAction.Accept);
            AssertOneRing(arcadia, Rows(arcadia)[0], $"erste Zeile in „{area}“", "FocusRing");
            arcadia.Press(UiAction.Up);
            AssertOneRing(arcadia, Rows(arcadia)[^1], $"letzte Zeile in „{area}“", "FocusRing");
            arcadia.Press(UiAction.Back).Press(UiAction.Down);
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-17")]
    public async Task The_rows_keep_their_width_whatever_the_info_box_shows()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        OpenByHoldingA(arcadia);
        arcadia.Press(UiAction.Down, times: 3).Press(UiAction.Accept);
        var widths = new List<double> { arcadia.Bounds(Rows(arcadia)[0]).Width };
        var box = arcadia.Bounds(Part<Border>(arcadia, "InfoBox"));
        arcadia.Press(UiAction.Down);
        widths.Add(arcadia.Bounds(Rows(arcadia)[0]).Width);
        arcadia.Press(UiAction.Back).Press(UiAction.Up).Press(UiAction.Accept);
        widths.Add(arcadia.Bounds(Rows(arcadia)[0]).Width);

        Assert.All(widths, width => Assert.Equal(widths[0], width, tolerance: 0.5));
        Assert.Equal(box.Width, arcadia.Bounds(Part<Border>(arcadia, "InfoBox")).Width, tolerance: 0.5);
        Assert.True(arcadia.Bounds(Part<Border>(arcadia, "Panel")).Contains(arcadia.Bounds(Part<Border>(arcadia, "InfoBox"))), "Die Infobox ragt aus dem Panel.");
    }

    [AvaloniaTheory]
    [Trait("TestCase", "T127-10")]
    [Trait("TestCase", "T499-26")]
    [InlineData("Burning Kingdom", false)]
    [InlineData("Burning Kingdom of the Gloomy Jagged Pyramids and the Forty Seven Sunken Lighthouses of Yonder", true)]
    public async Task A_long_title_takes_two_lines_then_ends_with_dots_and_pushes_the_buttons_not_into_the_panel(string title, bool trimmed)
    {
        using var arcadia = await ArcadiaWindow.ShowAsync([LauncherSpec.With(Gog, TestGames.Installed(Gog, title, ArcadiaDriver.Now.AddDays(-3)))]);
        OpenByHoldingA(arcadia);
        var heading = Part<TextBlock>(arcadia, "Title");

        Assert.Equal(trimmed, heading.TextLayout.TextLines.Any(line => line.HasCollapsed));
        Assert.True(heading.TextLayout.TextLines.Count <= 2);
        var drawn = arcadia.Bounds(heading);
        Assert.True(drawn.Height >= heading.TextLayout.Height - 0.5, "Titel kleiner gezeichnet, als sein Text braucht");
        // A line lower than the font's own spacing cuts the descenders (g, y) off.
        Assert.All(heading.TextLayout.TextLines, line => Assert.True(line.Height >= heading.FontSize * 1.15, $"Zeile {line.Height} hoch, Unterlängen abgeschnitten"));
        var panel = arcadia.Bounds(Part<Border>(arcadia, "Panel"));
        Assert.All(DetailButtons(arcadia), button => Assert.True(arcadia.Bounds(button).Bottom + (7 * arcadia.ScaleOf(button)) <= panel.Top,
            $"{Label(button)} samt Fokusrahmen reicht in das Panel."));
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

    /// <summary>The ring around <paramref name="control"/> drawn whole, and no other control of the page shows one.</summary>
    private static void AssertOneRing(ArcadiaWindow arcadia, Control control, string where, string ring)
    {
        arcadia.AssertRingAround(control, where, ring);
        var focused = DetailButtons(arcadia).Concat(RailButtons(arcadia)).Concat(Rows(arcadia)).Where(button => button.Classes.Contains("focused")).ToList();
        Assert.Equal([control], focused);
    }

    /// <summary>A named part of the page; the view has its own name scope, so the window's lookup doesn't find it.</summary>
    private static T Part<T>(ArcadiaWindow arcadia, string name)
        where T : Control => arcadia.Window.GetVisualDescendants().OfType<GameDetailView>().Single().GetVisualDescendants().OfType<T>().Single(control => control.Name == name);

    private static List<T> Visible<T>(ArcadiaWindow arcadia, string style)
        where T : Control => arcadia.Window.GetVisualDescendants().OfType<T>()
        .Where(control => control.Classes.Contains(style) && control.IsEffectivelyVisible)
        .ToList();

    private static List<Button> DetailButtons(ArcadiaWindow arcadia) => Visible<Button>(arcadia, "detailAction");

    private static List<Button> RailButtons(ArcadiaWindow arcadia) => Visible<Button>(arcadia, "detailRail");

    private static List<Button> Rows(ArcadiaWindow arcadia) => Visible<Button>(arcadia, "detailRow");

    /// <summary>What the value boxes of the info box say (#506); their texts may print buttons, so they are inlines.</summary>
    private static List<string?> ValueBoxTexts(ArcadiaWindow arcadia) =>
        [.. Visible<TextBlock>(arcadia, "valueBoxText").Select(text => text.Inlines?.Text)];

    /// <summary>The left stack, then the right one.</summary>
    private static List<Button> Stacks(ArcadiaWindow arcadia) => [.. Visible<Button>(arcadia, "detailStack").OrderBy(stack => arcadia.Bounds(stack).Left)];

    private static Control Cover(ArcadiaWindow arcadia, int offset) => arcadia.Window.GetVisualDescendants().OfType<Controls.DepthSlot>()
        .Single(slot => slot.DataContext is DetailCoverViewModel { Offset: var at } && at == offset);

    private static Button Focused(ArcadiaWindow arcadia) => Assert.Single(DetailButtons(arcadia), button => button.Classes.Contains("focused"));

    private static string Label(Button button) => button.GetVisualDescendants().OfType<TextBlock>().Last(text => text.IsEffectivelyVisible).Text ?? string.Empty;

    private static string RailName(Button button) => button.GetVisualDescendants().OfType<TextBlock>().First().Text ?? string.Empty;

    private static string RowTitle(Button button) => button.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("rowTitle")).Text ?? string.Empty;

    private static string? StackTitle(Button stack) => stack.GetVisualDescendants().OfType<TextBlock>().First(text => text.FontSize >= 18).Text;
}
