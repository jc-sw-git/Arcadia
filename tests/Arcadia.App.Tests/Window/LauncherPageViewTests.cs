using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Settings;
using Arcadia.Core.Search;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>A launcher's own page in the settings (#137) as drawn: its head, its rows and writing a name, with every input.</summary>
public sealed class LauncherPageViewTests
{
    private static readonly IReadOnlyList<LauncherSpec> Launchers = LauncherSettingsDriving.WithoutFolders;

    [AvaloniaTheory]
    [InlineData("A")]
    [InlineData("Leertaste")]
    [InlineData("Klick")]
    [Trait("TestCase", "T137-1")]
    public async Task A_Space_or_a_click_opens_the_page_with_its_head_and_groups_and_B_Esc_or_Launcher_lead_back_to_it_in_the_list(string how)
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Launchers);
        arcadia.Press(UiAction.Settings).Press(UiAction.Down);
        Assert.Equal("xbox", arcadia.LauncherCategory().FocusedRow?.Key);

        switch (how)
        {
            case "A":
                arcadia.Press(UiAction.Accept);
                break;
            case "Leertaste":
                arcadia.Key(Key.Space);
                break;
            default:
                arcadia.Click(arcadia.LauncherName(arcadia.LauncherRow("xbox")));
                break;
        }

        // The head: logo, "Launcher /" with the name, the neighbours in the list's order.
        var head = Head(arcadia);
        Assert.Equal("xbox", arcadia.LauncherCategory().PageLauncherId);
        Assert.Single(head.GetVisualDescendants().OfType<Controls.LauncherLogo>(), logo => logo.IsEffectivelyVisible && logo.Bounds.Width >= 56);
        Assert.Equal(["Launcher", "/", "Xbox", "GOG Galaxy", "Amazon Games"], Texts(head));

        // B or Esc and LT/RT, Q/E as key chips: round for the controller, square for the keyboard.
        var glyphs = ArcadiaWindow.Glyphs(head);
        var controller = how == "A";
        Assert.Equal(controller ? ["B", "LT", "RT"] : ["Esc", "Q", "E"], glyphs.OrderBy(glyph => arcadia.Bounds(glyph).Left).Select(glyph => (glyph.Child as TextBlock)?.Text));
        Assert.All(glyphs, glyph => Assert.Equal(controller, glyph.CornerRadius.TopLeft >= (glyph.Bounds.Height / 2) - 0.5));

        // The groups "Allgemein", "Bibliothek" and "Controller" (#178) with their rows.
        var headings = arcadia.GroupHeadings();
        Assert.Equal(["ALLGEMEIN", "BIBLIOTHEK", "CONTROLLER"], headings.Select(heading => heading.Text));
        var rows = arcadia.Arcadia.Main.Settings!.Selected.Rows;
        Assert.Equal(["launcher-active", "launcher-name", "launcher-keyword", "launcher-logo", "launcher-rescan", "launcher-controller-mode"], rows.Select(row => row.Key));
        Assert.All(rows.Take(4), row => Assert.InRange(arcadia.Bounds(arcadia.RowButton(row)).Top, arcadia.Bounds(headings[0]).Bottom, arcadia.Bounds(headings[1]).Top));
        Assert.InRange(arcadia.Bounds(arcadia.RowButton(rows[4])).Top, arcadia.Bounds(headings[1]).Bottom, arcadia.Bounds(headings[2]).Top);
        Assert.True(arcadia.Bounds(arcadia.RowButton(rows[5])).Top > arcadia.Bounds(headings[2]).Bottom, "„Steam Controller“ steht unter „Controller“");

        switch (how)
        {
            case "A":
                arcadia.Press(UiAction.Back);
                break;
            case "Leertaste":
                arcadia.Key(Key.Escape);
                break;
            default:
                arcadia.Click(Breadcrumb(arcadia));
                break;
        }

        Assert.True(arcadia.Arcadia.Main.IsSettingsOpen, how);
        Assert.Null(arcadia.LauncherCategory().Head);
        Assert.Equal("xbox", arcadia.LauncherCategory().FocusedRow?.Key);
        Assert.Contains("focused", arcadia.RowButton(arcadia.LauncherRow("xbox")).Classes);
    }

    [AvaloniaTheory]
    [InlineData("A")]
    [InlineData("Leertaste")]
    [InlineData("Klick")]
    [Trait("TestCase", "T137-4")]
    public async Task Writing_the_name_opens_the_keyboard_below_without_Tab_and_Dock_with_the_caret_in_the_field_and_the_footer_naming_its_keys(string how)
    {
        using var arcadia = await OpenGogPageAsync();
        var name = arcadia.Arcadia.FocusRow("launcher-name");
        arcadia.Settle();

        switch (how)
        {
            case "A":
                arcadia.Press(UiAction.Accept);
                break;
            case "Leertaste":
                arcadia.Key(Key.Space);
                break;
            default:
                arcadia.Click(arcadia.RowButton(name));
                break;
        }

        var category = arcadia.Arcadia.Main.Settings!.Selected;
        Assert.True(category.IsEditing, how);
        Assert.Equal("GOG Galaxy", category.Editor!.Text);
        AssertField(arcadia, name, "GOG Galaxy", "");

        // The keyboard below the page, without ↹ and the key that docks it.
        arcadia.WaitForTransitions();
        var drawer = Drawer(arcadia);
        Assert.True(drawer.IsEffectivelyVisible && drawer.Classes.Contains("out"), "Die Tastatur ist ausgefahren");
        Assert.True(arcadia.Bounds(drawer).Top >= arcadia.Bounds(arcadia.RowButton(name)).Bottom, "Die Tastatur lässt die Zeile frei");
        Assert.Equal(arcadia.Bounds(arcadia.SettingsFooterBar()).Top, arcadia.Bounds(drawer).Bottom, tolerance: 2);
        var kinds = drawer.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("boardKey") && button.IsEffectivelyVisible)
            .Select(button => ((BoardKeyViewModel)button.DataContext!).Key.Kind)
            .ToHashSet();
        Assert.Contains(BoardKeyKind.Enter, kinds);
        Assert.DoesNotContain(BoardKeyKind.Tab, kinds);
        Assert.DoesNotContain(BoardKeyKind.Dock, kinds);

        Assert.Equal(
            how == "A"
                ? ["A Eingeben", "X Löschen", "Y Leerzeichen", "LB RB ⇧ / AltGr", "≡ Übernehmen", "B Verwerfen"]
                : ["Klick Eingeben", "⌫ Löschen", "Enter Übernehmen", "Esc Verwerfen"],
            arcadia.SettingsFooter());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-4")]
    public async Task With_the_controller_the_D_pad_and_A_type_X_erases_Y_spaces_R3_opens_the_layouts_Menu_takes_and_B_drops()
    {
        using var arcadia = await OpenGogPageAsync();
        var name = arcadia.Arcadia.FocusRow("launcher-name");
        arcadia.Press(UiAction.Accept);
        var editor = arcadia.Arcadia.Main.Settings!.Selected.Editor!;

        arcadia.Press(UiAction.Search, times: 3);
        foreach (var action in OnScreenKeys.PathTo(editor.Keyboard, editor.Keyboard.Keys.Single(key => key.IsCharacter && key.Key.Cap!.Normal == 'x')))
        {
            arcadia.Press(action);
        }

        arcadia.Press(UiAction.Accept).Press(UiAction.ViewOptions);
        AssertField(arcadia, name, "GOG Galx ", "");

        // R3 opens the layouts, B closes only them; B again drops the text.
        arcadia.Press(UiAction.KeyboardLayout);
        Assert.True(editor.Keyboard.LayoutMenu.IsOpen);
        arcadia.Press(UiAction.Back);
        Assert.False(editor.Keyboard.LayoutMenu.IsOpen);
        Assert.True(arcadia.Arcadia.Main.Settings.Selected.IsEditing);
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Arcadia.Main.Settings.Selected.IsEditing);
        Assert.False(Drawer(arcadia).IsEffectivelyVisible);
        Assert.Equal("GOG Galaxy", arcadia.ValueField(name));

        arcadia.Press(UiAction.Accept).Press(UiAction.Search).Press(UiAction.Settings);
        Assert.Equal("GOG Galax", arcadia.ValueField(name));
        Assert.Equal("GOG Galax", arcadia.Arcadia.TabNames()[0]);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-4")]
    public async Task With_a_real_keyboard_keys_type_the_arrows_Home_and_End_move_the_caret_Enter_takes_and_Esc_drops()
    {
        using var arcadia = await OpenGogPageAsync();
        var name = arcadia.Arcadia.FocusRow("launcher-name");
        arcadia.Key(Key.Space).Type("X");
        AssertField(arcadia, name, "GOG GalaxyX", "");

        arcadia.Key(Key.Left);
        AssertField(arcadia, name, "GOG Galaxy", "X");
        arcadia.Key(Key.Home);
        AssertField(arcadia, name, "", "GOG GalaxyX");
        arcadia.Key(Key.Right);
        AssertField(arcadia, name, "G", "OG GalaxyX");
        arcadia.Key(Key.End).Key(Key.Back);
        AssertField(arcadia, name, "GOG Galaxy", "");

        arcadia.Type("Z").Key(Key.Escape);
        Assert.False(arcadia.Arcadia.Main.Settings!.Selected.IsEditing);
        Assert.Equal("GOG Galaxy", arcadia.ValueField(name));

        arcadia.Key(Key.Space).Type(" 2").Key(Key.Enter);
        Assert.False(arcadia.Arcadia.Main.Settings.Selected.IsEditing);
        Assert.Equal("GOG Galaxy 2", arcadia.ValueField(name));
        Assert.Equal("GOG Galaxy 2", arcadia.Arcadia.TabNames()[0]);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-4")]
    public async Task While_the_name_is_written_the_mouse_focuses_no_other_row_and_clicks_type_on_the_keyboard()
    {
        using var arcadia = await OpenGogPageAsync();
        var name = arcadia.Arcadia.FocusRow("launcher-name");
        arcadia.Settle();
        arcadia.Click(arcadia.RowButton(name));
        var category = arcadia.Arcadia.Main.Settings!.Selected;
        Assert.True(category.IsEditing);

        var keyword = arcadia.Row("launcher-keyword");
        arcadia.MouseMove(arcadia.CentreOf(arcadia.RowButton(keyword))).MouseMove(arcadia.CentreOf(arcadia.RowButton(keyword)) + new Point(30, 4));
        Assert.Equal("launcher-name", category.FocusedRow?.Key);
        Assert.DoesNotContain("focused", arcadia.RowButton(keyword).Classes);
        arcadia.Click(arcadia.RowButton(keyword));
        Assert.Equal("launcher-name", category.FocusedRow?.Key);
        Assert.True(category.IsEditing);

        arcadia.Click(DrawnKey(arcadia, category.Editor!.Keyboard.Keys.Single(key => key.IsCharacter && key.Key.Cap!.Normal == 'q')));
        AssertField(arcadia, name, "GOG Galaxyq", "");
        arcadia.Click(DrawnKey(arcadia, category.Editor.Keyboard.Keys.Single(key => key.Key.Kind == BoardKeyKind.Enter)));
        Assert.False(category.IsEditing);
        Assert.Equal("GOG Galaxyq", arcadia.Arcadia.TabNames()[0]);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-8")]
    public async Task One_hand_on_the_keyboard_turns_the_launchers_switches_one_and_writes_a_keyword()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Launchers);
        arcadia.Key(Key.Enter).Key(Key.Space);
        Assert.Equal("gog", arcadia.LauncherCategory().PageLauncherId);

        // Q/E turn the launchers, ⇧ E changes the category.
        arcadia.Key(Key.E);
        Assert.Equal("Xbox", Texts(Head(arcadia))[2]);
        arcadia.Key(Key.Q, times: 2);
        Assert.Equal("Amazon Games", Texts(Head(arcadia))[2]);
        arcadia.Key(Key.E, KeyModifiers.Shift);
        Assert.Equal("Controller", arcadia.Arcadia.Main.Settings!.Selected.Name);
        // Back on the tab "Launcher": the list, on the launcher whose page was open; Space opens it again.
        arcadia.Key(Key.Q, KeyModifiers.Shift);
        Assert.Null(arcadia.LauncherCategory().PageLauncherId);
        arcadia.Key(Key.Space);
        Assert.Equal("amazon", arcadia.LauncherCategory().PageLauncherId);

        // D switches "Aktiv" on the page.
        arcadia.Arcadia.FocusRow("launcher-active");
        arcadia.Key(Key.D);
        Assert.Equal("Aus", arcadia.ValueShown(arcadia.Row("launcher-active")));
        Assert.Equal(["GOG Galaxy", "Xbox"], arcadia.Arcadia.TabNames());

        // A keyword taken by another launcher shows red at once and is not taken.
        var keyword = arcadia.Arcadia.FocusRow("launcher-keyword");
        arcadia.Key(Key.Space).Key(Key.Back, times: 6).Type("xbox");
        var field = Field(arcadia, keyword);
        Assert.Contains("problem", field.Classes);
        Assert.Contains(arcadia.RowButton(keyword).GetVisualDescendants().OfType<TextBlock>(), text => text.Classes.Contains("rowProblem") && text.IsEffectivelyVisible);
        arcadia.Key(Key.Enter);
        Assert.Equal("amazon", arcadia.ValueField(arcadia.Row("launcher-keyword")));
        Assert.StartsWith("Such-Kürzel nicht übernommen: ", arcadia.Arcadia.Main.Toast.Text, StringComparison.Ordinal);
        await LauncherViewTests.AssertToastOverThePageAsync(arcadia, arcadia.Arcadia.Main.Toast.Text!);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T137-8")]
    public async Task The_mouse_alone_turns_by_the_neighbours_with_their_whole_hover_ring_switches_and_points_at_rows()
    {
        using var arcadia = await OpenGogPageAsync();
        var (previous, next) = Neighbours(arcadia);

        // The hover ring of the neighbour on the right shows whole, also on its right.
        arcadia.MouseMove(arcadia.CentreOf(next)).MouseMove(arcadia.CentreOf(next) + new Point(6, 2));
        arcadia.AssertRingAround(next, "Nachbar beim Überfahren", "HoverRing");
        arcadia.Click(next);
        Assert.Equal("xbox", arcadia.LauncherCategory().PageLauncherId);
        (previous, _) = Neighbours(arcadia);
        arcadia.Click(previous);
        Assert.Equal("gog", arcadia.LauncherCategory().PageLauncherId);

        // Pointing at a row focuses it for the info panel; a click on the switch flips it.
        var category = arcadia.Arcadia.Main.Settings!.Selected;
        var keyword = arcadia.Row("launcher-keyword");
        arcadia.MouseMove(arcadia.CentreOf(arcadia.RowButton(keyword))).MouseMove(arcadia.CentreOf(arcadia.RowButton(keyword)) + new Point(30, 4));
        Assert.Equal("launcher-keyword", category.FocusedRow?.Key);
        Assert.Contains("focused", arcadia.RowButton(keyword).Classes);

        // As every switch of the settings, a click on its value flips it.
        arcadia.Click(arcadia.RowButton(arcadia.Row("launcher-active")).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("valueText")));
        Assert.Equal("Aus", arcadia.ValueShown(arcadia.Row("launcher-active")));
        Assert.Equal(["Xbox", "Amazon Games"], arcadia.Arcadia.TabNames());
    }

    /// <summary>Arcadia with the settings open on GOG's page, the first launcher's.</summary>
    private static async Task<ArcadiaWindow> OpenGogPageAsync()
    {
        var arcadia = await ArcadiaWindow.ShowAsync(Launchers);
        arcadia.Press(UiAction.Settings).Press(UiAction.Accept);
        Assert.Equal("gog", arcadia.LauncherCategory().PageLauncherId);
        return arcadia;
    }

    private static Control Head(ArcadiaWindow arcadia) => arcadia.Showing(arcadia.LauncherCategory().Head!);

    private static IReadOnlyList<string?> Texts(Control within) => within.GetVisualDescendants().OfType<TextBlock>()
        .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text) && !text.GetVisualAncestors().OfType<Border>().Any(border => border.Classes.Contains("glyph")))
        .Select(text => text.Text)
        .ToList();

    private static Button Breadcrumb(ArcadiaWindow arcadia) =>
        Head(arcadia).GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("breadcrumb"));

    private static (Button Previous, Button Next) Neighbours(ArcadiaWindow arcadia)
    {
        var neighbours = Head(arcadia).GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("neighbour") && !button.Classes.Contains("breadcrumb"))
            .OrderBy(button => arcadia.Bounds(button).Left)
            .ToList();
        return (neighbours[0], neighbours[1]);
    }

    private static Control Drawer(ArcadiaWindow arcadia) =>
        arcadia.Window.GetVisualDescendants().OfType<Controls.RevealPanel>().Single(panel => panel.Classes.Contains("keyboardDrawer") && panel.DataContext == arcadia.Arcadia.Main);

    private static Button DrawnKey(ArcadiaWindow arcadia, BoardKeyViewModel key) => Drawer(arcadia).GetVisualDescendants().OfType<Button>()
        .Single(button => button.Classes.Contains("boardKey") && button.DataContext == key);

    private static Border Field(ArcadiaWindow arcadia, SettingsRowViewModel row) =>
        arcadia.RowButton(row).GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("textField"));

    /// <summary>The field of a text being written: the text on each side of the caret, as drawn.</summary>
    private static void AssertField(ArcadiaWindow arcadia, SettingsRowViewModel row, string beforeCaret, string afterCaret)
    {
        var field = Field(arcadia, row);
        Assert.Contains("editing", field.Classes);
        var parts = field.GetVisualDescendants().OfType<Control>()
            .Where(part => part.IsEffectivelyVisible && (part is TextBlock && part.Classes.Contains("fieldText") || part is Border && part.Classes.Contains("fieldCaret")))
            .OrderBy(part => arcadia.Bounds(part).Left)
            .ToList();
        var caret = parts.FindIndex(part => part is Border);
        Assert.True(caret >= 0, "Der Cursor ist zu sehen");
        Assert.Equal(beforeCaret, string.Concat(parts.Take(caret).OfType<TextBlock>().Select(text => text.Text)));
        Assert.Equal(afterCaret, string.Concat(parts.Skip(caret + 1).OfType<TextBlock>().Select(text => text.Text)));
    }
}
