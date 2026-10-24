using Arcadia.App.Input;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The detail page as a carousel over the tab's games (#11).</summary>
public sealed class DetailPageTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("Testfall", "T11-2")]
    public async Task LT_and_RT_show_the_previous_and_next_game_across_page_borders_and_wrap_around()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var lastOfPageOne = arcadia.Main.PageGames[^1].Game;
        arcadia.Press(UiAction.NextPage);
        var firstOfPageTwo = arcadia.Main.PageGames[0].Game;
        var lastOfTab = arcadia.Main.PageGames[^1].Game;
        arcadia.Press(UiAction.PreviousPage).Press(UiAction.Right, times: 24);
        Assert.Same(lastOfPageOne, arcadia.Main.FocusedGame!.Game);

        arcadia.Hold(UiAction.Accept, PastHoldTime).Press(UiAction.NextPage);
        Assert.Equal(firstOfPageTwo.Key, arcadia.Main.Detail!.Current.Tile.Game.Key);
        Assert.Equal("26 von 31", arcadia.Main.Detail.PositionText);

        arcadia.Press(UiAction.PreviousPage, times: 26);
        Assert.Equal(lastOfTab.Key, arcadia.Main.Detail.Current.Tile.Game.Key);
        Assert.Equal("31 von 31", arcadia.Main.Detail.PositionText);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T11-6")]
    public async Task B_goes_back_to_the_library_on_the_page_of_the_game_shown_last_with_the_focus_on_it()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime).Press(UiAction.NextPage, times: 27);
        var shownLast = arcadia.Main.Detail!.Current.Tile.Game;
        Assert.Equal("28 von 31", arcadia.Main.Detail.PositionText);

        arcadia.Press(UiAction.Back);

        Assert.False(arcadia.Main.IsDetailOpen);
        Assert.True(arcadia.Main.AreGamesShown);
        Assert.Equal("2", arcadia.Main.PageButtons.Single(button => button.IsCurrent).Label);
        Assert.Equal(shownLast.Key, arcadia.Main.FocusedGame?.Game.Key);
    }
}
