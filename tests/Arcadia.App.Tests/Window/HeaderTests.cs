using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;

namespace Arcadia.App.Tests;

/// <summary>The header's launcher tabs (#127) as drawn: where the strip stands, how it turns and fades.</summary>
public sealed class HeaderTests
{
    /// <summary>All eight launchers Arcadia knows: their tabs need more room than the header has.</summary>
    private static readonly IReadOnlyList<LauncherSpec> AllLaunchers = KnownLaunchers.All.Select(launcher => new LauncherSpec(launcher, 3)).ToList();

    [AvaloniaFact]
    [Trait("Testfall", "T127-5")]
    public async Task Few_launchers_the_strip_stands_in_the_middle_as_wide_as_needed_with_LB_and_RB_beside_it_and_no_dividers()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var header = Header.Of(arcadia);

        Assert.Equal((header.Logo.Right + header.Status.Left) / 2, header.Strip.Center.X, tolerance: 2);
        Assert.Equal(header.Scroller.Extent.Width, header.Scroller.Viewport.Width, tolerance: 1);
        Assert.False(header.FadesLeft || header.FadesRight, "Kein Ausblenden");
        Assert.Equal(12, header.Tabs.Left - header.Lb.Right, tolerance: 1);
        Assert.Equal(12, header.Rb.Left - header.Tabs.Right, tolerance: 1);
        Assert.False(header.ShowsDividers, "Keine Trennstriche an den Enden");
    }

    [AvaloniaFact]
    [Trait("Testfall", "T127-6")]
    [Trait("Testfall", "G-7")]
    public async Task Many_launchers_the_strip_reaches_logo_and_status_turns_with_LB_RB_and_the_wheel_and_fades_where_tabs_follow()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(AllLaunchers);
        var main = arcadia.Arcadia.Main;
        var header = Header.Of(arcadia);

        // 20 px, a divider, 20 px to logo and status.
        Assert.True(header.ShowsDividers, "Trennstriche zu Logo und Status");
        Assert.Equal(20, header.DividerLeft.Left - header.Logo.Right, tolerance: 1);
        Assert.Equal(20, header.Lb.Left - header.DividerLeft.Right, tolerance: 1);
        Assert.Equal(20, header.DividerRight.Left - header.Rb.Right, tolerance: 1);
        Assert.Equal(20, header.Status.Left - header.DividerRight.Right, tolerance: 1);

        // At the first tab the strip can only turn on: it fades on the right.
        Assert.Equal((false, true), (header.FadesLeft, header.FadesRight));

        // LB/RB: the selected tab comes to the middle as far as the ends allow, faded on both sides in between.
        arcadia.Press(UiAction.NextTab, times: 4);
        header = Header.Of(arcadia);
        Assert.Equal("EA app", main.SelectedTab?.Name);
        Assert.Equal(header.Scroller.Bounds.Width / 2, TabCentre(arcadia, header, main.SelectedTab!), tolerance: 2);
        Assert.Equal((true, true), (header.FadesLeft, header.FadesRight));
        arcadia.Press(UiAction.PreviousTab, times: 5);
        header = Header.Of(arcadia);
        Assert.Equal("Amazon Games", main.SelectedTab?.Name);
        Assert.Equal((true, false), (header.FadesLeft, header.FadesRight));

        // The wheel over the tabs turns the strip back to its start.
        arcadia.Wheel(arcadia.Named<ScrollViewer>("TabScroller"), notches: 10);
        header = Header.Of(arcadia);
        Assert.Equal(0, header.Scroller.Offset.X, tolerance: 1);
        Assert.Equal((false, true), (header.FadesLeft, header.FadesRight));

        // A tab cut off at the faded edge is clickable.
        var cutOff = main.Tabs.First(tab => arcadia.Bounds(arcadia.Showing(tab)).Right > header.Strip.Right - 12);
        arcadia.Click(arcadia.Showing(cutOff));
        Assert.Same(cutOff, main.SelectedTab);
    }

    private static double TabCentre(ArcadiaWindow arcadia, Header header, LauncherTabViewModel tab) =>
        arcadia.Bounds(arcadia.Showing(tab)).Center.X - arcadia.Bounds(header.ScrollerControl).Left;

    /// <summary>The header's parts in window coordinates.</summary>
    private sealed record Header(
        Rect Logo, Rect Status, Rect Strip, Rect Tabs, Rect Lb, Rect Rb, Rect DividerLeft, Rect DividerRight,
        ScrollViewer ScrollerControl, bool ShowsDividers, bool FadesLeft, bool FadesRight)
    {
        public ScrollViewer Scroller => ScrollerControl;

        public static Header Of(ArcadiaWindow arcadia)
        {
            arcadia.WaitForTransitions();
            var dividerLeft = arcadia.Named<Border>("TabDividerLeft");
            var dividerRight = arcadia.Named<Border>("TabDividerRight");
            var grid = (Grid)dividerLeft.Parent!;
            var logo = grid.Children.Single(child => Grid.GetColumn(child) == 0);
            var status = grid.Children.Single(child => Grid.GetColumn(child) == 4);
            var strip = (DockPanel)grid.Children.Single(child => Grid.GetColumn(child) == 2);
            var buttons = strip.Children.OfType<Button>().ToList();
            var scroller = arcadia.Named<ScrollViewer>("TabScroller");
            return new Header(
                arcadia.Bounds(logo),
                arcadia.Bounds(status),
                arcadia.Bounds(strip),
                arcadia.Bounds(scroller),
                arcadia.Bounds(buttons.Single(button => DockPanel.GetDock(button) == Dock.Left)),
                arcadia.Bounds(buttons.Single(button => DockPanel.GetDock(button) == Dock.Right)),
                arcadia.Bounds(dividerLeft),
                arcadia.Bounds(dividerRight),
                scroller,
                dividerLeft.Opacity > 0.99 && dividerRight.Opacity > 0.99,
                arcadia.Named<Border>("TabEdgeLeft").IsVisible,
                arcadia.Named<Border>("TabEdgeRight").IsVisible);
        }
    }
}
