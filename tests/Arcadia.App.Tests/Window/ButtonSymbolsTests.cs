using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.Input;
using Arcadia.Tests.Sdl;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using B = Arcadia.Input.SdlPadState.Button;

namespace Arcadia.App.Tests;

/// <summary>
/// The hints show the symbols of the controller in use (#215): the one pressed or pushed last, the first connected
/// before any input, the keyboard's keys with none. Texts keep the neutral names; the symbol is chosen when drawn.
/// </summary>
public sealed class ButtonSymbolsTests
{
    public static TheoryData<string, string, string, string> Pads => new()
    {
        // The pad, then what the library's footer shows for A, for LB|RB and for ⧉ (Optionen).
        { nameof(KnownPads.EightBitDoUltimate2), "A", "LB RB", "⧉" },
        { nameof(KnownPads.DualSense), "Cross", "L1 R1", "Create" },
        { nameof(KnownPads.DualShock4), "Cross", "L1 R1", "Share" },
        { nameof(KnownPads.SwitchPro), "B", "L R", "−" },
        { nameof(KnownPads.SteamController), "A", "L1 R1", "⧉" },
    };

    /// <summary>T215-1: legend, detail page, settings and on-screen keyboard in the printing of each controller.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Pads))]
    [Trait("TestCase", "T215-1")]
    public async Task Each_controller_shows_its_printing_in_legend_detail_page_settings_and_on_screen_keyboard(string type, string confirm, string tabs, string options)
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        arcadia.Arcadia.Sdl.Plug(Pad(type));
        arcadia.Arcadia.StartControllers();
        arcadia.Settle();
        var symbols = ButtonSymbolSets.For(Pad(type).Type);

        Assert.Equal(symbols, arcadia.Arcadia.Main.ButtonSymbols);
        Assert.Contains(arcadia.FooterHints(), hint => hint.StartsWith(confirm + " ", StringComparison.Ordinal));
        Assert.Contains(arcadia.FooterHints(), hint => hint.StartsWith(tabs + " ", StringComparison.Ordinal));
        Assert.Contains(arcadia.FooterHints(), hint => hint == options + " Optionen");
        AssertAllIn(arcadia, symbols, "Bibliothek");

        arcadia.Arcadia.PressDown(UiAction.Accept).Wait(TimeSpan.FromSeconds(1.1)).LetGo(UiAction.Accept);
        arcadia.WaitForTransitions();
        Assert.True(arcadia.Arcadia.Main.IsDetailOpen);
        AssertAllIn(arcadia, symbols, "Detailseite");
        arcadia.Press(UiAction.Back).WaitForTransitions();

        arcadia.Press(UiAction.Settings).WaitForTransitions();
        Assert.NotNull(arcadia.Arcadia.Main.Settings);
        AssertAllIn(arcadia, symbols, "Einstellungen");
        arcadia.Press(UiAction.Back).WaitForTransitions();

