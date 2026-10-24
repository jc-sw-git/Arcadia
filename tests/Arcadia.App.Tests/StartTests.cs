using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

public sealed class StartTests
{
    [AvaloniaFact]
    [Trait("Testfall", "G-1")]
    public async Task Start_shows_a_tab_per_launcher_with_the_first_tab_selected_and_a_game_focused()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();

        Assert.Equal(["GOG Galaxy", "Xbox", "Amazon Games"], arcadia.Main.Tabs.Select(tab => tab.Name));
        Assert.Same(arcadia.Main.Tabs[0], arcadia.Main.SelectedTab);
        Assert.NotNull(arcadia.Main.FocusedGame);
        Assert.Same(arcadia.Main.PageGames[0], arcadia.Main.FocusedGame);
        Assert.True(arcadia.Main.AreGamesShown);
    }
}
