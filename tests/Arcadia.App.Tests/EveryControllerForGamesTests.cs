using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Input;
using Arcadia.Pan;
using Arcadia.Tests.Pads;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using B = Arcadia.Input.SdlPadState.Button;

namespace Arcadia.App.Tests;

/// <summary>
/// Every controller passes to games through Arcadia (#238, part 1): Pan hides it and Arcadia plugs an Xbox 360 pad for
/// it. Pan, VIIPER and the controllers are faked; what a fake pad gets is what a game would see. Real pads, HidHide and
/// games are the manual part (docs/testplans/iteration-2/238-every-controller-through-arcadia.md).
/// </summary>
public sealed class EveryControllerForGamesTests
{
    private const ushort A = 0x1000;
    private const ushort Back = 0x0020;
    private const ushort Start = 0x0010;

    private static readonly LauncherSpec[] SteamAndGog = [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)];
    private static readonly PadDevice DualSense = KnownPads.DualSense with { Path = "hid-dualsense" };

    private static async Task<ArcadiaDriver> StartInBackgroundAsync(FakeSteam? steam = null, bool copy = false, params PadDevice[] pads)
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, steam ?? new FakeSteam());
        arcadia.SteamControllerCopy.CanCopy = copy;
        arcadia.SteamControllerCopy.PuckPresent = copy;
        foreach (var pad in pads)
        {
            arcadia.Sdl.Plug(pad);
        }

        arcadia.StartControllers();
        arcadia.IsArcadiaInFront = false;
        await arcadia.TickSteamControllerAsync();
        arcadia.PollControllers();
        return arcadia;
    }

    private static ControllerSettingsViewModel OpenControllerSettings(ArcadiaDriver arcadia)
    {
        arcadia.IsArcadiaInFront = true;
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        return Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
    }

    private static IReadOnlyList<string> RowsBelow(ControllerSettingsViewModel controller) =>
        [.. controller.Rows.Where(row => row.Group == PanRows.Group && row.Indent == 1).Select(row => row.Key)];

    [AvaloniaFact]
    [Trait("TestCase", "T238-1")]
    public async Task A_DualSense_and_an_Xbox_pad_are_hidden_and_each_gets_its_own_pad_without_a_Steam_Controller()
    {
        using var arcadia = await StartInBackgroundAsync(pads: [DualSense, KnownPads.EightBitDoUltimate2]);

        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
        Assert.Equal(["1 054C:0CE6", "1 2DC8:310B"], arcadia.Pan.ControllerHides);
        Assert.Equal(2, arcadia.XboxPads.Plugged);
        Assert.Equal(2, arcadia.ForGames.Others.Plugged);

        // Part 2 (D3): the DualSense as a DualSense, the 8BitDo in XInput mode as an Xbox 360 pad.
        Assert.Equal([PadKind.DualSense, PadKind.Xbox360], arcadia.XboxPads.All.Select(pad => pad.Kind));
    }

    /// <summary>#238 part 2 (D6, A5): touchpad, gyro and mute pass to the DualSense pad; its light bar comes back.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T238-5")]
    public async Task A_DualSense_pad_gets_touchpad_and_motion_and_its_light_bar_reaches_the_real_controller()
    {
        using var arcadia = await StartInBackgroundAsync(pads: DualSense);
        var dualSense = arcadia.Sdl.IdOf(DualSense);
        var pad = arcadia.XboxPads.Current!;
        arcadia.PollControllers();

        Assert.Contains(dualSense, arcadia.Sdl.MotionOn);

        arcadia.Sdl.Hold(dualSense, B.Misc1, B.Touchpad);
        arcadia.PollControllers();
        await pad.WaitForAsync(_ => pad.States is [.., { Extra: ExtraButtons.Misc1, Touchpad.Clicked: true, Motion.IsOn: true }]);

        pad.Light(0, 0, 255);
        arcadia.PollControllers();
        Assert.Contains($"pad {dualSense}: 0,0,255", arcadia.Sdl.Lights);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T238-3")]
    public async Task Back_and_Start_never_reach_the_game_from_a_DualSense_but_A_and_a_tap_of_View_do()
    {
        using var arcadia = await StartInBackgroundAsync(pads: DualSense);
        var dualSense = arcadia.Sdl.IdOf(DualSense);
        var pad = arcadia.XboxPads.Current!;

        arcadia.Sdl.Hold(dualSense, B.Back, B.Start);
        for (var i = 0; i < 50; i++)
        {
            arcadia.PollControllers();
        }

        arcadia.Sdl.Hold(dualSense);
        arcadia.PollControllers().PollControllers();
        arcadia.Sdl.Hold(dualSense, B.South);
        arcadia.PollControllers();
        await pad.WaitForAsync(state => state.Buttons == A);
        Assert.DoesNotContain(pad.Sent, state => (state.Buttons & (Back | Start)) != 0);

        arcadia.Sdl.Hold(dualSense);
        arcadia.PollControllers();
        arcadia.PressOn(dualSense, B.Back);
        await pad.WaitForAsync(state => state.Buttons == Back);
    }

    /// <summary>
    /// T238-3 "with each pad": Back + Start from the second pad reach neither its game pad nor the first one (QE review of
    /// #338). ⧉ held passes as held by the rule in docs/product/controls.md, so it isn't asserted here.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T238-3")]
    public async Task Back_and_Start_never_reach_the_game_from_the_second_pad_either()
    {
        using var arcadia = await StartInBackgroundAsync(pads: [DualSense, KnownPads.EightBitDoUltimate2]);
        var eightBitDo = arcadia.Sdl.IdOf(KnownPads.EightBitDoUltimate2);
        var pad = arcadia.XboxPads.All[1];

        arcadia.Sdl.Hold(eightBitDo, B.Back, B.Start);
        for (var i = 0; i < 50; i++)
        {
            arcadia.PollControllers();
        }

        arcadia.Sdl.Hold(eightBitDo);
        arcadia.PollControllers().PollControllers();
        arcadia.Sdl.Hold(eightBitDo, B.South);
        arcadia.PollControllers();
        await pad.WaitForAsync(state => state.Buttons == A);
        Assert.DoesNotContain(pad.Sent, state => (state.Buttons & (Back | Start)) != 0);
        Assert.All(arcadia.XboxPads.All[0].Sent, state => Assert.Equal(XboxPadState.Neutral, state));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T238-4")]
    public async Task With_Arcadia_in_front_or_the_mouse_mode_on_every_pad_rests()
    {
        using var arcadia = await StartInBackgroundAsync(pads: [DualSense, KnownPads.EightBitDoUltimate2]);
        var dualSense = arcadia.Sdl.IdOf(DualSense);
        var eightBitDo = arcadia.Sdl.IdOf(KnownPads.EightBitDoUltimate2);

        arcadia.IsArcadiaInFront = true;
        var first = arcadia.Main.Library.FocusedGame!.Game;
        arcadia.PressOn(dualSense, B.DPadRight);
        Assert.NotSame(first, arcadia.Main.Library.FocusedGame!.Game);

        arcadia.IsArcadiaInFront = false;
        arcadia.IsMouseModeOn = true;
        arcadia.Sdl.Hold(dualSense, B.South);
        arcadia.Sdl.Hold(eightBitDo, B.East);
        arcadia.PollControllers();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.All(arcadia.XboxPads.All, pad => Assert.All(pad.Sent, state => Assert.Equal(XboxPadState.Neutral, state)));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T238-6")]
    public async Task After_Controller_freigeben_a_DualSense_is_still_taken_over()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, new FakeSteam { IsSteamRunning = true });
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.Sdl.Plug(DualSense);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        Assert.Single(arcadia.Pan.Hidden);

        await arcadia.ForGames.ReleaseForFirmwareAsync();
        arcadia.IsArcadiaInFront = false;
        await arcadia.TickSteamControllerAsync();
        arcadia.PollControllers();
        await arcadia.TickSteamControllerAsync();

        Assert.Single(arcadia.Pan.Hidden);
        Assert.Contains("2 054C:0CE6", arcadia.Pan.ControllerHides);
        Assert.Equal(1, arcadia.ForGames.Others.Plugged);
        Assert.False(arcadia.XboxPad.IsPlugged, "kein Pad für den Steam Controller, Steam sieht ihn direkt");
        Assert.Equal(PanEmulationState.Running, arcadia.ForGames.Status.State);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T238-7")]
    public async Task A_controller_Pan_cant_hide_reaches_games_directly_and_one_row_lists_it_with_why()
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, new FakeSteam());
        using var _ = arcadia;
        arcadia.Pan.ControllerHide = model => model.Vendor == 0x2DC8
            ? new ControllerHidingResult(true, PanReason.ControllerNeedsReplug)
            : new ControllerHidingResult(false, PanReason.ControllerNotHideable);
        arcadia.Sdl.Plug(KnownPads.EightBitDoUltimate2);
        arcadia.Sdl.Plug(DualSense);
        arcadia.StartControllers();
        arcadia.IsArcadiaInFront = false;
        await arcadia.TickSteamControllerAsync();
        arcadia.PollControllers();

        Assert.Equal(0, arcadia.XboxPads.Plugged);
        var controller = OpenControllerSettings(arcadia);
        // The info row first, then "Controller zuordnen" (designs T238Settings).
        Assert.Equal(["pan:not-hidden", PanRows.LayoutsKey], RowsBelow(controller));
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(r => r.Key == "pan:not-hidden"));
        Assert.Equal("2 Controller lassen sich nicht verstecken", row.Title);
        Assert.Equal("Spiele sehen sie direkt, Arcadias Tastenkombinationen kommen dort mit an", row.Subtitle);
        Assert.Equal(HealthSeverity.Info, row.Severity);
        var affected = Assert.IsType<InfoSection>(row.Info.Single(block => block is InfoSection));
        Assert.Equal("BETROFFEN", affected.Title);
        Assert.Equal([KnownPads.EightBitDoUltimate2.Name, KnownPads.DualSense.Name], affected.Blocks.Cast<InfoCard>().Select(card => card.Title));
        Assert.Null(controller.Marker);

        // Pan off: nothing is emulated, and the row goes (the player chose that).
        arcadia.Settings.Update(settings => settings with { PanEnabled = false });
        await arcadia.TickSteamControllerAsync();
        Assert.Empty(RowsBelow(controller));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T238-12")]
    public async Task While_Xbox_Accessories_runs_the_Xbox_pad_waits_and_a_warning_says_why_until_it_is_closed()
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, new FakeSteam());
        using var _ = arcadia;
        arcadia.Pan.ControllerHide = _ => new ControllerHidingResult(false, PanReason.FirmwareToolRuns);
        arcadia.Sdl.Plug(KnownPads.XboxSeries);
        arcadia.StartControllers();
        arcadia.IsArcadiaInFront = false;
        await arcadia.TickSteamControllerAsync();

        var controller = OpenControllerSettings(arcadia);
        var row = Assert.IsType<ActionRowViewModel>(controller.Rows.Single(r => r.Key == "pan:firmware-tool:045E"));
        Assert.Equal("Xbox Zubehör ist geöffnet", row.Title);
        Assert.Equal("Arcadia übernimmt den Xbox Controller, sobald du es schließt", row.Subtitle);
        Assert.Equal(HealthSeverity.Warning, row.Severity);
        Assert.Equal(HealthSeverity.Warning, controller.Marker);
        Assert.Equal(0, arcadia.XboxPads.Plugged);

        arcadia.Pan.ControllerHide = null;
        arcadia.Wait(TakenOverControllers.FirmwareToolRetry);
        await arcadia.TickSteamControllerAsync();

        Assert.DoesNotContain(controller.Rows, r => r.Key == "pan:firmware-tool:045E");
        Assert.Equal(1, arcadia.XboxPads.Plugged);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T238-5")]
    public async Task A_games_rumble_on_the_pad_reaches_the_real_controller()
    {
        using var arcadia = await StartInBackgroundAsync(pads: DualSense);
        var dualSense = arcadia.Sdl.IdOf(DualSense);

        arcadia.XboxPads.Current!.Rumble(255, 0);
        arcadia.PollControllers();

        Assert.Contains($"pad {dualSense}: 65535/0 for 2000 ms", arcadia.Sdl.Rumbles);
    }

    /// <summary>QE review of #338: while Arcadia lets go of every controller for a hide, no poll feeds the Steam Controller's pad.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T238-4")]
    public async Task While_a_new_controller_is_hidden_the_Steam_Controllers_pad_rests_too()
    {
        using var arcadia = await StartInBackgroundAsync(pads: KnownPads.SteamController);
        var steamControllerPad = arcadia.XboxPads.Current!;
        arcadia.Sdl.Hold(arcadia.Sdl.IdOf(KnownPads.SteamController), B.North);
        arcadia.PollControllers();
        await steamControllerPad.WaitForAsync(state => state.Buttons == 0x8000);

        arcadia.Sdl.Plug(DualSense);
        arcadia.PollControllers();
        await arcadia.TickSteamControllerAsync();

        Assert.Contains("1 054C:0CE6", arcadia.Pan.ControllerHides);
        await steamControllerPad.WaitForAsync(state => state == XboxPadState.Neutral);
    }

    /// <summary>
    /// #315 for every controller: Steam sees a DualSense only through Arcadia's pad, so with Steam's desktop control
    /// allowed the pad passes in front of Arcadia and in the mouse mode, Back + Start included (Steam's action set switch).
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T238-4")]
    public async Task With_Steams_desktop_control_allowed_the_pads_pass_to_Steam_in_front_of_Arcadia_and_in_the_mouse_mode()
    {
        using var arcadia = await StartInBackgroundAsync(pads: DualSense);
        arcadia.Settings.Update(settings => settings with { IgnoreSteamDesktopLayout = false });
        var dualSense = arcadia.Sdl.IdOf(DualSense);
        var pad = arcadia.XboxPads.Current!;

        arcadia.IsArcadiaInFront = true;
        arcadia.Sdl.Hold(dualSense, B.South);
        arcadia.PollControllers();
        await pad.WaitForAsync(state => state.Buttons == A);

        arcadia.Sdl.Hold(dualSense);
        arcadia.PollControllers();
        arcadia.IsArcadiaInFront = false;
        arcadia.IsMouseModeOn = true;
        arcadia.Sdl.Hold(dualSense, B.Back, B.Start);
        for (var i = 0; i < 20; i++)
        {
            arcadia.PollControllers();
        }

        await pad.WaitForAsync(state => state.Buttons == (Back | Start));
    }

    private static readonly PadDevice GameCube = KnownPads.GameCube with { Path = "hid-gamecube" };

    /// <summary>The settings' focus moved down to the row with this key, as the player does with the D-pad.</summary>
    private static void FocusDownTo(ArcadiaDriver arcadia, ControllerSettingsViewModel controller, string key)
    {
        for (var i = 0; i < 20 && controller.FocusedRow?.Key != key; i++)
        {
            arcadia.Press(UiAction.Down);
        }

        Assert.Equal(key, controller.FocusedRow?.Key);
    }

    private static ControllerLayoutsViewModel OpenLayouts(ArcadiaDriver arcadia, ControllerSettingsViewModel controller)
    {
        FocusDownTo(arcadia, controller, PanRows.LayoutsKey);
        arcadia.Press(UiAction.Accept);
        return Assert.IsType<ControllerLayoutsViewModel>(controller.Dialog);
    }

    private static IReadOnlyList<string> Shown(ControllerLayoutsViewModel layouts) =>
        [.. layouts.Rows.Select(row => $"{row.Title}: {row.ValueText}{(row.IsLocked ? " (gesperrt)" : string.Empty)}")];

    /// <summary>
    /// #238 part 3 (D3a, scenario 7): with the gamepad alone the player makes the DualSense an Xbox 360 pad and the
    /// GameCube controller a Switch 2 Pro, then leaves it to games; each applies at the next tick, B closes, and the
    /// choices are kept after a restart. The Steam Controller's row can't be changed.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T238-13")]
    public async Task Controller_zuordnen_gives_each_model_its_layout_or_leaves_it_to_games_with_the_gamepad_alone_and_keeps_it()
    {
        using var arcadia = await StartInBackgroundAsync(pads: [DualSense, GameCube, KnownPads.SteamController]);
        // The first tick plugs the emulation's pads, then the Steam Controller's.
        var steamControllerPad = arcadia.XboxPads.All[^1];
        Assert.True(arcadia.ForGames.Pad.IsPlugged);
        Assert.Equal([PadKind.DualSense, PadKind.Xbox360], PluggedKinds(arcadia, steamControllerPad));
        var controller = OpenControllerSettings(arcadia);

        FocusDownTo(arcadia, controller, PanRows.LayoutsKey);
        var info = Assert.Single(controller.FocusedRow!.Info.OfType<InfoFacts>());
        Assert.Equal("SPIELE SEHEN", info.Title);
        Assert.Equal(
            ["Steam Controller (Modus „Arcadia“) → Xbox 360 Controller", "DualSense Wireless Controller → DualSense", "Nintendo GameCube Controller → Xbox 360 Controller"],
            info.Facts.Select(fact => $"{fact.Label} → {fact.Value}"));

        var layouts = OpenLayouts(arcadia, controller);
        Assert.Equal(
            ["Steam Controller (Modus „Arcadia“): Xbox 360 Controller (gesperrt)", "DualSense Wireless Controller: DualSense", "Nintendo GameCube Controller: Xbox 360 Controller"],
            Shown(layouts));
        Assert.Equal(["Vorgabe: Xbox 360 Controller", "Vorgabe: DualSense", "Vorgabe: Xbox 360 Controller"], layouts.Rows.Select(row => row.Subtitle));

        // The Steam Controller takes the focus, but ◀ ▶ do nothing there; its note says why.
        arcadia.Press(UiAction.Right).Press(UiAction.Left);
        Assert.True(layouts.Rows[0].ShowsLockNote);
        Assert.False(layouts.CanChange);
        Assert.Equal("Xbox 360 Controller", layouts.Rows[0].ValueText);

        // DualSense ◀ ◀: DualShock 4, then Xbox 360 Controller.
        arcadia.Press(UiAction.Down).Press(UiAction.Left, times: 2);
        Assert.True(layouts.CanChange);
        Assert.Equal("Xbox 360 Controller", layouts.Rows[1].ValueText);
        await arcadia.TickSteamControllerAsync();
        Assert.Equal([PadKind.Xbox360, PadKind.Xbox360], PluggedKinds(arcadia, steamControllerPad));

        // GameCube ▶ ▶ ▶: DualShock 4, DualSense, Switch 2 Pro Controller.
        arcadia.Press(UiAction.Down).Press(UiAction.Right, times: 3);
        Assert.Equal("Switch 2 Pro Controller", layouts.Rows[2].ValueText);
        await arcadia.TickSteamControllerAsync();
        Assert.Equal([PadKind.Xbox360, PadKind.Switch2Pro], PluggedKinds(arcadia, steamControllerPad));

        // ▶ once more: "Nicht übernehmen". Its pad goes, then Pan shows the real controller to games.
        arcadia.Press(UiAction.Right);
        Assert.Equal("Nicht übernehmen", layouts.Rows[2].ValueText);
        await arcadia.TickSteamControllerAsync();
        Assert.Equal([PadKind.Xbox360], PluggedKinds(arcadia, steamControllerPad));
        Assert.Contains("1 057E:0337", arcadia.Pan.ControllerShows);
        Assert.False(steamControllerPad.IsUnplugged);

        // B closes the modal, not the page; the focus stays on the row, whose panel says what games see now.
        arcadia.Press(UiAction.Back);
        Assert.Null(controller.Dialog);
        Assert.Same(controller, arcadia.Main.Settings?.Selected);
        Assert.Equal(PanRows.LayoutsKey, controller.FocusedRow?.Key);
        Assert.Contains(controller.FocusedRow!.Info.OfType<InfoFacts>().Single().Facts,
            fact => fact is { Label: "Nintendo GameCube Controller", Value: "den echten Controller (nicht übernommen)" });

        using var restarted = await arcadia.RestartAsync();
        restarted.Sdl.Plug(DualSense);
        restarted.Sdl.Plug(GameCube);
        restarted.StartControllers();
        restarted.IsArcadiaInFront = false;
        await restarted.TickSteamControllerAsync();
        restarted.PollControllers();
        await restarted.TickSteamControllerAsync();
        Assert.Equal([PadKind.Xbox360], PluggedKinds(restarted, null));
        Assert.DoesNotContain(restarted.Pan.ControllerHides, hide => hide.EndsWith("057E:0337", StringComparison.Ordinal));
    }

    /// <summary>The pads plugged now for the other controllers, in the order they were plugged.</summary>
    private static IReadOnlyList<PadKind> PluggedKinds(ArcadiaDriver arcadia, FakeVirtualXboxPads.FakePad? steamControllerPad) =>
        [.. arcadia.XboxPads.All.Where(pad => !pad.IsUnplugged && pad != steamControllerPad).Select(pad => pad.Kind)];

    /// <summary>D3a: the row shows only while Pan is on and a controller is connected; the modal closes when it goes.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T238-13")]
    public async Task Controller_zuordnen_shows_only_while_a_controller_is_connected_and_closes_with_the_last_one()
    {
        using var arcadia = await StartInBackgroundAsync();
        var controller = OpenControllerSettings(arcadia);
        Assert.DoesNotContain(PanRows.LayoutsKey, RowsBelow(controller));

        arcadia.Sdl.Plug(GameCube);
        arcadia.PollControllers();
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(PanRows.LayoutsKey, RowsBelow(controller));
        var layouts = OpenLayouts(arcadia, controller);
        Assert.Equal(["Nintendo GameCube Controller: Xbox 360 Controller"], Shown(layouts));

        // Two DualSense controllers are one row: the layout is the model's (D3b).
        var firstDualSense = arcadia.Sdl.Plug(DualSense);
        var secondDualSense = arcadia.Sdl.Plug(DualSense);
        arcadia.PollControllers();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["Nintendo GameCube Controller: Xbox 360 Controller", "DualSense Wireless Controller: DualSense"], Shown(layouts));

        // One of them goes: the row stays for the other.
        arcadia.Sdl.Unplug(firstDualSense);
        arcadia.Time.Advance(TakenOverControllers.RereadGrace);
        arcadia.PollControllers();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["Nintendo GameCube Controller: Xbox 360 Controller", "DualSense Wireless Controller: DualSense"], Shown(layouts));

        arcadia.Sdl.Unplug(arcadia.Sdl.IdOf(GameCube));
        arcadia.Sdl.Unplug(secondDualSense);
        arcadia.Time.Advance(TakenOverControllers.RereadGrace);
        arcadia.PollControllers();
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain(PanRows.LayoutsKey, RowsBelow(controller));
        Assert.Null(controller.Dialog);
    }
}
