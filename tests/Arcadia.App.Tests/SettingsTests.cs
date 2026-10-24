using Arcadia.App.Input;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The settings page over the library (#15).</summary>
public sealed class SettingsTests
{
    [AvaloniaFact]
    [Trait("Testfall", "T15-1")]
    [Trait("Testfall", "G-31")]
    public async Task Menu_opens_the_settings_and_B_goes_back_to_the_same_tab_page_and_game()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.NextTab).Press(UiAction.NextPage).Press(UiAction.Right, times: 2).Press(UiAction.Down);
        var focused = arcadia.Main.FocusedGame!.Game.Key;

        arcadia.Press(UiAction.Settings);
        var settings = Assert.IsType<Arcadia.App.Settings.SettingsPageViewModel>(arcadia.Main.Settings);
        Assert.NotNull(settings.FocusedRow);

        // The library underneath ignores the controller while the settings are open.
        arcadia.Press(UiAction.Down).Press(UiAction.NextTab);
        Assert.Equal(focused, arcadia.Main.FocusedGame?.Game.Key);

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
        Assert.Equal("Xbox", arcadia.Main.SelectedTab?.Name);
        Assert.Equal("2", arcadia.Main.PageButtons.Single(button => button.IsCurrent).Label);
        Assert.Equal(focused, arcadia.Main.FocusedGame?.Game.Key);
    }
}