        arcadia.Press(UiAction.Search).WaitForTransitions();
        Assert.True(arcadia.Arcadia.Main.Search?.IsKeyboardOut);
        var keyboard = arcadia.Window.GetVisualDescendants().OfType<Views.OnScreenKeyboardView>().Single(view => view.IsEffectivelyVisible);
        Assert.Contains(ArcadiaWindow.Glyphs(keyboard), glyph => glyph is PadGlyph { Button: "X" });
        AssertAllIn(arcadia, symbols, "Bildschirmtastatur");
    }

    /// <summary>T215-2, T215-3: each press takes the symbols to its controller; stick noise on the other switches nothing.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T215-2")]
    [Trait("TestCase", "T215-3")]
    public async Task The_symbols_follow_each_press_and_stick_noise_switches_nothing()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var xbox = arcadia.Arcadia.Sdl.Plug(KnownPads.EightBitDoUltimate2);
        var dualSense = arcadia.Arcadia.Sdl.Plug(KnownPads.DualSense);
        arcadia.Arcadia.StartControllers();
        arcadia.Arcadia.Wait(ControllerInUse.StartGrace);
        arcadia.Settle();
        Assert.Contains("A", ConfirmHints(arcadia));

        arcadia.Arcadia.PressOn(dualSense, B.DPadRight);
        arcadia.Settle();
        Assert.Contains("Cross", ConfirmHints(arcadia));

        // The idle 8BitDo's stick resting a little off centre, as worn sticks do.
        arcadia.Arcadia.Sdl.Set(xbox, new SdlPadState(0, 6000, -7000, -5000, 4000, 0, 0));
        arcadia.Arcadia.PollControllers().PollControllers();
        arcadia.Settle();
        Assert.Contains("Cross", ConfirmHints(arcadia));

        // Pushed past the dead zone, it counts as use.
        arcadia.Arcadia.Sdl.Set(xbox, new SdlPadState(0, 30000, 0, 0, 0, 0, 0));
        arcadia.Arcadia.PollControllers();
        arcadia.Arcadia.Sdl.Hold(xbox);
        arcadia.Arcadia.PollControllers();
        arcadia.Settle();
        Assert.Contains("A", ConfirmHints(arcadia));

        arcadia.Arcadia.PressOn(dualSense, B.DPadLeft);
        arcadia.Settle();
        Assert.Contains("Cross", ConfirmHints(arcadia));
        arcadia.Arcadia.PressOn(xbox, B.DPadRight);
        arcadia.Settle();
        Assert.Contains("A", ConfirmHints(arcadia));
    }

    /// <summary>T215-4: at the start the connected controller's symbols, without one the keyboard's keys; nothing is kept.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T215-4")]
    public async Task At_the_start_a_connected_controller_shows_its_symbols_and_without_one_the_keyboard()
    {
        using var withPad = await ArcadiaWindow.ShowAsync();
        withPad.Arcadia.Sdl.Plug(KnownPads.SwitchPro);
        withPad.Arcadia.StartControllers();
        withPad.Settle();
        Assert.True(withPad.Arcadia.Main.UsesController);
        Assert.Equal(ButtonSymbols.Nintendo, withPad.Arcadia.Main.ButtonSymbols);

        using var restarted = await withPad.RestartAsync();
        restarted.Arcadia.StartControllers();
        restarted.Settle();
        Assert.False(restarted.Arcadia.Main.UsesController, "without a controller the hints show the keyboard");
        Assert.All(ArcadiaWindow.Glyphs(restarted.Window), glyph => Assert.IsNotType<PadGlyph>(glyph));
        Assert.Contains(restarted.FooterHints(), hint => hint.StartsWith("Leertaste", StringComparison.Ordinal));
    }

    /// <summary>T215-4, failure case: without a usable SDL the hints show the keyboard, as with no controller.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T215-4")]
    public async Task Without_a_usable_SDL_the_hints_show_the_keyboard()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        arcadia.Arcadia.Sdl.InitError = new DllNotFoundException("Unable to load shared library 'SDL3' or one of its dependencies.");
        arcadia.Arcadia.Sdl.Plug(KnownPads.DualSense);

        arcadia.Arcadia.StartControllers();
        arcadia.Settle();
        Assert.False(arcadia.Arcadia.Main.UsesController);
        Assert.All(ArcadiaWindow.Glyphs(arcadia.Window), glyph => Assert.IsNotType<PadGlyph>(glyph));
        Assert.Contains(arcadia.FooterHints(), hint => hint.StartsWith("Leertaste", StringComparison.Ordinal));
    }

    /// <summary>T215-5: the controller in use unplugged: the other one's symbols, then the keyboard; a new one changes nothing until used.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T215-5")]
    public async Task Unplugging_the_controller_in_use_shows_the_next_one_then_the_keyboard()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var switchPro = arcadia.Arcadia.Sdl.Plug(KnownPads.SwitchPro);
        var dualSense = arcadia.Arcadia.Sdl.Plug(KnownPads.DualSense);
        arcadia.Arcadia.StartControllers();
        arcadia.Arcadia.Wait(ControllerInUse.StartGrace);
        arcadia.Arcadia.PressOn(dualSense, B.DPadRight);
        arcadia.Settle();
        Assert.Contains("Cross", ConfirmHints(arcadia));

        arcadia.Arcadia.Sdl.Unplug(dualSense);
        arcadia.Arcadia.PollControllers();
        arcadia.Settle();
        Assert.Contains("B", ConfirmHints(arcadia));
        Assert.True(arcadia.Arcadia.Main.UsesController);

        arcadia.Arcadia.Sdl.Unplug(switchPro);
        arcadia.Arcadia.PollControllers();
        arcadia.Settle();
        Assert.False(arcadia.Arcadia.Main.UsesController);
        Assert.All(ArcadiaWindow.Glyphs(arcadia.Window), glyph => Assert.IsNotType<PadGlyph>(glyph));

        var xbox = arcadia.Arcadia.Sdl.Plug(KnownPads.XboxSeries);
        arcadia.Arcadia.PollControllers().PollControllers();
        arcadia.Settle();
        Assert.False(arcadia.Arcadia.Main.UsesController, "a controller plugged in changes nothing until it is used");
        Assert.Equal(ButtonSymbols.Nintendo, arcadia.Arcadia.Main.ButtonSymbols);

        arcadia.Arcadia.PressOn(xbox, B.DPadLeft);
        arcadia.Settle();
        Assert.True(arcadia.Arcadia.Main.UsesController);
        Assert.Equal(ButtonSymbols.Xbox, arcadia.Arcadia.Main.ButtonSymbols);
    }

    /// <summary>T215-6: on a Nintendo pad the bottom button is printed B, and it still confirms.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T215-6")]
    public async Task On_a_Nintendo_controller_the_bottom_button_shows_B_and_confirms()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var switchPro = arcadia.Arcadia.Sdl.Plug(KnownPads.SwitchPro);
        arcadia.Arcadia.StartControllers();
        arcadia.Settle();
        var accept = ConfirmGlyphs(arcadia).ToList();
        Assert.NotEmpty(accept);
        Assert.All(accept, glyph => Assert.Equal("B", ArcadiaWindow.Printed(glyph)));
        Assert.All(accept, glyph => Assert.Contains("round", glyph.Classes));

        var focused = arcadia.Arcadia.Main.Library.FocusedGame!.Game;
        arcadia.Arcadia.PressOn(switchPro, B.South);
        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Starts.Keys.Count > 0, "the bottom button (B on the pad) starts the focused game");
        Assert.Equal([focused.Key], arcadia.Arcadia.Starts.Keys);
    }

    private static PadDevice Pad(string type) => (PadDevice)typeof(KnownPads).GetField(type)!.GetValue(null)!;

    // The footer's hints of A: their symbol, as drawn.
    private static IEnumerable<string?> ConfirmHints(ArcadiaWindow arcadia) => ConfirmGlyphs(arcadia).Select(ArcadiaWindow.Printed);

    private static IEnumerable<PadGlyph> ConfirmGlyphs(ArcadiaWindow arcadia) => arcadia.Window.GetVisualDescendants().OfType<KeyHint>()
        .Where(hint => hint.IsEffectivelyVisible && hint.Pad == "A" && arcadia.Bounds(hint).Top > arcadia.Bounds(arcadia.Window.Content as Control ?? arcadia.Window).Bottom - 80)
        .SelectMany(hint => hint.Children.OfType<PadGlyph>());

    private static void AssertAllIn(ArcadiaWindow arcadia, ButtonSymbols symbols, string where)
    {
        var glyphs = ArcadiaWindow.Glyphs(arcadia.Window).OfType<PadGlyph>().ToList();
        Assert.NotEmpty(glyphs);
        Assert.All(glyphs, glyph => Assert.True(
            glyph.Shown == ButtonSymbolSets.Symbol(glyph.Button ?? string.Empty, symbols),
            $"{where}: {glyph.Button} zeigt {ArcadiaWindow.Printed(glyph)} statt der Beschriftung von {symbols}"));
        Assert.All(arcadia.Window.GetVisualDescendants().OfType<KeyText>().Where(text => text.IsEffectivelyVisible && text.Pad is not null),
            text => Assert.Equal(ButtonSymbolSets.Symbol(text.Pad!, symbols).Text, text.Text));
    }
}
