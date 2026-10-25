using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>Arcadia with a real keyboard and mouse (#127): keys, clicks, and the hints that follow the input in use.</summary>
public sealed class KeyboardAndMouseTests
{
    private static readonly TimeSpan HoldForDetails = TimeSpan.FromSeconds(1.1);

    [AvaloniaFact]
    [Trait("TestCase", "T127-7")]
    public async Task The_hints_show_controller_buttons_after_a_button_and_keys_after_a_key_or_click_but_not_after_a_mouse_move()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();

        AssertController(arcadia);
        Assert.Equal(["A Spielen", "A halten: Details", "⧉ Optionen", "Y Ansicht", "LB RB Launcher", "LT RT Seite"], arcadia.FooterHints());

        arcadia.Key(Key.D);
        AssertKeyboard(arcadia);
        Assert.StartsWith("Leertaste / Doppelklick ", arcadia.FooterHints()[0], StringComparison.Ordinal);
        Assert.NotNull(arcadia.ButtonWithTip("Vorheriger Launcher").GetVisualDescendants().OfType<TextBlock>().SingleOrDefault(text => text.Text == "⇧ Q"));

        arcadia.Press(UiAction.Right);
        AssertController(arcadia);

        // Only moving the mouse doesn't switch; a click does.
        arcadia.MouseMove(new Point(700, 500)).MouseMove(new Point(900, 600));
        AssertController(arcadia);
        arcadia.Click(arcadia.Tile(arcadia.Arcadia.PageGames[3]));
        AssertKeyboard(arcadia);

        // The detail page and a dialog follow too.
        arcadia.Press(UiAction.ViewOptions);
        AssertController(arcadia);
        arcadia.Key(Key.Escape).Arcadia.PressDown(UiAction.Accept).Wait(HoldForDetails).LetGo(UiAction.Accept);
        arcadia.Settle();
        AssertController(arcadia);
        arcadia.Key(Key.D);
        AssertKeyboard(arcadia);

        // Every glyph has its darker lower edge.
        using var frame = arcadia.Capture();
        foreach (var glyph in ArcadiaWindow.Glyphs(arcadia.Window).Where(glyph => !glyph.Classes.Contains("dark")).Take(12))
        {
            var bounds = arcadia.Bounds(glyph);
            var face = frame.Brightness(new Point(bounds.Center.X, bounds.Top + 3));
            var edge = frame.Brightness(new Point(bounds.Center.X, bounds.Bottom - 1));
            Assert.True(edge < face - 3, $"Glyphe {bounds}: Unterkante {edge:0} nicht dunkler als {face:0}");
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T127-8")]
    [Trait("TestCase", "G-10")]
    [Trait("TestCase", "T10-4")]
    public async Task The_keyboard_plays_like_the_controller()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var main = arcadia.Arcadia.Main;

        // WASD and the arrows move.
        arcadia.Key(Key.D).Key(Key.S);
        Assert.Equal(1 + MainViewModel.GridColumns, FocusedIndex(main));
        arcadia.Key(Key.W).Key(Key.A);
        Assert.Equal(0, FocusedIndex(main));
        arcadia.Key(Key.Right).Key(Key.Down).Key(Key.Up).Key(Key.Left);
        Assert.Equal(0, FocusedIndex(main));

        // Space is A: tapped it starts the game on the release, held it opens the details (with the ring after 0.2 s).
        arcadia.KeyDown(Key.Space);
        Assert.Empty(arcadia.Arcadia.Starts.Keys);
        arcadia.KeyUp(Key.Space);
        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Starts.Keys.Count == 1, "Leertaste startet das Spiel");
        arcadia.KeyDown(Key.Space);
        arcadia.Arcadia.Wait(TimeSpan.FromSeconds(0.3));
        Assert.True(arcadia.Settle().Window.GetVisualDescendants().OfType<HoldRing>().Single().IsEffectivelyVisible, "Ring nach 0,2 s");
        arcadia.Arcadia.Wait(TimeSpan.FromSeconds(0.3));
        arcadia.KeyUp(Key.Space);
        Assert.False(main.IsDetailOpen, "Loslassen vor 1 s bricht ab");
        Assert.Single(arcadia.Arcadia.Starts.Keys);
        arcadia.KeyDown(Key.Space);
        arcadia.Arcadia.Wait(HoldForDetails);
        Assert.True(main.IsDetailOpen, "Gehalten öffnet die Leertaste die Details");
        arcadia.KeyUp(Key.Space);

        // On the detail page: Q/E the game, 1 … 0 the launcher, Esc and Backspace back.
        var shown = arcadia.Arcadia.Detail!.Current.Tile.Title;
        arcadia.Key(Key.E);
        Assert.NotEqual(shown, arcadia.Arcadia.Detail!.Current.Tile.Title);
        arcadia.Key(Key.Q);
        Assert.Equal(shown, arcadia.Arcadia.Detail!.Current.Tile.Title);
        arcadia.Key(Key.D2);
        Assert.Equal("Xbox", arcadia.Arcadia.SelectedTab?.Name);
        arcadia.Key(Key.Back);
        Assert.False(main.IsDetailOpen);
        arcadia.Key(Key.D1);
        Assert.Equal("GOG Galaxy", arcadia.Arcadia.SelectedTab?.Name);

        // Q/E, Page Up/Down the pages; Shift+Q/E and Ctrl+(Shift+)Tab the launchers; 1 … 0 too.
        arcadia.Key(Key.E);
        Assert.Equal("2", CurrentPage(main));
        arcadia.Key(Key.Q);
        Assert.Equal("1", CurrentPage(main));
        arcadia.Key(Key.PageDown);
        Assert.Equal("2", CurrentPage(main));
        arcadia.Key(Key.PageUp);
        Assert.Equal("1", CurrentPage(main));
        arcadia.Key(Key.E, KeyModifiers.Shift);
        Assert.Equal("Xbox", arcadia.Arcadia.SelectedTab?.Name);
        arcadia.Key(Key.Q, KeyModifiers.Shift);
        Assert.Equal("GOG Galaxy", arcadia.Arcadia.SelectedTab?.Name);
        arcadia.Key(Key.Tab, KeyModifiers.Control);
        Assert.Equal("Xbox", arcadia.Arcadia.SelectedTab?.Name);
        arcadia.Key(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.Equal("GOG Galaxy", arcadia.Arcadia.SelectedTab?.Name);
        arcadia.Key(Key.D3);
        Assert.Equal("Amazon Games", arcadia.Arcadia.SelectedTab?.Name);
        arcadia.Key(Key.D1);

        // R options, V view, Esc closes each.
        arcadia.Key(Key.R);
        Assert.True(main.IsGameMenuOpen);
        arcadia.Key(Key.Escape);
        Assert.False(main.IsGameMenuOpen);
        arcadia.Key(Key.V);
        Assert.True(main.IsViewOptionsOpen);
        arcadia.Key(Key.Escape);
        Assert.False(main.IsViewOptionsOpen);

        // Enter and F2 the settings, Enter again closes them; held, Enter opens them only once.
        arcadia.Key(Key.Enter);
        Assert.True(main.IsSettingsOpen);
        arcadia.Key(Key.Enter);
        Assert.False(main.IsSettingsOpen);
        arcadia.Key(Key.F2);
        Assert.True(main.IsSettingsOpen);
        arcadia.Key(Key.Escape);
        arcadia.KeyDown(Key.Enter).KeyDown(Key.Enter).KeyDown(Key.Enter).KeyUp(Key.Enter);
        Assert.True(main.IsSettingsOpen, "Gehaltenes Enter öffnet die Einstellungen nur einmal");
        arcadia.Key(Key.Escape);

        // F the search: the letters of the keys above type there, Enter starts the hit.
        arcadia.Key(Key.F);
        Assert.True(main.IsSearchShown);
        arcadia.Key(Key.W).Key(Key.A).Key(Key.S).Key(Key.D).Key(Key.Q).Key(Key.E).Key(Key.R).Key(Key.V).Key(Key.F);
        Assert.Equal("wasdqervf", arcadia.Arcadia.Search!.Text);
        arcadia.Key(Key.Back, times: 9);
        Assert.Equal(string.Empty, arcadia.Arcadia.Search!.Text);
        // Not the game Space started above: that one is starting, and A brings it to the front instead.
        var other = arcadia.Arcadia.PageGames.Skip(1).First(tile => tile.Game.IsInstalled).Title;
        foreach (var letter in other.ToLowerInvariant().Where(char.IsLetter).Take(6))
        {
            arcadia.Key(Enum.Parse<Key>(letter.ToString(), ignoreCase: true));
        }

        var hit = arcadia.Arcadia.Search!.FocusedResult!.Game.Key;
        arcadia.Key(Key.Enter);
        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Starts.Keys.Count == 2, "Enter startet den Treffer");
        Assert.Equal(hit, arcadia.Arcadia.Starts.Keys[^1]);
        arcadia.Key(Key.Escape).Key(Key.Escape);
        Assert.False(main.IsSearchShown);

        // F11 full screen and back.
        arcadia.Key(Key.F11);
        Assert.Equal(WindowState.FullScreen, arcadia.Window.WindowState);
        arcadia.Key(Key.F11);
        Assert.NotEqual(WindowState.FullScreen, arcadia.Window.WindowState);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-11")]
    [Trait("TestCase", "T10-4")]
    public async Task The_mouse_focuses_starts_holds_for_details_and_clicks_every_button_of_the_library()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var main = arcadia.Arcadia.Main;

        arcadia.Click(arcadia.Tile(arcadia.Arcadia.PageGames[2]));
        Assert.Equal(2, FocusedIndex(main));
        Assert.Empty(arcadia.Arcadia.Starts.Keys);
        arcadia.DoubleClick(arcadia.Tile(arcadia.Arcadia.PageGames[2]));
        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Starts.Keys.Count == 1, "Doppelklick startet das Spiel");
        Assert.Equal(arcadia.Arcadia.PageGames[2].Game.Key, arcadia.Arcadia.Starts.Keys[0]);

        // The left button held a second opens the details, with the blue ring around the preview meanwhile.
        var cover = arcadia.CentreOf(arcadia.Tile(arcadia.Arcadia.PageGames[4]));
        arcadia.MouseDown(cover);
        arcadia.Arcadia.Wait(TimeSpan.FromSeconds(0.3));
        var ring = arcadia.Settle().Window.GetVisualDescendants().OfType<HoldRing>().Single();
        // Its filling is a keyframe animation, which doesn't run headless; that it fills stays with the manual test.
        Assert.True(ring.IsEffectivelyVisible, "Ring um die Vorschau");
        Assert.Equal(ArcadiaWindow.ThemeResource<ISolidColorBrush>("HoldRing").Color, ((ISolidColorBrush)ring.Stroke!).Color);
        arcadia.Arcadia.Wait(TimeSpan.FromSeconds(0.8));
        Assert.True(main.IsDetailOpen);
        arcadia.MouseUp(cover);
        Assert.Single(arcadia.Arcadia.Starts.Keys);
        arcadia.Click(arcadia.OneWithText("Bibliothek"));

        // LB/RB, LT/RT, page numbers, tabs, "Ansicht", search and settings.
        arcadia.Click(arcadia.ButtonWithTip("Nächster Launcher"));
        Assert.Equal("Xbox", arcadia.Arcadia.SelectedTab?.Name);
        arcadia.Click(arcadia.ButtonWithTip("Vorheriger Launcher"));
        Assert.Equal("GOG Galaxy", arcadia.Arcadia.SelectedTab?.Name);
        arcadia.Click(arcadia.ButtonWithTip("Nächste Seite"));
        Assert.Equal("2", CurrentPage(main));
        arcadia.Click(arcadia.ButtonWithTip("Vorherige Seite"));
        Assert.Equal("1", CurrentPage(main));
        arcadia.Click(PageButton(arcadia, "2"));
        Assert.Equal("2", CurrentPage(main));
        arcadia.Click(arcadia.Showing(main.LauncherTabs.Items[2]));
        Assert.Equal("Amazon Games", arcadia.Arcadia.SelectedTab?.Name);
        arcadia.Click(arcadia.Window.GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible && button.Command == main.OpenViewOptionsCommand));
        Assert.True(main.IsViewOptionsOpen);
        arcadia.Key(Key.Escape);
        arcadia.Click(arcadia.ButtonWithTip("Suchen"));
        Assert.True(main.IsSearchShown);
        arcadia.Key(Key.Escape).Key(Key.Escape);
        arcadia.Click(arcadia.ButtonWithTip("Einstellungen"));
        Assert.True(main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T8-1")]
    public async Task A_tab_chosen_by_click_is_the_same_after_a_restart()
    {
        using var first = await ArcadiaWindow.ShowAsync();
        first.Click(first.Showing(first.Arcadia.Main.LauncherTabs.Items[1])).Click(first.ButtonWithTip("Nächste Seite"));

        using var restarted = await first.RestartAsync();
        var main = restarted.Arcadia.Main;
        Assert.Equal("Xbox", restarted.Arcadia.SelectedTab?.Name);
        Assert.Equal("1", CurrentPage(main));
        Assert.Equal(0, FocusedIndex(main));
        Assert.Contains("focused", restarted.Tile(restarted.Arcadia.PageGames[0]).Classes);
    }

    private static void AssertController(ArcadiaWindow arcadia)
    {
        var glyphs = ArcadiaWindow.Glyphs(arcadia.Window);
        Assert.NotEmpty(glyphs);
        Assert.All(glyphs, glyph => Assert.False(glyph.Classes.Contains("kbd"), "Nach einer Controllertaste keine Tasten"));
        Assert.Contains(glyphs, glyph => glyph.Classes.Contains("round") && glyph.CornerRadius.TopLeft >= glyph.Bounds.Height / 2 - 0.5);
    }

    private static void AssertKeyboard(ArcadiaWindow arcadia)
    {
        var glyphs = ArcadiaWindow.Glyphs(arcadia.Window);
        Assert.NotEmpty(glyphs);
        Assert.All(glyphs, glyph => Assert.True(glyph.Classes.Contains("kbd") || glyph.GetVisualAncestors().Any(a => a.Classes.Contains("keys")), "Nach einer Taste eckige Tasten"));
        Assert.All(glyphs, glyph => Assert.True(glyph.CornerRadius.TopLeft < glyph.Bounds.Height / 2 - 1, "Tasten sind eckig"));
    }

    private static Button PageButton(ArcadiaWindow arcadia, string label) => arcadia.Window.GetVisualDescendants().OfType<Button>()
        .Single(button => button.Classes.Contains("page") && button.IsEffectivelyVisible && Equals(button.Content, label));

    private static int FocusedIndex(MainViewModel main) => main.FocusedGame is { } focused ? main.PageGames.IndexOf(focused) : -1;

    private static string? CurrentPage(MainViewModel main) => main.PageButtons.SingleOrDefault(button => button.IsCurrent)?.Label;
}
