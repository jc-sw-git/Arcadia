using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// Quick turns of the detail page (#506, scenario 1) beyond a plain series: mixed directions, the focus carried along,
/// very short lists and dialogs over the page.
/// </summary>
public sealed class DetailQuickTurnTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T506-4")]
    public async Task Mixed_quick_turns_end_on_their_sum_and_go_round_at_the_end_of_the_list()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, 4)], new AppSettings { ActiveLaunchers = ["gog"] });
        var titles = arcadia.PageGames.Select(tile => tile.Title).ToList();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!;
        var jumps = 0;
        detail.Jumped += _ => jumps++;

        // RT, RT, LT: one game on, and nothing replayed afterwards.
        arcadia.Press(UiAction.NextPage).Press(UiAction.NextPage).Press(UiAction.PreviousPage);
        Assert.Equal(titles[1], detail.Current.Tile.Title);
        Assert.Equal(1, jumps);
        arcadia.Wait(TimeSpan.FromSeconds(2));
        Assert.Equal(titles[1], detail.Current.Tile.Title);
        Assert.Equal(1, jumps);

        // Quick LT, LT from the second game: round over the start to the last game, as single turns go.
        arcadia.Press(UiAction.PreviousPage, times: 2);
        Assert.Equal(titles[^1], detail.Current.Tile.Title);
        arcadia.Press(UiAction.NextPage, times: 3);
        Assert.Equal(titles[2], detail.Current.Tile.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-7")]
    public async Task Quick_turns_on_the_second_controller_row_keep_area_and_row_and_show_the_newest_games_values()
    {
        var games = Enumerable.Range(0, 4).Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i}", DateTimeOffset.UnixEpoch.AddDays(i))).ToList();
        var settings = new AppSettings
        {
            ActiveLaunchers = ["gog"],
            PanEnabled = true,
            MouseMode = new MouseModeSettings { InGames = MouseModeInGames.PerGame },
        };
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, [.. games])], settings);
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!;

        // The first game's mouse mode is set to "Blockiert"; the next ones keep "Standard".
        var row = arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.MouseMode);
        Assert.False(row.IsGreyed, row.Note);
        arcadia.Press(UiAction.Right, times: 2);
        Assert.Equal(GameMouseModes.Blocked, detail.Current.FocusedRow!.ValueText);
        var first = detail.Current.Tile.Title;

        foreach (var action in new[] { UiAction.NextPage, UiAction.NextPage, UiAction.NextPage, UiAction.NextPage })
        {
            arcadia.Press(action);
            var page = detail.Current;
            Assert.Equal(DetailAreaViewModel.ControllerKey, page.OpenArea.Key);
            Assert.Equal(DetailZone.Rows, page.Zone);
            Assert.Equal(GameActionKind.MouseMode, page.FocusedRow?.Kind);
            Assert.Same(page.FocusedRow, page.InfoRow);
            Assert.Equal(page.FocusedRow!.ValueText, Assert.Single(page.InfoRow!.ValueBoxes, box => box.IsCurrent).Title);
            Assert.True(page.ShowsChangeHint, "◀ ▶ im Fußbereich");
        }

        // Round to the first game: its own value, chip and highlight alike.
        Assert.Equal(first, detail.Current.Tile.Title);
        Assert.Equal(GameMouseModes.Blocked, detail.Current.FocusedRow!.ValueText);
        Assert.Equal(GameMouseModes.Blocked, Assert.Single(detail.Current.InfoRow!.ValueBoxes, box => box.IsCurrent).Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-8")]
    public async Task With_two_games_quick_turns_alternate_and_with_one_they_do_nothing()
    {
        using (var two = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, 2)], new AppSettings { ActiveLaunchers = ["gog"] }))
        {
            var titles = two.PageGames.Select(tile => tile.Title).ToList();
            two.Hold(UiAction.Accept, PastHoldTime);
            two.Press(UiAction.NextPage, times: 3);
            Assert.Equal(titles[1], two.Detail!.Current.Tile.Title);
            two.Press(UiAction.PreviousPage, times: 2).Press(UiAction.NextPage, times: 4);
            Assert.Equal(titles[1], two.Detail!.Current.Tile.Title);
        }

        using var one = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, 1)], new AppSettings { ActiveLaunchers = ["gog"] });
        one.Hold(UiAction.Accept, PastHoldTime);
        var detail = one.Detail!;
        var page = detail.Current;
        var jumps = 0;
        detail.Jumped += _ => jumps++;
        one.Press(UiAction.NextPage, times: 3).Press(UiAction.PreviousPage, times: 2);
        Assert.Same(page, detail.Current);
        Assert.Equal(0, jumps);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T506-9")]
    public async Task Quick_turns_under_the_artwork_dialog_or_the_uninstall_question_do_nothing_and_queue_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!;
        var page = detail.Current;
        var jumps = 0;
        detail.Jumped += _ => jumps++;

        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Artwork);
        arcadia.Press(UiAction.Accept);
        Assert.NotNull(arcadia.Main.ChangeArtwork);
        arcadia.Press(UiAction.NextPage, times: 3).Press(UiAction.PreviousPage);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.ChangeArtwork);
        Assert.Same(page, detail.Current);
        arcadia.Wait(TimeSpan.FromSeconds(2));
        Assert.Same(page, detail.Current);
        Assert.Equal(0, jumps);

        // ▲ goes round from "Artwork ändern" to the last row.
        arcadia.Press(UiAction.Up);
        Assert.Equal(GameActionKind.Uninstall, page.FocusedRow?.Kind);
        arcadia.Press(UiAction.Accept);
        Assert.NotNull(arcadia.Main.Confirmation);
        arcadia.Press(UiAction.NextPage, times: 3).Press(UiAction.PreviousPage);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Confirmation);
        arcadia.Wait(TimeSpan.FromSeconds(2));
        Assert.Same(page, detail.Current);
        Assert.Equal(0, jumps);
    }
}
