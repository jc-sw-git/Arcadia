using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Input;
using Arcadia.Launchers;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// Held LT/RT and LB/RB (#476): they turn on after 400 ms, then every 150 ms; a hold stops at the end once, only a fresh
/// press goes round; a hold ends where the page, a dialog or the layout changed. The default library: Gog 31 games,
/// Xbox 62, Amazon 6, in this tab order.
/// </summary>
public sealed class HeldButtonsTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T476-8")]
    public async Task Held_RT_turns_the_detail_page_on_after_400_ms_and_then_every_150_ms()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal("1 von 31", arcadia.Detail!.PositionText);

        // Shorter than the delay: one turn, as a tap.
        arcadia.HoldPad(TimeSpan.FromMilliseconds(390), PadButtons.RightTrigger);
        Assert.Equal("2 von 31", arcadia.Detail.PositionText);

        // A second: the press, then repeats at 400, 550, 700 and 850 ms (read every 8 ms).
        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.RightTrigger);
        Assert.Equal("7 von 31", arcadia.Detail.PositionText);

        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.LeftTrigger);
        Assert.Equal("2 von 31", arcadia.Detail.PositionText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-8")]
    [Trait("TestCase", "T476-9")]
    public async Task Held_RB_turns_the_tabs_and_stops_at_the_last_one_until_a_fresh_press()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        Assert.Equal(KnownLaunchers.Gog.Id, arcadia.SelectedTab!.Launcher.Id);

        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.RightShoulder);
        Assert.Equal(KnownLaunchers.Amazon.Id, arcadia.SelectedTab!.Launcher.Id);

        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.RightShoulder);
        Assert.Equal(KnownLaunchers.Gog.Id, arcadia.SelectedTab!.Launcher.Id);

        // Held LB from the first tab: the press goes round to the last, the repeats on to the first, and there it stops.
        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.LeftShoulder);
        Assert.Equal(KnownLaunchers.Gog.Id, arcadia.SelectedTab!.Launcher.Id);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-9")]
    public async Task Held_RT_stops_at_the_detail_pages_last_game_and_a_fresh_press_goes_round()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime).Press(UiAction.PreviousPage, times: 2);
        Assert.Equal("30 von 31", arcadia.Detail!.PositionText);

        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.RightTrigger);
        Assert.Equal("31 von 31", arcadia.Detail.PositionText);

        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.RightTrigger);
        Assert.Equal("1 von 31", arcadia.Detail.PositionText);
    }

    /// <summary>A ◀ ▶ selector in the settings, as a held D-pad steps it: it stops at the last value, a fresh press wraps.</summary>
    [Fact]
    [Trait("TestCase", "T476-9")]
    public void A_held_arrow_stops_at_the_end_of_a_selector()
    {
        var value = 8;
        var row = new Settings.ChoiceRowViewModel("speed", "Tempo", () => value, chosen => value = chosen, shown => shown.ToString(), 1, 9);

        HeldPress.Run(repeat: true, () => row.Change(1));
        HeldPress.Run(repeat: true, () => row.Change(1));
        Assert.Equal(9, value);

        HeldPress.Run(repeat: false, () => row.Change(1));
        Assert.Equal(1, value);
        HeldPress.Run(repeat: true, () => row.Change(-1));
        Assert.Equal(1, value);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-10")]
    public async Task A_held_RT_stops_when_B_closes_the_detail_page_and_doesnt_turn_the_library_below()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.NextTab).Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal("1 von 62", arcadia.Detail!.PositionText);

        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.RightTrigger, letGo: false)
            .HoldPad(TimeSpan.FromMilliseconds(50), PadButtons.RightTrigger | PadButtons.B, letGo: false);
        Assert.Null(arcadia.Detail);

        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.RightTrigger);
        Assert.Equal("1", CurrentPage(arcadia));

        // Pressed afresh, RT turns the library.
        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.RightTrigger);
        Assert.Equal("2", CurrentPage(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-10")]
    public async Task A_held_D_pad_doesnt_run_through_the_context_menu_opened_meanwhile()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();

        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.DPadDown, letGo: false)
            .HoldPad(TimeSpan.FromMilliseconds(250), PadButtons.DPadDown | PadButtons.View, letGo: false);
        var menu = arcadia.Main.GameMenu!;
        var focused = menu.Actions.Single(action => action.IsFocused);

        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.DPadDown);
        Assert.Same(menu, arcadia.Main.GameMenu);
        Assert.Same(focused, menu.Actions.Single(action => action.IsFocused));

        // Pressed afresh, the D-pad moves in the menu.
        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.DPadDown);
        Assert.NotSame(focused, menu.Actions.Single(action => action.IsFocused));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-11")]
    public async Task A_held_RT_goes_on_with_the_games_a_rescan_left_and_stays_in_range()
    {
        var games = Enumerable.Range(1, 60)
            .Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:00}", ArcadiaDriver.Now.AddDays(-i)))
            .ToList();
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, games.Count, Library: games)]);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal("1 von 60", arcadia.Detail!.PositionText);

        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.RightTrigger, letGo: false);
        games.RemoveRange(5, 55);
        await arcadia.RescanAsync();
        Assert.EndsWith("von 5", arcadia.Detail!.PositionText);

        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.RightTrigger);
        Assert.Equal("5 von 5", arcadia.Detail!.PositionText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-8")]
    public async Task Held_RT_turns_the_library_pages_after_400_ms_and_then_every_150_ms()
    {
        var games = Enumerable.Range(1, 500)
            .Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:000}", ArcadiaDriver.Now.AddDays(-i)))
            .ToList();
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, games.Count, Library: games)]);
        Assert.Equal("1", arcadia.CurrentPage);

        // The press, then repeats at 400, 550, 700 and 850 ms.
        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.RightTrigger);
        Assert.Equal("6", arcadia.CurrentPage);

        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.LeftTrigger);
        Assert.Equal("1", arcadia.CurrentPage);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-8")]
    [Trait("TestCase", "T476-9")]
    public async Task Held_RT_turns_the_search_row_stops_at_its_last_hits_and_a_fresh_press_goes_round()
    {
        using var arcadia = await SearchingAsync();
        var search = arcadia.Search!;
        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.RightTrigger);
        var step = search.WindowStart;
        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.LeftTrigger);
        Assert.Equal(0, search.WindowStart);

        // 40 hits, 7 in the row: the press and four repeats reach the last window (33), then it stays held there.
        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.RightTrigger, letGo: false);
        var last = search.Hits.Count - step;
        Assert.Equal(last, search.WindowStart);
        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.RightTrigger);
        Assert.Equal(last, search.WindowStart);

        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.RightTrigger);
        Assert.Equal(0, search.WindowStart);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-10")]
    public async Task A_held_RT_stops_when_B_puts_the_search_keyboard_away()
    {
        using var arcadia = await SearchingAsync();
        var search = arcadia.Search!;
        Assert.True(search.IsKeyboardOut);

        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.RightTrigger, letGo: false)
            .HoldPad(TimeSpan.FromMilliseconds(50), PadButtons.RightTrigger | PadButtons.B, letGo: false);
        Assert.False(search.IsKeyboardOut);
        var turned = search.WindowStart;

        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.RightTrigger);
        Assert.Equal(turned, search.WindowStart);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-8")]
    [Trait("TestCase", "T476-9")]
    public async Task Held_RT_turns_the_launcher_pages_in_the_settings_and_stops_at_the_last_one()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var settings = arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);
        var first = settings.PageLauncherId;
        arcadia.Press(UiAction.PreviousPage);
        var last = settings.PageLauncherId;
        arcadia.Press(UiAction.NextPage);
        Assert.Equal(first, settings.PageLauncherId);

        // Three launchers: the press and the repeat at 400 ms reach the last; the one at 550 ms stops there.
        arcadia.HoldPad(TimeSpan.FromMilliseconds(600), PadButtons.RightTrigger);
        Assert.Equal(last, settings.PageLauncherId);

        arcadia.HoldPad(TimeSpan.FromMilliseconds(100), PadButtons.RightTrigger);
        Assert.Equal(first, settings.PageLauncherId);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T476-9")]
    public async Task A_held_arrow_on_a_launchers_switch_flips_it_once()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.OpenLauncherSettings();
        Assert.Equal(["gog", "xbox", "amazon"], arcadia.Settings.Current.ActiveLaunchers);

        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.DPadRight);
        Assert.Equal(["xbox", "amazon"], arcadia.Settings.Current.ActiveLaunchers);
    }

    /// <summary>Pan hides a controller while RT is held (#367): SDL lets every controller go and opens them again.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T476-12")]
    public async Task A_held_RT_doesnt_turn_on_after_Pan_let_the_controllers_go()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Sdl.Plug(KnownPads.Xbox360);
        arcadia.StartControllers().PollControllers();
        var pad = arcadia.Sdl.IdOf(KnownPads.Xbox360);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal("1 von 31", arcadia.Detail!.PositionText);

        HoldTrigger(arcadia, pad, TimeSpan.FromMilliseconds(500));
        var turned = arcadia.Detail.PositionText;
        Assert.Equal("3 von 31", turned);

        arcadia.LetGoOfControllers();
        pad = arcadia.Sdl.IdOf(KnownPads.Xbox360);
        HoldTrigger(arcadia, pad, TimeSpan.FromSeconds(1));
        Assert.Equal(turned, arcadia.Detail.PositionText);
    }

    private static void HoldTrigger(ArcadiaDriver arcadia, uint pad, TimeSpan duration)
    {
        arcadia.Sdl.Set(pad, new SdlPadState(0, 0, 0, 0, 0, 0, short.MaxValue));
        for (var waited = TimeSpan.Zero; waited <= duration; waited += TimeSpan.FromMilliseconds(8))
        {
            arcadia.PollControllers();
        }
    }

    /// <summary>The search over 40 hits, its keyboard out: a single row that turns.</summary>
    private static async Task<ArcadiaDriver> SearchingAsync()
    {
        var games = Enumerable.Range(1, 40)
            .Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i:00}", ArcadiaDriver.Now.AddDays(-i)))
            .ToList();
        var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, games.Count, Library: games)]);
        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("spiel");
        Assert.Equal(40, arcadia.Search!.Hits.Count);
        return arcadia;
    }

    private static string? CurrentPage(ArcadiaDriver arcadia) =>
        arcadia.Main.Library.PageButtons.SingleOrDefault(button => button.IsCurrent)?.Label;
}
