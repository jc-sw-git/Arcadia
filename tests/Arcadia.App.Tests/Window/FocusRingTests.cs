using Arcadia.App.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

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
        Assert.Same(main.PageGames[^1], main.FocusedGame);
        AssertRing(arcadia, "letztes Cover der Seite", "FocusRingLifted");
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-32")]
    public async Task The_ring_shows_whole_on_every_choice_of_the_view_options()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var main = arcadia.Arcadia.Main;
        arcadia.Press(UiAction.ViewOptions);

        // Through the sort choices, then down to the page sizes and along them.
        var seen = new HashSet<Button>();
        foreach (var move in new[] { UiAction.Right, UiAction.Right, UiAction.Down, UiAction.Right, UiAction.Down, UiAction.Down, UiAction.Left, UiAction.Left })
        {
            var focused = Focused(arcadia);
            seen.Add(focused);
            arcadia.AssertRingAround(focused, $"Ansicht: {Describe(focused)}");
            arcadia.Press(move);
        }

        Assert.Contains(seen, button => button.Classes.Contains("segment"));
        Assert.Contains(seen, button => button.Classes.Contains("size"));
        Assert.True(main.IsViewOptionsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-32")]
    public async Task The_ring_shows_whole_on_the_settings_rows_and_their_buttons()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        arcadia.Press(UiAction.Settings);

        // Every category, down its rows to the last one.
        var seen = 0;
        for (var category = 0; category < 4; category++)
        {
            Button? last = null;
            for (var row = 0; row < 12; row++)
            {
                var focused = Focused(arcadia);
                if (focused == last)
                {
                    break;
                }

                // A row not available here (Steam Controller support off Windows) is drawn dimmed, its ring with it.
                if (focused.IsEffectivelyEnabled && focused.GetSelfAndVisualAncestors().OfType<Visual>().All(visual => visual.Opacity >= 1))
                {
                    arcadia.AssertRingAround(focused, $"Einstellungen {category}: {Describe(focused)}");
                    seen++;
                }

                last = focused;
                arcadia.Press(UiAction.Down);
            }

            arcadia.Press(UiAction.NextTab);
        }

        Assert.True(seen >= 8, $"Nur {seen} Zeilen geprüft");
    }

    private static void AssertRing(ArcadiaWindow arcadia, string where, string ring) =>
        arcadia.AssertRingAround(arcadia.Tile(arcadia.Arcadia.Main.FocusedGame!), where, ring);

    private static Button Focused(ArcadiaWindow arcadia) => Assert.Single(
        arcadia.Window.GetVisualDescendants().OfType<Button>(),
        button => button.IsEffectivelyVisible && button.Classes.Contains("focused") && !button.Classes.Contains("tile"));

    private static string Describe(Button button) =>
        string.Join(' ', button.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text).Take(2));
}
