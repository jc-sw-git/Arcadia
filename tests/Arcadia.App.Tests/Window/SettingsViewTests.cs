using Arcadia.App.Controls;
using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>The settings page (#15) as drawn: how it opens and closes, its rows, info panel and footer, with every input.</summary>
public sealed class SettingsViewTests
{
    private static readonly TimeSpan HoldForDetails = TimeSpan.FromSeconds(1.1);

    [AvaloniaFact]
    [Trait("TestCase", "T15-1")]
    public async Task The_settings_cover_the_whole_page_and_Menu_B_Enter_Esc_the_chip_and_X_lead_back_to_the_same_tab_page_and_game_or_detail_page()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var main = arcadia.Arcadia.Main;
        arcadia.Press(UiAction.NextTab).Press(UiAction.NextPage).Press(UiAction.Right, times: 2).Press(UiAction.Down);
        var focused = arcadia.Arcadia.FocusedGame!.Game.Key;
        var cover = arcadia.CentreOf(arcadia.Tile(arcadia.Arcadia.FocusedGame!));

        void AssertCovers(string how)
        {
            Assert.True(main.IsSettingsOpen, how);
            arcadia.WaitForTransitions();
            foreach (var point in new[] { cover, new Point(40, 40), new Point(ArcadiaWindow.Width - 40, ArcadiaWindow.Height - 40) })
            {
                var hit = arcadia.Window.InputHitTest(point) as Visual;
                Assert.True(hit is not null && hit.GetSelfAndVisualAncestors().Any(visual => visual is Panel panel && panel.IsVisible && panel.DataContext == main
                    && panel.Children.OfType<Grid>().Any(grid => grid.GetVisualDescendants().Contains(arcadia.Named<ItemsControl>("SettingsRows")))),
                    $"{how}: bei {point} liegt {hit} statt der Einstellungen");
            }
        }

        void AssertBack(string how)
        {
            Assert.False(main.IsSettingsOpen, how);
            Assert.Equal("Xbox", arcadia.Arcadia.SelectedTab?.Name);
            Assert.Equal("2", arcadia.Arcadia.CurrentPage);
            Assert.Equal(focused, arcadia.Arcadia.FocusedGame?.Game.Key);
            Assert.Contains("focused", arcadia.Tile(arcadia.Arcadia.FocusedGame!).Classes);
        }

        arcadia.Press(UiAction.Settings);
        AssertCovers("≡");
        arcadia.Press(UiAction.Back);
        AssertBack("B");

        arcadia.Key(Key.Enter);
        AssertCovers("Enter");
        arcadia.Key(Key.Escape);
        AssertBack("Esc");

        arcadia.Click(arcadia.ButtonWithTip("Einstellungen"));
        AssertCovers("Klick auf den Chip");
        arcadia.Click(arcadia.CloseButton(main.CloseSettingsCommand));
        AssertBack("✕");

        // From a detail page B leads back to it.
        arcadia.Arcadia.Hold(UiAction.Accept, HoldForDetails);
        arcadia.Settle();
        Assert.True(main.IsDetailOpen);
        arcadia.Press(UiAction.Settings);
        AssertCovers("≡ auf der Detailseite");
        arcadia.Press(UiAction.Back);
        Assert.True(main.IsDetailOpen);
        Assert.Equal(focused, arcadia.Arcadia.Detail!.Current.Tile.Game.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T15-2")]
    public async Task LB_RB_shift_Q_E_and_a_click_switch_the_category_round_from_the_last_to_the_first_the_strip_in_the_middle()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        arcadia.Press(UiAction.Settings);
        var settings = arcadia.Arcadia.Main.Settings!;
        Assert.Equal(["Launcher", "Controller", "Über Arcadia"], settings.Categories.Select(category => category.Name));

        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Controller", settings.Selected.Name);
        AssertSelectedTabDrawn(arcadia, "Controller");
        arcadia.Press(UiAction.NextTab, times: 2);
        Assert.Equal("Launcher", settings.Selected.Name);
        arcadia.Press(UiAction.PreviousTab);
        Assert.Equal("Über Arcadia", settings.Selected.Name);

        arcadia.Key(Key.E, KeyModifiers.Shift);
        Assert.Equal("Launcher", settings.Selected.Name);
        arcadia.Key(Key.Q, KeyModifiers.Shift);
        Assert.Equal("Über Arcadia", settings.Selected.Name);

        arcadia.Click(arcadia.Showing(settings.Categories[1]));
        Assert.Equal("Controller", settings.Selected.Name);
        AssertSelectedTabDrawn(arcadia, "Controller");

        // Three categories fit: the strip stands in the middle of the header, as wide as they need, not faded.
        arcadia.WaitForTransitions();
        var scroller = arcadia.Named<ScrollViewer>("CategoryScroller");
        Assert.Equal(scroller.Extent.Width, scroller.Viewport.Width, tolerance: 1);
        Assert.Equal(ArcadiaWindow.Width / 2, arcadia.Bounds(scroller).Center.X, tolerance: 120);
        Assert.False(arcadia.Named<Border>("CategoryEdgeLeft").IsVisible || arcadia.Named<Border>("CategoryEdgeRight").IsVisible, "Kein Ausblenden");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T15-4")]
    public async Task In_Controller_the_D_pad_walks_the_rows_with_their_headings_in_view_values_change_at_once_Aus_dims_and_all_stays_after_a_restart()
    {
        using var first = await ArcadiaWindow.ShowAsync(steam: new FakeSteam());
        var main = first.Arcadia.Main;
        first.Press(UiAction.Settings).Press(UiAction.NextTab);
        var settings = main.Settings!;
        Assert.Equal("Controller", settings.Selected.Name);
        var rows = settings.Selected.Rows;

        // ▲ ▼ row by row, stopping at both ends, each row in view with its group's heading.
        first.Press(UiAction.Up);
        Assert.Same(rows[0], settings.FocusedRow);
        for (var i = 1; i < rows.Count; i++)
        {
            first.Press(UiAction.Down);
            Assert.Same(rows[i], settings.FocusedRow);
            AssertRowInView(first, rows[i]);
        }

        first.Press(UiAction.Down);
        Assert.Same(rows[^1], settings.FocusedRow);
        for (var i = rows.Count - 2; i >= 0; i--)
        {
            first.Press(UiAction.Up);
            AssertRowInView(first, rows[i]);
        }

        // ◀ ▶ change a choice at once, A counts on, A flips a switch.
        first.Arcadia.FocusRow("mouse-mode-hold");
        Assert.Equal("3 Sekunden", first.ValueShown(settings.FocusedRow!));
        first.Press(UiAction.Right);
        Assert.Equal("4 Sekunden", first.ValueShown(settings.FocusedRow!));
        first.Press(UiAction.Accept);
        Assert.Equal("5 Sekunden", first.ValueShown(settings.FocusedRow!));
        first.Arcadia.FocusRow("mouse-mode-for-dialogs");
        first.Press(UiAction.Accept);
        Assert.Equal("Aus", first.ValueShown(settings.FocusedRow!));

        // "Aus" dims the rows that need the mouse mode; they keep their values.
        first.Arcadia.FocusRow("mouse-mode");
        first.Press(UiAction.Left);
        Assert.Equal("Aus", first.ValueShown(settings.FocusedRow!));
        var hold = rows.Single(row => row.Key == "mouse-mode-hold");
        Assert.Contains("unavailable", first.RowButton(hold).Classes);
        Assert.True(first.RowButton(hold).Opacity < 1, "Abhängige Zeile ausgegraut");
        Assert.DoesNotContain("unavailable", first.RowButton(rows.Single(row => row.Key == "steam-desktop-layout")).Classes);
        Assert.Equal("5 Sekunden", first.ValueShown(hold));

        // The SISR row's boxes stand under its description in the info panel, in this order; only settings and actions are rows.
        first.Arcadia.FocusRow("sisr");
        var boxes = first.InfoPanel().GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("infoSection") && border.IsEffectivelyVisible).ToList();
        Assert.Equal(["STATUS", "SO FUNKTIONIERT ES", "NACHTEILE", "SISR-VERSION"], boxes.OrderBy(box => first.Bounds(box).Top).Select(box => box.GetVisualDescendants().OfType<TextBlock>().First().Text));
        var description = first.InfoPanel().GetVisualDescendants().OfType<TextBlock>().First(text => text.Classes.Contains("infoText") && text.IsEffectivelyVisible);
        Assert.True(first.Bounds(description).Bottom <= first.Bounds(boxes[0]).Top, "Die Boxen stehen unter der Beschreibung");
        Assert.All(rows, row => Assert.IsNotType<InfoRowViewModel>(row));

        using var restarted = await first.RestartAsync();
        restarted.Press(UiAction.Settings).Press(UiAction.NextTab);
        var again = restarted.Arcadia.Main.Settings!.Selected.Rows;
        Assert.Equal("Aus", restarted.ValueShown(again.Single(row => row.Key == "mouse-mode")));
        Assert.Equal("5 Sekunden", restarted.ValueShown(again.Single(row => row.Key == "mouse-mode-hold")));
        Assert.Equal("Aus", restarted.ValueShown(again.Single(row => row.Key == "mouse-mode-for-dialogs")));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T15-6")]
    public async Task Mausmodus_explains_itself_with_a_box_the_info_panel_fades_where_it_scrolls_RS_shift_W_S_page_keys_and_the_wheel_scroll_it()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        arcadia.Arcadia.FocusRow("mouse-mode");
        var info = arcadia.Named<ScrollViewer>("InfoScroller");

        // The description, below it the box "So funktioniert es"; faded at the bottom, the footer names RS.
        var box = Assert.Single(arcadia.InfoPanel().GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("infoSection") && border.IsEffectivelyVisible);
        Assert.Equal("SO FUNKTIONIERT ES", box.GetVisualDescendants().OfType<TextBlock>().First().Text);
        Assert.StartsWith("Durch Halten von", arcadia.InfoPanel().GetVisualDescendants().OfType<TextBlock>().First(text => text.Classes.Contains("infoText")).Inlines!.Text, StringComparison.Ordinal);
        AssertInfoFades(arcadia, top: false, bottom: true);
        Assert.Contains("RS Infotext", arcadia.SettingsFooter());

        arcadia.Press(UiAction.SecondaryDown);
        Assert.True(info.Offset.Y > 0, "RS scrollt");
        AssertInfoFades(arcadia, top: true, bottom: info.Offset.Y < info.Extent.Height - info.Viewport.Height - 1);
        arcadia.Press(UiAction.SecondaryUp);
        Assert.Equal(0, info.Offset.Y);
        AssertInfoFades(arcadia, top: false, bottom: true);

        arcadia.Key(Key.S, KeyModifiers.Shift);
        Assert.True(info.Offset.Y > 0, "Umschalt+S scrollt");
        arcadia.Key(Key.W, KeyModifiers.Shift);
        Assert.Equal(0, info.Offset.Y);
        arcadia.Key(Key.PageDown);
        Assert.True(info.Offset.Y > 0, "Bild ab scrollt");
        arcadia.Key(Key.PageUp);
        Assert.Equal(0, info.Offset.Y);
        arcadia.Wheel(info, notches: -2);
        Assert.True(info.Offset.Y > 0, "Das Mausrad scrollt");
        Assert.Equal("mouse-mode", arcadia.Arcadia.Main.Settings!.FocusedRow?.Key);

        // Another row starts at the top.
        arcadia.Press(UiAction.Down);
        Assert.Equal(0, info.Offset.Y);
        AssertInfoFades(arcadia, top: false, bottom: info.Extent.Height > info.Viewport.Height + 1);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T15-9")]
    public async Task Every_setting_is_reached_with_one_hand_on_the_keyboard_and_with_the_mouse_alone_which_focuses_a_row_without_changing_it()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        var main = arcadia.Arcadia.Main;

        // One hand on the keyboard: Enter, Shift+E, S, D, A, space, Esc.
        arcadia.Key(Key.Enter);
        var settings = main.Settings!;
        arcadia.Key(Key.E, KeyModifiers.Shift);
        Assert.Equal("Controller", settings.Selected.Name);
        arcadia.Key(Key.S, times: 2);
        Assert.Equal("mouse-mode", settings.FocusedRow?.Key);
        arcadia.Key(Key.D);
        Assert.Equal(MouseModeActivation.Automatic, arcadia.Arcadia.Settings.Current.MouseMode.Activation);
        arcadia.Key(Key.A);
        Assert.Equal(MouseModeActivation.Manual, arcadia.Arcadia.Settings.Current.MouseMode.Activation);
        arcadia.Key(Key.S, times: 3);
        Assert.Equal("mouse-mode-for-dialogs", settings.FocusedRow?.Key);
        arcadia.Key(Key.Space);
        Assert.False(arcadia.Arcadia.Settings.Current.MouseMode.ForDialogs);
        arcadia.Key(Key.W, times: 3);
        Assert.Equal("mouse-mode", settings.FocusedRow?.Key);
        arcadia.Key(Key.S, KeyModifiers.Shift);
        Assert.True(arcadia.Named<ScrollViewer>("InfoScroller").Offset.Y > 0, "Umschalt+S scrollt das Infopanel");
        arcadia.Key(Key.Escape);
        Assert.False(main.IsSettingsOpen);

        // The mouse alone: the gear, a category, hovering a row focuses it without a change, its info explains it.
        arcadia.Click(arcadia.ButtonWithTip("Einstellungen"));
        settings = main.Settings!;
        arcadia.Click(arcadia.Showing(settings.Categories[1]));
        Assert.Equal("Controller", settings.Selected.Name);
        var speed = settings.Selected.Rows.Single(row => row.Key == "mouse-mode-speed");
        var before = arcadia.ValueShown(speed);
        var resting = arcadia.CentreOf(arcadia.RowButton(speed));
        arcadia.MouseMove(resting - new Point(0, 30)).MouseMove(resting);
        Assert.Same(speed, settings.FocusedRow);
        Assert.Equal(before, arcadia.ValueShown(speed));
        Assert.Equal(speed.InfoTitle, arcadia.InfoPanel().GetVisualDescendants().OfType<TextBlock>().First(text => text.IsEffectivelyVisible).Text);

        // The arrows beside the value change it; a click on a switch's row flips it.
        arcadia.Click(arcadia.ValueArrows(speed)[1]);
        Assert.NotEqual(before, arcadia.ValueShown(speed));
        arcadia.Click(arcadia.ValueArrows(speed)[0]);
        Assert.Equal(before, arcadia.ValueShown(speed));
        var forDialogs = settings.Selected.Rows.Single(row => row.Key == "mouse-mode-for-dialogs");
        Assert.Equal("Aus", arcadia.ValueShown(forDialogs));
        arcadia.Click(arcadia.RowButton(forDialogs));
        Assert.Equal("An", arcadia.ValueShown(forDialogs));

        // The wheel over the info panel scrolls it.
        var mouseMode = settings.Selected.Rows.Single(row => row.Key == "mouse-mode");
        arcadia.MouseMove(arcadia.CentreOf(arcadia.RowButton(mouseMode)) + new Point(0, 10));
        Assert.Same(mouseMode, settings.FocusedRow);
        arcadia.Wheel(arcadia.Named<ScrollViewer>("InfoScroller"), notches: -2);
        Assert.True(arcadia.Named<ScrollViewer>("InfoScroller").Offset.Y > 0, "Das Mausrad scrollt das Infopanel");

        // The mouse resting while the rows scroll under it with keys: the focus stays with the keys.
        arcadia.MouseMove(arcadia.CentreOf(arcadia.RowButton(settings.Selected.Rows[0])));
        var pointer = arcadia.CentreOf(arcadia.RowButton(settings.Selected.Rows[0]));
        for (var step = 0; step < settings.Selected.Rows.Count; step++)
        {
            arcadia.Key(Key.S).MouseMove(pointer);
        }

        Assert.Same(settings.Selected.Rows[^1], settings.FocusedRow);

        // ✕ closes.
        arcadia.Click(arcadia.CloseButton(main.CloseSettingsCommand));
        Assert.False(main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-31")]
    public async Task The_focused_setting_scrolls_into_view_in_every_category_and_B_closes()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(steam: new FakeSteam());
        var main = arcadia.Arcadia.Main;
        arcadia.Press(UiAction.Settings);
        var settings = main.Settings!;
        var scrolled = false;
        foreach (var _ in settings.Categories)
        {
            var rows = settings.Selected.Rows;
            AssertRowInView(arcadia, settings.FocusedRow!);
            for (var i = 1; i < rows.Count; i++)
            {
                arcadia.Press(UiAction.Down);
                AssertRowInView(arcadia, settings.FocusedRow!);
                scrolled |= arcadia.Named<ScrollViewer>("SettingsRowsScroller").Offset.Y > 0;
            }

            arcadia.Press(UiAction.NextTab);
        }

        Assert.True(scrolled, "Die Zeilen sind gescrollt");
        arcadia.Press(UiAction.Back);
        Assert.False(main.IsSettingsOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T64-11")]
    public async Task A_mouse_move_or_a_key_shows_the_X_at_the_top_right_of_each_dialog_which_closes_only_it_and_a_button_hides_it_again()
    {
        var steam = new FakeSteam { IsSteamRunning = true };
        steam.Sisr.IsInstalled = false;
        using var arcadia = await ArcadiaWindow.ShowAsync(steam: steam);
        var main = arcadia.Arcadia.Main;

        // "Ansicht": no ✕ with the controller; the mouse shows it, a click closes the dialog only.
        arcadia.Press(UiAction.ViewOptions);
        Assert.Null(arcadia.VisibleClose(main.CloseViewOptionsCommand));
        Wiggle(arcadia);
        AssertTopRight(arcadia, arcadia.CloseButton(main.CloseViewOptionsCommand));
        arcadia.Click(arcadia.CloseButton(main.CloseViewOptionsCommand));
        Assert.False(main.IsViewOptionsOpen);
        Assert.False(main.IsSettingsOpen || main.IsSearchShown);

        // A controller button hides it again.
        arcadia.Press(UiAction.ViewOptions);
        Assert.Null(arcadia.VisibleClose(main.CloseViewOptionsCommand));
        arcadia.Press(UiAction.Back);

        // "Filter und Sortierung" over the search: the ✕ closes the filter, the search stays.
        arcadia.Press(UiAction.Search).Press(UiAction.Filter);
        Assert.Null(arcadia.VisibleClose(main.CloseViewOptionsCommand));
        Wiggle(arcadia);
        arcadia.Click(arcadia.CloseButton(main.CloseViewOptionsCommand));
        Assert.False(main.IsViewOptionsOpen);
        Assert.True(main.IsSearchShown);
        arcadia.Press(UiAction.Back).Press(UiAction.Back);

        // The settings: a key shows it.
        arcadia.Press(UiAction.Settings);
        Assert.Null(arcadia.VisibleClose(main.CloseSettingsCommand));
        arcadia.Key(Key.S);
        AssertTopRight(arcadia, arcadia.CloseButton(main.CloseSettingsCommand), whole: true);
        arcadia.Click(arcadia.CloseButton(main.CloseSettingsCommand));
        Assert.False(main.IsSettingsOpen);

        // SISR's dialog over the settings: the ✕ closes the dialog, the settings stay.
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab).Press(UiAction.Accept);
        var controller = Assert.IsType<ControllerSettingsViewModel>(main.Settings!.Selected);
        Assert.True(controller.IsSteamStopPrompt);
        Assert.Null(arcadia.VisibleClose(controller.ClosePromptCommand));
        Wiggle(arcadia);
        AssertTopRight(arcadia, arcadia.CloseButton(controller.ClosePromptCommand));
        arcadia.Click(arcadia.CloseButton(controller.ClosePromptCommand));
        Assert.Null(main.Settings!.Selected.Dialog);
        Assert.True(main.IsSettingsOpen);
        Assert.Equal(0, steam.Sisr.Installs);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T92-3")]
    public async Task A_click_on_the_mode_in_the_preview_switches_it_and_back_to_the_start_value_nothing_is_saved()
    {
        var steam = new FakeSteam { IsSteamRunning = true };
        using var arcadia = await ArcadiaWindow.ShowAsync(
            [new LauncherSpec(KnownLaunchers.Steam, 3), new LauncherSpec(KnownLaunchers.Gog, 3)],
            new AppSettings { ActiveLaunchers = ["steam", "gog"], SteamControllerSupport = true },
            steam: steam);
        await arcadia.Arcadia.TickControllerSupportAsync();
        await arcadia.Arcadia.WaitOnClockUntilAsync(() => arcadia.Arcadia.ControllerSupport.State == SisrState.Running, "SISR läuft", TimeSpan.FromSeconds(10));
        await arcadia.Arcadia.TickControllerSupportAsync();
        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Main.ShowControllerSupport, "der Steam Controller ist erkannt");
        arcadia.Settle();

        var mode = Assert.Single(arcadia.Window.GetVisualDescendants().OfType<Button>(), button => button.Classes.Contains("modeValue") && button.IsEffectivelyVisible
            && button.Command == arcadia.Arcadia.Main.CycleControllerSupportCommand);
        Assert.Equal(ControllerModes.Steam, Text(mode));
        arcadia.Click(mode);
        Assert.Equal(ControllerModes.Sisr, Text(mode));
        Assert.Single(arcadia.Arcadia.Settings.Current.ControllerSupportOverrides);
        arcadia.Click(mode);
        Assert.Equal(ControllerModes.Steam, Text(mode));
        Assert.Empty(arcadia.Arcadia.Settings.Current.ControllerSupportOverrides);
        Assert.Empty(new SettingsStore(arcadia.Arcadia.SettingsPath).Load().ControllerSupportOverrides);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T92-5")]
    public async Task The_settings_scroll_and_the_footer_stays_visible_at_the_bottom_also_scrolled_to_the_top_and_to_the_end()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(steam: new FakeSteam());
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);
        var settings = arcadia.Arcadia.Main.Settings!;
        var scroller = arcadia.Named<ScrollViewer>("SettingsRowsScroller");
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height + 1, "Die Einstellungen scrollen");

        AssertFooterShown(arcadia, "oben");
        arcadia.Press(UiAction.Down, times: settings.Selected.Rows.Count);
        arcadia.WaitForTransitions();
        Assert.Equal(scroller.Extent.Height - scroller.Viewport.Height, scroller.Offset.Y, tolerance: 1);
        AssertFooterShown(arcadia, "unten");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T127-9")]
    public async Task The_mouse_mode_settings_draw_LS_and_L3_as_symbols_in_their_text_and_So_funktioniert_es_LS_RS_and_the_D_pad()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync();
        arcadia.Press(UiAction.Settings).Press(UiAction.NextTab);

        arcadia.Arcadia.FocusRow("mouse-mode-speed");
        var text = arcadia.InfoPanel().GetVisualDescendants().OfType<TextBlock>().First(block => block.Classes.Contains("infoText") && block.IsEffectivelyVisible);
        Assert.Equal(["LS", "L3"], GlyphsIn(text));
        Assert.DoesNotContain("[", text.Inlines!.Text, StringComparison.Ordinal);

        arcadia.Arcadia.FocusRow("mouse-mode");
        var box = Assert.Single(arcadia.InfoPanel().GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("infoSection") && border.IsEffectivelyVisible);
        var symbols = GlyphsIn(box);
        Assert.Contains("LS", symbols);
        Assert.Contains("RS", symbols);
        Assert.Contains("D-Pad", symbols);
        Assert.DoesNotContain(box.GetVisualDescendants().OfType<TextBlock>(), block => block.IsEffectivelyVisible && (block.Inlines?.Text ?? block.Text ?? string.Empty).Contains('[', StringComparison.Ordinal));
    }

    private static string? Text(Button button) => button.GetVisualDescendants().OfType<TextBlock>().Single(text => text.IsEffectivelyVisible).Text;

    private static IReadOnlyList<string?> GlyphsIn(Control within) => ArcadiaWindow.Glyphs(within).Select(glyph => (glyph.Child as TextBlock)?.Text).ToList();

    /// <summary>The row stands whole in the visible part of the rows, with the heading above it where it starts a group.</summary>
    private static void AssertRowInView(ArcadiaWindow arcadia, SettingsRowViewModel row)
    {
        arcadia.WaitForTransitions();
        var view = arcadia.Bounds(arcadia.Named<ScrollViewer>("SettingsRowsScroller")).Inflate(1);
        var button = arcadia.Bounds(arcadia.RowButton(row));
        Assert.True(view.Contains(button), $"{row.Key}: {button} nicht ganz in {view}");
        if (row.StartsGroup)
        {
            var container = (Control)arcadia.Named<ItemsControl>("SettingsRows").ContainerFromItem(row)!;
            var heading = container.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("settingsGroup"));
            Assert.True(view.Contains(arcadia.Bounds(heading)), $"{row.Key}: Überschrift {row.Group} nicht im Bild");
        }
    }

    private static void AssertInfoFades(ArcadiaWindow arcadia, bool top, bool bottom)
    {
        arcadia.WaitForTransitions();
        Assert.Equal(top ? 1 : 0, arcadia.Named<Border>("InfoFadeTop").Opacity, 2);
        Assert.Equal(bottom ? 1 : 0, arcadia.Named<Border>("InfoFadeBottom").Opacity, 2);
    }

    private static void AssertSelectedTabDrawn(ArcadiaWindow arcadia, string name)
    {
        var tabs = arcadia.Named<ItemsControl>("CategoryList").GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("tab")).ToList();
        Assert.Equal([name], tabs.Where(tab => tab.Classes.Contains("selected")).Select(tab => ((SettingsCategoryViewModel)tab.DataContext!).Name));
    }

    private static void AssertFooterShown(ArcadiaWindow arcadia, string where)
    {
        arcadia.WaitForTransitions();
        var back = arcadia.SettingsFooterBar().GetVisualDescendants().OfType<KeyHint>().Single(hint => hint.IsEffectivelyVisible && arcadia.Hints([hint])[0].EndsWith("Zurück", StringComparison.Ordinal));
        var bounds = arcadia.Bounds(back);
        Assert.True(bounds.Bottom <= ArcadiaWindow.Height && bounds.Top > ArcadiaWindow.Height - 80, $"{where}: Fußzeile bei {bounds}");
        var hit = arcadia.Window.InputHitTest(bounds.Center) as Visual;
        Assert.True(hit is null || hit.GetSelfAndVisualAncestors().Contains(arcadia.SettingsFooterBar()), $"{where}: {hit} liegt über der Fußzeile");
        Assert.Contains("B Zurück", arcadia.SettingsFooter());
    }

    /// <summary>The mouse moved for real, more than a resting pointer's jitter.</summary>
    private static void Wiggle(ArcadiaWindow arcadia) => arcadia.MouseMove(new Point(300, 300)).MouseMove(new Point(340, 330));

    /// <summary>The ✕ stands at the top right of its dialog (or of the whole page).</summary>
    private static void AssertTopRight(ArcadiaWindow arcadia, Button close, bool whole = false)
    {
        var dialog = whole
            ? new Rect(0, 0, ArcadiaWindow.Width, ArcadiaWindow.Height)
            : arcadia.Bounds(close.GetVisualAncestors().OfType<Border>().First(border => arcadia.Bounds(border).Width > 400));
        var bounds = arcadia.Bounds(close);
        Assert.True(bounds.Center.X > dialog.Right - (dialog.Width / 4) && bounds.Center.Y < dialog.Top + (dialog.Height / 4), $"✕ bei {bounds} nicht oben rechts in {dialog}");
    }
}
