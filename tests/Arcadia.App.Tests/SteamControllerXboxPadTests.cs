using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Arcadia.Pan;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;
using B = Arcadia.Input.SdlPadState.Button;

namespace Arcadia.App.Tests;

/// <summary>
/// The Steam Controller as an Xbox 360 pad for games (#213, part A): VIIPER's pad is faked; what it gets is what a game
/// would see. The real pad, VIIPER and games are the manual part (T213-1, T213-5).
/// </summary>
public sealed class SteamControllerXboxPadTests
{
    private const ushort A = 0x1000;
    private const ushort Back = 0x0020;
    private const ushort Start = 0x0010;

    private static async Task<(ArcadiaDriver Arcadia, uint SteamController)> StartInBackgroundAsync()
    {
        var arcadia = await ArcadiaDriver.StartAsync();
        var steamController = arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.PlayGameOnArcadiaInFrontAsync();
        return (arcadia, steamController);
    }

    /// <summary>T213-1 (what reaches the game); the game seeing exactly one Xbox controller is manual.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-1")]
    public async Task With_a_game_in_front_the_Steam_Controller_s_presses_reach_the_Xbox_pad()
    {
        var (arcadia, steamController) = await StartInBackgroundAsync();
        using var _ = arcadia;
        Assert.Equal(1, arcadia.XboxPads.Plugged);

        arcadia.Sdl.Hold(steamController, B.South);
        arcadia.PollControllers();
        await arcadia.XboxPads.Current!.WaitForAsync(state => state.Buttons == A);
        arcadia.Sdl.Hold(steamController);
        arcadia.PollControllers();
        await arcadia.XboxPads.Current.WaitForAsync(state => state.Buttons == 0);
    }

    /// <summary>T213-2: Back + Start (the mouse mode) never reaches the game; a tap of View does.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-2")]
    public async Task Back_and_Start_never_reach_the_game_but_a_tap_of_View_does()
    {
        var (arcadia, steamController) = await StartInBackgroundAsync();
        using var _ = arcadia;

        arcadia.Sdl.Hold(steamController, B.Back, B.Start);
        for (var i = 0; i < 50; i++)
        {
            arcadia.PollControllers();
        }

        arcadia.Sdl.Hold(steamController, B.Back);
        for (var i = 0; i < 150; i++)
        {
            arcadia.PollControllers();
        }

        arcadia.Sdl.Hold(steamController);
        arcadia.PollControllers().PollControllers();
        arcadia.Sdl.Hold(steamController, B.South);
        arcadia.PollControllers();
        var pad = arcadia.XboxPads.Current!;
        await pad.WaitForAsync(state => state.Buttons == A);
        Assert.DoesNotContain(pad.Sent, state => (state.Buttons & (Back | Start)) != 0);

        arcadia.Sdl.Hold(steamController);
        arcadia.PollControllers();
        arcadia.PressOn(steamController, B.Back);
        await pad.WaitForAsync(state => state.Buttons == Back);
        for (var i = 0; i < 10; i++)
        {
            arcadia.PollControllers();
        }

        await pad.WaitForAsync(state => state.Buttons == 0);
    }

    /// <summary>T213-3: with Arcadia in front or the mouse mode on, the Steam Controller acts in Arcadia, not in the game.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-3")]
    public async Task With_Arcadia_in_front_or_the_mouse_mode_on_the_game_gets_nothing()
    {
        var (arcadia, steamController) = await StartInBackgroundAsync();
        using var _ = arcadia;
        var pad = arcadia.XboxPads.Current!;

        arcadia.IsArcadiaInFront = true;
        var first = arcadia.Main.Library.FocusedGame!.Game;
        arcadia.PressOn(steamController, B.DPadRight);
        Assert.NotSame(first, arcadia.Main.Library.FocusedGame!.Game);

        arcadia.IsArcadiaInFront = false;
        arcadia.IsMouseModeOn = true;
        arcadia.Sdl.Hold(steamController, B.South);
        arcadia.PollControllers();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.All(pad.Sent, state => Assert.Equal(XboxPadState.Neutral, state));
    }

