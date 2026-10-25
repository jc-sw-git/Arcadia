using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.App.Views;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>The detail page (#11, #127) as drawn: path, carousel, its buttons and the key figures.</summary>
public sealed class DetailPageViewTests
{
    private static readonly LauncherInfo Gog = KnownLaunchers.Gog;

    [AvaloniaFact]
    [Trait("Testfall", "T11-1")]
    public async Task Holding_A_or_Details_in_the_menu_opens_the_page_with_its_path_position_neighbours_and_footer()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var main = arcadia.Arcadia.Main;
        arcadia.Press(UiAction.Right, times: 2);
        var third = main.FocusedGame!.Title;

        OpenByHoldingA(arcadia);
        AssertDetailOf(arcadia, third);
        Assert.Equal(["B", "Bibliothek", "/", "GOG Galaxy", "/", third], Crumbs(arcadia));
        Assert.Single(arcadia.WithText("3 von 31"));
        Assert.Single(arcadia.WithText("Name A–Z"));

        // The game in the middle, its neighbours cut off at the window's edges and dimmed.
        var pages = Pages(arcadia);
        var centre = arcadia.Bounds(pages.Single(page => page.DataContext is GameDetailPageViewModel { IsCentre: true }));
        Assert.Equal(ArcadiaWindow.Width / 2.0, centre.Center.X, tolerance: 2);
        var left = arcadia.Bounds(pages.Single(page => page.DataContext is GameDetailPageViewModel { Offset: -1 }));
        var right = arcadia.Bounds(pages.Single(page => page.DataContext is GameDetailPageViewModel { Offset: 1 }));
        Assert.True(left.Left < 0 && left.Right > 0, $"Vorheriges Spiel {left} links angeschnitten");
        Assert.True(right.Left < ArcadiaWindow.Width && right.Right > ArcadiaWindow.Width, $"Nächstes Spiel {right} rechts angeschnitten");
        Assert.All(pages.Where(page => page.DataContext is GameDetailPageViewModel { IsSide: true }), page => Assert.True(page.Opacity < 1, "Nachbar abgedunkelt"));

        Assert.Equal(["D-Pad Aktion wählen", "A Auswählen", "Y Ansicht", "LB RB Launcher", "LT RT Spiel", "B Zurück"], arcadia.FooterHints());

        // ⧉ → "Details anzeigen" opens the same page.
        arcadia.Press(UiAction.Back).Press(UiAction.Options).Press(UiAction.Down).Press(UiAction.Accept);
        AssertDetailOf(arcadia, third);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T11-2")]
    public async Task LT_and_RT_slide_the_pages_and_clicks_wheel_and_keys_turn_the_carousel_too()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var main = arcadia.Arcadia.Main;
        var games = main.PageGames.Select(tile => tile.Title).ToList();
        OpenByHoldingA(arcadia);

        // RT: the pages slide to the left (the track starts right of its rest and moves back to it), LT the other way.
        // Read before the next frame: the slide runs on the render clock, which is real time.
        arcadia.Arcadia.Press(UiAction.NextPage);
        Assert.True(Slide(arcadia) > 0, $"RT schiebt nach links: Spur bei {Slide(arcadia)}");
        AssertDetailOf(arcadia, games[1]);
        Assert.Equal(0, Slide(arcadia.WaitForTransitions()), 1);
        arcadia.Arcadia.Press(UiAction.PreviousPage);
        Assert.True(Slide(arcadia) < 0, $"LT schiebt nach rechts: Spur bei {Slide(arcadia)}");
        AssertDetailOf(arcadia, games[0]);

        // Before the first stands the last, after the last the first.
        arcadia.WaitForTransitions().Press(UiAction.PreviousPage);
        Assert.Equal("31 von 31", main.Detail!.PositionText);
        arcadia.Press(UiAction.NextPage);
        AssertDetailOf(arcadia, games[0]);

        // Pressed quickly three times, three games on.
        arcadia.WaitForTransitions().Press(UiAction.NextPage, times: 3);
        AssertDetailOf(arcadia, games[3]);
        arcadia.WaitForTransitions();

        // A click on a neighbour, on RT and LT at the edges.
        arcadia.Click(NeighbourButton(arcadia, offset: 1));
        AssertDetailOf(arcadia, games[4]);
        arcadia.WaitForTransitions().Click(CarouselButton(arcadia, "RT"));
        AssertDetailOf(arcadia, games[5]);
        arcadia.WaitForTransitions().Click(CarouselButton(arcadia, "LT"));
        AssertDetailOf(arcadia, games[4]);
        arcadia.WaitForTransitions().Click(NeighbourButton(arcadia, offset: -1));
        AssertDetailOf(arcadia, games[3]);

        // The wheel, one notch one game, down the next; Page Up/Down and Shift+arrows.
        var view = arcadia.Window.GetVisualDescendants().OfType<GameDetailView>().Single();
        arcadia.WaitForTransitions().Wheel(view, notches: -1);
        AssertDetailOf(arcadia, games[4]);
        arcadia.WaitForTransitions().Wheel(view, notches: 1);
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
    [Trait("Testfall", "T11-6")]
    public async Task A_click_on_the_path_goes_back_to_the_library_on_the_page_of_the_game_shown_last_with_the_focus_on_it()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var main = arcadia.Arcadia.Main;
        OpenByHoldingA(arcadia);

        // Before the first stands the last game, on page 2 of GOG's 31.
        arcadia.Press(UiAction.PreviousPage);
        var last = main.Detail!.Current.Tile.Title;
        arcadia.Click(arcadia.OneWithText("Bibliothek"));

        Assert.False(main.IsDetailOpen);
        Assert.Equal(last, main.FocusedGame?.Title);
        Assert.Equal(6, main.PageGames.Count);
        Assert.Equal(main.PageGames[^1], main.FocusedGame);
        Assert.True(arcadia.Tile(main.FocusedGame!).Classes.Contains("focused"), "Die Kachel zeigt den Fokus.");
    }

    [AvaloniaFact]
    [Trait("Testfall", "T11-7")]
    public async Task The_buttons_stand_in_rows_inside_the_card_the_D_pad_skips_disabled_ones_and_shows_the_hint_below()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(
        [
            LauncherSpec.With(Gog, TestGames.Installed(Gog, "Alpha Strike", ArcadiaDriver.Now.AddDays(-3)) with
            {
                OpenInLauncher = new LaunchTarget.ProtocolUri("goggalaxy://openGameView/1"),
            }),
        ]);
        OpenByHoldingA(arcadia);
        var page = CentrePage(arcadia);
        var buttons = DetailButtons(page);

        Assert.Equal(["Spielen", "Zu Favoriten", "In GOG Galaxy öffnen", "Artwork ändern", "Deinstallieren …"], buttons.Select(Label));
        Assert.Equal([true, false, true, false, true], buttons.Select(button => button.IsEffectivelyEnabled));
        Assert.True(buttons[0].Classes.Contains("focused"), "„Spielen“ ist fokussiert.");

        // The first four in one row, "Deinstallieren …" alone below them, red; all inside the card.
        var rows = buttons.Select(button => arcadia.Bounds(button).Center.Y).ToList();
        Assert.All(rows.Take(4), row => Assert.Equal(rows[0], row, tolerance: 4));
        Assert.True(arcadia.Bounds(buttons[4]).Top > arcadia.Bounds(buttons[0]).Bottom, "„Deinstallieren …“ steht in einer eigenen Reihe darunter.");
        Assert.Equal(ArcadiaWindow.ThemeResource<ISolidColorBrush>("Danger").Color, (buttons[4].Foreground as ISolidColorBrush)?.Color);
        var card = arcadia.Bounds(Card(page));
        Assert.All(buttons, button => Assert.True(card.Contains(arcadia.Bounds(button)), $"{Label(button)} ragt aus der Karte."));

        // The D-pad skips the disabled buttons; the focused one's hint stands below the rows.
        arcadia.Press(UiAction.Right);
        Assert.Equal("In GOG Galaxy öffnen", Label(Focused(arcadia)));
        arcadia.Press(UiAction.Down);
        Assert.Equal("Deinstallieren …", Label(Focused(arcadia)));
        var hint = arcadia.OneWithText("Nur in GOG Galaxy möglich – öffnet GOG Galaxy");
        Assert.True(arcadia.Bounds(hint).Top > arcadia.Bounds(buttons[4]).Bottom, "Hinweis unter den Reihen");

        // ⧉ opens no menu here.
        arcadia.Press(UiAction.Options);
        Assert.False(arcadia.Arcadia.Main.IsGameMenuOpen);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T127-1")]
    public async Task The_focus_ring_touches_no_neighbour_and_the_key_figures_show_whole_also_with_an_update_note()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(
        [
            LauncherSpec.With(Gog, TestGames.Installed(Gog, "Alpha Strike", ArcadiaDriver.Now.AddDays(-3)) with
            {
                UpdateAvailable = true,
                OpenInLauncher = new LaunchTarget.ProtocolUri("goggalaxy://openGameView/1"),
                Uninstall = new LaunchTarget.ProtocolUri("goggalaxy://uninstall/1"),
            }),
        ]);
        OpenByHoldingA(arcadia);
        var page = CentrePage(arcadia);
        var buttons = DetailButtons(page).Where(button => button.IsEffectivelyEnabled).ToList();
        var others = DetailButtons(page);

        foreach (var move in new[] { UiAction.Right, UiAction.Down, UiAction.Up })
        {
            var focused = Focused(arcadia);
            arcadia.AssertRingAround(focused, Label(focused), "FocusRingGap");
            var ring = arcadia.Bounds(focused).Inflate(7 * arcadia.ScaleOf(focused));
            Assert.All(others.Where(other => other != focused), other =>
                Assert.False(ring.Intersects(arcadia.Bounds(other)), $"Fokusrahmen von {Label(focused)} berührt {Label(other)}"));
            arcadia.Press(move);
        }

        Assert.Equal(3, buttons.Count);
        Assert.Single(arcadia.WithText("GOG Galaxy installiert das Update eventuell, bevor das Spiel startet."));
        var card = arcadia.Bounds(Card(page));
        var stats = page.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("detailStat")).ToList();
        Assert.Equal(4, stats.Count);
        Assert.All(stats, stat => Assert.True(card.Contains(arcadia.Bounds(stat)), $"Kennzahl {arcadia.Bounds(stat)} nicht ganz in der Karte {card}"));
        var note = arcadia.Bounds(arcadia.OneWithText("GOG Galaxy installiert das Update eventuell, bevor das Spiel startet."));
        Assert.All(stats, stat => Assert.True(arcadia.Bounds(stat).Top >= note.Bottom, "Kennzahlen unter der Hinweiszeile"));
    }

    [AvaloniaTheory]
    [Trait("Testfall", "T127-10")]
    [InlineData("Burning Kingdom")]
    [InlineData("Burning Kingdom of the Gloomy Jagged Pyramids")]
    public async Task A_title_with_descenders_shows_whole_also_on_two_lines(string title)
    {
        using var arcadia = await ArcadiaWindow.ShowAsync([LauncherSpec.With(Gog, TestGames.Installed(Gog, title, ArcadiaDriver.Now.AddDays(-3)))]);
        OpenByHoldingA(arcadia);
        var page = CentrePage(arcadia);
        var heading = page.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == title && text.FontSize >= 60);

        Assert.False(heading.TextLayout.TextLines.Any(line => line.HasCollapsed), "Titel gekürzt");
        Assert.True(heading.TextLayout.TextLines.Count <= 2);

        // Nothing of the title is cut off by the clipping hero above the buttons.
        var drawn = arcadia.Bounds(heading);
        var hero = heading.GetVisualAncestors().OfType<Panel>().First(panel => panel.ClipToBounds);
        Assert.True(arcadia.Bounds(hero).Contains(drawn), $"Titel {drawn} reicht aus dem Bild {arcadia.Bounds(hero)}");
        Assert.True(drawn.Height >= heading.TextLayout.Height - 0.5, "Titel kleiner gezeichnet, als sein Text braucht");
    }

    private static void OpenByHoldingA(ArcadiaWindow arcadia)
    {
        arcadia.Arcadia.PressDown(UiAction.Accept).Wait(TimeSpan.FromSeconds(1.1)).LetGo(UiAction.Accept);
        arcadia.WaitForTransitions();
        Assert.True(arcadia.Arcadia.Main.IsDetailOpen, "Detailseite offen");
    }

    private static void AssertDetailOf(ArcadiaWindow arcadia, string title)
    {
        Assert.Equal(title, arcadia.Arcadia.Main.Detail?.Current.Tile.Title);
        Assert.Contains(CentrePage(arcadia).GetVisualDescendants().OfType<TextBlock>(), text => text.Text == title && text.FontSize >= 60);
    }

    private static List<string?> Crumbs(ArcadiaWindow arcadia)
    {
        var crumb = arcadia.OneWithText("Bibliothek").GetVisualAncestors().OfType<Button>().First();
        return crumb.GetVisualDescendants()
            .Select(visual => visual switch
            {
                Border { Child: TextBlock glyph } border when border.Classes.Contains("glyph") => glyph.Text,
                TextBlock { Parent: not Border } text when text.IsEffectivelyVisible => text.Text,
                _ => null,
            })
            .Where(text => text is not null)
            .ToList();
    }

    private static double Slide(ArcadiaWindow arcadia) =>
        ((TranslateTransform)arcadia.Window.GetVisualDescendants().OfType<Panel>().Single(panel => panel.Name == "Track").RenderTransform!).X;

    private static List<Control> Pages(ArcadiaWindow arcadia) => arcadia.Window.GetVisualDescendants()
        .OfType<Panel>()
        .Where(panel => panel.Classes.Contains("detailPage") && panel.IsEffectivelyVisible)
        .Cast<Control>()
        .ToList();

    private static Control CentrePage(ArcadiaWindow arcadia) =>
        Pages(arcadia).Single(page => page.DataContext is GameDetailPageViewModel { IsCentre: true });

    private static Border Card(Control page) => page.GetVisualChildren().OfType<Border>().First();

    private static Button NeighbourButton(ArcadiaWindow arcadia, int offset) =>
        Pages(arcadia).Single(page => page.DataContext is GameDetailPageViewModel side && side.Offset == offset)
            .GetVisualChildren().OfType<Button>().Single();

    private static Button CarouselButton(ArcadiaWindow arcadia, string pad) => arcadia.Window.GetVisualDescendants()
        .OfType<Button>()
        .Single(button => button.Classes.Contains("carouselNav") && button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == pad));

    private static List<Button> DetailButtons(Control page) => page.GetVisualDescendants()
        .OfType<Button>()
        .Where(button => button.Classes.Contains("detailAction") && button.IsEffectivelyVisible)
        .ToList();

    private static Button Focused(ArcadiaWindow arcadia) => Assert.Single(DetailButtons(CentrePage(arcadia)), button => button.Classes.Contains("focused"));

    private static string Label(Button button) => button.GetVisualDescendants().OfType<TextBlock>()
        .Last(text => text.IsEffectivelyVisible && text.Parent is StackPanel { Orientation: Avalonia.Layout.Orientation.Vertical }).Text ?? string.Empty;
}
