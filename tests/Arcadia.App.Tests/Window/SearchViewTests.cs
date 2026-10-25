using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Search;
using Arcadia.Core.Launchers;
using Arcadia.App.ViewModels;
using Arcadia.Core.Search;
using Arcadia.Input;
using Arcadia.Launchers.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>The search (#12) as drawn: its page, the chips, the on-screen keyboard's drawer and the hits, with every input.</summary>
public sealed class SearchViewTests
{
    private static readonly DateTimeOffset Installed = ArcadiaDriver.Now.AddDays(-30);

    private static readonly TimeSpan HoldForDetails = TimeSpan.FromSeconds(1.1);

    /// <summary>105 games called "Hollow …", 40 of them in GOG and 65 in Xbox, and two others.</summary>
    private static readonly IReadOnlyList<LauncherSpec> Hollows =
    [
        LauncherSpec.With(KnownLaunchers.Gog, [.. Many(KnownLaunchers.Gog, "Hollow Knight", 40), Installed(KnownLaunchers.Gog, "Iron Harbor", Installed)]),
        LauncherSpec.With(KnownLaunchers.Xbox, [.. Many(KnownLaunchers.Xbox, "Hollow Drift", 65), Installed(KnownLaunchers.Xbox, "Silent Tides", Installed)]),
        LauncherSpec.With(KnownLaunchers.Amazon, Installed(KnownLaunchers.Amazon, "Ember Lake", Installed)),
    ];

    [AvaloniaFact]
    [Trait("Testfall", "T12-1")]
    public async Task X_opens_the_search_with_its_head_field_chips_empty_state_keyboard_and_controller_legend_F_and_the_lens_without_the_keyboard()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Hollows);
        var main = arcadia.Arcadia.Main;

        arcadia.Press(UiAction.Search).WaitForTransitions();
        var page = Page(arcadia);
        var head = (arcadia.Bounds(One(arcadia, page, "ARCADIA")), arcadia.Bounds(One(arcadia, page, "Suche")), arcadia.Bounds(One(arcadia, page, "Schließen")));
        Assert.Equal(head.Item1.Center.Y, head.Item2.Center.Y, tolerance: 2);
        Assert.True(head.Item2.Left > head.Item1.Right, "„Suche“ steht rechts neben „ARCADIA“");
        Assert.True(head.Item3.Left > ArcadiaWindow.Width / 2, "„Schließen“ steht rechts im Kopf");
        Assert.Equal(head.Item1.Center.Y, head.Item3.Center.Y, tolerance: 4);

        // The field shows only its placeholder, no hint at the keywords.
        var placeholder = One(arcadia, page, "Spiele suchen …");
        var field = placeholder.GetVisualAncestors().OfType<Border>().First(border => border.Height == 76);
        Assert.DoesNotContain(Texts(field), text => text.Contains('/', StringComparison.Ordinal));
        Assert.True(arcadia.Bounds(One(arcadia, page, "Suche in allen Launchern")).Top > arcadia.Bounds(placeholder).Bottom);

        // One row of chips, each launcher with its keyword, the sort order with L3 on the right.
        var chips = new[] { "Alle Launcher", "GOG Galaxy", "Xbox", "Amazon Games" }.Select(name => arcadia.Bounds(One(arcadia, page, name))).ToList();
        Assert.All(chips, chip => Assert.Equal(chips[0].Center.Y, chip.Center.Y, tolerance: 2));
        Assert.All(new[] { "/gog", "/xbox", "/amazon" }, keyword => One(arcadia, page, keyword));
        var sort = arcadia.Bounds(One(arcadia, page, "Sortiert nach Relevanz"));
        Assert.Equal(chips[0].Center.Y, sort.Center.Y, tolerance: 4);
        Assert.True(sort.Left > chips[^1].Right, "Sortierung rechts der Chips");
        Assert.Contains(GlyphTexts(arcadia.Named<Button>("SearchSortButton")), glyph => glyph == "L3");

        // In the middle the empty state, its glow without a rim.
        var empty = One(arcadia, page, "Tippe einen Suchbegriff ein");
        Assert.Equal(ArcadiaWindow.Width / 2, arcadia.Bounds(empty).Center.X, tolerance: 2);
        AssertGlowWithoutRim(arcadia);

        // The keyboard is out, German, with the focus on its starting key, and the legend names the controller's buttons.
        var keyboard = main.Search!.Keyboard;
        Assert.True(main.Search.IsKeyboardOut);
        Assert.Equal("DE", keyboard.Layout.Code);
        var focusedKey = KeyButton(arcadia, keyboard.FocusedKey);
        Assert.Contains("focused", focusedKey.Classes);
        Assert.Equal("a", keyboard.FocusedKey.Label);
        Assert.Equal(["D-Pad Tastatur", "A Eingeben", "B Tastatur einfahren", "RS Treffer wählen", "LT RT Weitere Treffer", "≡ Spielen", "≡ halten: Details", "⧉ Optionen"], Legend(arcadia));

        // F opens it without the keyboard, the legend names keys and the mouse.
        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.False(main.IsSearchShown);
        arcadia.Key(Key.F);
        Assert.True(main.IsSearchShown);
        Assert.False(main.Search!.IsKeyboardOut);
        Assert.Equal(["↑ ↓ ← → Treffer wählen", "Bild ↑ ↓ / Mausrad Weitere Treffer", "Enter / Doppelklick Spielen", "Enter / Klick halten: Details", "Menütaste / Rechtsklick Optionen", "Esc Schließen"], Legend(arcadia));

        // So does the lens in the header.
        arcadia.Key(Key.Escape);
        Assert.False(main.IsSearchShown);
        arcadia.Click(arcadia.ButtonWithTip("Suchen"));
        Assert.True(main.IsSearchShown);
        Assert.False(main.Search!.IsKeyboardOut);
        Assert.Equal("Esc Schließen", Legend(arcadia)[^1]);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T12-4")]
    public async Task A_typed_prefix_searches_only_its_launchers_the_chips_follow_and_an_unknown_one_is_named_in_orange()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Hollows);
        var main = arcadia.Arcadia.Main;
        arcadia.Key(Key.F).Type("/gog holl");
        var search = main.Search!;
        var page = Page(arcadia);

        Assert.All(search.Hits, hit => Assert.Equal("gog", hit.LauncherId));
        Assert.Equal(40, search.Hits.Count);
        Assert.Equal(["GOG Galaxy"], ChipsOn(search));
        One(arcadia, page, "Per Kürzel gefiltert: GOG Galaxy");

        Erase(arcadia).Type("/gog-xbox holl");
        Assert.Equal(105, search.Hits.Count);
        Assert.Equal(["GOG Galaxy", "Xbox"], ChipsOn(search));
        One(arcadia, page, "Per Kürzel gefiltert: GOG Galaxy, Xbox");

        Erase(arcadia).Type("/xyz holl");
        var unknown = One(arcadia, page, "· Unbekanntes Kürzel: /xyz");
        Assert.Equal(ArcadiaWindow.ThemeResource<Avalonia.Media.ISolidColorBrush>("Warning").Color, ((Avalonia.Media.ISolidColorBrush)((TextBlock)unknown).Foreground!).Color);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T12-5")]
    public async Task Chips_and_the_L3_filter_switch_launchers_the_last_stays_on_and_Alle_Launcher_switches_all_on_a_typed_prefix_is_rewritten()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Hollows);
        var main = arcadia.Arcadia.Main;
        arcadia.Key(Key.F).Type("holl");
        var search = main.Search!;
        var page = Page(arcadia);

        // Clicks on the chips; without a prefix the text stays and the scope says what is searched.
        arcadia.Click(Chip(arcadia, "Xbox"));
        Assert.Equal(["GOG Galaxy", "Amazon Games"], ChipsOn(search));
        Assert.Equal("holl", search.Text);
        One(arcadia, page, "Suche in: GOG Galaxy, Amazon Games");
        arcadia.Click(Chip(arcadia, "GOG Galaxy")).Click(Chip(arcadia, "Amazon Games"));
        Assert.Equal(["Amazon Games"], ChipsOn(search));
        Assert.Empty(search.Hits);
        arcadia.Click(Chip(arcadia, "Alle Launcher"));
        Assert.Equal(["Alle Launcher", "GOG Galaxy", "Xbox", "Amazon Games"], ChipsOn(search, all: true));
        Assert.Equal(105, search.Hits.Count);

        // A typed prefix is rewritten by a chip, and "Alle Launcher" takes it away.
        Erase(arcadia).Type("/gog holl");
        arcadia.Click(Chip(arcadia, "Xbox"));
        Assert.Equal("/gog-xbox holl", search.Text);
        Assert.Equal(["GOG Galaxy", "Xbox"], ChipsOn(search));
        arcadia.Click(Chip(arcadia, "Alle Launcher"));
        Assert.Equal("holl", search.Text);

        // L3: the filter's launchers switch the same way.
        arcadia.Press(UiAction.Filter);
        Assert.True(main.IsViewOptionsOpen);
        arcadia.Click(FilterLauncher(arcadia, "Xbox"));
        Assert.Equal(["GOG Galaxy", "Amazon Games"], ChipsOn(search));
        arcadia.Click(FilterLauncher(arcadia, "GOG Galaxy")).Click(FilterLauncher(arcadia, "Amazon Games"));
        Assert.Equal(["Amazon Games"], ChipsOn(search));
        arcadia.Click(FilterLauncher(arcadia, "Alle Launcher"));
        Assert.Equal(["Alle Launcher", "GOG Galaxy", "Xbox", "Amazon Games"], ChipsOn(search, all: true));
    }

    [AvaloniaFact]
    [Trait("Testfall", "T12-6")]
    public async Task R3_opens_the_layout_menu_over_the_hits_A_picks_a_layout_B_and_R3_close_it_first_and_the_layout_stays_after_a_restart()
    {
        using var first = await ArcadiaWindow.ShowAsync(Hollows);
        first.Press(UiAction.Search);
        first.Arcadia.Main.TypeInSearch("holl");
        first.Settle();
        var keyboard = first.Arcadia.Main.Search!.Keyboard;

        first.Press(UiAction.KeyboardLayout).WaitForTransitions();
        var menu = LayoutMenu(first);
        One(first, menu, "TASTATURLAYOUT");
        Assert.Equal(["DE", "US", "UK", "FR", "ES"], keyboard.LayoutMenu.Items.Select(item => item.Layout.Code));
        Assert.All(keyboard.LayoutMenu.Items.Select(item => item.Layout.Code), code => One(first, menu, code));
        first.AssertOnTop(menu);
        Assert.Single(menu.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>(), path => path.IsEffectivelyVisible && path.GetVisualAncestors().OfType<Button>().Any(b => b.Classes.Contains("selected")));

        // D-pad to US, A: the keys change at once and the menu closes.
        Assert.StartsWith("qwertz", DrawnRow(first, keyboard, 1), StringComparison.Ordinal);
        first.Press(UiAction.Down).Press(UiAction.Accept);
        Assert.False(keyboard.LayoutMenu.IsOpen);
        Assert.Equal("US", keyboard.Layout.Code);
        Assert.StartsWith("qwerty", DrawnRow(first, keyboard, 1), StringComparison.Ordinal);

        // B and R3 close the menu first, the search stays.
        first.Press(UiAction.KeyboardLayout).Press(UiAction.Back);
        Assert.False(keyboard.LayoutMenu.IsOpen);
        Assert.True(first.Arcadia.Main.IsSearchShown);
        Assert.True(first.Arcadia.Main.Search!.IsKeyboardOut);
        first.Press(UiAction.KeyboardLayout).Press(UiAction.KeyboardLayout);
        Assert.False(keyboard.LayoutMenu.IsOpen);

        // A click on the globe key opens it too.
        first.Click(KeyButton(first, keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Layout)));
        Assert.True(keyboard.LayoutMenu.IsOpen);
        first.Press(UiAction.Back);

        using var restarted = await first.RestartAsync();
        restarted.Press(UiAction.Search);
        Assert.Equal("US", restarted.Arcadia.Main.Search!.Keyboard.Layout.Code);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T12-10")]
    public async Task A_real_keyboard_types_erases_moves_through_the_hits_starts_holds_for_details_and_closes_but_never_reaches_the_on_screen_keys()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Hollows);
        var main = arcadia.Arcadia.Main;
        arcadia.Press(UiAction.Search);
        var search = main.Search!;
        var keyBefore = search.Keyboard.FocusedKey;

        // A key puts the on-screen keyboard away; umlauts, capitals and spaces type.
        arcadia.Type("Ä Öl");
        Assert.False(search.IsKeyboardOut);
        Assert.Equal("Ä Öl", search.Text);
        One(arcadia, Page(arcadia), "Ä Öl");

        // Backspace erases, held it repeats.
        arcadia.Key(Key.Back);
        Assert.Equal("Ä Ö", search.Text);
        arcadia.KeyDown(Key.Back).KeyDown(Key.Back).KeyDown(Key.Back).KeyUp(Key.Back);
        Assert.Equal(string.Empty, search.Text);

        // The arrows move through the hits, not over the on-screen keys.
        arcadia.Type("Hollow");
        Assert.Same(search.Results[0], search.FocusedResult);
        arcadia.Key(Key.Right).Key(Key.Right).Key(Key.Down);
        Assert.Same(search.Results[2 + SearchViewModel.ResultColumns], search.FocusedResult);
        arcadia.Key(Key.Left);
        Assert.Same(search.Results[1 + SearchViewModel.ResultColumns], search.FocusedResult);
        Assert.Same(keyBefore, search.Keyboard.FocusedKey);
        Assert.False(search.IsKeyboardOut);

        // Enter starts the focused hit on its release, not on its press. The start is requested synchronously
        // (IsStarting); the launcher hears of it later.
        var tile = search.FocusedResult!;
        arcadia.KeyDown(Key.Enter);
        Assert.False(tile.IsRunningOrStarting, "Enter gedrückt startet noch nicht");
        arcadia.KeyUp(Key.Enter);
        Assert.True(tile.IsStarting, "Enter losgelassen startet den Treffer");
        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Starts.Keys.Count == 1, "Enter startet den Treffer");
        Assert.Equal(tile.Game.Key, arcadia.Arcadia.Starts.Keys[0]);

        // Esc closes the search; held, Enter opens the details of a hit before it is let go, and letting go starts nothing.
        arcadia.Key(Key.Escape);
        Assert.False(main.IsSearchShown);
        arcadia.Key(Key.F).Type("Hollow").Key(Key.Right);
        search = main.Search!;
        var other = search.FocusedResult!;
        arcadia.KeyDown(Key.Enter);
        arcadia.Arcadia.Wait(HoldForDetails);
        arcadia.Settle();
        Assert.True(main.IsDetailOpen, "Enter gehalten öffnet die Details schon vor dem Loslassen");
        Assert.Equal(other.Game.Key, main.Detail!.Current.Tile.Game.Key);
        arcadia.KeyUp(Key.Enter);
        Assert.True(main.IsDetailOpen);
        Assert.False(other.IsRunningOrStarting, "Loslassen nach dem Halten startet nichts");
        Assert.Single(arcadia.Arcadia.Starts.Keys);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T12-12")]
    public async Task The_keyboard_rolls_out_and_in_with_its_tab_on_its_top_edge_by_X_B_a_click_on_the_tab_or_a_real_key_and_the_hits_follow()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Hollows);
        var main = arcadia.Arcadia.Main;
        arcadia.Key(Key.F).Type("Hollow");
        var search = main.Search!;
        var drawer = Drawer(arcadia);

        // It rolls: the drawer's height follows a transition, softly in and out.
        Assert.Contains(drawer.Transitions ?? [], transition => transition is Avalonia.Animation.DoubleTransition { Property.Name: "Reveal", Duration.TotalMilliseconds: > 0 });

        // In: the hits stand in a grid, the D-pad moves through them; the tab sits on the legend.
        AssertDrawer(arcadia, isOut: false);
        Assert.Contains("grid", arcadia.Named<Control>("SearchResults").Classes);
        Assert.Equal(["↑ ↓ ← → Treffer wählen", "Bild ↑ ↓ / Mausrad Weitere Treffer", "Enter / Doppelklick Spielen", "Enter / Klick halten: Details", "Menütaste / Rechtsklick Optionen", "Esc Schließen"], Legend(arcadia));
        arcadia.Press(UiAction.Right).Press(UiAction.Down);
        Assert.Same(search.Results[1 + SearchViewModel.ResultColumns], search.FocusedResult);
        Assert.True(arcadia.Bounds(HitButton(arcadia, search.Results[1 + SearchViewModel.ResultColumns])).Top > arcadia.Bounds(HitButton(arcadia, search.Results[1])).Bottom, "Zweite Reihe unter der ersten");
        Assert.Equal(["D-Pad Treffer wählen", "A Spielen", "A halten: Details", "LT RT Weitere Treffer", "⧉ Optionen", "X Tastatur", "B Schließen"], Legend(arcadia));

        // A starts the focused hit, held it opens its details.
        var hit = search.FocusedResult!.Game.Key;
        arcadia.Press(UiAction.Accept);
        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Starts.Keys.Count == 1, "A startet den Treffer");
        Assert.Equal(hit, arcadia.Arcadia.Starts.Keys[0]);
        arcadia.Press(UiAction.Left);
        arcadia.Arcadia.Hold(UiAction.Accept, HoldForDetails);
        Assert.True(main.IsDetailOpen);
        Assert.Equal(search.Results[SearchViewModel.ResultColumns].Game.Key, main.Detail!.Current.Tile.Game.Key);
        arcadia.Press(UiAction.Search);
        Assert.True(main.IsSearchShown);
        search = main.Search!;
        AssertDrawer(arcadia, isOut: false);

        // X brings it out: one row of hits above it, the tab on its top edge with B.
        arcadia.Press(UiAction.Search);
        AssertDrawer(arcadia, isOut: true);
        Assert.DoesNotContain("grid", arcadia.Named<Control>("SearchResults").Classes);
        Assert.Equal("D-Pad Tastatur", Legend(arcadia)[0]);

        // B puts it in, X out again; a click on the tab both ways, keeping the mouse's legend.
        arcadia.Press(UiAction.Back);
        AssertDrawer(arcadia, isOut: false);
        arcadia.Press(UiAction.Search);
        AssertDrawer(arcadia, isOut: true);
        arcadia.Click(Tab(arcadia));
        AssertDrawer(arcadia, isOut: false);
        arcadia.Click(Tab(arcadia));
        AssertDrawer(arcadia, isOut: true);
        Assert.Equal(["Klick Eingeben", "Mausrad Weitere Treffer", "Doppelklick Spielen", "Klick halten: Details", "Rechtsklick Optionen", "Esc Tastatur einfahren"], Legend(arcadia));

        // Any key of a real keyboard puts it in.
        arcadia.Key(Key.Right);
        AssertDrawer(arcadia, isOut: false);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T12-16")]
    public async Task More_hits_than_fit_the_row_shows_faded_neighbours_and_turns_round_and_the_grid_scrolls_by_two_rows_and_stops_at_its_ends()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Hollows);
        var main = arcadia.Arcadia.Main;
        arcadia.Press(UiAction.Search);
        main.TypeInSearch("Hollow");
        arcadia.Settle();
        var search = main.Search!;
        var viewport = arcadia.Bounds(arcadia.Named<Control>("SearchViewport"));

        // The row: seven hits, the next ones peek in faded at the right, LT and RT beside them, RS named above them.
        Assert.Equal("1–7 von 105", search.RangeText);
        One(arcadia, Page(arcadia), "1–7 von 105");
        Assert.Contains(search.Slots, slot => slot.IsShown && slot.Tile is { } tile && arcadia.Bounds(HitButton(arcadia, tile)).Left > viewport.Center.X + (3.5 * 210));
        Assert.Equal(["LT", "RT"], TurnButtons(arcadia).Select(button => VisibleTexts(button).Single()));
        var stick = Assert.Single(Page(arcadia).GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("stickHint") && border.IsEffectivelyVisible);
        Assert.Equal(["RS", "Treffer wählen"], VisibleTexts(stick));
        Assert.True(arcadia.Bounds(stick).Bottom <= arcadia.Bounds(HitButton(arcadia, search.Results[0])).Top);
        AssertRowFadesToBackground(arcadia);

        // RS moves the focus one by one, the row goes with it.
        arcadia.Press(UiAction.SecondaryRight, times: 7);
        Assert.Same(search.Results[7], search.FocusedResult);
        Assert.Equal("2–8 von 105", search.RangeText);
        AssertRowFadesToBackground(arcadia);

        // LT/RT turn a whole row and round from the end to the start, and back.
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("9–15 von 105", search.RangeText);
        arcadia.Press(UiAction.PreviousPage, times: 2);
        Assert.Equal("1–7 von 105", search.RangeText);
        arcadia.Press(UiAction.PreviousPage);
        Assert.Equal("99–105 von 105", search.RangeText);
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("1–7 von 105", search.RangeText);

        // The scroll bar below the row: a click at its end scrolls there.
        var rowBar = arcadia.Named<ScrollBar>("HitsRowScrollBar");
        Assert.True(rowBar.IsEffectivelyVisible);
        Assert.True(arcadia.Bounds(rowBar).Top > arcadia.Bounds(HitButton(arcadia, search.Results[0])).Bottom, "Die Leiste steht unter der Reihe");
        Assert.True(arcadia.Bounds(rowBar).Width > 2 * arcadia.Bounds(rowBar).Height, "waagrecht");
        ClickTrackNearEnd(arcadia, rowBar);
        Assert.NotEqual("1–7 von 105", search.RangeText);
        DragThumb(arcadia, rowBar, toEnd: false);
        Assert.Equal("1–7 von 105", search.RangeText);

        // With the mouse the turn buttons show only their arrows.
        arcadia.MouseMove(new Point(10, 10)).Click(Tab(arcadia)).Click(Tab(arcadia));
        Assert.All(TurnButtons(arcadia), button => Assert.Empty(VisibleTexts(button)));

        // Keyboard in: a grid of two rows, faded above and below, LT/RT and Page Up/Down turn by two rows and stop at the ends.
        arcadia.Press(UiAction.Back);
        Assert.False(search.IsKeyboardOut);
        Assert.Equal("1–14 von 105", search.RangeText);
        var fades = Page(arcadia).GetVisualDescendants().OfType<Border>().Where(border => border.IsEffectivelyVisible && !border.IsHitTestVisible
            && border.Background is Avalonia.Media.LinearGradientBrush && arcadia.Bounds(border).Width > 1000).Select(border => arcadia.Bounds(border)).ToList();
        Assert.Equal(2, fades.Count);
        arcadia.Press(UiAction.PreviousPage);
        Assert.Equal("1–14 von 105", search.RangeText);
        arcadia.Press(UiAction.NextPage);
        Assert.Equal("15–28 von 105", search.RangeText);
        arcadia.Key(Key.PageDown);
        Assert.Equal("29–42 von 105", search.RangeText);
        arcadia.Key(Key.PageUp).Press(UiAction.NextPage, times: 10);
        Assert.Equal("92–105 von 105", search.RangeText);
        arcadia.Key(Key.PageDown);
        Assert.Equal("92–105 von 105", search.RangeText);

        // The wheel scrolls as far, a click on a hit cut off at the edge brings it in, the bar stands beside the grid.
        for (var notch = 0; notch < 14; notch++)
        {
            arcadia.Wheel(arcadia.Named<Control>("SearchViewport"), notches: 1);
        }

        Assert.Equal("1–14 von 105", search.RangeText);
        viewport = arcadia.Bounds(arcadia.Named<Control>("SearchViewport"));
        var gridBar = arcadia.Named<ScrollBar>("HitsGridScrollBar");
        Assert.True(gridBar.IsEffectivelyVisible);
        Assert.True(arcadia.Bounds(gridBar).Left > arcadia.Bounds(HitButton(arcadia, search.Results[SearchViewModel.ResultColumns - 1])).Right, "Die Leiste steht neben dem Raster");
        Assert.True(arcadia.Bounds(gridBar).Height > 2 * arcadia.Bounds(gridBar).Width, "senkrecht");
        var cutOff = search.Slots.Where(slot => slot.IsShown && slot.Tile is not null).Select(slot => slot.Tile!)
            .First(tile => arcadia.Bounds(HitButton(arcadia, tile)).Bottom > viewport.Bottom);
        arcadia.Click(new Point(arcadia.Bounds(HitButton(arcadia, cutOff)).Center.X, viewport.Bottom - 20));
        Assert.Equal("8–21 von 105", search.RangeText);
        Assert.Same(cutOff, search.FocusedResult);
        DragThumb(arcadia, gridBar, toEnd: true);
        Assert.Equal("92–105 von 105", search.RangeText);
    }

    [AvaloniaTheory]
    [Trait("Testfall", "T12-16")]
    [InlineData(1745, 1000, 1.0)]
    [InlineData(1536, 864, 1.25)]
    public async Task The_row_of_hits_fades_out_to_the_background_without_a_line_at_its_edges_also_at_a_scaled_size(int width, int height, double scaling)
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Hollows, width: width, height: height, scaling: scaling);
        arcadia.Press(UiAction.Search);
        arcadia.Arcadia.Main.TypeInSearch("Hollow");
        arcadia.Press(UiAction.NextPage);

        AssertRowFadesToBackground(arcadia);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T12-18")]
    public async Task The_first_click_on_a_key_or_the_tab_acts_and_switches_the_legend_to_the_mouse_a_button_brings_the_controller_back()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Hollows);
        var main = arcadia.Arcadia.Main;
        arcadia.Press(UiAction.Search).WaitForTransitions();
        var search = main.Search!;
        var keyboard = search.Keyboard;
        var erase = keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Backspace);
        Assert.NotEmpty(GlyphTexts(KeyButton(arcadia, erase)));
        Assert.Equal("D-Pad Tastatur", Legend(arcadia)[0]);

        // The first click types, the legend is the mouse's, the glyphs on the keys and the tab go, the keyboard stays.
        arcadia.Click(KeyButton(arcadia, keyboard.Keys.Single(key => key.IsCharacter && key.Label == "h")));
        Assert.Equal("h", search.Text);
        Assert.True(search.IsKeyboardOut);
        Assert.Equal("Klick Eingeben", Legend(arcadia)[0]);
        Assert.Empty(GlyphTexts(KeyButton(arcadia, erase)));
        Assert.Empty(GlyphTexts(Tab(arcadia)));
        Assert.Empty(GlyphTexts((Control)One(arcadia, Page(arcadia), "Schließen").Parent!));
        AssertDrawer(arcadia, isOut: true);

        // A button brings the controller's legend and glyphs back.
        arcadia.Press(UiAction.Right);
        Assert.Equal("D-Pad Tastatur", Legend(arcadia)[0]);
        Assert.NotEmpty(GlyphTexts(KeyButton(arcadia, erase)));
        Assert.Equal(["B"], GlyphTexts(Tab(arcadia)));

        // The first click on the tab puts the keyboard in at once.
        arcadia.Click(Tab(arcadia));
        Assert.False(search.IsKeyboardOut);
        Assert.Equal("↑ ↓ ← → Treffer wählen", Legend(arcadia)[0]);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T110-10")]
    public async Task The_searchs_keyboard_is_the_mouse_modes_without_tab_and_edge_key_its_enter_starts_the_hit_and_Ausblenden_puts_it_in()
    {
        using var mouseMode = MouseModeKeyboard.Show();
        using var arcadia = await ArcadiaWindow.ShowAsync(Hollows);
        var main = arcadia.Arcadia.Main;
        arcadia.Press(UiAction.Search);
        var search = main.Search!;
        var keyboard = search.Keyboard;

        // The same keys in the same places, only ↹ and the edge key are gone and nothing else takes their places.
        var hidden = keyboard.Keys.Where(key => key.Key.Kind is BoardKeyKind.Tab or BoardKeyKind.Dock).ToList();
        Assert.Equal(2, hidden.Count);
        Assert.All(hidden, key => Assert.DoesNotContain(arcadia.Window.GetVisualDescendants().OfType<Button>(), button => button.DataContext == key && button.IsEffectivelyVisible));
        Assert.Equal(Places(mouseMode.Keyboard.Keys), Places(keyboard.Keys));
        Assert.Equal(
            mouseMode.Keyboard.Keys.Select(key => (key.Key.Kind, key.Label, key.Symbol, key.Text)),
            keyboard.Keys.Select(key => (key.Key.Kind, key.Label, key.Symbol, key.Text)));
        Assert.All(hidden, key => Assert.DoesNotContain(keyboard.Keys.Except(hidden), other => PlaceOf(other).Intersects(PlaceOf(key).Deflate(2))));

        // The D-pad and the left stick move the focus over the keys.
        arcadia.Press(UiAction.Right);
        Assert.Equal("s", keyboard.FocusedKey.Label);
        arcadia.Arcadia.HoldPad(TimeSpan.FromMilliseconds(100), leftStick: new StickPosition(1, 0));
        Assert.Equal("d", keyboard.FocusedKey.Label);

        // ↵ starts the focused hit as ≡ does, with A or a click.
        main.TypeInSearch("Hollow");
        arcadia.Settle();
        var enter = keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Enter);
        arcadia.Arcadia.GoTo(enter).Press(UiAction.Accept);
        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Starts.Keys.Count == 1, "↵ startet den Treffer");
        Assert.Equal(search.Results[0].Game.Key, arcadia.Arcadia.Starts.Keys[0]);
        arcadia.Press(UiAction.SecondaryRight).Click(KeyButton(arcadia, enter));
        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Starts.Keys.Count == 2, "Klick auf ↵ startet den Treffer");
        Assert.Equal(search.Results[1].Game.Key, arcadia.Arcadia.Starts.Keys[1]);

        // "Ausblenden" puts the keyboard in as B does.
        arcadia.Press(UiAction.Up);
        arcadia.Arcadia.GoTo("Ausblenden").Press(UiAction.Accept);
        Assert.False(search.IsKeyboardOut);
        Assert.True(main.IsSearchShown);

        // Filter and sort with L3, or the button in the row of chips.
        arcadia.Press(UiAction.Filter);
        Assert.True(main.IsViewOptionsOpen);
        arcadia.Press(UiAction.Back).Click(arcadia.Named<Button>("SearchSortButton"));
        Assert.True(main.IsViewOptionsOpen);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T110-14")]
    public async Task The_focus_ring_shows_whole_on_the_keys_at_the_keyboards_four_edges_in_the_search_and_in_the_mouse_mode()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Hollows);
        arcadia.Press(UiAction.Search).WaitForTransitions();
        var keyboard = arcadia.Arcadia.Main.Search!.Keyboard;
        foreach (var key in EdgeKeys(keyboard))
        {
            arcadia.Arcadia.GoTo(key);
            arcadia.Settle().AssertRingAround(KeyButton(arcadia, key), $"Suche: {Describe(key)}", "KeyCapEdgeFocused");
        }

        using var mouseMode = MouseModeKeyboard.Show();
        foreach (var key in EdgeKeys(mouseMode.Keyboard))
        {
            GoTo(mouseMode, key);
            ArcadiaWindow.AssertRingAround(mouseMode.Window, mouseMode.KeyButton(key), $"Mausmodus: {Describe(key)}", "KeyCapEdgeFocused");
        }
    }

    private static IEnumerable<Game> Many(LauncherInfo launcher, string title, int count) =>
        Enumerable.Range(1, count).Select(n => Installed(launcher, $"{title} {n:00}", Installed));

    /// <summary>The search's page: everything inside it, nothing of the library underneath.</summary>
    private static Control Page(ArcadiaWindow arcadia) => arcadia.Showing(arcadia.Arcadia.Main.Search!);

    private static IEnumerable<string> Texts(Control within) => within.GetVisualDescendants().OfType<TextBlock>()
        .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))
        .Select(text => text.Text!);

    /// <summary>The one visible text block inside <paramref name="within"/> that says <paramref name="text"/>.</summary>
    private static TextBlock One(ArcadiaWindow arcadia, Control within, string text) => Assert.Single(
        within.GetVisualDescendants().OfType<TextBlock>(),
        block => block.IsEffectivelyVisible && block.Text == text && arcadia.Bounds(block).Width > 0);

    private static IReadOnlyList<string> ChipsOn(SearchViewModel search, bool all = false) =>
        search.Chips.Where(chip => chip.IsOn && (all || chip.IsLauncher)).Select(chip => chip.Name).ToList();

    private static Button Chip(ArcadiaWindow arcadia, string name) => Assert.Single(
        arcadia.Named<ItemsControl>("SearchChips").GetVisualDescendants().OfType<Button>(),
        button => button.DataContext is SearchChipViewModel chip && chip.Name == name);

    private static Button FilterLauncher(ArcadiaWindow arcadia, string name) => Assert.Single(
        arcadia.Window.GetVisualDescendants().OfType<Button>(),
        button => button.IsEffectivelyVisible && button.Classes.Contains("launcher") && Equals(button.Content, name));

    private static ArcadiaWindow Erase(ArcadiaWindow arcadia)
    {
        while (arcadia.Arcadia.Main.Search!.Text.Length > 0)
        {
            arcadia.Key(Key.Back);
        }

        return arcadia;
    }

    private static Control Drawer(ArcadiaWindow arcadia) => Assert.Single(
        Page(arcadia).GetVisualDescendants().OfType<Arcadia.App.Controls.RevealPanel>(), panel => panel.Classes.Contains("keyboardDrawer"));

    private static Button Tab(ArcadiaWindow arcadia) => Assert.Single(
        Page(arcadia).GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("keyboardTab"));

    /// <summary>
    /// The drawer all the way out or in, the tab in the middle on its top edge with its glyph, keyboard symbol and arrow on
    /// one line, the arrow pointing where the keyboard goes next.
    /// </summary>
    private static void AssertDrawer(ArcadiaWindow arcadia, bool isOut)
    {
        arcadia.WaitForTransitions();
        var drawer = (Arcadia.App.Controls.RevealPanel)Drawer(arcadia);
        Assert.Equal(isOut ? 1 : 0, drawer.Reveal, 3);
        Assert.Equal(isOut, arcadia.Arcadia.Main.Search!.IsKeyboardOut);
        var tab = Tab(arcadia);
        var top = arcadia.Bounds(drawer).Top;
        Assert.Equal(top, arcadia.Bounds(tab).Bottom, tolerance: 1);
        Assert.Equal(arcadia.Bounds(Page(arcadia)).Center.X, arcadia.Bounds(tab).Center.X, tolerance: 1);
        var parts = tab.GetVisualDescendants().OfType<Control>()
            .Where(part => part.IsEffectivelyVisible && (part is Avalonia.Controls.Shapes.Path || part is Border { Classes: var classes } && classes.Contains("glyph")))
            .ToList();
        Assert.All(parts, part => Assert.Equal(arcadia.Bounds(tab).Center.Y, arcadia.Bounds(part).Center.Y, tolerance: 1.5));
        // The arrow up comes before the one down.
        var arrows = tab.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Where(path => !path.Classes.Contains("keyboardIcon")).ToList();
        Assert.Equal([!isOut, isOut], arrows.Select(arrow => arrow.IsEffectivelyVisible));
        if (arcadia.Arcadia.Main.UsesController)
        {
            Assert.Equal([isOut ? "B" : "X"], GlyphTexts(tab));
        }
    }

    private static Button HitButton(ArcadiaWindow arcadia, GameTileViewModel tile) => Assert.Single(
        arcadia.Named<Control>("SearchResults").GetVisualDescendants().OfType<Button>(),
        button => button.Classes.Contains("resultTile") && button.DataContext == tile && button.IsEffectivelyVisible);

    private static IReadOnlyList<Button> TurnButtons(ArcadiaWindow arcadia) => Page(arcadia).GetVisualDescendants().OfType<Button>()
        .Where(button => button.Classes.Contains("turnHits") && button.IsEffectivelyVisible)
        .OrderBy(button => arcadia.Bounds(button).Left)
        .ToList();

    private static IReadOnlyList<string> VisibleTexts(Control within) => [.. Texts(within)];

    /// <summary>At the left and right edge of the hits, the last device pixels are the background all along the row.</summary>
    private static void AssertRowFadesToBackground(ArcadiaWindow arcadia)
    {
        var search = arcadia.Arcadia.Main.Search!;
        var row = arcadia.Bounds(HitButton(arcadia, search.FocusedResult!));
        using var frame = arcadia.Capture();
        var viewport = arcadia.Bounds(arcadia.Named<Control>("SearchViewport"));
        var background = ArcadiaWindow.ThemeColour("BgColor");
        var columns = new[] { (int)Math.Ceiling(viewport.Left * frame.Scaling), (int)Math.Floor(viewport.Right * frame.Scaling) - 1 };
        for (var y = (int)(row.Top * frame.Scaling); y < row.Bottom * frame.Scaling; y++)
        {
            foreach (var x in columns)
            {
                Assert.True(ArcadiaWindow.Distance(frame.At(x, y), background) <= 12, $"Kante am Rand der Treffer bei {x},{y} (Gerätepixel): {frame.At(x, y)} statt {background}");
            }
        }

        // A neighbour peeks in faded: not the background a little further in.
        Assert.Contains(columns, x => Enumerable.Range(1, 120).Any(step =>
            ArcadiaWindow.Distance(frame.At(x + (x < frame.Height ? step : -step) * 1, (int)(row.Center.Y * frame.Scaling)), background) > 12));
    }

    private static void ClickTrackNearEnd(ArcadiaWindow arcadia, ScrollBar bar)
    {
        var bounds = arcadia.Bounds(bar);
        arcadia.Click(bar.Orientation == Avalonia.Layout.Orientation.Horizontal
            ? new Point(bounds.Right - 6, bounds.Center.Y)
            : new Point(bounds.Center.X, bounds.Bottom - 6));
    }

    /// <summary>The bar's thumb dragged past the start of the bar (or its end), in steps as a hand drags it.</summary>
    private static void DragThumb(ArcadiaWindow arcadia, ScrollBar bar, bool toEnd)
    {
        var thumb = bar.GetVisualDescendants().OfType<Thumb>().Single();
        var from = arcadia.CentreOf(thumb);
        var bounds = arcadia.Bounds(bar);
        var to = bar.Orientation == Avalonia.Layout.Orientation.Horizontal
            ? new Point(toEnd ? bounds.Right + 40 : bounds.Left - 40, from.Y)
            : new Point(from.X, toEnd ? bounds.Bottom + 40 : bounds.Top - 40);
        arcadia.MouseDown(from).MouseMove((from + to) / 2).MouseMove(to).MouseUp(to);
    }

    private static IReadOnlyList<(BoardKeyKind Kind, double Left, double Top, double Width, double Height)> Places(IEnumerable<BoardKeyViewModel> keys) =>
        keys.Select(key => (key.Key.Kind, key.Left, key.Top, key.Width, key.Height)).ToList();

    private static Rect PlaceOf(BoardKeyViewModel key) => new(key.Left, key.Top, key.Width, key.Height);

    /// <summary>The keys at the keyboard's four edges: ^ and ⌫ above, the layout key and → on the right, ⇧ and space below.</summary>
    private static IEnumerable<BoardKeyViewModel> EdgeKeys(OnScreenKeyboardViewModel keyboard) =>
    [
        keyboard.Keys.Where(key => key.IsCharacter && key.Key.Row == 0).MinBy(key => key.Key.Left)!,
        keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Backspace),
        keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Layout),
        keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Right),
        keyboard.Keys.Where(key => key.Key.Kind == BoardKeyKind.Shift).MinBy(key => key.Key.Left)!,
        keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Space),
    ];

    private static string Describe(BoardKeyViewModel key) => key.IsCharacter ? key.Label : $"{key.Key.Kind} {key.Text}";

    /// <summary>The D-pad's way to <paramref name="key"/>, pressed as the mouse mode's buttons.</summary>
    private static void GoTo(MouseModeKeyboard mouseMode, BoardKeyViewModel key)
    {
        foreach (var step in OnScreenKeys.PathTo(mouseMode.Keyboard, key))
        {
            mouseMode.Press(step switch
            {
                UiAction.Up => KeyboardButton.Up,
                UiAction.Down => KeyboardButton.Down,
                UiAction.Left => KeyboardButton.Left,
                _ => KeyboardButton.Right,
            });
        }

        Assert.Same(key, mouseMode.Keyboard.FocusedKey);
    }

    /// <summary>The button of <paramref name="key"/> on the keyboard shown.</summary>
    private static Button KeyButton(ArcadiaWindow arcadia, BoardKeyViewModel key) =>
        arcadia.ShowingAll(key).SelectMany(control => control.GetSelfAndVisualDescendants().OfType<Button>()).First(button => button.Classes.Contains("boardKey"));

    /// <summary>The characters drawn on the character keys of <paramref name="row"/>, left to right.</summary>
    private static string DrawnRow(ArcadiaWindow arcadia, OnScreenKeyboardViewModel keyboard, int row) => string.Concat(keyboard.Keys
        .Where(key => key.IsCharacter && key.Key.Row == row)
        .OrderBy(key => key.Key.Left)
        .Select(key => KeyButton(arcadia, key).GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.IsEffectivelyVisible && !text.Classes.Contains("keyHint") && !string.IsNullOrEmpty(text.Text)).Text));

    private static Control LayoutMenu(ArcadiaWindow arcadia) => Assert.Single(
        arcadia.Window.GetVisualDescendants().OfType<Border>(),
        border => border.Classes.Contains("keyboardLayoutMenu") && border.IsEffectivelyVisible);

    private static IEnumerable<string?> GlyphTexts(Control within) =>
        ArcadiaWindow.Glyphs(within).Select(glyph => (glyph.Child as TextBlock)?.Text);

    /// <summary>
    /// The search's legend at the bottom, left to right: each hint as its glyphs and texts, e.g. "LT RT Weitere Treffer".
    /// </summary>
    private static IReadOnlyList<string> Legend(ArcadiaWindow arcadia)
    {
        arcadia.WaitForTransitions();
        var page = Page(arcadia);
        var bottom = arcadia.Bounds(page).Bottom - 64;
        return page.GetVisualDescendants().OfType<StackPanel>()
            .Where(panel => panel.IsEffectivelyVisible && panel.Children.Any(child => child is TextBlock { Classes: var classes } && classes.Contains("hint"))
                && arcadia.Bounds(panel).Top >= bottom && !panel.Children.OfType<StackPanel>().Any())
            .OrderBy(panel => arcadia.Bounds(panel).Left)
            .Select(panel => string.Join(' ', panel.Children.Select(child => child switch
            {
                Border { Child: TextBlock glyph } => glyph.Text,
                TextBlock text => text.Text,
                _ => null,
            }).Where(text => !string.IsNullOrEmpty(text))))
            .ToList();
    }

    /// <summary>The empty state's glow fades to the background before its edge: no rim where the ellipse ends.</summary>
    private static void AssertGlowWithoutRim(ArcadiaWindow arcadia)
    {
        var glow = Assert.Single(Page(arcadia).GetVisualDescendants().OfType<Ellipse>(), ellipse => ellipse.IsEffectivelyVisible && ellipse.Width >= 600);
        using var frame = arcadia.Capture();
        var bounds = arcadia.Bounds(glow);
        var background = ArcadiaWindow.ThemeColour("BgColor");
        foreach (var point in new[] { new Point(bounds.Left + 2, bounds.Center.Y), new Point(bounds.Right - 2, bounds.Center.Y), new Point(bounds.Center.X, bounds.Top + 2) })
        {
            Assert.True(ArcadiaWindow.Distance(frame.At(point), background) <= 6, $"Rand des Scheins bei {point}: {frame.At(point)} statt {background}");
        }

        Assert.True(ArcadiaWindow.Distance(frame.At(new Point(bounds.Center.X, bounds.Top + 40)), background) > 6, "Der Schein ist zu sehen");
    }
}
