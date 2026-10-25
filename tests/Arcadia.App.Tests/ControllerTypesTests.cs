using Arcadia.Input;
using Arcadia.App.Input;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using B = Arcadia.Input.SdlPadState.Button;

namespace Arcadia.App.Tests;

/// <summary>
/// Every common controller through SDL3 (#72), as SDL reports it: by position, the bottom face button confirms on every
/// one; Steam's virtual pads and the extra buttons do nothing; pads come and go while Arcadia runs.
/// </summary>
public sealed class ControllerTypesTests
{
    public static TheoryData<string> Pads => new(nameof(KnownPads.Xbox360), nameof(KnownPads.EightBitDoUltimate2), nameof(KnownPads.DualSense),
        nameof(KnownPads.DualShock4), nameof(KnownPads.SwitchPro));

    /// <summary>Partly T72-1: the mapping of each type; the real devices are the manual part.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Pads))]
    [Trait("TestCase", "T72-1")]
    [Trait("TestCase", "T72-4")]
    public async Task Each_controller_moves_with_the_D_pad_and_its_face_buttons_act_by_position(string type)
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var pad = arcadia.Sdl.Plug(Pad(type));
        arcadia.StartControllers();
        var first = arcadia.Main.FocusedGame!.Game;

        arcadia.PressOn(pad, B.DPadRight);
        Assert.NotSame(first, arcadia.Main.FocusedGame!.Game);
        arcadia.PressOn(pad, B.DPadLeft);
        Assert.Same(first, arcadia.Main.FocusedGame!.Game);

        arcadia.PressOn(pad, B.West);
        var search = arcadia.Main.Search!;
        Assert.True(search.IsKeyboardOut);
        arcadia.PressOn(pad, B.East);
        Assert.False(search.IsKeyboardOut);
        arcadia.PressOn(pad, B.East);
        Assert.Null(arcadia.Main.Search);

        arcadia.PressOn(pad, B.South);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "the bottom face button starts the focused game");
        Assert.Equal([first.Key], arcadia.Starts.Keys);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T72-7")]
    public async Task Guide_PS_Home_capture_and_touchpad_click_do_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var dualSense = arcadia.Sdl.Plug(KnownPads.DualSense);
        var switchPro = arcadia.Sdl.Plug(KnownPads.SwitchPro);
        arcadia.StartControllers();
        var focused = arcadia.Main.FocusedGame;

        arcadia.PressOn(dualSense, B.Guide).PressOn(dualSense, B.Touchpad).PressOn(dualSense, B.Misc1);
        arcadia.PressOn(switchPro, B.Guide).PressOn(switchPro, B.Misc1).Wait(TimeSpan.FromSeconds(1));

        Assert.Same(focused, arcadia.Main.FocusedGame);
        Assert.False(arcadia.Main.IsDialogOpen);
        Assert.Null(arcadia.Main.Search);
        Assert.Empty(arcadia.Starts.Keys);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T72-6")]
    public async Task Two_controllers_work_side_by_side_and_one_plugged_in_or_out_while_Arcadia_runs()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var dualSense = arcadia.Sdl.Plug(KnownPads.DualSense);
        arcadia.StartControllers();
        var first = arcadia.Main.FocusedGame!.Game;

        var switchPro = arcadia.Sdl.Plug(KnownPads.SwitchPro);
        arcadia.PressOn(switchPro, B.DPadRight).PressOn(dualSense, B.DPadRight);
        var third = arcadia.Main.FocusedGame!.Game;
        Assert.NotSame(first, third);

        arcadia.Sdl.Unplug(switchPro);
        arcadia.PressOn(dualSense, B.DPadLeft).PressOn(dualSense, B.DPadLeft);
        Assert.Same(first, arcadia.Main.FocusedGame!.Game);

        var log = await File.ReadAllTextAsync(Path.Combine(Services.AppLog.Directory!, "arcadia.log"), TestContext.Current.CancellationToken);
        Assert.Contains($"Controller #{switchPro} verbunden: \"Nintendo Switch Pro Controller\" (SwitchPro, 057E:2009)", log);
        Assert.Contains($"Controller #{switchPro} getrennt: \"Nintendo Switch Pro Controller\" (SwitchPro, 057E:2009)", log);
    }

    /// <summary>Steam Input on: Steam's virtual pad mirrors the DualSense; only the DualSense counts, once.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T72-12")]
    public async Task Steam_s_virtual_pad_and_the_Steam_Controller_add_no_input_of_their_own()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var dualSense = arcadia.Sdl.Plug(KnownPads.DualSense);
        var steamVirtual = arcadia.Sdl.Plug(KnownPads.SteamVirtual);
        var steamController = arcadia.Sdl.Plug(KnownPads.SteamController);
        var joyCon = arcadia.Sdl.Plug(KnownPads.JoyConLeft);
        arcadia.StartControllers();
        var first = arcadia.Main.FocusedGame!.Game;

        arcadia.PressOn(steamVirtual, B.DPadRight).PressOn(steamController, B.DPadRight).PressOn(joyCon, B.DPadRight);
        Assert.Same(first, arcadia.Main.FocusedGame!.Game);

        // The DualSense and the virtual pad Steam made of it, a moment apart: one step, not two.
        arcadia.Sdl.Hold(dualSense, B.DPadRight);
        arcadia.PollControllers();
        arcadia.Sdl.Hold(steamVirtual, B.DPadRight);
        arcadia.PollControllers();
        arcadia.Sdl.Hold(dualSense);
        arcadia.PollControllers();
        arcadia.Sdl.Hold(steamVirtual);
        arcadia.PollControllers();
        arcadia.Press(UiAction.Left);
        Assert.Same(first, arcadia.Main.FocusedGame!.Game);
        Assert.Equal([dualSense], arcadia.Sdl.OpenPads);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T72-9")]
    public async Task Without_a_usable_SDL3_Arcadia_says_so_and_keyboard_and_mouse_keep_working()
    {
        using var window = await ArcadiaWindow.ShowAsync();
        var arcadia = window.Arcadia;
        arcadia.Sdl.InitError = new DllNotFoundException("Unable to load shared library 'SDL3' or one of its dependencies.");
        arcadia.Sdl.Plug(KnownPads.Xbox360);

        arcadia.StartControllers();
        Assert.Equal(ControllerInput.UnavailableText, arcadia.Main.Toast.Text);
        var log = await File.ReadAllTextAsync(Path.Combine(Services.AppLog.Directory!, "arcadia.log"), TestContext.Current.CancellationToken);
        Assert.Contains("Controller-Eingabe nicht verfügbar (SDL3): System.DllNotFoundException", log);

        var first = arcadia.Main.FocusedGame!.Game;
        window.Key(Key.D);
        Assert.NotSame(first, arcadia.Main.FocusedGame!.Game);
        window.Key(Key.F);
        Assert.NotNull(arcadia.Main.Search);
    }

    private static Arcadia.Input.PadDevice Pad(string name) => (Arcadia.Input.PadDevice)typeof(KnownPads).GetField(name)!.GetValue(null)!;
}
