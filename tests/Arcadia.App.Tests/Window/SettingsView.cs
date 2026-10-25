using Arcadia.App.Controls;
using Arcadia.App.Settings;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>The settings page as drawn (#15): its rows, values, info panel, footer and the dialogs' ✕.</summary>
internal static class SettingsView
{
    public static Button RowButton(this ArcadiaWindow arcadia, SettingsRowViewModel row) => Assert.Single(
        arcadia.Named<ItemsControl>("SettingsRows").GetVisualDescendants().OfType<Button>(),
        button => button.Classes.Contains("settingRow") && button.DataContext == row);

    /// <summary>The value drawn in the row's pill, e.g. "3 Sekunden".</summary>
    public static string? ValueShown(this ArcadiaWindow arcadia, SettingsRowViewModel row) => arcadia.RowButton(row).GetVisualDescendants().OfType<TextBlock>()
        .Single(text => text.Classes.Contains("valueText") && text.IsEffectivelyVisible).Text;

    /// <summary>The text drawn in the row's field while it isn't written, e.g. a launcher's name (#137).</summary>
    public static string? ValueField(this ArcadiaWindow arcadia, SettingsRowViewModel row) => arcadia.RowButton(row).GetVisualDescendants().OfType<TextBlock>()
        .Single(text => text.Classes.Contains("fieldText") && text.IsEffectivelyVisible).Text;

    public static IReadOnlyList<Button> ValueArrows(this ArcadiaWindow arcadia, SettingsRowViewModel row) => arcadia.RowButton(row).GetVisualDescendants().OfType<Button>()
        .Where(button => button.Classes.Contains("valueArrow"))
        .OrderBy(button => arcadia.Bounds(button).Left)
        .ToList();

    public static Control InfoPanel(this ArcadiaWindow arcadia) => arcadia.Named<ScrollViewer>("InfoScroller");

    /// <summary>The settings' footer, left to right, as in <see cref="ArcadiaWindow.FooterHints"/>.</summary>
    public static IReadOnlyList<string> SettingsFooter(this ArcadiaWindow arcadia) =>
        arcadia.Hints(arcadia.SettingsFooterBar().GetVisualDescendants().OfType<KeyHint>().Where(hint => hint.IsEffectivelyVisible));

    public static Border SettingsFooterBar(this ArcadiaWindow arcadia) => (Border)arcadia.ShowingAll(arcadia.Arcadia.Main.Settings!).Single(control => control is Border && Grid.GetRow(control) == 2);

    public static Button? VisibleClose(this ArcadiaWindow arcadia, System.Windows.Input.ICommand command) =>
        arcadia.Window.GetVisualDescendants().OfType<Button>().SingleOrDefault(button => button.Classes.Contains("dialogClose") && button.IsEffectivelyVisible && button.Command == command);

    public static Button CloseButton(this ArcadiaWindow arcadia, System.Windows.Input.ICommand command) =>
        arcadia.VisibleClose(command) ?? throw new InvalidOperationException("Kein ✕ zu sehen.");

    /// <summary>The category "Launcher" as shown now: a launcher switched makes it anew.</summary>
    public static LauncherSettingsViewModel LauncherCategory(this ArcadiaWindow arcadia) => Assert.IsType<LauncherSettingsViewModel>(arcadia.Arcadia.Main.Settings?.Selected);

    /// <summary>The row of a launcher in the list (#136), as shown now.</summary>
    public static LauncherRowViewModel LauncherRow(this ArcadiaWindow arcadia, string key) =>
        arcadia.LauncherCategory().Rows.OfType<LauncherRowViewModel>().Single(row => row.Key == key);

    /// <summary>The row <paramref name="key"/> of the shown category, e.g. "launcher-name" on a launcher's page (#137).</summary>
    public static SettingsRowViewModel Row(this ArcadiaWindow arcadia, string key) =>
        arcadia.Arcadia.Main.Settings!.Selected.Rows.Single(row => row.Key == key);

    public static TextBlock LauncherName(this ArcadiaWindow arcadia, LauncherRowViewModel row) =>
        arcadia.RowButton(row).GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("launcherName"));

    /// <summary>The part of a switch a click anywhere on flips it.</summary>
    public static Button Toggle(this ArcadiaWindow arcadia, SettingsRowViewModel row) =>
        arcadia.RowButton(row).GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("valueToggle"));

    /// <summary>The group headings drawn above the rows, top to bottom.</summary>
    public static IReadOnlyList<TextBlock> GroupHeadings(this ArcadiaWindow arcadia) => arcadia.Named<ItemsControl>("SettingsRows").GetVisualDescendants().OfType<TextBlock>()
        .Where(text => text.Classes.Contains("settingsGroup") && text.IsEffectivelyVisible)
        .OrderBy(text => arcadia.Bounds(text).Top)
        .ToList();
}
