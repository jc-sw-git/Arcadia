using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The value chip and the value boxes of the detail page follow the value in use (#506, scenarios 2 to 4): the arrows,
/// a click, the library's context menu, and a Steam Controller coming and going.
/// </summary>
public sealed class ValueBoxFollowTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly LauncherSpec[] SteamAndGog = [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)];

    [AvaloniaFact]
    [Trait("TestCase", "T506-12")]
    public async Task The_arrows_and_a_click_beside_the_chip_change_the_value_going_round_and_the_rail_follows()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        var row = arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.ControllerMode);
        var values = row.ValueBoxes.Select(box => box.Title).ToList();
        var at = values.IndexOf(row.ValueText);

        // ▶ three times goes round to where it started; each step shows the next value at once.
        for (var step = 1; step <= values.Count; step++)
        {
            arcadia.Press(UiAction.Right);
            Assert.Equal(values[(at + step) % values.Count], row.ValueText);
            Assert.Contains(row.ValueText, page.OpenArea.ValueLine, StringComparison.Ordinal);
        }

        arcadia.Press(UiAction.Left);
        Assert.Equal(values[(at + values.Count - 1) % values.Count], row.ValueText);

        // A click on the arrows beside the chip does the same.
        arcadia.Click(row.StepOnCommand);
        Assert.Equal(values[at], row.ValueText);
        AssertCurrent(row);
        arcadia.Click(row.StepBackCommand);
        Assert.Equal(values[(at + values.Count - 1) % values.Count], row.ValueText);
        AssertCurrent(row);
        Assert.Contains(row.ValueText, page.OpenArea.ValueLine, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-18")]
    public async Task A_mode_set_in_the_librarys_context_menu_is_highlighted_when_the_detail_page_opens()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Press(UiAction.Options);
        var menu = arcadia.Main.GameMenu!;
        menu.Actions.Single(action => action.Kind == GameActionKind.ControllerMode).Invoke();
        var chosen = arcadia.Settings.Current.ControllerSupportOverrides;
        Assert.Single(chosen);
        if (arcadia.Main.GameMenu is not null)
        {
            arcadia.Press(UiAction.Back);
        }

        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var row = arcadia.Detail!.Current.Row(GameActionKind.ControllerMode);
        Assert.NotEqual(row.ValueBoxes[0].Title, row.ValueText);
        AssertCurrent(row);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-25")]
    public async Task The_reason_of_a_mode_row_without_a_Steam_Controller_comes_and_goes_with_the_controller_while_the_focus_stays()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"], PanEnabled = true });
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var row = arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.ControllerMode);
        Assert.True(row.HasNote);
        Assert.Equal(AppTexts.DetailModeNoSteamController, row.Note);

        var pad = arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        var page = arcadia.Detail!.Current;
        Assert.Equal(GameActionKind.ControllerMode, page.FocusedRow?.Kind);
        Assert.False(page.FocusedRow!.IsGreyed);
        Assert.False(page.InfoRow!.HasNote, page.InfoRow.Note);

        arcadia.Sdl.Unplug(pad);
        for (var tick = 0; tick < 20 && !arcadia.Detail!.Current.Row(GameActionKind.ControllerMode).IsGreyed; tick++)
        {
            arcadia.PollControllers().Wait(TimeSpan.FromSeconds(1));
            await arcadia.TickSteamControllerAsync();
        }

        page = arcadia.Detail!.Current;
        Assert.Equal(GameActionKind.ControllerMode, page.FocusedRow?.Kind);
        Assert.True(page.FocusedRow!.IsGreyed, "ohne Steam Controller wieder ausgegraut");
        Assert.Equal(AppTexts.DetailModeNoSteamController, page.InfoRow!.Note);
    }

    private static async Task<ArcadiaDriver> StartWithSteamControllerAsync()
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"], PanEnabled = true });
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        return arcadia;
    }

    /// <summary>Exactly the box named as the chip's value is highlighted.</summary>
    private static void AssertCurrent(DetailRowViewModel row) =>
        Assert.Equal(row.ValueText, Assert.Single(row.ValueBoxes, box => box.IsCurrent).Title);
}
