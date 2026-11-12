using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Search;
using Arcadia.Input;
using Arcadia.Launchers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests.Guards;

/// <summary>The focus ring is drawn whole wherever the focus goes (G-32): nothing clips or covers it.</summary>
public sealed class FocusRingTests
{
    [AvaloniaFact]
    [Trait("TestCase", "G-32")]
    public async Task The_ring_of_a_cover_shows_whole_at_the_grids_corners()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var main = arcadia.Arcadia.Main;

        AssertRing(arcadia, "erstes Cover", "FocusRingLifted");
        arcadia.Press(UiAction.Right, times: 7);
        AssertRing(arcadia, "letzte Spalte", "FocusRingLifted");
        arcadia.Press(UiAction.Down, times: 2);
        AssertRing(arcadia, "untere Reihe, letzte Spalte", "FocusRingLifted");
        arcadia.Press(UiAction.Down);
        Assert.Same(arcadia.Arcadia.PageGames[^1], arcadia.Arcadia.FocusedGame);
        AssertRing(arcadia, "letztes Cover der Seite", "FocusRingLifted");
    }

    /// <summary>
    /// Wherever the focus goes on any page, dialog or settings category of <see cref="GuardPages"/> (e.g. every choice of
    /// "Ansicht", every item of the game menu, every settings row), its ring shows whole. A row not available here (Steam
    /// Controller support off Windows) is drawn dimmed, its ring with it, and is left out.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T180-3")]
    [Trait("TestCase", "G-32")]
    public async Task The_ring_shows_whole_wherever_the_focus_goes_on_every_page_dialog_and_settings_category()
    {
        var rings = 0;
        await GuardWalk.EveryAsync(_ => true, (arcadia, page, state) =>
        {
            if (page.FocusedControl(arcadia) is not { } focused || (page.RingFinding is { } known && state.EndsWith(known.State, StringComparison.Ordinal)))
            {
                return;
            }

            var where = $"{state}: {Describe(focused)}";
            if (GuardPage.RingOf(arcadia, focused) is var (ringed, ring))
            {
                arcadia.AssertRingAround(ringed, where, ring);
            }
            else
            {
                // Without a ring the focus is the button filled in the strong text colour, drawn whole as a tab's pill.
                arcadia.AssertDrawnWhole(focused, where, "TextStrong");
            }

            rings++;
        });

        Assert.True(rings >= 50, $"Nur {rings} Fokusrahmen geprüft");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T180-3")]
    [Trait("TestCase", "G-32")]
    public async Task The_selected_tab_shows_whole_at_the_first_a_middle_and_the_last_tab_of_a_long_strip()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(KnownLaunchers.All.Select(launcher => new LauncherSpec(launcher, 3)).ToList());
        var tabs = arcadia.Arcadia.Main.LauncherTabs.Items;

        foreach (var (moves, where) in new[] { (0, "erster Tab"), (tabs.Count / 2, "Tab in der Mitte"), (tabs.Count - 1 - (tabs.Count / 2), "letzter Tab") })
        {
            arcadia.Press(UiAction.NextTab, times: moves);
            arcadia.AssertDrawnWhole(TabButton(arcadia, arcadia.Arcadia.SelectedTab!), $"{where} ({arcadia.Arcadia.SelectedTab!.Name})", "TextStrong");
        }

        Assert.Same(tabs[^1], arcadia.Arcadia.SelectedTab);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T180-3")]
    [Trait("TestCase", "G-32")]
    public async Task The_ring_shows_whole_on_the_tall_Enter_of_the_search_keyboard()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        arcadia.Press(UiAction.Search).WaitForTransitions();
        var keyboard = arcadia.Arcadia.Search!.Keyboard;

        var enter = keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Enter);
        arcadia.Arcadia.GoTo(enter);
        arcadia.Settle();
        using var frame = arcadia.Capture();
        AssertTallEnterRing(frame, arcadia.Window, "Suche");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T180-3")]
    [Trait("TestCase", "G-32")]
    public void The_ring_shows_whole_on_the_tall_Enter_and_the_layout_menu_of_the_mouse_modes_keyboard()
    {
        using var mouseMode = MouseModeKeyboard.Show();
        var keyboard = mouseMode.Keyboard;

        foreach (var step in OnScreenKeys.PathTo(keyboard, keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Enter)))
        {
            mouseMode.Press(step switch
            {
                UiAction.Up => KeyboardButton.Up,
                UiAction.Down => KeyboardButton.Down,
                UiAction.Left => KeyboardButton.Left,
                _ => KeyboardButton.Right,
            });
        }

        using (var frame = ArcadiaWindow.CaptureSettled(mouseMode.Window))
        {
            AssertTallEnterRing(frame, mouseMode.Window, "Mausmodus");
        }

        mouseMode.Press(KeyboardButton.Layout);
        Assert.True(keyboard.LayoutMenu.IsOpen);
        for (var item = 0; item < keyboard.LayoutMenu.Items.Count; item++)
        {
            var focused = Focused(mouseMode.Window, "layoutItem");
            ArcadiaWindow.AssertRingAround(mouseMode.Window, focused, $"Mausmodus, Layoutmenü: {Describe(focused)}", "FocusRingInset");
            mouseMode.Press(KeyboardButton.Down);
        }
    }

    /// <summary>
    /// The tall ISO Enter draws its ring on its L shape (<c>Path.enterRing</c>), not as a shadow: its stroke shows on the
    /// top, the right, the bottom of its lower part and the left of its upper part.
    /// </summary>
    private static void AssertTallEnterRing(Frame frame, Avalonia.Controls.Window window, string where)
    {
        var ring = Assert.Single(window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>(), path => path.Classes.Contains("enterRing") && path.IsEffectivelyVisible);
        var bounds = ArcadiaWindow.Bounds(window, ring);
        var colour = ArcadiaWindow.ThemeResource<Avalonia.Media.ISolidColorBrush>("TextStrong").Color;
        var probes = new[]
        {
            ("oben", new Point(bounds.Center.X, bounds.Top), new Vector(0, 1)),
            ("unten", new Point(bounds.Left + (bounds.Width * 0.75), bounds.Bottom), new Vector(0, -1)),
            ("links", new Point(bounds.Left, bounds.Top + (bounds.Height * 0.2)), new Vector(1, 0)),
            ("rechts", new Point(bounds.Right, bounds.Center.Y), new Vector(-1, 0)),
        };
        foreach (var (side, edge, inwards) in probes)
        {
            // From just outside the shape's edge to past its 3 px stroke.
            var found = Enumerable.Range(-2, 8).Any(i => ArcadiaWindow.Distance(frame.At(edge + (inwards * i / frame.Scaling)), colour) <= 40);
            Assert.True(found, $"{where}: Rahmen des hohen Enter {side} bei {edge} fehlt oder ist abgeschnitten.");
        }
    }

    private static Button TabButton(ArcadiaWindow arcadia, LauncherTabViewModel tab) => arcadia.ShowingAll(tab)
        .SelectMany(control => control.GetSelfAndVisualDescendants().OfType<Button>())
        .First(button => button.Classes.Contains("tab") && button.GetVisualAncestors().OfType<ScrollViewer>().Any(scroller => scroller.Name == "TabScroller"));

    private static Button Focused(Avalonia.Controls.Window window, string kind) => Assert.Single(
        window.GetVisualDescendants().OfType<Button>(),
        button => button.IsEffectivelyVisible && button.Classes.Contains("focused") && button.Classes.Contains(kind));

    private static void AssertRing(ArcadiaWindow arcadia, string where, string ring) =>
        arcadia.AssertRingAround(arcadia.Tile(arcadia.Arcadia.FocusedGame!), where, ring);

    private static string Describe(Control button) =>
        string.Join(' ', button.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text).Take(2));
}
