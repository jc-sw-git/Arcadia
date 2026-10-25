using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace Arcadia.App.Tests;

/// <summary>The fades at the grid's edges (#102), drawn: where they show, and that nothing unfaded is left at the edge.</summary>
public sealed class GridFadeTests
{
    [AvaloniaFact]
    [Trait("Testfall", "T102-1")]
    public async Task A_page_taller_than_the_grid_fades_only_towards_where_it_scrolls_and_the_end_rows_show_whole()
    {
        // GOG has 31 games: four rows at 50 per page, a little more than fits.
        using var arcadia = await ArcadiaWindow.ShowAsync(settings: ArcadiaWindow.WithPageSize(50));
        var main = arcadia.Arcadia.Main;

        AssertFades(arcadia, top: false, bottom: true);
        Assert.True(IsInView(arcadia, FirstRowTile(main)), "Ganz oben ist die erste Reihe vollständig sichtbar.");
        AssertFadedToBackground(arcadia, top: false);

        arcadia.Press(UiAction.Down, times: 3);
        AssertFades(arcadia, top: true, bottom: false);
        Assert.True(IsInView(arcadia, LastRowTile(main)), "Ganz unten ist die letzte Reihe vollständig sichtbar.");
        AssertFadedToBackground(arcadia, top: true);

        // Xbox has 50 games on page 1: seven rows, so the middle can scroll both ways.
        arcadia.Press(UiAction.NextTab).Press(UiAction.Down, times: 3);
        AssertFades(arcadia, top: true, bottom: true);

        // Softly: both fades change their opacity over a moment, not at once.
        Assert.All(new[] { "GridFadeTop", "GridFadeBottom" }, name => Assert.Contains(
            arcadia.Named<Border>(name).Transitions ?? [],
            transition => transition is DoubleTransition { Property.Name: "Opacity", Duration.TotalMilliseconds: > 0 }));
    }

    [AvaloniaFact]
    [Trait("Testfall", "T102-2")]
    public async Task A_page_that_fits_the_grid_has_no_fade()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(settings: ArcadiaWindow.WithPageSize(10));

        AssertFades(arcadia, top: false, bottom: false);
        arcadia.Press(UiAction.Down, times: 2);
        AssertFades(arcadia, top: false, bottom: false);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T102-3")]
    public async Task The_focused_tile_never_lies_under_a_fade_and_the_wheel_switches_the_fades_too()
    {
        // Xbox: seven rows on page 1 at 50 per page.
        using var arcadia = await ArcadiaWindow.ShowAsync(settings: ArcadiaWindow.WithPageSize(50));
        var main = arcadia.Arcadia.Main;
        arcadia.Press(UiAction.NextTab);

        for (var step = 0; step < 7; step++)
        {
            AssertFocusClearOfFades(arcadia, $"{step}× runter");
            arcadia.Press(UiAction.Down);
        }

        for (var step = 0; step < 7; step++)
        {
            AssertFocusClearOfFades(arcadia, $"{step}× hoch");
            arcadia.Press(UiAction.Up);
        }

        AssertFades(arcadia, top: false, bottom: true);
        arcadia.Wheel(arcadia.Named<ScrollViewer>("GridScroller"), notches: -2);
        AssertFades(arcadia, top: true, bottom: true);
        arcadia.Wheel(arcadia.Named<ScrollViewer>("GridScroller"), notches: -20);
        AssertFades(arcadia, top: true, bottom: false);
        arcadia.Wheel(arcadia.Named<ScrollViewer>("GridScroller"), notches: 20);
        AssertFades(arcadia, top: false, bottom: true);
        Assert.NotNull(main.FocusedGame);
    }

    [AvaloniaTheory]
    [Trait("Testfall", "T102-4")]
    [InlineData(1745, 1000, 1.0)]
    [InlineData(1536, 864, 1.25)]
    public async Task No_line_of_unfaded_covers_is_left_at_the_edge_of_a_fade_also_at_a_scaled_size(int width, int height, double scaling)
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(settings: ArcadiaWindow.WithPageSize(50), width: width, height: height, scaling: scaling);

        AssertFadedToBackground(arcadia, top: false);
        arcadia.Press(UiAction.Down, times: 3);
        AssertFadedToBackground(arcadia, top: true);
    }

    private static GameTileViewModel FirstRowTile(MainViewModel main) =>
        main.PageGames.Take(MainViewModel.GridColumns).First(tile => !tile.IsFocused);

    private static GameTileViewModel LastRowTile(MainViewModel main) =>
        main.PageGames.Skip((main.PageGames.Count - 1) / MainViewModel.GridColumns * MainViewModel.GridColumns).First(tile => !tile.IsFocused);

    private static void AssertFades(ArcadiaWindow arcadia, bool top, bool bottom)
    {
        arcadia.WaitForTransitions();
        Assert.Equal(top ? 1 : 0, arcadia.Named<Border>("GridFadeTop").Opacity, 2);
        Assert.Equal(bottom ? 1 : 0, arcadia.Named<Border>("GridFadeBottom").Opacity, 2);
    }

    /// <summary>Not the focused tile: its lift and ring reach into the grid's padding.</summary>
    private static bool IsInView(ArcadiaWindow arcadia, GameTileViewModel tile) =>
        arcadia.Bounds(arcadia.Named<ScrollViewer>("GridScroller")).Contains(arcadia.Bounds(arcadia.Tile(tile)));

    private static void AssertFocusClearOfFades(ArcadiaWindow arcadia, string when)
    {
        arcadia.WaitForTransitions();
        var tile = arcadia.Tile(arcadia.Arcadia.Main.FocusedGame!);
        var focused = arcadia.Bounds(tile);
        foreach (var name in new[] { "GridFadeTop", "GridFadeBottom" })
        {
            var fade = arcadia.Named<Border>(name);
            if (fade.Opacity > 0)
            {
                Assert.False(arcadia.Bounds(fade).Intersects(focused), $"{when}: Fokus unter {name}");
            }
        }

        arcadia.AssertRingAround(tile, when);
    }

    /// <summary>The fade's outer edge, the last device pixels of the grid, is the window's background across all covers.</summary>
    private static void AssertFadedToBackground(ArcadiaWindow arcadia, bool top)
    {
        var grid = arcadia.Bounds(arcadia.Named<ScrollViewer>("GridScroller"));
        using var frame = arcadia.Capture();
        var background = ArcadiaWindow.ThemeColour("BgColor");
        var edge = (int)Math.Round((top ? grid.Top : grid.Bottom) * frame.Scaling);
        var rows = top ? new[] { edge, edge + 1 } : [edge - 1, edge - 2];
        for (var x = (int)((grid.Left + 20) * frame.Scaling); x < (grid.Right - 80) * frame.Scaling; x++)
        {
            foreach (var y in rows)
            {
                var colour = frame.At(x, y);
                Assert.True(Close(colour, background), $"Unverblasstes Cover am Rand bei {x},{y} (Gerätepixel): {colour} statt {background}");
            }
        }
    }

    private static bool Close(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B) <= 12;
}
