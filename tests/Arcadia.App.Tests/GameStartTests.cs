using Arcadia.App.Input;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>A in the library: a tap starts the game, a hold opens its detail page (#10, #11).</summary>
public sealed class GameStartTests
{
    private static readonly TimeSpan Tap = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan BetweenFeedbackAndDetails = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("Testfall", "T10-4")]
    public async Task A_tapped_starts_the_focused_game_through_its_launcher()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Right, times: 2);
        var game = arcadia.Main.FocusedGame!.Game;

        arcadia.Hold(UiAction.Accept, Tap);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "der Launcher soll das Spiel starten");

        Assert.Equal([game.Key], arcadia.Starts.Keys);
        Assert.False(arcadia.Main.IsDetailOpen);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T10-4")]
    [Trait("Testfall", "T11-1")]
    public async Task A_held_for_a_second_opens_the_detail_page_of_the_focused_game_and_starts_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Right, times: 2);
        var game = arcadia.Main.FocusedGame!.Game;

        arcadia.Hold(UiAction.Accept, PastHoldTime);

        var detail = Assert.IsType<Arcadia.App.ViewModels.GameDetailViewModel>(arcadia.Main.Detail);
        Assert.Equal(game.Key, detail.Current.Tile.Game.Key);
        Assert.Equal("GOG Galaxy", detail.TabName);
        Assert.Equal("3 von 31", detail.PositionText);
        await Task.Delay(100);
        Assert.Empty(arcadia.Starts.Keys);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T10-4")]
    public async Task A_let_go_between_the_hold_feedback_and_the_details_does_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();

        arcadia.Hold(UiAction.Accept, BetweenFeedbackAndDetails);

        Assert.False(arcadia.Main.IsDetailOpen);
        await Task.Delay(100);
        Assert.Empty(arcadia.Starts.Keys);
    }
}
