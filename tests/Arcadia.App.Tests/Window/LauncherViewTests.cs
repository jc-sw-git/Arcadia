using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>The category "Launcher" (#136) as drawn: switching, moving and reading launchers, and their empty tabs, with every input.</summary>
public sealed class LauncherViewTests
{
    private static readonly IReadOnlyList<LauncherSpec> Launchers = LauncherSettingsDriving.WithoutFolders;

    [AvaloniaTheory]
    [InlineData("◀")]
    [InlineData("▶")]
    [InlineData("A")]
    [InlineData("D")]
    [InlineData("Klick")]
    [Trait("TestCase", "T136-2")]
    public async Task A_launcher_switched_off_goes_under_Nicht_aktiv_with_its_tab_and_hits_and_switched_on_again_comes_last_and_is_read(string how)
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Launchers);
        var main = arcadia.Arcadia.Main;
        arcadia.Press(UiAction.Settings).Press(UiAction.Down);
        Assert.Equal("xbox", arcadia.LauncherCategory().FocusedRow?.Key);
        Assert.Equal("Aktiv", arcadia.ValueShown(arcadia.LauncherRow("xbox")));

        Switch(arcadia, arcadia.LauncherRow("xbox"), how);
        Assert.Equal("Aus", arcadia.ValueShown(arcadia.LauncherRow("xbox")));
        Assert.Equal(["gog", "amazon", "xbox"], arcadia.LauncherCategory().LauncherKeys());
        var inactive = Assert.Single(arcadia.GroupHeadings(), heading => heading.Text!.StartsWith("NICHT AKTIV", StringComparison.Ordinal));
        Assert.True(arcadia.Bounds(arcadia.RowButton(arcadia.LauncherRow("xbox"))).Top > arcadia.Bounds(inactive).Bottom, "Xbox steht unter „Nicht aktiv“");
        Assert.Equal(["GOG Galaxy", "Amazon Games"], arcadia.Arcadia.TabNames());

        arcadia.Press(UiAction.Back).Key(Key.F).Type("Ember");
        Assert.Empty(arcadia.Arcadia.Search!.Results);
        Assert.Equal(["GOG Galaxy", "Amazon Games"], DrawnTabs(arcadia));
        arcadia.Press(UiAction.Back).Press(UiAction.Settings);
        arcadia.Arcadia.FocusRow("xbox");
        arcadia.Settle();

        Switch(arcadia, arcadia.LauncherRow("xbox"), how);
        Assert.Equal("Aktiv", arcadia.ValueShown(arcadia.LauncherRow("xbox")));
        Assert.Equal(["gog", "amazon", "xbox"], arcadia.LauncherCategory().LauncherKeys());
        Assert.Equal(["GOG Galaxy", "Amazon Games", "Xbox"], arcadia.Arcadia.TabNames());
        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.LauncherRow("xbox").Fact("Einträge") == "3 Spiele", "Xbox soll eingelesen werden");

        arcadia.Press(UiAction.Back);
        Assert.Equal(["GOG Galaxy", "Amazon Games", "Xbox"], DrawnTabs(arcadia));
        arcadia.Key(Key.F).Type("Ember");
        Assert.Equal(["Ember Hollow"], arcadia.Arcadia.Search!.Results.Select(hit => hit.Title));

        using var restarted = await arcadia.RestartAsync();
        Assert.Equal(["GOG Galaxy", "Amazon Games", "Xbox"], DrawnTabs(restarted));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-8")]
    public async Task One_hand_on_the_keyboard_lifts_moves_puts_down_cancels_and_reads_all_again_with_the_toast_over_the_page()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Launchers);
        arcadia.Key(Key.Enter);
        Assert.Equal("gog", arcadia.LauncherCategory().FocusedRow?.Key);

        // V lifts, S moves, V puts down: the tabs follow at once.
        arcadia.Key(Key.V);
        Assert.Contains("lifted", arcadia.RowButton(arcadia.LauncherRow("gog")).Classes);
        Assert.Equal(["W S / Ziehen Verschieben", "V / Loslassen Ablegen", "Esc Abbrechen"], arcadia.SettingsFooter());
        arcadia.Key(Key.S);
        Assert.Equal(["xbox", "gog", "amazon"], arcadia.LauncherCategory().LauncherKeys());
        Assert.True(arcadia.Bounds(arcadia.RowButton(arcadia.LauncherRow("gog"))).Top > arcadia.Bounds(arcadia.RowButton(arcadia.LauncherRow("xbox"))).Bottom, "GOG steht unter Xbox");
        arcadia.Key(Key.V);
        Assert.DoesNotContain("lifted", arcadia.RowButton(arcadia.LauncherRow("gog")).Classes);
        Assert.Equal(["Xbox", "GOG Galaxy", "Amazon Games"], arcadia.Arcadia.TabNames());

        // Esc while lifted puts everything back.
        arcadia.Key(Key.V).Key(Key.S).Key(Key.Escape);
        Assert.True(arcadia.Arcadia.Main.IsSettingsOpen);
        Assert.False(arcadia.LauncherCategory().IsReordering);
        Assert.Equal(["xbox", "gog", "amazon"], arcadia.LauncherCategory().LauncherKeys());

        // A and D switch; F reads all again, the toast stands over the page.
        arcadia.Key(Key.A);
        Assert.Equal("Aus", arcadia.ValueShown(arcadia.LauncherRow("gog")));
        arcadia.Arcadia.FocusRow("gog");
        arcadia.Key(Key.D);
        Assert.Equal("Aktiv", arcadia.ValueShown(arcadia.LauncherRow("gog")));
        arcadia.Key(Key.F);
        await AssertToastOverThePageAsync(arcadia, "Alle Launcher sind neu eingelesen.");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-8")]
    public async Task The_mouse_alone_drags_a_launcher_to_another_place_or_under_Nicht_aktiv_and_reads_all_again()
    {
        using var arcadia = await ArcadiaWindow.ShowAsync(Launchers, new AppSettings { ActiveLaunchers = ["gog", "xbox"] });
        arcadia.Click(arcadia.Window.GetVisualDescendants().OfType<Button>().Single(button => button.IsEffectivelyVisible && ToolTip.GetTip(button) is "Einstellungen (Enter)"));

        // GOG dragged below Xbox: the tabs follow.
        Drag(arcadia, "gog", below: "xbox");
        Assert.Equal(["xbox", "gog", "amazon"], arcadia.LauncherCategory().LauncherKeys());
        Assert.Equal(["Xbox", "GOG Galaxy"], arcadia.Arcadia.TabNames());
        Assert.True(arcadia.LauncherRow("gog").IsActive, "Ziehen schaltet nicht um");

        // Xbox dragged under the inactive Amazon: it is off.
        Drag(arcadia, "xbox", below: "amazon");
        // Off, it stands at the top of the inactive ones.
        Assert.Equal(["gog", "xbox", "amazon"], arcadia.LauncherCategory().LauncherKeys());
        Assert.False(arcadia.LauncherRow("xbox").IsActive);
        Assert.Equal("Aus", arcadia.ValueShown(arcadia.LauncherRow("xbox")));
        Assert.Equal(["GOG Galaxy"], arcadia.Arcadia.TabNames());
        Assert.Equal(["gog"], arcadia.Arcadia.Settings.Current.ActiveLaunchers);

        // A click on the switch of Amazon, then on "Alle neu einlesen".
        arcadia.Click(arcadia.Toggle(arcadia.LauncherRow("amazon")));
        Assert.Equal(["GOG Galaxy", "Amazon Games"], arcadia.Arcadia.TabNames());
        var readAll = Assert.Single(arcadia.LauncherCategory().Rows, row => row.Title == "Alle neu einlesen");
        arcadia.Click(arcadia.RowButton(readAll));
        await AssertToastOverThePageAsync(arcadia, "Alle Launcher sind neu eingelesen.");
    }

    [AvaloniaTheory]
    [InlineData("Tastatur")]
    [InlineData("Maus")]
    [Trait("TestCase", "T136-8")]
    public async Task The_buttons_of_a_tab_without_games_and_of_no_launcher_active_work_with_the_keyboard_and_the_mouse(string how)
    {
        IReadOnlyList<LauncherSpec> launchers = [new(KnownLaunchers.Gog, 31), new(KnownLaunchers.Xbox, 0)];
        using var arcadia = await ArcadiaWindow.ShowAsync(launchers);
        var main = arcadia.Arcadia.Main;
        arcadia.Key(Key.E, KeyModifiers.Shift);
        var empty = main.Library.EmptyTab!;
        Assert.Equal("Keine Spiele in Xbox", empty.Title);
        Assert.Contains("focused", EmptyButton(arcadia, empty.OpenSettingsCommand).Classes);

        if (how == "Tastatur")
        {
            arcadia.Key(Key.D);
            Assert.Contains("focused", EmptyButton(arcadia, empty.RescanCommand).Classes);
            arcadia.Key(Key.Space);
        }
        else
        {
            arcadia.Click(EmptyButton(arcadia, empty.RescanCommand));
        }

        await AssertToastOverThePageAsync(arcadia, "Weiterhin keine Spiele in Xbox.");

        if (how == "Tastatur")
        {
            arcadia.Key(Key.A).Key(Key.Space);
        }
        else
        {
            arcadia.Click(EmptyButton(arcadia, empty.OpenSettingsCommand));
        }

        Assert.True(main.IsSettingsOpen, how);
        Assert.Equal("xbox", main.Settings!.Selected.FocusedRow?.Key);

        // Every launcher off: "Kein Launcher aktiv" offers only the launcher settings.
        arcadia.Key(Key.A);
        arcadia.Arcadia.FocusRow("gog");
        arcadia.Key(Key.A).Key(Key.Escape);
        var none = main.Library.EmptyTab!;
        Assert.Equal("Kein Launcher aktiv", none.Title);
        Assert.False(EmptyButton(arcadia, none.RescanCommand).IsEffectivelyVisible);
        if (how == "Tastatur")
        {
            arcadia.Key(Key.Space);
        }
        else
        {
            arcadia.Click(EmptyButton(arcadia, none.OpenSettingsCommand));
        }

        Assert.Equal("Launcher", main.Settings?.Selected.Name);
    }

    private static void Switch(ArcadiaWindow arcadia, LauncherRowViewModel row, string how)
    {
        switch (how)
        {
            case "◀":
                arcadia.Press(UiAction.Left);
                break;
            case "▶":
                arcadia.Press(UiAction.Right);
                break;
            case "A":
                arcadia.Key(Key.A);
                break;
            case "D":
                arcadia.Key(Key.D);
                break;
            default:
                arcadia.Click(arcadia.Toggle(row));
                break;
        }
    }

    /// <summary>The row of <paramref name="key"/> pressed on its name, moved below the middle of the row <paramref name="below"/> and let go.</summary>
    private static void Drag(ArcadiaWindow arcadia, string key, string below)
    {
        var start = arcadia.CentreOf(arcadia.LauncherName(arcadia.LauncherRow(key)));
        var target = arcadia.Bounds(arcadia.RowButton(arcadia.LauncherRow(below)));
        var end = new Point(start.X, target.Center.Y + (target.Height / 4));
        arcadia.MouseDown(start);
        for (var step = 1; step <= 4; step++)
        {
            arcadia.MouseMove(new Point(start.X, start.Y + ((end.Y - start.Y) * step / 4)));
        }

        Assert.Contains("lifted", arcadia.RowButton(arcadia.LauncherRow(key)).Classes);
        arcadia.MouseUp(end);
        Assert.DoesNotContain("lifted", arcadia.RowButton(arcadia.LauncherRow(key)).Classes);
    }

    /// <summary>The tabs drawn in the header, left to right.</summary>
    private static IReadOnlyList<string> DrawnTabs(ArcadiaWindow arcadia)
    {
        arcadia.WaitForTransitions();
        return arcadia.Arcadia.Main.LauncherTabs.Items
            .Select(tab => (tab.Name, Control: arcadia.Showing(tab)))
            .Where(tab => tab.Control.IsEffectivelyVisible)
            .OrderBy(tab => arcadia.Bounds(tab.Control).Left)
            .Select(tab => tab.Name)
            .ToList();
    }

    private static Button EmptyButton(ArcadiaWindow arcadia, System.Windows.Input.ICommand command) =>
        arcadia.Window.GetVisualDescendants().OfType<Button>().Single(button => button.Command == command);

    internal static async Task AssertToastOverThePageAsync(ArcadiaWindow arcadia, string text)
    {
        await arcadia.Arcadia.WaitUntilAsync(() => arcadia.Arcadia.Main.Toast.Text == text, $"der Toast „{text}“");
        arcadia.WaitForTransitions();
        var toast = arcadia.OneWithText(text).FindAncestorOfType<Border>()!;
        Assert.True(toast.IsEffectivelyVisible, "Der Toast ist zu sehen");
        arcadia.AssertOnTop(toast);
    }
}
