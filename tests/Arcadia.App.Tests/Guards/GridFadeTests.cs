using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace Arcadia.App.Tests.Guards;

/// <summary>The fades at the grid's edges (#102), drawn: where they show, and that nothing unfaded is left at the edge.</summary>
public sealed class GridFadeTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T102-1")]
    public async Task A_page_taller_than_the_grid_fades_only_towards_where_it_scrolls_and_the_end_rows_show_whole()
    {
        // GOG has 31 games: four rows at 64 per page, a little more than fits.
        using var arcadia = await ArcadiaWindow.ShowAsync(settings: ArcadiaWindow.WithPageSize(64));
        var main = arcadia.Arcadia.Main;

        AssertFades(arcadia, top: false, bottom: true);
        Assert.True(IsInView(arcadia, FirstRowTile(main)), "Ganz oben ist die erste Reihe vollständig sichtbar.");
        AssertFadedToBackground(arcadia, top: false);

        arcadia.Press(UiAction.Down, times: 3);
        AssertFades(arcadia, top: true, bottom: false);
        Assert.True(IsInView(arcadia, LastRowTile(main)), "Ganz unten ist die letzte Reihe vollständig sichtbar.");
        AssertFadedToBackground(arcadia, top: true);

        // Xbox has 62 games on page 1: eight rows, so the middle can scroll both ways.
        arcadia.Press(UiAction.NextTab).Press(UiAction.Down, times: 3);
        AssertFades(arcadia, top: true, bottom: true);

        // Softly: both fades change their opacity over a moment, not at once.
        var fade = arcadia.Named<EdgeFade>("GridFade");
        Assert.All(new[] { fade.StartFade, fade.EndFade }, strip => Assert.Contains(
            strip.Transitions ?? [],
            transition => transition is DoubleTransition { Property.Name: "Opacity", Duration.TotalMilliseconds: > 0 }));
    }

    /// <summary>T24-2: the default page of 24, three full rows, fits the grid whole: nothing to scroll, no fade.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T102-2")]
    [Trait("TestCase", "T24-2")]
    public async Task A_page_that_fits_the_grid_has_no_fade()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(settings: ArcadiaWindow.WithPageSize(24));
        Assert.Equal(24, arcadia.Arcadia.PageGames.Count);
        var scroller = arcadia.Named<ScrollViewer>("GridScroller");
        Assert.True(scroller.Extent.Height <= scroller.Viewport.Height, $"Die Seite ist {scroller.Extent.Height} hoch, sichtbar sind {scroller.Viewport.Height}.");

        AssertFades(arcadia, top: false, bottom: false);
        arcadia.Press(UiAction.Down, times: 2);
        AssertFades(arcadia, top: false, bottom: false);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T102-3")]
    public async Task The_focused_tile_never_lies_under_a_fade_and_the_wheel_switches_the_fades_too()
    {
        // Xbox: eight rows on page 1 at 64 per page.
        using var arcadia = await ArcadiaWindow.ShowAsync(settings: ArcadiaWindow.WithPageSize(64));
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
        arcadia.Wheel(arcadia.Named<ScrollViewer>("GridScroller"), notches: -40);
        AssertFades(arcadia, top: true, bottom: false);
        arcadia.Wheel(arcadia.Named<ScrollViewer>("GridScroller"), notches: 40);
        AssertFades(arcadia, top: false, bottom: true);
        Assert.NotNull(arcadia.Arcadia.FocusedGame);
    }

    [AvaloniaTheory]
    [Trait("TestCase", "T102-4")]
    [InlineData(1745, 1000, 1.0)]
    [InlineData(1536, 864, 1.25)]
    public async Task No_line_of_unfaded_covers_is_left_at_the_edge_of_a_fade_also_at_a_scaled_size(int width, int height, double scaling)
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(settings: ArcadiaWindow.WithPageSize(64), width: width, height: height, scaling: scaling);

        AssertFadedToBackground(arcadia, top: false);
        arcadia.Press(UiAction.Down, times: 3);
        AssertFadedToBackground(arcadia, top: true);
    }

    private static GameTileViewModel FirstRowTile(MainViewModel main) =>
        main.Library.PageGames.Take(LibraryViewModel.GridColumns).First(tile => !tile.IsFocused);

    private static GameTileViewModel LastRowTile(MainViewModel main) =>
        main.Library.PageGames.Skip((main.Library.PageGames.Count - 1) / LibraryViewModel.GridColumns * LibraryViewModel.GridColumns).First(tile => !tile.IsFocused);

    private static void AssertFades(ArcadiaWindow arcadia, bool top, bool bottom)
    {
        arcadia.WaitForTransitions();
        var fade = arcadia.Named<EdgeFade>("GridFade");
        Assert.Equal(top ? 1 : 0, fade.StartFade.Opacity, 2);
        Assert.Equal(bottom ? 1 : 0, fade.EndFade.Opacity, 2);
    }

    /// <summary>Not the focused tile: its lift and ring reach into the grid's padding.</summary>
    private static bool IsInView(ArcadiaWindow arcadia, GameTileViewModel tile) =>
        arcadia.Bounds(arcadia.Named<ScrollViewer>("GridScroller")).Contains(arcadia.Bounds(arcadia.Tile(tile)));

    private static void AssertFocusClearOfFades(ArcadiaWindow arcadia, string when)
    {
        arcadia.WaitForTransitions();
        var tile = arcadia.Tile(arcadia.Arcadia.FocusedGame!);
        var focused = arcadia.Bounds(tile);
        var fade = arcadia.Named<EdgeFade>("GridFade");
        foreach (var strip in new[] { fade.StartFade, fade.EndFade })
        {
            if (strip.Opacity > 0)
            {
                Assert.False(arcadia.Bounds(strip).Intersects(focused), $"{when}: Fokus unter der Ausblendung");
            }
        }

        arcadia.AssertRingAround(tile, when);
    }

    /// <summary>The fade's outer edge, the last device pixels of the grid, is the window's background across all covers.</summary>
    private static void AssertFadedToBackground(ArcadiaWindow arcadia, bool top)
    {
        using var frame = arcadia.Capture();
        var grid = arcadia.Bounds(arcadia.Named<ScrollViewer>("GridScroller"));
        var background = ArcadiaWindow.ThemeColour("BgColor");
        var edge = (int)Math.Round((top ? grid.Top : grid.Bottom) * frame.Scaling);
        // The outermost row of device pixels: there the fade has reached the background (a few steps off where the
        // gradient starts); a cover not covered by the fade stands out by twice that or more.
        var rows = top ? new[] { edge } : [edge - 1];
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
