using Arcadia.App.Input;
using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;
using B = Arcadia.Input.SdlPadState.Button;

namespace Arcadia.App.Tests;

/// <summary>
/// The Steam Controller read raw through SDL3 (#212): it operates Arcadia like any other pad, SISR's virtual Xbox pad
/// of it doesn't count a second time, and "connected" for the per-game mode follows SDL.
/// </summary>
public sealed class SteamControllerTests
{
    /// <summary>T212-2 (the operation; Steam's desktop layout is the manual part) and T212-4's mapping.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T212-2")]
    public async Task The_Steam_Controller_moves_starts_and_goes_back_like_any_other_pad()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var pad = arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        var first = arcadia.Main.FocusedGame!.Game;

        arcadia.PressOn(pad, B.DPadRight);
        Assert.NotSame(first, arcadia.Main.FocusedGame!.Game);
        arcadia.PressOn(pad, B.DPadLeft);
        Assert.Same(first, arcadia.Main.FocusedGame!.Game);

        arcadia.PressOn(pad, B.West);
        Assert.NotNull(arcadia.Main.Search);
        arcadia.PressOn(pad, B.East).PressOn(pad, B.East);
        Assert.Null(arcadia.Main.Search);

        arcadia.PressOn(pad, B.South);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "A starts the focused game");
        Assert.Equal([first.Key], arcadia.Starts.Keys);
    }

    /// <summary>T212-3: SISR runs; its virtual Xbox pad (on usbip-win2's software USB host) repeats every press 1–4 ms later.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T212-3")]
    public async Task With_SISR_running_every_press_of_the_Steam_Controller_counts_once()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.XusbDevices.Add(new XusbDevice(OnSoftwareHost: true, Slot: 0));
        var steamController = arcadia.Sdl.Plug(KnownPads.SteamController);
        var sisrPad = arcadia.Sdl.Plug(KnownPads.SisrXbox360);
        arcadia.StartControllers();
        var first = arcadia.Main.FocusedGame!.Game;

        // The press on the controller, then the same press on SISR's pad a poll later: one step, not two.
        arcadia.Sdl.Hold(steamController, B.DPadRight);
        arcadia.PollControllers();
        arcadia.Sdl.Hold(sisrPad, B.DPadRight);
        arcadia.PollControllers();
        arcadia.Sdl.Hold(steamController);
        arcadia.PollControllers();
        arcadia.Sdl.Hold(sisrPad);
        arcadia.PollControllers();
        arcadia.Press(UiAction.Left);

        Assert.Same(first, arcadia.Main.FocusedGame!.Game);
        Assert.Equal([steamController], arcadia.Sdl.OpenPads);
        var log = await File.ReadAllTextAsync(Path.Combine(Services.AppLog.Directory!, "arcadia.log"), TestContext.Current.CancellationToken);
        Assert.Contains($"Controller #{sisrPad} ignoriert (virtueller Xbox Controller, z. B. von SISR)", log);
    }

    /// <summary>T212-7: a real Xbox 360 pad (PCI host) beside SISR's pad and an Xbox pad under Steam Input all count once.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T212-7")]
    public async Task Other_pads_beside_the_Steam_Controller_count_once_each()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.XusbDevices.AddRange([new XusbDevice(OnSoftwareHost: false, Slot: 0), new XusbDevice(OnSoftwareHost: true, Slot: 2)]);
        var steamController = arcadia.Sdl.Plug(KnownPads.SteamController);
        var xbox = arcadia.Sdl.Plug(KnownPads.Xbox360);
        var sisrPad = arcadia.Sdl.Plug(KnownPads.SisrXbox360 with { Path = "XInput#2" });
        var steamVirtual = arcadia.Sdl.Plug(KnownPads.SteamVirtual);
        arcadia.StartControllers();
        var first = arcadia.Main.FocusedGame!.Game;

        arcadia.PressOn(xbox, B.DPadRight).PressOn(steamController, B.DPadRight);
        arcadia.PressOn(sisrPad, B.DPadRight).PressOn(steamVirtual, B.DPadRight);
        arcadia.Press(UiAction.Left).Press(UiAction.Left);

        Assert.Same(first, arcadia.Main.FocusedGame!.Game);
        Assert.Equal([steamController, xbox], arcadia.Sdl.OpenPads.Order());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T212-6")]
    public async Task The_Steam_button_the_dots_button_and_the_back_grips_do_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var pad = arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        var focused = arcadia.Main.FocusedGame;

        foreach (var button in new[] { B.Guide, B.Misc1, B.LeftPaddle1, B.LeftPaddle2, B.RightPaddle1, B.RightPaddle2, B.Touchpad, B.Misc2 })
        {
            arcadia.PressOn(pad, button);
        }

        arcadia.Wait(TimeSpan.FromSeconds(1));
        Assert.Same(focused, arcadia.Main.FocusedGame);
        Assert.False(arcadia.Main.IsDialogOpen);
        Assert.Null(arcadia.Main.Search);
        Assert.Null(arcadia.Main.Settings);
        Assert.Empty(arcadia.Starts.Keys);
    }

    /// <summary>
    /// T212-5, T212-10 and T213-8: the per-game mode shows as soon as SDL sees the Steam Controller, with no switch to turn
    /// on (#213), and goes as soon as it is switched off.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T212-5")]
    [Trait("TestCase", "T212-10")]
    [Trait("TestCase", "T213-8")]
    public async Task The_per_game_mode_follows_the_Steam_Controller_as_SDL_sees_it()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.StartControllers();
        Assert.False(arcadia.Main.ShowControllerSupport);

        var pad = arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.PollControllers();
        Assert.True(arcadia.Main.ShowControllerSupport, "SDL sees the Steam Controller");

        // Switched off (or the puck pulled): SDL reports it gone at once.
        arcadia.Sdl.Unplug(pad);
        arcadia.PollControllers();
        Assert.False(arcadia.Main.ShowControllerSupport, "SDL no longer sees it");

        // Switched on again: a new SDL id, and it works at once.
        var again = arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.PollControllers();
        Assert.True(arcadia.Main.ShowControllerSupport);
        var first = arcadia.Main.FocusedGame!.Game;
        arcadia.PressOn(again, B.DPadRight);
        Assert.NotSame(first, arcadia.Main.FocusedGame!.Game);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T212-10")]
    public async Task Another_pad_alone_shows_no_per_game_mode()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Sdl.Plug(KnownPads.XboxSeries);
        arcadia.StartControllers();

        Assert.False(arcadia.Main.ShowControllerSupport);
    }

    private static AppSettings SwitchedOn() => new()
    {
        ActiveLaunchers = [.. ArcadiaDriver.DefaultLaunchers.Select(spec => spec.Launcher.Id)],
        SteamControllerSupport = true,
    };
}
