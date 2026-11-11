using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Input;
using Arcadia.Launchers;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The search's hits go round at their ends (#476): left/right past the first or last hit, up/down in the grid past
/// its first or last row, LT/RT past the first or last window; a hold stops at the end. 40 hits: the row shows 7, the
/// grid two rows of 7, so its windows start at 0, 14 and 28, and its last row holds hits 35 to 39.
/// </summary>
public sealed class SearchWrapTests
{
    private static readonly TimeSpan Tap = TimeSpan.FromMilliseconds(100);

    [AvaloniaFact]
    [Trait("TestCase", "T476-2")]
    public async Task The_right_stick_in_the_row_goes_round_from_the_first_hit_to_the_last_and_back()
    {
        using var arcadia = await SearchingAsync();
        var search = arcadia.Search!;
        Assert.True(search.IsKeyboardOut);

        arcadia.Press(UiAction.SecondaryLeft);
        Assert.Equal(("Spiel 40", 33), (search.FocusedResult?.Title, search.WindowStart));
        arcadia.Press(UiAction.SecondaryRight);
        Assert.Equal(("Spiel 01", 0), (search.FocusedResult?.Title, search.WindowStart));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-2")]
    [Trait("TestCase", "T476-3")]
    public async Task The_D_pad_in_the_grid_goes_round_left_and_right_and_up_and_down()
    {
        using var arcadia = await GridAsync();
        var search = arcadia.Search!;

        arcadia.Press(UiAction.Left);
        Assert.Equal(("Spiel 40", 28), (search.FocusedResult?.Title, search.WindowStart));
        arcadia.Press(UiAction.Right);
        Assert.Equal(("Spiel 01", 0), (search.FocusedResult?.Title, search.WindowStart));

        // Up in the first row: the last row, same column (hit 38 is column 3 of the row 35 to 39); down goes back.
        arcadia.Press(UiAction.Right, times: 3).Press(UiAction.Up);
        Assert.Equal(("Spiel 39", 28), (search.FocusedResult?.Title, search.WindowStart));
        arcadia.Press(UiAction.Down);
        Assert.Equal(("Spiel 04", 0), (search.FocusedResult?.Title, search.WindowStart));

        // Column 6 is empty in the last row: up lands on the last hit.
        arcadia.Press(UiAction.Right, times: 3).Press(UiAction.Up);
        Assert.Equal("Spiel 40", search.FocusedResult?.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-9")]
    public async Task A_held_D_pad_or_RT_in_the_grid_stops_at_the_end_and_a_fresh_press_goes_round()
    {
        using var arcadia = await GridAsync();
        var search = arcadia.Search!;

        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.RightTrigger);
        Assert.Equal(28, search.WindowStart);
        arcadia.HoldPad(Tap, PadButtons.RightTrigger);
        Assert.Equal(0, search.WindowStart);

        // Held down from the first row: row by row to the last, and there it stops.
        arcadia.HoldPad(TimeSpan.FromSeconds(2), PadButtons.DPadDown);
        Assert.Equal("Spiel 36", search.FocusedResult?.Title);
        arcadia.HoldPad(Tap, PadButtons.DPadDown);
        Assert.Equal("Spiel 01", search.FocusedResult?.Title);
    }

    /// <summary>A half-pulled trigger that dips below half way and back is still held: no fresh press, no turn round.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T476-14")]
    public async Task A_held_RT_wobbling_round_half_way_stays_at_the_last_window()
    {
        using var arcadia = await GridAsync();
        var search = arcadia.Search!;
        var pulled = new TriggerPosition(0, 0.6);

        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.RightTrigger, letGo: false, triggers: pulled);
        Assert.Equal(28, search.WindowStart);

        arcadia.HoldPad(TimeSpan.FromMilliseconds(30), letGo: false, triggers: new TriggerPosition(0, 0.45));
        arcadia.HoldPad(Tap, PadButtons.RightTrigger, triggers: pulled);
        Assert.Equal(28, search.WindowStart);

        // Let go, pulled afresh: round to the first window.
        arcadia.HoldPad(Tap, PadButtons.RightTrigger, triggers: pulled);
        Assert.Equal(0, search.WindowStart);
    }

    private static async Task<ArcadiaDriver> SearchingAsync()
    {
        var games = Enumerable.Range(1, 40)
            .Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:00}", ArcadiaDriver.Now.AddDays(-i)))
            .ToList();
        var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, games.Count, Library: games)]);
        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("spiel");
        Assert.Equal(40, arcadia.Search!.Hits.Count);
        Assert.Equal("Spiel 01", arcadia.Search.FocusedResult?.Title);
        return arcadia;
    }

    /// <summary>The search with the keyboard put away: the grid of two rows, the D-pad on the hits.</summary>
    private static async Task<ArcadiaDriver> GridAsync()
    {
        var arcadia = await SearchingAsync();
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Search!.IsKeyboardOut);
        return arcadia;
    }
}
