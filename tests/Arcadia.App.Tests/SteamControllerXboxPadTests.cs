using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Arcadia.Input;
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
        arcadia.IsArcadiaInFront = false;
        await arcadia.TickSteamControllerAsync();
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
        var first = arcadia.Main.FocusedGame!.Game;
        arcadia.PressOn(steamController, B.DPadRight);
        Assert.NotSame(first, arcadia.Main.FocusedGame!.Game);

        arcadia.IsArcadiaInFront = false;
        arcadia.IsMouseModeOn = true;
        arcadia.Sdl.Hold(steamController, B.South);
        arcadia.PollControllers();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.All(pad.Sent, state => Assert.Equal(XboxPadState.Neutral, state));
    }

    /// <summary>T213-4: only the Steam Controller is emulated; an Xbox pad beside it never reaches the virtual pad.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-4")]
    public async Task Other_controllers_are_never_emulated()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var xbox = arcadia.Sdl.Plug(KnownPads.XboxSeries);
        arcadia.StartControllers();
        arcadia.IsArcadiaInFront = false;
        await arcadia.TickSteamControllerAsync();
        Assert.Equal(0, arcadia.XboxPads.Plugged);

        var steamController = arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.PollControllers();
        await arcadia.TickSteamControllerAsync();
        arcadia.Sdl.Hold(xbox, B.South);
        arcadia.PollControllers();
        arcadia.Sdl.Hold(steamController, B.North);
        arcadia.PollControllers();
        var pad = arcadia.XboxPads.Current!;
        await pad.WaitForAsync(state => state.Buttons == 0x8000);
        Assert.DoesNotContain(pad.Sent, state => (state.Buttons & A) != 0);
    }

    /// <summary>T213-5 (automated part): the pad goes with the Steam Controller and comes back with it, and while SISR is on there is none.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-5")]
    public async Task The_pad_goes_and_comes_with_the_Steam_Controller_and_not_beside_SISR()
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
        Assert.Equal(SisrState.Off, arcadia.ControllerSupport.State);
    }

    /// <summary>T213-6 (automated part): VIIPER lost is plugged in again; lost too often, a toast says so and Arcadia stops trying.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T213-6")]
    public async Task A_lost_VIIPER_comes_back_and_too_many_losses_show_a_toast()
    {
        var (arcadia, _) = await StartInBackgroundAsync();
        using var __ = arcadia;

        for (var i = 0; i <= SteamControllerXboxPad.MaxReplugsPerWindow; i++)
        {
            arcadia.XboxPads.Current!.Lose();
            await arcadia.TickSteamControllerAsync();
        }

        Assert.Equal(SteamControllerXboxPad.MaxReplugsPerWindow + 1, arcadia.XboxPads.Plugged);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text?.Contains("VIIPER fiel") == true, "the toast about VIIPER");
        Assert.False(arcadia.XboxPad.IsPlugged);
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
