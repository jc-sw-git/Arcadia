using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.Core.Search;
using Arcadia.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>Arcadia's on-screen keyboard (#110, #129) as drawn: clicked with the mouse, and the mouse mode's own window.</summary>
public sealed class OnScreenKeyboardViewTests
{
    [AvaloniaFact]
    [Trait("Testfall", "T129-7")]
    public async Task A_click_on_a_key_types_the_character_shown_and_clicks_on_shift_and_AltGr_switch_the_level()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        arcadia.Press(UiAction.Search);
        var search = arcadia.Arcadia.Main.Search!;
        var keyboard = search.Keyboard;
        var q = keyboard.Keys.Single(key => key.IsCharacter && key.Key.Cap!.Normal == 'q');
        var shift = keyboard.Keys.Where(key => key.Key.Kind == BoardKeyKind.Shift).MinBy(key => key.Key.Left)!;
        var altGr = keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.AltGr);

        arcadia.Click(KeyButton(arcadia, q));
        Assert.Equal("q", search.Text);

        arcadia.Click(KeyButton(arcadia, shift));
        Assert.Contains("on", KeyButton(arcadia, shift).Classes);
        Assert.Equal("Q", Drawn(arcadia, q));
        arcadia.Click(KeyButton(arcadia, q));
        Assert.Equal("qQ", search.Text);
        arcadia.Click(KeyButton(arcadia, shift));
        Assert.Equal("q", Drawn(arcadia, q));

        arcadia.Click(KeyButton(arcadia, altGr));
        Assert.Contains("on", KeyButton(arcadia, altGr).Classes);
        Assert.Equal("@", Drawn(arcadia, q));
        arcadia.Click(KeyButton(arcadia, q));
        Assert.Equal("qQ@", search.Text);
        arcadia.Click(KeyButton(arcadia, altGr));
        Assert.Equal("q", Drawn(arcadia, q));
        arcadia.Click(KeyButton(arcadia, q));
        Assert.Equal("qQ@q", search.Text);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T110-4")]
    public void In_the_mouse_mode_R3_opens_the_layout_menu_top_right_A_picks_B_closes_only_it_and_US_has_the_ANSI_keys_with_AltGr_greyed()
    {
        using var mouseMode = MouseModeKeyboard.Show();
        var keyboard = mouseMode.Keyboard;
        var german = Shape(keyboard);
        Assert.True(german.TallEnter, "Deutsch: hohes Enter");

        // R3: the menu at the keyboard's top right.
        mouseMode.Press(KeyboardButton.Layout);
        Assert.True(keyboard.LayoutMenu.IsOpen);
        var menu = Assert.Single(mouseMode.Window.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("keyboardLayoutMenu") && border.IsEffectivelyVisible);
        var board = mouseMode.Bounds(mouseMode.Window.GetVisualDescendants().OfType<Views.OnScreenKeyboardView>().Single());
        Assert.Equal(board.Right, mouseMode.Bounds(menu).Right, tolerance: 2);
        Assert.Equal(board.Top, mouseMode.Bounds(menu).Top, tolerance: 2);

        // B closes only the menu.
        mouseMode.Press(KeyboardButton.Hide);
        Assert.False(keyboard.LayoutMenu.IsOpen);
        Assert.False(mouseMode.Typed.IsHidden);
        Assert.True(mouseMode.Window.IsVisible);

        // The D-pad to US, A picks it.
        mouseMode.Press(KeyboardButton.Layout).Press(KeyboardButton.Down).Press(KeyboardButton.Type);
        Assert.False(keyboard.LayoutMenu.IsOpen);
        Assert.Equal("US", keyboard.Layout.Code);
        Assert.Empty(mouseMode.Typed.Text);

        // ANSI: a wide Enter in the middle row with \ above it, a wide left shift, AltGr greyed.
        var us = Shape(keyboard);
        Assert.False(us.TallEnter, "US: kein hohes Enter");
        Assert.Equal(2, us.EnterRow);
        Assert.True(us.EnterWidth > 1.5 * BoardKeyViewModel.Pitch, $"Enter {us.EnterWidth} breit");
        Assert.Equal("\\", us.LastOfRowAboveEnter);
        Assert.True(us.LeftShiftWidth > german.LeftShiftWidth + (BoardKeyViewModel.Pitch / 2), $"⇧ links {us.LeftShiftWidth} statt {german.LeftShiftWidth}");
        var altGr = mouseMode.KeyButton(keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.AltGr));
        Assert.Contains("unavailable", altGr.Classes);
        Assert.Equal(ArcadiaWindow.ThemeResource<ISolidColorBrush>("TextFaint").Color, ((ISolidColorBrush)altGr.Foreground!).Color);
    }

    private static Button KeyButton(ArcadiaWindow arcadia, BoardKeyViewModel key) => arcadia.Window.GetVisualDescendants().OfType<Button>()
        .First(button => button.Classes.Contains("boardKey") && button.DataContext == key && button.IsEffectivelyVisible);

    /// <summary>The character drawn large on <paramref name="key"/>.</summary>
    private static string? Drawn(ArcadiaWindow arcadia, BoardKeyViewModel key) => KeyButton(arcadia, key).GetVisualDescendants().OfType<TextBlock>()
        .First(text => text.IsEffectivelyVisible && !text.Classes.Contains("keyHint") && !string.IsNullOrEmpty(text.Text)).Text;

    private static (bool TallEnter, int EnterRow, double EnterWidth, string LastOfRowAboveEnter, double LeftShiftWidth) Shape(OnScreenKeyboardViewModel keyboard)
    {
        var enter = keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Enter);
        var aboveEnter = keyboard.Keys.Where(key => key.IsCharacter && key.Key.Row == enter.Key.Row - 1).MaxBy(key => key.Key.Left)!;
        var leftShift = keyboard.Keys.Where(key => key.Key.Kind == BoardKeyKind.Shift).MinBy(key => key.Key.Left)!;
        return (enter.IsTallEnter, enter.Key.LastRow, enter.Width, aboveEnter.Label, leftShift.Width);
    }
}
