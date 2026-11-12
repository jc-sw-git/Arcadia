using Arcadia.App.Controls;
using Arcadia.App.Settings;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests.Guards;

/// <summary>The settings page as drawn (#15): its rows and the category shown.</summary>
internal static class SettingsView
{
    public static Button RowButton(this ArcadiaWindow arcadia, SettingsRowViewModel row) => Assert.Single(
        arcadia.Named<ItemsControl>("SettingsRows").GetVisualDescendants().OfType<Button>(),
        button => button.Classes.Contains("settingRow") && button.DataContext == row);

    /// <summary>The category "Launcher" as shown now: a launcher switched makes it anew.</summary>
    public static LauncherSettingsViewModel LauncherCategory(this ArcadiaWindow arcadia) => Assert.IsType<LauncherSettingsViewModel>(arcadia.Arcadia.Main.Settings?.Selected);
}
