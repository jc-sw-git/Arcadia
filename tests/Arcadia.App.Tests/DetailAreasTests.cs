using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Tests.Sdl;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The new detail page (#499): the focus path from the button row through the rail of areas into the rows, the info box,
/// rows greyed out with their reason, and the carousel turning while area and focus stay where they are.
/// </summary>
public sealed class DetailAreasTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly LauncherSpec[] SteamAndGog = [new(KnownLaunchers.Steam, 3), new(KnownLaunchers.Gog, 3)];

    [AvaloniaFact]
    [Trait("TestCase", "T499-1")]
    [Trait("TestCase", "T499-4")]
    public async Task The_page_opens_on_Spielen_with_Uebersicht_open_also_after_leaving_it_elsewhere()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        AssertStart(page);
        Assert.Equal([AppTexts.DetailAreaOverview, AppTexts.DetailAreaController, AppTexts.DetailAreaManage], page.Areas.Select(area => area.Name));
        Assert.All(page.Areas, area => Assert.False(string.IsNullOrEmpty(area.ValueLine), $"{area.Name} ohne Wertzeile"));

        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Uninstall);
        arcadia.Press(UiAction.Back).Press(UiAction.Back);
        Assert.False(arcadia.Main.IsDetailOpen);

        arcadia.Press(UiAction.Right).Hold(UiAction.Accept, PastHoldTime);
        AssertStart(arcadia.Detail!.Current);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-5")]
    [Trait("TestCase", "T499-7")]
    public async Task Down_leads_into_the_rail_which_goes_round_shows_each_area_at_once_and_Up_on_the_first_returns_to_Spielen()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;

        // From another button than "Spielen".
        arcadia.Press(UiAction.Right).Press(UiAction.Down);
        Assert.Equal(DetailZone.Rail, page.Zone);
        Assert.Null(page.FocusedKind);
        Assert.True(page.Areas[0].IsFocused);

        arcadia.Press(UiAction.Down);
        Assert.Same(page.Areas[1], page.OpenArea);
        Assert.True(page.Areas[1].IsOpen && page.Areas[1].IsFocused);
        arcadia.Press(UiAction.Down);
        Assert.Same(page.Areas[2], page.OpenArea);
        arcadia.Press(UiAction.Down);
        Assert.Same(page.Areas[0], page.OpenArea);

        // ◀ ▶ do nothing in the rail, A on "Übersicht" neither.
        arcadia.Press(UiAction.Left).Press(UiAction.Right).Press(UiAction.Accept);
        Assert.Equal(DetailZone.Rail, page.Zone);
        Assert.Same(page.Areas[0], page.OpenArea);

        arcadia.Press(UiAction.Up);
        Assert.Equal(GameActionKind.Play, page.FocusedKind);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-6")]
    [Trait("TestCase", "T499-11")]
    public async Task A_on_an_area_focuses_its_first_row_B_returns_to_the_area_and_B_in_the_rail_back_to_the_library()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.Right, times: 2);
        var game = arcadia.FocusedGame!;
        arcadia.Hold(UiAction.Accept, PastHoldTime).Press(UiAction.NextPage);
        var shown = arcadia.Detail!.Current.Tile.Game.Key;
        var page = arcadia.Detail.Current;

        arcadia.Press(UiAction.Down, times: 3).Press(UiAction.Accept);
        Assert.Equal(DetailZone.Rows, page.Zone);
        Assert.Same(page.OpenArea.Rows[0], page.FocusedRow);
        arcadia.Press(UiAction.Back);
        Assert.Equal(DetailZone.Rail, page.Zone);
        Assert.Equal(DetailAreaViewModel.ManageKey, page.OpenArea.Key);
        Assert.True(arcadia.Main.IsDetailOpen);

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsDetailOpen);
        Assert.NotSame(game, arcadia.FocusedGame);
        Assert.Equal(shown, arcadia.FocusedGame!.Game.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-8")]
    public async Task Left_and_right_change_the_mode_at_once_both_ways_and_the_rail_shows_it()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        var row = arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.ControllerMode);
        var game = page.Tile.Game;
        Assert.False(row.IsGreyed);
        var standard = row.ValueText;

        arcadia.Press(UiAction.Right);
        Assert.Equal(ControllerModes.Arcadia, row.ValueText);
        Assert.Equal(AppTexts.DetailControllerLine(ControllerModes.Arcadia), page.OpenArea.ValueLine);
        Assert.True(arcadia.ControllerSupport.ChoiceFor(game));
        arcadia.Press(UiAction.Left);
        Assert.Equal(standard, row.ValueText);
        arcadia.Press(UiAction.Left);
        Assert.Equal(ControllerModes.Steam, row.ValueText);

        // Held from "Standard", ▶ goes to "Arcadia" and "Steam-Eingabe" and stops there instead of going round.
        arcadia.Press(UiAction.Right);
        Assert.Equal(standard, row.ValueText);
        arcadia.HoldPad(TimeSpan.FromSeconds(1), PadButtons.DPadRight);
        Assert.Equal(ControllerModes.Steam, row.ValueText);

        // For this game only, also after leaving and opening the page again.
        arcadia.Press(UiAction.Back).Press(UiAction.Back).Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal(ControllerModes.Steam, arcadia.Detail!.Current.Row(GameActionKind.ControllerMode).ValueText);
        arcadia.Press(UiAction.NextPage);
        Assert.Equal(standard, arcadia.Detail.Current.Row(GameActionKind.ControllerMode).ValueText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-14")]
    [Trait("TestCase", "T499-16")]
    public async Task Without_a_Steam_Controller_the_mode_row_is_greyed_yet_focusable_says_why_and_comes_alive_when_one_connects()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"], PanEnabled = true });
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        var row = arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.ControllerMode);
        Assert.True(row.IsGreyed);
        Assert.Same(row, page.InfoRow);
        Assert.Equal(AppTexts.DetailModeNoSteamController, row.Note);
        var value = row.ValueText;

        // ◀ ▶ and A do nothing.
        arcadia.Press(UiAction.Right).Press(UiAction.Left).Press(UiAction.Accept);
        Assert.Equal(value, row.ValueText);
        Assert.Empty(arcadia.Settings.Current.ControllerSupportOverrides);
        Assert.Same(row, page.FocusedRow);

        // The info box follows the focus.
        arcadia.Press(UiAction.Down);
        Assert.Equal(GameActionKind.MouseMode, page.InfoRow?.Kind);
        Assert.Equal(AppTexts.DetailMouseModeAllowedInfo, page.InfoRow?.ValueBoxes[1].Text);
        arcadia.Press(UiAction.Up);

        // A Steam Controller connects while the focus is on the row: active, the focus stays.
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        page = arcadia.Detail!.Current;
        Assert.False(page.Row(GameActionKind.ControllerMode).IsGreyed);
        Assert.Equal(GameActionKind.ControllerMode, page.FocusedRow?.Kind);
        Assert.Equal(DetailZone.Rows, page.Zone);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-15")]
    public async Task A_row_that_doesnt_apply_now_stays_greyed_with_its_reason_never_hidden()
    {
        var notInstalled = TestGames.NotInstalled(KnownLaunchers.Gog, "Bravo");
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, notInstalled)]);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;

        Assert.Equal([GameActionKind.ControllerMode, GameActionKind.MouseMode], page.Areas[1].Rows.Select(row => row.Kind));
        Assert.Equal([GameActionKind.Artwork, GameActionKind.Uninstall], page.Areas[2].Rows.Select(row => row.Kind));
        Assert.All(page.Areas.SelectMany(area => area.Rows), row => Assert.True(row.IsGreyed && row.HasNote, $"{row.Title} nicht ausgegraut mit Grund"));

        // Greyed rows take the focus.
        var artwork = arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Artwork);
        Assert.Equal(AppTexts.DetailSetAfterInstall, artwork.Note);
        arcadia.Press(UiAction.Accept);
        Assert.Null(arcadia.Main.ChangeArtwork);
        arcadia.Press(UiAction.Down);
        Assert.Equal(AppTexts.NotInstalled, page.FocusedRow!.Note);
        arcadia.Press(UiAction.Accept);
        Assert.Null(arcadia.Main.Confirmation);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-9")]
    public async Task Deinstallieren_asks_as_in_the_context_menu_and_B_returns_to_the_row()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        var uninstall = arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Uninstall);
        Assert.False(uninstall.IsGreyed);

        arcadia.Press(UiAction.Accept);
        Assert.NotNull(arcadia.Main.Confirmation);
        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Confirmation);
        Assert.Same(page, arcadia.Detail!.Current);
        Assert.Equal(GameActionKind.Uninstall, page.FocusedRow?.Kind);
        Assert.Equal(DetailAreaViewModel.ManageKey, page.OpenArea.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-18")]
    [Trait("TestCase", "T499-12")]
    public async Task LT_RT_and_LB_RB_keep_the_open_area_and_the_focused_row_also_when_it_is_greyed_for_the_next_game()
    {
        var installed = TestGames.Installed(KnownLaunchers.Gog, "Alpha", DateTimeOffset.UnixEpoch);
        var notInstalled = TestGames.NotInstalled(KnownLaunchers.Gog, "Bravo");
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, installed, notInstalled), new(KnownLaunchers.Epic, 3)]);
        arcadia.FocusTile("Alpha");
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Uninstall);

        arcadia.Press(UiAction.NextPage);
        var page = arcadia.Detail!.Current;
        Assert.Equal("Bravo", page.Tile.Title);
        Assert.Equal(DetailAreaViewModel.ManageKey, page.OpenArea.Key);
        Assert.Equal(GameActionKind.Uninstall, page.FocusedRow?.Kind);
        Assert.True(page.FocusedRow!.IsGreyed);

        arcadia.Press(UiAction.PreviousPage);
        Assert.Equal(GameActionKind.Uninstall, arcadia.Detail!.Current.FocusedRow?.Kind);

        // From the rail the area stays focused, from the buttons the button.
        arcadia.Press(UiAction.Back).Press(UiAction.NextPage);
        Assert.Equal((DetailZone.Rail, DetailAreaViewModel.ManageKey), (arcadia.Detail!.Current.Zone, arcadia.Detail.Current.OpenArea.Key));

        // LB/RB: another launcher's first game, the same area and focus.
        arcadia.Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.NextTab);
        page = arcadia.Detail!.Current;
        Assert.Equal(KnownLaunchers.Epic.Id, page.Tile.Game.LauncherId);
        Assert.Equal(DetailAreaViewModel.ManageKey, page.OpenArea.Key);
        Assert.Equal(GameActionKind.Uninstall, page.FocusedRow?.Kind);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-19")]
    public async Task Held_RT_turns_only_the_covers_and_the_page_shows_the_game_reached_on_release_with_area_and_focus()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!;
        arcadia.FocusDetailRow(DetailAreaViewModel.ControllerKey, GameActionKind.MouseMode);
        var first = detail.Current;

        // The press shows the next game at once; the repeats at 400, 550 and 700 ms only turn the covers.
        arcadia.HoldPad(TimeSpan.FromMilliseconds(800), PadButtons.RightTrigger, letGo: false);
        Assert.Equal("5 von 31", detail.PositionText);
        var during = detail.Current;
        Assert.NotSame(first, during);
        Assert.Equal(detail.Games[1].Key, during.Tile.Game.Key);
        Assert.Equal(detail.Games[4].Key, detail.Covers.Single(cover => cover.Offset == 0).Tile.Game.Key);

        arcadia.HoldPad(TimeSpan.FromMilliseconds(20));
        Assert.Equal(detail.Games[4].Key, detail.Current.Tile.Game.Key);
        Assert.Equal(DetailAreaViewModel.ControllerKey, detail.Current.OpenArea.Key);
        Assert.Equal(GameActionKind.MouseMode, detail.Current.FocusedRow?.Kind);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-20")]
    public async Task The_stacks_show_two_covers_with_five_games_one_with_two_and_none_with_one()
    {
        foreach (var (count, depth) in new[] { (5, 2), (3, 1), (2, 1), (1, 0) })
        {
            var games = Enumerable.Range(1, count).Select(i => TestGames.Installed(KnownLaunchers.Gog, $"Spiel {i}", DateTimeOffset.UnixEpoch)).ToArray();
            using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, games)]);
            arcadia.Hold(UiAction.Accept, PastHoldTime);
            var detail = arcadia.Detail!;
            Assert.Equal(depth, detail.StackDepth);
            Assert.Equal(count > 1, detail.HasNeighbours);
            if (count == 2)
            {
                Assert.Equal(detail.Previous!.Title, detail.Next!.Title);
            }

            if (count == 1)
            {
                Assert.Null(detail.Previous);
                arcadia.Press(UiAction.NextPage);
                Assert.Equal("Spiel 1", detail.Current.Tile.Title);
            }
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-10")]
    public async Task The_button_hints_follow_the_focus()
    {
        using var arcadia = await StartWithSteamControllerAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        Assert.Equal((AppTexts.GameTypePlay, true, false, AppTexts.Back), (page.AcceptHint, page.ShowsAreasHint, page.ShowsChangeHint, page.BackHint));

        arcadia.Press(UiAction.Down);
        Assert.Equal((null, false, false, AppTexts.Back), (page.AcceptHint, page.ShowsAreasHint, page.ShowsChangeHint, page.BackHint));

        arcadia.FocusDetailRowFromRail(DetailAreaViewModel.ControllerKey);
        Assert.Equal((false, true, AppTexts.DetailAreas), (page.ShowsAreasHint, page.ShowsChangeHint, page.BackHint));

        arcadia.Press(UiAction.Back).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Down);
        Assert.Equal(GameActionKind.Uninstall, page.FocusedRow?.Kind);
        Assert.Equal((AppTexts.ActionUninstallMore, false), (page.AcceptHint, page.ShowsChangeHint));
    }

    private static void AssertStart(GameDetailPageViewModel page)
    {
        Assert.Equal(DetailZone.Buttons, page.Zone);
        Assert.Equal(GameActionKind.Play, page.FocusedKind);
        Assert.Equal(DetailAreaViewModel.OverviewKey, page.OpenArea.Key);
        Assert.Null(page.InfoRow);
    }

    private static async Task<ArcadiaDriver> StartWithSteamControllerAsync()
    {
        var arcadia = await ArcadiaDriver.StartAsync(SteamAndGog, new AppSettings { ActiveLaunchers = ["steam", "gog"], PanEnabled = true });
        arcadia.Sdl.Plug(KnownPads.SteamController);
        arcadia.StartControllers();
        await arcadia.TickSteamControllerAsync();
        return arcadia;
    }
}

file static class DetailRailSteps
{
    /// <summary>From the rail down to the area <paramref name="areaKey"/> and A into its first row.</summary>
    public static void FocusDetailRowFromRail(this ArcadiaDriver arcadia, string areaKey)
    {
        var page = arcadia.Detail!.Current;
        while (page.OpenArea.Key != areaKey)
        {
            arcadia.Press(UiAction.Down);
        }

        arcadia.Press(UiAction.Accept);
    }
}
