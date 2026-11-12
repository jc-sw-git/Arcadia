using System.Globalization;
using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Views;
using Arcadia.Core.Search;
using Arcadia.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests.Guards;

/// <summary>
/// The design polish of #24 as drawn: covers in 2 : 3 wherever one is shown, and ⧉ ≡ ↹ as theme icons instead of
/// characters no embedded font has.
/// </summary>
public sealed class CoverAndIconTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    /// <summary>T24-6: library 146 × 219 in 8 columns, search hit 190 × 285, detail page 210 × 315.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T24-6")]
    public async Task Every_cover_is_2_to_3_in_the_library_the_search_and_on_the_detail_page()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var driver = arcadia.Arcadia;

        // At rest: the second cover, as the first is lifted by the focus.
        var tile = arcadia.Tile(driver.PageGames[1]);
        AssertCover(146, 219, tile.Bounds.Size, "Bibliothek");
        var tops = driver.PageGames.Take(9).Select(game => Math.Round(arcadia.LayoutBounds(arcadia.Tile(game)).Top)).ToList();
        Assert.Equal(8, tops.Count(top => top == tops[0]));
        Assert.Equal(tops[0] + 219 + 20, tops[8], 1);

        arcadia.Press(UiAction.Search).Type("Hollow").Settle();
        var hit = arcadia.Named<Control>("SearchResults").GetVisualDescendants().OfType<Button>().First(button => button.Classes.Contains("resultTile") && button.IsEffectivelyVisible);
        AssertCover(190, 285, hit.Bounds.Size, "Suche");
        arcadia.Press(UiAction.Back).Press(UiAction.Back);

        driver.Hold(UiAction.Accept, PastHoldTime);
        arcadia.Settle().WaitForTransitions();
        var detail = arcadia.Window.GetVisualDescendants().OfType<GameDetailView>().Single();
        var cover = detail.GetVisualDescendants().OfType<Border>().First(border => border.IsEffectivelyVisible && border.Background is not null && border.Bounds.Width == 180);
        AssertCover(180, 270, cover.Bounds.Size, "Detailseite");
    }

    /// <summary>T24-7: the monogram stays top left and a long title at the bottom, inside the taller cover.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T24-7")]
    public async Task The_monogram_stays_at_the_top_and_the_title_at_the_bottom_of_the_taller_cover()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var game = arcadia.Arcadia.PageGames[1];
        var tile = arcadia.Tile(game);
        var cover = arcadia.Bounds(tile);

        var monogram = arcadia.Bounds(tile.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == game.Monogram));
        var title = arcadia.Bounds(tile.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == game.Title));

        Assert.InRange(monogram.Top - cover.Top, 0, 24);
        Assert.InRange(cover.Bottom - title.Bottom, 0, 24);
        Assert.True(cover.Contains(monogram) && cover.Contains(title), $"Monogramm {monogram} und Titel {title} im Cover {cover}");
    }

    /// <summary>
    /// T24-11: Xbox's and the Steam Controller's ⧉ and ≡ are the theme icons in the footer, as high as a capital letter of
    /// the badge and in its colour; no text on screen holds ⧉ ≡ ↹ as a character.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(ButtonSymbols.Xbox)]
    [InlineData(ButtonSymbols.SteamController)]
    [Trait("TestCase", "T24-11")]
    public async Task View_and_Menu_are_theme_icons_as_high_as_a_capital_letter(ButtonSymbols symbols)
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        InputHints.Symbols = symbols;
        arcadia.Settle();

        var view = Icon(arcadia, "⧉");
        Assert.Equal(ButtonIcon.View, view.Glyph.Shown?.Icon);
        Assert.Same(ArcadiaWindow.ThemeResource<Geometry>("PadIconView"), view.Path.Data);

        arcadia.Press(UiAction.Settings).Settle();
        var menu = Icon(arcadia, "≡");
        Assert.Equal(ButtonIcon.Options, menu.Glyph.Shown?.Icon);
        Assert.Same(ArcadiaWindow.ThemeResource<Geometry>("PadIconOptions"), menu.Path.Data);

        foreach (var (glyph, path) in new[] { view, menu })
        {
            var capital = CapitalHeight(13, FontWeight.ExtraBold);
            Assert.InRange(path.Bounds.Height, capital - 1, capital + 1);
            Assert.Equal(ArcadiaWindow.ThemeResource<IBrush>("TextStrong"), path.Stroke);
        }

        Assert.Empty(TextsWithSymbolsDrawnAsIcons(arcadia.Window));
    }

    /// <summary>T24-11: no page, dialog or settings category of <see cref="GuardPages"/> holds ⧉ ≡ ↹ as a character in a text.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T24-11")]
    public Task No_page_dialog_or_settings_category_shows_a_symbol_drawn_as_an_icon_as_a_character() =>
        GuardWalk.EveryAsync(_ => true, (arcadia, _, state) =>
        {
            InputHints.Symbols = ButtonSymbols.Xbox;
            var texts = TextsWithSymbolsDrawnAsIcons(arcadia.Settle().Window);
            Assert.True(texts.Count == 0, $"{state}: {string.Join(", ", texts)}");
        }, walks: _ => false);

    /// <summary>T24-13: on the on-screen keyboard, Tab shows KeyIconTab beside its name, the dock key ⧉ and Enter ≡ as icons.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T24-13")]
    public void The_on_screen_keyboard_draws_tab_dock_and_enter_with_icons()
    {
        // The mouse mode's keyboard has every key; the search's leaves out Tab and the dock key.
        using var mouseMode = MouseModeKeyboard.Show();
        InputHints.Symbols = ButtonSymbols.Xbox;
        var window = mouseMode.Window;
        var keyboard = mouseMode.Keyboard;
        Dispatcher.UIThread.RunJobs();

        var tab = KeyButton(window, keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Tab));
        var tabIcon = Assert.Single(tab.GetVisualDescendants().OfType<Path>(), path => path.IsEffectivelyVisible);
        Assert.Same(ArcadiaWindow.ThemeResource<Geometry>("KeyIconTab"), tabIcon.Data);
        Assert.Contains(tab.GetVisualDescendants().OfType<TextBlock>(), text => text.IsEffectivelyVisible && text.Text == "Tab");

        var dock = KeyButton(window, keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Dock));
        Assert.Contains(dock.GetVisualDescendants().OfType<PadGlyph>(), glyph => glyph.IsEffectivelyVisible && glyph.Shown?.Icon == ButtonIcon.View);
        var enter = KeyButton(window, keyboard.Keys.First(key => key.Key.Kind == BoardKeyKind.Enter));
        Assert.Contains(enter.GetVisualDescendants().OfType<PadGlyph>(), glyph => glyph.IsEffectivelyVisible && glyph.Shown?.Icon == ButtonIcon.Options);

        Assert.Empty(TextsWithSymbolsDrawnAsIcons(window));
    }

    private static void AssertCover(double width, double height, Size size, string where)
    {
        Assert.True(Math.Abs(size.Width - width) <= 1 && Math.Abs(size.Height - height) <= 1, $"{where}: {size} statt {width} × {height}");
        Assert.Equal(2.0 / 3.0, size.Width / size.Height, 2);
    }

    private static (PadGlyph Glyph, Path Path) Icon(ArcadiaWindow arcadia, string button)
    {
        var glyph = arcadia.Window.GetVisualDescendants().OfType<PadGlyph>().First(glyph => glyph.IsEffectivelyVisible && glyph.Button == button);
        return (glyph, Assert.IsType<Path>(glyph.Child));
    }

    // The height of a capital letter of Figtree at the badge's size: the icon stands as high as the letters beside it.
    private static double CapitalHeight(double fontSize, FontWeight weight)
    {
        var capital = new FormattedText("H", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(ArcadiaFonts.Figtree, FontStyle.Normal, weight), fontSize, null);
        return capital.Extent;
    }

    // Every text on screen, plain or with inlines (ButtonTextBlock), that still holds ⧉ ≡ ↹ as a character.
    private static IReadOnlyList<string> TextsWithSymbolsDrawnAsIcons(Window window) => window.GetVisualDescendants()
        .OfType<TextBlock>()
        .Where(text => text.IsEffectivelyVisible)
        .Select(text => text.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Run>().Select(run => run.Text)) : text.Text ?? string.Empty)
        .Where(text => text.AsSpan().IndexOfAny("⧉≡↹") >= 0)
        .ToList();

    private static Button KeyButton(Window window, BoardKeyViewModel key) => window.GetVisualDescendants().OfType<Button>()
        .First(button => button.Classes.Contains("boardKey") && button.DataContext == key && button.IsEffectivelyVisible);
}
