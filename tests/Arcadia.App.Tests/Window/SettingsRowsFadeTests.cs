using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// The settings rows fade out where more rows follow (#237), with the shared edge fade: where the fades show, that the
/// focused row and its group's heading stay clear of them, and that the mouse neither makes the rows jump nor is blocked.
/// </summary>
public sealed class SettingsRowsFadeTests
{
    /// <summary>A focus ring's width (FocusRing in Theme.axaml).</summary>
    private const double Ring = 3;

    /// <summary>With Pan on, the category "Controller" has more rows than fit.</summary>
    private static async Task<ArcadiaWindow> ControllerCategoryAsync(int width = ArcadiaWindow.Width, int height = ArcadiaWindow.Height, double scaling = 1)
    {
        var arcadia = await ArcadiaWindow.ShowAsync(
            [new LauncherSpec(KnownLaunchers.Steam, 3), new LauncherSpec(KnownLaunchers.Gog, 3)],
            new AppSettings { ActiveLaunchers = ["steam", "gog"], PanEnabled = true }, width, height, scaling, steam: new FakeSteam());
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        Assert.Equal(AppTexts.ControllerCategory, arcadia.Arcadia.Main.Settings!.Selected.Name);
        return arcadia.Settle();
    }

    [AvaloniaFact]
    [Trait("TestCase", "T237-1")]
    public async Task A_category_whose_rows_fit_has_no_fade()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        arcadia.Press(UiAction.Settings);
        var rows = arcadia.Arcadia.Main.Settings!.Selected.Rows;

