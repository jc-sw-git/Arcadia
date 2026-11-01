using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers;
using Arcadia.Tests.Sdl;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// "Controller zuordnen" drawn (#238 D3a, designs T238Modal): the focus ring around the first and the last row is whole,
/// many models scroll with the focus and fade where more follow, the ✕ shows with the mouse.
/// </summary>
public sealed class ControllerLayoutsViewTests
{
    private static readonly PadDevice[] Many =
    [
        KnownPads.SteamController,
        KnownPads.DualSense with { Path = "hid-dualsense" },
        KnownPads.GameCube with { Path = "hid-gamecube" },
        KnownPads.XboxSeries with { Path = "XInput#2" },
        KnownPads.Switch2Pro with { Path = "hid-switch2" },
        KnownPads.EightBitDoUltimate2,
        KnownPads.DualShock4 with { Path = "hid-ds4" },
        KnownPads.SwitchPro with { Path = "hid-switchpro" },
    ];

    private static async Task<(ArcadiaWindow Window, ControllerLayoutsViewModel Layouts)> OpenAsync(params PadDevice[] pads)
    {
        var arcadia = await ArcadiaWindow.ShowAsync(
            [new LauncherSpec(KnownLaunchers.Steam, 3), new LauncherSpec(KnownLaunchers.Gog, 3)],
            new AppSettings { ActiveLaunchers = ["steam", "gog"] }, steam: new FakeSteam());
        foreach (var pad in pads)
        {
            arcadia.Arcadia.Sdl.Plug(pad);
        }

        arcadia.Arcadia.StartControllers();
        arcadia.Arcadia.IsArcadiaInFront = false;
        await arcadia.Arcadia.TickSteamControllerAsync();
        arcadia.Arcadia.PollControllers();
        arcadia.Arcadia.IsArcadiaInFront = true;
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Arcadia.Main.Settings!.Selected);
        for (var i = 0; i < 20 && controller.FocusedRow?.Key != PanRows.LayoutsKey; i++)
        {
            arcadia.Press(UiAction.Down);
        }

        Assert.True(controller.FocusedRow?.Key == PanRows.LayoutsKey, string.Join(", ", controller.Rows.Select(row => row.Key)));
        arcadia.Press(UiAction.Accept);
        arcadia.Settle();
        return (arcadia, Assert.IsType<ControllerLayoutsViewModel>(controller.Dialog));
    }

    private static Button RowButton(ArcadiaWindow arcadia, ControllerLayoutRowViewModel row) => Assert.Single(
        arcadia.Window.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("settingRow") && button.DataContext == row && button.IsEffectivelyVisible);

    private static Border Fade(ArcadiaWindow arcadia, string name) =>
        Assert.Single(arcadia.Window.GetVisualDescendants().OfType<Border>(), border => border.Name == name && border.IsEffectivelyVisible);

    /// <summary>Set ARCADIA_CAPTURES to a folder to look at the drawn states (not needed for the test).</summary>
    private static void Keep(ArcadiaWindow arcadia, string name)
    {
        if (Environment.GetEnvironmentVariable("ARCADIA_CAPTURES") is { Length: > 0 } folder)
        {
            using var frame = arcadia.Capture();
            frame.Save(Path.Combine(folder, $"{name}.png"));
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T238-13")]
    public async Task Three_models_show_whole_rings_on_the_locked_first_and_the_last_row_and_no_fade()
    {
        var (arcadia, layouts) = await OpenAsync(Many[..3]);
        using var _ = arcadia;
        Assert.Equal(3, layouts.Rows.Count);

        arcadia.AssertRingAround(RowButton(arcadia, layouts.Rows[0]), "Steam Controller, gesperrt");
        Keep(arcadia, "layouts-steam-controller");
        arcadia.Press(UiAction.Down, times: 2);
        arcadia.AssertRingAround(RowButton(arcadia, layouts.Rows[2]), "letzte Zeile");
        Keep(arcadia, "layouts-last");
        Assert.DoesNotContain("shown", Fade(arcadia, "FadeTop").Classes);
        Assert.DoesNotContain("shown", Fade(arcadia, "FadeBottom").Classes);
        RingGeometry.AssertNoRingCutOff(arcadia.Window, () => arcadia.Settle().WaitForTransitions(), "Controller zuordnen");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T238-13")]
    public async Task Many_models_scroll_with_the_focus_and_fade_where_more_follow()
    {
        var (arcadia, layouts) = await OpenAsync(Many);
        using var _ = arcadia;
        Assert.Equal(Many.Length, layouts.Rows.Count);
        Assert.DoesNotContain("shown", Fade(arcadia, "FadeTop").Classes);
        Assert.Contains("shown", Fade(arcadia, "FadeBottom").Classes);
        Keep(arcadia, "layouts-many-top");

        arcadia.Press(UiAction.Down, times: Many.Length - 1);
        arcadia.Settle();
        arcadia.AssertRingAround(RowButton(arcadia, layouts.Rows[^1]), "letzte von vielen Zeilen");
        Assert.Contains("shown", Fade(arcadia, "FadeTop").Classes);
        Assert.DoesNotContain("shown", Fade(arcadia, "FadeBottom").Classes);
        Keep(arcadia, "layouts-many-end");
        RingGeometry.AssertNoRingCutOff(arcadia.Window, () => arcadia.Settle().WaitForTransitions(), "Controller zuordnen, viele");

        // Below the last row: [Übernehmen], the only way to apply.
        arcadia.Press(UiAction.Down);
        arcadia.Settle();
        Assert.True(layouts.IsApplyFocused);
        arcadia.AssertRingAround(Assert.Single(arcadia.Window.GetVisualDescendants().OfType<Button>(), button => button.Name == "ApplyButton"), "Übernehmen");
        Keep(arcadia, "layouts-apply");
    }
}