    /// <summary>
    /// T213-4, superseded by #238 D1: an Xbox pad beside the Steam Controller gets a pad of its own; neither reaches the
    /// other's pad.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-4")]
    public async Task Another_controller_gets_its_own_pad_and_never_reaches_the_Steam_Controllers()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Sdl.Plug(KnownPads.XboxSeries);
        arcadia.StartControllers();
        await arcadia.PlayGameOnArcadiaInFrontAsync();
        Assert.Equal(1, arcadia.XboxPads.Plugged);
        var xboxPad = arcadia.XboxPads.Current!;

        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.PollControllers();
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(2, arcadia.XboxPads.Plugged);
        var steamControllerPad = arcadia.XboxPads.Current!;
        arcadia.Sdl.Hold(arcadia.Sdl.IdOf(KnownPads.XboxSeries), B.South);
        arcadia.PollControllers();
        arcadia.Sdl.Hold(arcadia.Sdl.IdOf(KnownPads.SteamController), B.North);
        arcadia.PollControllers();

        await steamControllerPad.WaitForAsync(state => state.Buttons == 0x8000);
        await xboxPad.WaitForAsync(state => state.Buttons == A);
        Assert.DoesNotContain(steamControllerPad.Sent, state => (state.Buttons & A) != 0);
        Assert.DoesNotContain(xboxPad.Sent, state => (state.Buttons & 0x8000) != 0);
    }

    /// <summary>T213-5 (automated part): the pad goes with the Steam Controller and comes back with it.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-5")]
    public async Task The_pad_goes_and_comes_with_the_Steam_Controller()
    {
        var (arcadia, steamController) = await StartInBackgroundAsync();
        using var _ = arcadia;

        arcadia.Sdl.Unplug(steamController);
        arcadia.PollControllers();
        await arcadia.TickSteamControllerAsync();
        Assert.False(arcadia.XboxPad.IsPlugged);

        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.PollControllers();
        await arcadia.TickSteamControllerAsync();
        Assert.True(arcadia.XboxPad.IsPlugged);
        Assert.Equal(2, arcadia.XboxPads.Plugged);
    }

    /// <summary>T213-6 (automated part): VIIPER lost is plugged in again; lost too often, a toast says so and Arcadia stops trying.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-6")]
    public async Task A_lost_VIIPER_comes_back_and_too_many_losses_show_a_notice()
    {
        var (arcadia, _) = await StartInBackgroundAsync();
        using var __ = arcadia;

        for (var i = 0; i <= SteamControllerXboxPad.MaxReplugsPerWindow; i++)
        {
            arcadia.XboxPads.Current!.Lose();
            await arcadia.TickSteamControllerAsync();
        }

        Assert.Equal(SteamControllerXboxPad.MaxReplugsPerWindow + 1, arcadia.XboxPads.Plugged);
        // A game is in front: the overlay's notice, not a toast (#243 B2).
        await arcadia.WaitUntilAsync(() => arcadia.OverlayNotices.Any(notice => notice.Contains("VIIPER fiel")), "the notice about VIIPER");
        Assert.False(arcadia.XboxPad.IsPlugged);
    }

    /// <summary>T243-39 (automated part): Pan taking the pad away, e.g. after the PC woke, never makes Arcadia give up.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T243-39")]
    public async Task Pan_taking_the_pad_away_again_and_again_never_makes_Arcadia_give_up()
    {
        var (arcadia, _) = await StartInBackgroundAsync();
        using var __ = arcadia;

        for (var i = 0; i <= SteamControllerXboxPad.MaxReplugsPerWindow + 1; i++)
        {
            arcadia.Pan.TearDown(PanReason.Hang);
            await arcadia.TickSteamControllerAsync();
            Assert.True(arcadia.XboxPad.IsPlugged);
        }

        Assert.DoesNotContain(arcadia.OverlayNotices, notice => notice.Contains("VIIPER fiel"));
    }

    /// <summary>T243-40 (automated part): after Arcadia gave up on VIIPER, "Controller wiederherstellen" brings the pad back without a restart.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T243-40")]
    public async Task Restoring_the_controllers_brings_the_pad_back_after_Arcadia_gave_up()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(steam: new FakeSteam());
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        arcadia.IsArcadiaInFront = false;
        await arcadia.TickSteamControllerAsync();
        for (var i = 0; i <= SteamControllerXboxPad.MaxReplugsPerWindow; i++)
        {
            arcadia.XboxPads.Current!.Lose();
            await arcadia.TickSteamControllerAsync();
        }

        await arcadia.WaitUntilAsync(() => arcadia.OverlayNotices.Any(notice => notice.Contains("Controller wiederherstellen")), "the notice about VIIPER");
        Assert.False(arcadia.XboxPad.IsPlugged);

        arcadia.IsArcadiaInFront = true;
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var controller = Assert.IsType<ControllerSettingsViewModel>(arcadia.Main.Settings!.Selected);
        await arcadia.WaitUntilAsync(() => controller.Rows.Any(row => row.Key == "pan-restore"), "the row „Controller wiederherstellen“");
        var restored = false;
        arcadia.ControllerSupport.ControllersRestored += (_, _) => restored = true;
        Assert.IsType<ActionRowViewModel>(controller.Rows.Single(row => row.Key == "pan-restore")).Activate();
        await arcadia.WaitUntilAsync(() => restored, "the restore");

        await arcadia.TickSteamControllerAsync();
        Assert.True(arcadia.XboxPad.IsPlugged);
    }

    /// <summary>T213-2, owner 2026-10-26: View held alone reaches the game as held, also while it brings Arcadia back.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-2")]
    public async Task View_held_alone_reaches_the_game_as_held()
    {
        var (arcadia, steamController) = await StartInBackgroundAsync();
        using var _ = arcadia;

        arcadia.Sdl.Hold(steamController, B.Back);
        for (var i = 0; i < 40; i++)
        {
            arcadia.PollControllers();
        }

        await arcadia.XboxPads.Current!.WaitForAsync(state => state.Buttons == Back);
    }

    /// <summary>T213-2 with the mouse mode "Aus": View and Menu are the game's at once.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-2")]
    public async Task With_the_mouse_mode_off_Back_and_Start_reach_the_game_at_once()
    {
        var (arcadia, steamController) = await StartInBackgroundAsync();
        using var _ = arcadia;
        arcadia.Settings.Update(settings => settings with
        {
            MouseMode = settings.MouseMode with { Activation = MouseModeActivation.Off },
        });

        arcadia.Sdl.Hold(steamController, B.Back, B.Start);
        arcadia.PollControllers();
        await arcadia.XboxPads.Current!.WaitForAsync(state => state.Buttons == (Back | Start));
    }

    /// <summary>T213-6, Codex review of #231: SDL failing for good while A is held lets go of A in the game and unplugs the pad.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-6")]
    public async Task Losing_SDL_while_a_button_is_held_lets_go_of_it_in_the_game()
    {
        var (arcadia, steamController) = await StartInBackgroundAsync();
        using var _ = arcadia;
        arcadia.Sdl.Hold(steamController, B.South);
        arcadia.PollControllers();
        var pad = arcadia.XboxPads.Current!;
        await pad.WaitForAsync(state => state.Buttons == A);

        arcadia.Sdl.ReadError = new InvalidOperationException("SDL broke");
        arcadia.PollControllers();

        await pad.WaitForAsync(state => state == XboxPadState.Neutral);
        await arcadia.TickSteamControllerAsync();
        Assert.False(arcadia.XboxPad.IsPlugged);
    }
}
