using Arcadia.App.Input;
using Arcadia.Core.Search;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The search's on-screen keyboard worked with the controller alone (#12, #110, #129): the D-pad and A on the keys, the
/// buttons that stand for keys, shift and AltGr, the caret, the layouts.
/// </summary>
public sealed class OnScreenKeyboardTests
{
    private static readonly TimeSpan Tap = TimeSpan.FromMilliseconds(100);

    /// <summary>A held erase waits 400 ms, then deletes every 60 ms.</summary>
    private static readonly TimeSpan ShortOfRepeat = TimeSpan.FromMilliseconds(300);

    [AvaloniaFact]
    [Trait("TestCase", "T12-2")]
    public async Task The_D_pad_walks_the_keys_to_the_nearest_one_and_stops_at_the_edges_A_types_Y_a_space_X_erases()
    {
        using var arcadia = await OpenSearchAsync();
        var search = arcadia.Main.Search!;
        Assert.Equal("a", arcadia.FocusedLabel());

        // Up to the nearest key: the row above starts a quarter key further left, so q is nearer to a than w is.
        arcadia.Press(UiAction.Up);
        Assert.Equal("q", arcadia.FocusedLabel());
        arcadia.Press(UiAction.Accept);
        Assert.Equal("q", search.Text);

        // The edges: nothing above the number row, nothing left of its first key.
        arcadia.GoTo("1").Press(UiAction.Up);
        Assert.Equal("1", arcadia.FocusedLabel());
        arcadia.Press(UiAction.Left);
        Assert.Equal("^", arcadia.FocusedLabel());
        arcadia.Press(UiAction.Left);
        Assert.Equal("^", arcadia.FocusedLabel());

        arcadia.TypeOnKeys("uiz");
        arcadia.Press(UiAction.ViewOptions);
        Assert.Equal("quiz ", search.Text);

        arcadia.Press(UiAction.Search);
        Assert.Equal("quiz", search.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T12-2")]
    public async Task X_held_or_A_held_on_the_erase_key_erases_one_then_after_a_pause_one_after_another()
    {
        using var arcadia = await OpenSearchAsync();
        var search = arcadia.Main.Search!;
        arcadia.TypeOnKeys("hollowhollow");

        arcadia.PressDown(UiAction.Search);
        Assert.Equal("hollowhollo", search.Text);
        arcadia.Wait(ShortOfRepeat);
        Assert.Equal("hollowhollo", search.Text);
        arcadia.Wait(TimeSpan.FromMilliseconds(300));
        Assert.True(search.Text.Length < "hollowhollo".Length);
        arcadia.LetGo(UiAction.Search);
        var afterX = search.Text;
        arcadia.Wait(TimeSpan.FromSeconds(1));
        Assert.Equal(afterX, search.Text);
        Assert.StartsWith(afterX, "hollowhollow");

        arcadia.GoTo("⌫").PressDown(UiAction.Accept);
        Assert.Equal(afterX[..^1], search.Text);
        arcadia.Wait(ShortOfRepeat);
        Assert.Equal(afterX[..^1], search.Text);
        arcadia.Wait(TimeSpan.FromSeconds(2));
        Assert.Equal(string.Empty, search.Text);
        arcadia.LetGo(UiAction.Accept);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T12-13")]
    [Trait("TestCase", "T129-2")]
    public async Task LB_tapped_latches_shift_until_tapped_again_and_held_it_counts_only_while_held()
    {
        using var arcadia = await OpenSearchAsync();
        var search = arcadia.Main.Search!;
        var keyboard = arcadia.Keyboard();
        var shiftKeys = keyboard.Keys.Where(k => k.Key.Kind == BoardKeyKind.Shift).ToList();

        arcadia.Hold(UiAction.PreviousTab, Tap);
        Assert.All(shiftKeys, key => Assert.True(key.IsOn));
        Assert.Equal("°!\"§$%&/()=?`", arcadia.Row(0));
        Assert.Equal("QWERTZUIOPÜ*", arcadia.Row(1));
        Assert.Equal(">YXCVBNM;:_", arcadia.Row(3));
        Assert.All(keyboard.Keys.Where(k => k.IsCharacter), key => Assert.Null(key.ShiftHint ?? key.AltGrHint));

        // What is shown is typed, and shift stays on.
        arcadia.TypeOnKeys("H?");
        Assert.Equal("H?", search.Text);
        Assert.Equal(KeyLevel.Shift, keyboard.Level);

        arcadia.Hold(UiAction.PreviousTab, Tap);
        Assert.Equal(KeyLevel.Normal, keyboard.Level);
        Assert.All(shiftKeys, key => Assert.False(key.IsOn));
        Assert.Equal("qwertzuiopü+", arcadia.Row(1));

        // Held: capitals while held, also for a letter typed meanwhile, small again on the release.
        arcadia.PressDown(UiAction.PreviousTab);
        Assert.Equal("QWERTZUIOPÜ*", arcadia.Row(1));
        arcadia.TypeOnKeys("Z");
        arcadia.LetGo(UiAction.PreviousTab);
        Assert.Equal(KeyLevel.Normal, keyboard.Level);
        Assert.Equal("qwertzuiopü+", arcadia.Row(1));
        arcadia.TypeOnKeys("z");
        Assert.Equal("H?Zz", search.Text);

        // Held long without typing counts as held too.
        arcadia.Hold(UiAction.PreviousTab, TimeSpan.FromMilliseconds(700));
        Assert.Equal(KeyLevel.Normal, keyboard.Level);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T129-3")]
    public async Task RB_tapped_latches_AltGr_whose_characters_alone_show_and_type_and_LB_and_RB_switch_each_other_off()
    {
        using var arcadia = await OpenSearchAsync();
        var search = arcadia.Main.Search!;
        var keyboard = arcadia.Keyboard();
        var altGrKey = keyboard.Keys.Single(k => k.Key.Kind == BoardKeyKind.AltGr);

        arcadia.Hold(UiAction.NextTab, Tap);
        Assert.True(altGrKey.IsOn);
        Assert.Equal("²³{[]}\\", arcadia.Row(0));
        Assert.Equal("@€~", arcadia.Row(1));
        Assert.Equal(string.Empty, arcadia.Row(2));
        Assert.Equal("|µ", arcadia.Row(3));

        arcadia.TypeOnKeys("@€{~|µ");
        Assert.Equal("@€{~|µ", search.Text);
        Assert.True(altGrKey.IsOn);

        arcadia.Hold(UiAction.NextTab, Tap);
        Assert.False(altGrKey.IsOn);
        Assert.Equal("qwertzuiopü+", arcadia.Row(1));

        arcadia.PressDown(UiAction.NextTab);
        Assert.Equal(KeyLevel.AltGr, keyboard.Level);
        arcadia.TypeOnKeys("€");
        arcadia.LetGo(UiAction.NextTab);
        Assert.Equal(KeyLevel.Normal, keyboard.Level);

        // LB switches AltGr off and RB shift.
        arcadia.Hold(UiAction.NextTab, Tap).Hold(UiAction.PreviousTab, Tap);
        Assert.Equal(KeyLevel.Shift, keyboard.Level);
        Assert.False(altGrKey.IsOn);
        arcadia.Hold(UiAction.NextTab, Tap);
        Assert.Equal(KeyLevel.AltGr, keyboard.Level);
        Assert.All(keyboard.Keys.Where(k => k.Key.Kind == BoardKeyKind.Shift), key => Assert.False(key.IsOn));

        // Shift really went off, it doesn't come back when AltGr goes.
        arcadia.Hold(UiAction.NextTab, Tap);
        Assert.Equal(KeyLevel.Normal, keyboard.Level);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T129-4")]
    public async Task With_AltGr_on_the_D_pad_passes_over_empty_keys_and_rows_and_without_it_up_and_down_come_back()
    {
        using var arcadia = await OpenSearchAsync();

        // e lies between 3 and 4; up takes the right one, down the left one, so the way back leads home (KeyGrid).
        arcadia.GoTo("e").Press(UiAction.Up);
        Assert.Equal("4", arcadia.FocusedLabel());
        arcadia.Press(UiAction.Down);
        Assert.Equal("e", arcadia.FocusedLabel());

        arcadia.Hold(UiAction.NextTab, Tap);
        Assert.Equal("€", arcadia.FocusedLabel());
        arcadia.Press(UiAction.Left);
        Assert.Equal("@", arcadia.FocusedLabel());
        arcadia.Press(UiAction.Right).Press(UiAction.Right);
        Assert.Equal("~", arcadia.FocusedLabel());

        arcadia.GoTo("€").Press(UiAction.Up);
        Assert.Equal("³", arcadia.FocusedLabel());

        // The row a s d … has no AltGr character at all.
        arcadia.GoTo("€").Press(UiAction.Down);
        Assert.Equal("|", arcadia.FocusedLabel());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T129-5")]
    public async Task The_dead_keys_type_their_character_at_once()
    {
        using var arcadia = await OpenSearchAsync();
        var search = arcadia.Main.Search!;

        arcadia.TypeOnKeys("^");
        Assert.Equal("^", search.Text);
        arcadia.TypeOnKeys("´");
        Assert.Equal("^´", search.Text);
        arcadia.Hold(UiAction.PreviousTab, Tap).TypeOnKeys("`").Hold(UiAction.PreviousTab, Tap);
        Assert.Equal("^´`", search.Text);

        ChooseLayout(arcadia, "FR");
        arcadia.TypeOnKeys("^");
        arcadia.Hold(UiAction.PreviousTab, Tap).TypeOnKeys("¨").Hold(UiAction.PreviousTab, Tap);
        Assert.Equal("^´`^¨", search.Text);

        ChooseLayout(arcadia, "ES");
        arcadia.TypeOnKeys("`´");
        arcadia.Hold(UiAction.PreviousTab, Tap).TypeOnKeys("^¨");
        Assert.Equal("^´`^¨`´^¨", search.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T129-1")]
    public async Task X_opens_the_German_keyboard_with_the_real_rows_and_the_shift_and_AltGr_characters_small_on_the_keys()
    {
        using var arcadia = await OpenSearchAsync();

        Assert.Equal("DE", arcadia.Keyboard().Layout.Code);
        Assert.Equal("^1234567890ß´", arcadia.Row(0));
        Assert.Equal("qwertzuiopü+", arcadia.Row(1));
        Assert.Equal("asdfghjklöä#", arcadia.Row(2));
        Assert.Equal("<yxcvbnm,.-", arcadia.Row(3));

        // Each row starts half a key or so further right, as on the real keyboard.
        Assert.True(arcadia.CharacterKey('^').Left < arcadia.CharacterKey('q').Left);
        Assert.True(arcadia.CharacterKey('q').Left < arcadia.CharacterKey('a').Left);
        Assert.True(arcadia.CharacterKey('<').Left < arcadia.CharacterKey('a').Left);
        Assert.True(arcadia.CharacterKey('a').Left < arcadia.CharacterKey('y').Left);

        AssertKey(arcadia, 'q', shift: null, altGr: "@");
        AssertKey(arcadia, 'e', shift: null, altGr: "€");
        AssertKey(arcadia, '7', shift: "/", altGr: "{");
        AssertKey(arcadia, '+', shift: "*", altGr: "~");
        AssertKey(arcadia, 'a', shift: null, altGr: null);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T129-6")]
    public async Task R3_switches_to_US_UK_FR_and_ES_each_with_its_real_keys()
    {
        using var arcadia = await OpenSearchAsync();
        var keyboard = arcadia.Keyboard();
        var altGrKey = () => keyboard.Keys.Single(k => k.Key.Kind == BoardKeyKind.AltGr);

        // AltGr on while switching to US goes off; US has no AltGr: greyed out, RB does nothing.
        arcadia.Hold(UiAction.NextTab, Tap);
        ChooseLayout(arcadia, "US");
        Assert.Equal("`1234567890-=", arcadia.Row(0));
        Assert.Equal("qwertyuiop[]\\", arcadia.Row(1));
        Assert.Equal("zxcvbnm,./", arcadia.Row(3));
        Assert.DoesNotContain(keyboard.Keys, k => k.IsCharacter && k.Label == "<");
        Assert.Equal(KeyLevel.Normal, keyboard.Level);
        Assert.True(altGrKey().IsUnavailable);
        arcadia.Hold(UiAction.NextTab, Tap);
        Assert.Equal(KeyLevel.Normal, keyboard.Level);
        Assert.Equal("~", arcadia.CharacterKey('`').ShiftHint);

        ChooseLayout(arcadia, "UK");
        Assert.Contains(keyboard.Keys, k => k.IsCharacter && k.Label == "#");
        Assert.Contains(keyboard.Keys, k => k.IsCharacter && k.Label == "\\");
        Assert.Equal("€", arcadia.CharacterKey('4').AltGrHint);
        Assert.False(altGrKey().IsUnavailable);

        ChooseLayout(arcadia, "FR");
        Assert.StartsWith("azerty", arcadia.Row(1));
        Assert.StartsWith("²&é\"'(", arcadia.Row(0));

        ChooseLayout(arcadia, "ES");
        Assert.StartsWith("º", arcadia.Row(0));
        Assert.EndsWith("¡", arcadia.Row(0));
        Assert.Contains("ñ", arcadia.Row(2));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T110-11")]
    public async Task The_keyboards_arrows_move_the_caret_where_typing_and_erasing_then_act_also_from_a_real_keyboard()
    {
        using var arcadia = await OpenSearchAsync();
        var search = arcadia.Main.Search!;
        arcadia.TypeOnKeys("hollow");

        arcadia.GoTo("←").Press(UiAction.Accept, times: 3);
        Assert.Equal("hol", search.TextBeforeCaret);
        Assert.Equal("low", search.TextAfterCaret);

        arcadia.TypeOnKeys("x");
        Assert.Equal("holxlow", search.Text);
        arcadia.GoTo("⌫").Press(UiAction.Accept);
        Assert.Equal("hollow", search.Text);
        Assert.Equal("hol", search.TextBeforeCaret);

        arcadia.Main.TypeInSearch("y");
        Assert.Equal("holylow", search.Text);

        arcadia.GoTo("↑").Press(UiAction.Accept);
        Assert.Equal(string.Empty, search.TextBeforeCaret);
        arcadia.GoTo("↓").Press(UiAction.Accept);
        Assert.Equal("holylow", search.TextBeforeCaret);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T110-13")]
    public async Task With_AltGr_on_left_of_the_first_key_with_a_character_leads_to_shift_and_left_of_the_tall_enter_to_the_tilde()
    {
        using var arcadia = await OpenSearchAsync();
        arcadia.Hold(UiAction.NextTab, Tap);

        arcadia.GoTo("²").Press(UiAction.Left);
        Assert.Equal("⇧", arcadia.FocusedLabel());

        // The tall enter reached from the erase key above it…
        arcadia.GoTo("⌫").Press(UiAction.Down);
        Assert.Equal("↵", arcadia.FocusedLabel());
        arcadia.Press(UiAction.Left);
        Assert.Equal("~", arcadia.FocusedLabel());

        // …and from the shift key below it.
        var rightShift = arcadia.Keyboard().Keys.Where(k => k.Key.Kind == BoardKeyKind.Shift).MaxBy(k => k.Key.Left)!;
        arcadia.GoTo(rightShift).Press(UiAction.Up);
        Assert.Equal("↵", arcadia.FocusedLabel());
        arcadia.Press(UiAction.Left);
        Assert.Equal("~", arcadia.FocusedLabel());
    }

    /// <summary>X with the controller: the search opens with the on-screen keyboard out.</summary>
    private static async Task<ArcadiaDriver> OpenSearchAsync()
    {
        var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Search);
        Assert.True(arcadia.Main.Search!.IsKeyboardOut);
        return arcadia;
    }

    /// <summary>R3 opens the layout menu; the D-pad walks to the layout and A picks it.</summary>
    private static void ChooseLayout(ArcadiaDriver arcadia, string code)
    {
        var menu = arcadia.Keyboard().LayoutMenu;
        arcadia.Press(UiAction.KeyboardLayout);
        Assert.True(menu.IsOpen);
        arcadia.Press(UiAction.Up, times: menu.Items.Count);
        for (var i = 0; i < menu.Items.Count && menu.Items.Single(item => item.IsFocused).Layout.Code != code; i++)
        {
            arcadia.Press(UiAction.Down);
        }

        arcadia.Press(UiAction.Accept);
        Assert.False(menu.IsOpen);
        Assert.Equal(code, arcadia.Keyboard().Layout.Code);
    }

    private static void AssertKey(ArcadiaDriver arcadia, char normal, string? shift, string? altGr)
    {
        var key = arcadia.CharacterKey(normal);
        Assert.Equal(normal.ToString(), key.Label);
        Assert.Equal(shift, key.ShiftHint);
        Assert.Equal(altGr, key.AltGrHint);
    }
}
