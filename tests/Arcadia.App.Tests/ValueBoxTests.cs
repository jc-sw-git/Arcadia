using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// One box per value in the info box (#506, scenario 3): on the detail page's value rows and on every settings row with
/// named values, the current value's box highlighted, following ◀ ▶ at once; a number range has none.
/// </summary>
public sealed class ValueBoxTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly LauncherSpec[] SteamAndGog = [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)];

    [AvaloniaFact]
    [Trait("TestCase", "T506-14")]
    [Trait("TestCase", "T506-15")]
    public async Task The_detail_pages_value_rows_show_a_box_per_value_and_the_highlight_follows_the_arrows()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;

        var mode = arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.ControllerMode);
        Assert.Same(mode, page.InfoRow);
        Assert.Equal(AppTexts.ControllerModeDefault(page.Tile.LauncherName == "Steam" ? ControllerModes.Steam : ControllerModes.Arcadia), mode.ValueBoxes[0].Title);
        Assert.Equal([ControllerModes.Arcadia, ControllerModes.Steam], Titles(mode.ValueBoxes)[1..]);
        Assert.Equal(
            [AppTexts.DetailControllerModeDefaultInfo, AppTexts.DetailControllerModeArcadiaInfo, AppTexts.DetailControllerModeSteamInfo],
            mode.ValueBoxes.Select(box => box.Text));
        Assert.Equal(AppTexts.DetailControllerModeInfo, mode.InfoText);
        AssertCurrent(mode.ValueBoxes, mode.ValueText);

        // ▶ and ◀ both ways and round at the ends: the highlight moves with the chip in the same step.
        foreach (var action in new[] { UiAction.Right, UiAction.Right, UiAction.Right, UiAction.Left, UiAction.Left })
        {
            arcadia.Press(action);
            AssertCurrent(mode.ValueBoxes, mode.ValueText);
        }

        var mouse = page.Row(GameActionKind.MouseMode);
        Assert.Equal([AppTexts.MouseModeDefaultAllowed, GameMouseModes.Allowed, GameMouseModes.Blocked], Titles(mouse.ValueBoxes));
        Assert.False(mouse.HasInfoText);
        AssertCurrent(mouse.ValueBoxes, mouse.ValueText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-19")]
    [Trait("TestCase", "T506-24")]
    public async Task A_greyed_value_row_keeps_its_boxes_and_its_reason_and_the_arrows_move_nothing()
    {
        // Without "Individuell" in the mouse mode settings the game's own mouse mode doesn't apply.
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);

        var mouse = arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.MouseMode);
        Assert.True(mouse.IsGreyed);
        Assert.Equal(AppTexts.DetailMouseModeNotPerGame, mouse.Note);
        Assert.Equal(3, mouse.ValueBoxes.Count);
        var current = Assert.Single(mouse.ValueBoxes, box => box.IsCurrent);

        arcadia.Press(UiAction.Right).Press(UiAction.Left).Press(UiAction.Right);
        Assert.Same(current, Assert.Single(mouse.ValueBoxes, box => box.IsCurrent));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-16")]
    [Trait("TestCase", "T506-17")]
    public async Task Every_settings_row_with_named_values_has_a_box_per_value_and_number_ranges_have_none()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        Assert.Equal(AppTexts.ControllerCategory, arcadia.Main.Settings!.Selected.Name);

        var mode = arcadia.FocusRow("mouse-mode");
        Assert.Equal([AppTexts.Off, AppTexts.MouseModeManual, AppTexts.MouseModeAutomatic], Titles(mode.ValueBoxes));
        Assert.Equal([AppTexts.MouseModeActivationOff, AppTexts.MouseModeActivationManual, AppTexts.MouseModeActivationAutomatic], mode.ValueBoxes.Select(box => box.Text));
        Assert.DoesNotContain(mode.Info, block => block is InfoText { Text: var text } && mode.ValueBoxes.Any(box => box.Text == text));
        AssertCurrent(mode.ValueBoxes, AppTexts.MouseModeManual);
        arcadia.Press(UiAction.Right);
        AssertCurrent(mode.ValueBoxes, AppTexts.MouseModeAutomatic);
        arcadia.Press(UiAction.Right);
        AssertCurrent(mode.ValueBoxes, AppTexts.Off);
        arcadia.Press(UiAction.Right);
        AssertCurrent(mode.ValueBoxes, AppTexts.MouseModeManual);

        // A switch: "Aus", then "An", as ◀ ▶ go through them.
        var dialogs = arcadia.FocusRow("mouse-mode-for-dialogs");
        Assert.Equal([AppTexts.Off, AppTexts.On], Titles(dialogs.ValueBoxes));
        var before = Assert.Single(dialogs.ValueBoxes, box => box.IsCurrent).Title;
        arcadia.Press(UiAction.Right);
        Assert.NotEqual(before, Assert.Single(dialogs.ValueBoxes, box => box.IsCurrent).Title);

        var inGames = arcadia.FocusRow("mouse-mode-in-games");
        Assert.Equal([AppTexts.MouseModeInGamesAllow, AppTexts.MouseModeInGamesEnd, AppTexts.MouseModeInGamesPerGame], inGames.ValueBoxes.Select(box => box.Text));
        Assert.Equal(2, arcadia.FocusRow("mouse-mode-per-game-default").ValueBoxes.Count);
        Assert.Equal(2, arcadia.FocusRow("mouse-mode-end-in-front").ValueBoxes.Count);
        Assert.Equal(2, arcadia.FocusRow("return-with-view").ValueBoxes.Count);

        // Values that differ only in size have no boxes.
        foreach (var key in new[] { "mouse-mode-hold", "mouse-mode-speed", "return-with-view-hold" })
        {
            Assert.False(arcadia.FocusRow(key).HasValueBoxes, $"{key} hat Wertboxen");
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-16")]
    [Trait("TestCase", "T506-20")]
    public async Task Pans_switch_has_a_box_for_off_and_on_while_it_is_a_switch()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);

        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        var pan = Assert.IsType<PanRowViewModel>(controller.Rows.Single(row => row.Key == PanRows.MainKey));
        if (!pan.IsSwitch)
        {
            // Drivers missing: the row installs, an action without values.
            Assert.False(pan.HasValueBoxes);
            return;
        }

        Assert.Equal([AppTexts.Off, AppTexts.On], Titles(pan.ValueBoxes));
        Assert.Equal([AppTexts.PanOffInfo, AppTexts.PanOnInfo], pan.ValueBoxes.Select(box => box.Text));
        AssertCurrent(pan.ValueBoxes, pan.ValueText);
    }

    private static async Task<ArcadiaDriver> StartWithSteamControllerAsync()
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"], PanEnabled = true });
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        return arcadia;
    }

    private static List<string> Titles(IReadOnlyList<ValueBox> boxes) => [.. boxes.Select(box => box.Title)];

    /// <summary>Exactly the box named as the value is highlighted.</summary>
    private static void AssertCurrent(IReadOnlyList<ValueBox> boxes, string value) =>
        Assert.Equal(value, Assert.Single(boxes, box => box.IsCurrent).Title);
}
