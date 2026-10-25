using Arcadia.App.Input;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The settings page over the library (#15).</summary>
public sealed class SettingsTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T15-1")]
    public async Task Menu_opens_the_settings_and_B_goes_back_to_the_same_tab_page_and_game()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.NextTab).Press(UiAction.NextPage).Press(UiAction.Right, times: 2).Press(UiAction.Down);
        var focused = arcadia.Main.FocusedGame!.Game.Key;

        arcadia.Press(UiAction.Settings);
        var settings = Assert.IsType<Arcadia.App.Settings.SettingsPageViewModel>(arcadia.Main.Settings);

        // The controller drives the settings, not the library underneath.
        arcadia.Press(UiAction.Down).Press(UiAction.NextTab);
        Assert.Equal("Controller", settings.Selected.Name);
        Assert.Equal(focused, arcadia.Main.FocusedGame?.Game.Key);
        Assert.Equal("Xbox", arcadia.Main.SelectedTab?.Name);

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Equal("Xbox", arcadia.Main.SelectedTab?.Name);
        Assert.Equal("2", arcadia.Main.PageButtons.Single(button => button.IsCurrent).Label);
        Assert.Equal(focused, arcadia.Main.FocusedGame?.Game.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-31")]
    public async Task The_settings_are_worked_with_the_controller_alone_rows_categories_and_values()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Settings);
        var settings = arcadia.Main.Settings!;
        Assert.Equal("Launcher", settings.Selected.Name);
        Assert.Equal("gog", settings.FocusedRow?.Key);

        // Rows: down to Xbox and switch it off with ◀.
        arcadia.Press(UiAction.Down);
        Assert.Equal("xbox", settings.FocusedRow?.Key);
        arcadia.Press(UiAction.Left);
        Assert.Equal(["gog", "amazon"], arcadia.Settings.Current.ActiveLaunchers);

        // Categories with RB, after the last one back to the first.
        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Controller", settings.Selected.Name);

        // A choice: "Mausmodus" from "Von Hand" one step on with ▶.
        arcadia.Press(UiAction.Down);
        Assert.Equal("mouse-mode", settings.FocusedRow?.Key);
        Assert.Equal(MouseModeActivation.Manual, arcadia.Settings.Current.MouseMode.Activation);
        arcadia.Press(UiAction.Right);
        Assert.Equal(MouseModeActivation.Automatic, arcadia.Settings.Current.MouseMode.Activation);

        arcadia.Press(UiAction.NextTab, times: 2);
        Assert.Equal("Launcher", settings.Selected.Name);

        // B closes; the library shows the launcher switched off as gone.
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Equal(["GOG Galaxy", "Amazon Games"], arcadia.Main.Tabs.Select(tab => tab.Name));
    }
}