        AssertFades(arcadia, top: false, bottom: false);
        arcadia.Press(UiAction.Down, times: rows.Count);
        AssertFades(arcadia, top: false, bottom: false);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T237-2")]
    public async Task Down_to_the_end_and_up_again_the_fades_follow_and_the_focused_row_and_its_heading_stay_clear_of_them()
    {
        using var arcadia = await ControllerCategoryAsync();
        var settings = arcadia.Arcadia.Main.Settings!;
        var rows = settings.Selected.Rows;
        AssertFades(arcadia, top: false, bottom: true);

        for (var i = 1; i < rows.Count; i++)
        {
            arcadia.Press(UiAction.Down);
            Assert.Same(rows[i], settings.FocusedRow);
            AssertFocusClear(arcadia, $"{i}× ▼");
        }

        AssertFades(arcadia, top: true, bottom: false);
        for (var i = rows.Count - 2; i >= 0; i--)
        {
            arcadia.Press(UiAction.Up);
            Assert.Same(rows[i], settings.FocusedRow);
            AssertFocusClear(arcadia, $"{rows.Count - 1 - i}× ▲");
        }

        AssertFades(arcadia, top: false, bottom: true);

        // Softly, as the grid's.
        var fade = RowsFade(arcadia);
        Assert.All(new[] { fade.StartFade, fade.EndFade }, strip => Assert.Contains(
            strip.Transitions ?? [], transition => transition is Avalonia.Animation.DoubleTransition { Duration.TotalMilliseconds: > 0 }));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T237-3")]
    public async Task The_wheel_to_the_middle_fades_both_edges_and_the_mouse_over_a_faded_row_focuses_it_without_a_jump_and_clicks_through()
    {
        using var arcadia = await ControllerCategoryAsync();
        var scroller = arcadia.Named<ScrollViewer>("SettingsRowsScroller");
        arcadia.Wheel(scroller, notches: -1).Settle();
        Assert.True(scroller.Offset.Y > 1, "Das Mausrad scrollt durch die Ausblendung");
        AssertFades(arcadia, top: true, bottom: true);

        // A row partly under the bottom fade, pointed at where it is faded.
        var faded = arcadia.Bounds(RowsFade(arcadia).EndFade);
        var settings = arcadia.Arcadia.Main.Settings!;
        var row = settings.Selected.Rows.First(row => arcadia.Bounds(arcadia.RowButton(row)).Intersect(faded).Height >= 16);
        var under = arcadia.Bounds(arcadia.RowButton(row)).Intersect(faded);
        var point = under.Center;
        var offset = scroller.Offset.Y;
        arcadia.MouseMove(new Point(point.X, point.Y - 300)).MouseMove(point).Settle();
        Assert.Same(row, settings.FocusedRow);
        Assert.Equal(offset, scroller.Offset.Y, 1);

        // The click reaches the row under the fade, and nothing scrolls.
        var clicks = 0;
        arcadia.RowButton(row).Click += (_, _) => clicks++;
        arcadia.Click(point).Settle();
        Assert.Equal(1, clicks);
        Assert.Equal(offset, scroller.Offset.Y, 1);
        Assert.False(RowsFade(arcadia).IsHitTestVisible, "Die Ausblendung lässt Klicks durch");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T237-4")]
    public async Task Another_category_brings_its_own_fades_and_coming_back_starts_at_the_first_row_at_the_top()
    {
        using var arcadia = await ControllerCategoryAsync();
        var settings = arcadia.Arcadia.Main.Settings!;
        arcadia.Press(UiAction.Down, times: settings.Selected.Rows.Count - 1);
        AssertFades(arcadia, top: true, bottom: false);

        // The category before fits: no fade, from its top.
        arcadia.Press(UiAction.PreviousTab).Settle();
        Assert.NotEqual(AppTexts.ControllerCategory, settings.Selected.Name);
        Assert.Equal(0, arcadia.Named<ScrollViewer>("SettingsRowsScroller").Offset.Y, 1);
        AssertFades(arcadia, top: false, bottom: false);

        // Back in "Controller" it starts again at its first row, at the top.
        arcadia.Press(UiAction.NextTab).Settle();
        Assert.Same(settings.Selected.Rows[0], settings.FocusedRow);
        Assert.Equal(0, arcadia.Named<ScrollViewer>("SettingsRowsScroller").Offset.Y, 1);
        AssertFades(arcadia, top: false, bottom: true);
        AssertFocusClear(arcadia, "zurück in Controller");

        // The layout is scaled into the window: a smaller window keeps what fits and so the fades.
        arcadia.Window.Width = 1280;
        arcadia.Window.Height = 720;
        arcadia.Settle();
        AssertFades(arcadia, top: false, bottom: true);
        AssertFocusClear(arcadia, "kleineres Fenster");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T237-4")]
    public async Task Rows_that_go_or_come_take_the_fades_with_them_without_scrolling()
    {
        using var arcadia = await ControllerCategoryAsync();
        var rows = arcadia.Arcadia.Main.Settings!.Selected.Rows;
        var scroller = arcadia.Named<ScrollViewer>("SettingsRowsScroller");
        AssertFades(arcadia, top: false, bottom: true);

        // Rows go until the rest fits (e.g. a group that goes away): the bottom fade goes with them, nothing scrolls.
        var gone = new List<SettingsRowViewModel>();
        while (scroller.Extent.Height > scroller.Viewport.Height + 1)
        {
            gone.Add(rows[^1]);
            rows.RemoveAt(rows.Count - 1);
            arcadia.Settle();
        }

        Assert.Equal(0, scroller.Offset.Y, 1);
        AssertFades(arcadia, top: false, bottom: false);

        // They come back: so does the fade, still from the top.
        gone.Reverse();
        foreach (var row in gone)
        {
            rows.Add(row);
        }

        arcadia.Settle();
        Assert.Equal(0, scroller.Offset.Y, 1);
        AssertFades(arcadia, top: false, bottom: true);
    }

    [AvaloniaTheory]
    [Trait("TestCase", "T237-5")]
    [InlineData(1920, 1080, 1.0)]
    [InlineData(1280, 720, 1.0)]
    [InlineData(1536, 864, 1.25)]
    public async Task No_line_of_unfaded_rows_is_left_at_the_edges_also_at_a_scaled_size(int width, int height, double scaling)
    {
        using var arcadia = await ControllerCategoryAsync(width, height, scaling);
        AssertFadedToBackground(arcadia, top: false);
        arcadia.Press(UiAction.Down, times: arcadia.Arcadia.Main.Settings!.Selected.Rows.Count);
        AssertFadedToBackground(arcadia, top: true);
    }

    private static EdgeFade RowsFade(ArcadiaWindow arcadia) => arcadia.Named<EdgeFade>("SettingsRowsFade");

    private static void AssertFades(ArcadiaWindow arcadia, bool top, bool bottom)
    {
        arcadia.WaitForTransitions();
        var fade = RowsFade(arcadia);
        Assert.Equal((top, bottom), (fade.ShowsStart, fade.ShowsEnd));
        Assert.Equal(top ? 1 : 0, fade.StartFade.Opacity, 2);
        Assert.Equal(bottom ? 1 : 0, fade.EndFade.Opacity, 2);
    }

    /// <summary>The focused row with its ring, and at a group's start its heading, lie outside every fade shown.</summary>
    private static void AssertFocusClear(ArcadiaWindow arcadia, string when)
    {
        arcadia.WaitForTransitions();
        var row = arcadia.Arcadia.Main.Settings!.FocusedRow!;
        var button = arcadia.RowButton(row);
        var clear = new List<(string What, Rect Bounds)> { ("Zeile", arcadia.Bounds(button).Inflate(Ring)) };
        if (button.FindAncestorOfType<StackPanel>()?.GetVisualDescendants().OfType<TextBlock>()
                .FirstOrDefault(text => text.Classes.Contains("settingsGroup") && text.IsEffectivelyVisible) is { } heading)
        {
            clear.Add(("Gruppenüberschrift", arcadia.Bounds(heading)));
        }

        var fade = RowsFade(arcadia);
        foreach (var strip in new[] { fade.StartFade, fade.EndFade }.Where(strip => strip.Opacity > 0))
        {
            var faded = arcadia.Bounds(strip);
            foreach (var (what, bounds) in clear)
            {
                Assert.False(faded.Intersects(bounds), $"{when}: {what} von „{row.Title}“ unter der Ausblendung ({bounds} / {faded})");
            }
        }
    }

    /// <summary>The fade's outer edge, the last device pixels of the rows, is the page's background across the rows.</summary>
    private static void AssertFadedToBackground(ArcadiaWindow arcadia, bool top)
    {
        arcadia.WaitForTransitions();
        using var frame = arcadia.Capture();
        var rows = arcadia.Bounds(arcadia.Named<ScrollViewer>("SettingsRowsScroller"));
        var background = ArcadiaWindow.ThemeColour("BgColor");
        var y = (int)Math.Round((top ? rows.Top : rows.Bottom) * frame.Scaling) - (top ? 0 : 1);
        for (var x = (int)Math.Ceiling(rows.Left * frame.Scaling); x < (int)(rows.Right * frame.Scaling); x++)
        {
            var colour = frame.At(x, y);
            Assert.True(ArcadiaWindow.Distance(colour, background) <= 12, $"Unverblasste Zeile am Rand bei {x},{y} (Gerätepixel): {colour} statt {background}");
        }
    }
}
