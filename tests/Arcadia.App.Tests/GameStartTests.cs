using Arcadia.App.Input;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// A in the library (T10-4): a tap starts the game on the release, a hold shows the frame after about 0.2 s and opens
/// the detail page after 1 s without letting go; letting go in between does nothing.
/// </summary>
public sealed class GameStartTests
{
    private static readonly TimeSpan Tap = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PastFeedback = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan ShortOfHoldTime = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("Testfall", "T10-4")]
    public async Task A_tapped_starts_the_focused_game_through_its_launcher_on_the_release()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Right, times: 2);
        var tile = arcadia.Main.FocusedGame!;
        var game = tile.Game;

        arcadia.PressDown(UiAction.Accept).Wait(Tap);
        Assert.False(tile.IsRunningOrStarting);
        Assert.Empty(arcadia.Starts.Keys);
        Assert.False(tile.IsHolding);

        // The start is requested synchronously on the release; this is what lets the other tests assert "no start"
        // without waiting for the thread pool.
        arcadia.LetGo(UiAction.Accept);
        Assert.True(tile.IsStarting);
        await arcadia.WaitUntilAsync(() => arcadia.Starts.Keys.Count > 0, "der Launcher soll das Spiel starten");

        Assert.Equal([game.Key], arcadia.Starts.Keys);
        Assert.False(arcadia.Main.IsDetailOpen);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T10-4")]
    [Trait("Testfall", "T11-1")]
    public async Task A_held_shows_the_frame_then_opens_the_detail_page_after_a_second_while_still_held_and_starts_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Right, times: 2);
        var tile = arcadia.Main.FocusedGame!;

        arcadia.PressDown(UiAction.Accept).Wait(PastFeedback);
        Assert.True(tile.IsHolding);
        Assert.False(arcadia.Main.IsDetailOpen);

        arcadia.Wait(ShortOfHoldTime - PastFeedback);
        Assert.False(arcadia.Main.IsDetailOpen);

        arcadia.Wait(PastHoldTime - ShortOfHoldTime);
        var detail = Assert.IsType<Arcadia.App.ViewModels.GameDetailViewModel>(arcadia.Main.Detail);
        Assert.Equal(tile.Game.Key, detail.Current.Tile.Game.Key);
        Assert.Equal("GOG Galaxy", detail.TabName);
        Assert.Equal("3 von 31", detail.PositionText);

        arcadia.LetGo(UiAction.Accept);
        Assert.Same(detail, arcadia.Main.Detail);
        Assert.False(tile.IsRunningOrStarting);
        Assert.Empty(arcadia.Starts.Keys);
    }

    [AvaloniaFact]
    [Trait("Testfall", "T10-4")]
    public async Task A_let_go_between_the_frame_and_the_details_does_nothing_also_later()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var tile = arcadia.Main.FocusedGame!;

        arcadia.PressDown(UiAction.Accept).Wait(PastFeedback);
        Assert.True(tile.IsHolding);

        arcadia.LetGo(UiAction.Accept);
        Assert.False(tile.IsHolding);

        // Well past the moment the details would have opened: no timer of the cancelled hold may still fire.
        arcadia.Wait(TimeSpan.FromSeconds(2));
        Assert.False(arcadia.Main.IsDetailOpen);
        Assert.False(tile.IsRunningOrStarting);
        Assert.Empty(arcadia.Starts.Keys);
    }
}
