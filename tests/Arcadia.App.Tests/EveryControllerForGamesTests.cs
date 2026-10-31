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
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;
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
    public async Task After_Nein_to_the_Steam_Controller_a_DualSense_is_still_taken_over()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"] }, new FakeSteam { IsSteamRunning = true });
        arcadia.SteamControllerCopy.CanCopy = true;
        arcadia.SteamControllerCopy.PuckPresent = true;
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.Sdl.Plug(DualSense);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        await arcadia.WaitUntilAsync(() => arcadia.Main.TakeoverConsent is not null, "die Frage „Steam Controller und Puck übernehmen?“");

        // "Nein, nicht übernehmen" is focused: held 1 s.
        arcadia.Hold(UiAction.Accept, TimeSpan.FromMilliseconds(1100));
        Assert.Null(arcadia.Main.TakeoverConsent);
        arcadia.IsArcadiaInFront = false;
        await arcadia.TickSteamControllerAsync();
        arcadia.PollControllers();
        await arcadia.TickSteamControllerAsync();

        Assert.Equal(TakeoverState.Declined, arcadia.ForGames.Takeover.State);
        Assert.Empty(arcadia.Pan.Hidden);
        Assert.Contains("1 054C:0CE6", arcadia.Pan.ControllerHides);
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
        Assert.Equal(["pan:not-hidden"], RowsBelow(controller));
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
}
