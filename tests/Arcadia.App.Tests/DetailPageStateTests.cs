using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The new detail page (#499) while things happen around it: dialogs over it, the game's state changing while it is open,
/// a game without artwork, and the context menu offering what the page offers.
/// </summary>
public sealed class DetailPageStateTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T499-12")]
    public async Task L3_and_X_open_over_the_page_and_after_B_the_open_area_and_the_focused_row_are_where_they_were()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!;
        var game = detail.Current.Tile.Game.Key;
        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Uninstall);

        // L3: another sort keeps the shown game, its area and row.
        arcadia.Press(UiAction.ViewOptions);
        Assert.True(arcadia.Main.IsViewOptionsOpen);
        arcadia.Press(UiAction.Right).Press(UiAction.Accept).Press(UiAction.Back);
        Assert.False(arcadia.Main.IsViewOptionsOpen);
        AssertKept(arcadia, game);

        // X: the search over the page, B returns to it.
        // The first B puts the on-screen keyboard away, the second leaves the search.
        arcadia.Press(UiAction.Search);
        Assert.NotNull(arcadia.Main.Search);
        arcadia.Press(UiAction.Back, times: 2);
        Assert.Null(arcadia.Main.Search);
        AssertKept(arcadia, game);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-13")]
    public async Task While_the_uninstall_question_is_open_LT_RT_and_LB_RB_do_nothing_and_the_page_keeps_game_area_and_focus()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!;
        var page = detail.Current;
        var game = page.Tile.Game.Key;
        var tab = detail.TabName;
        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Uninstall);
        arcadia.Press(UiAction.Accept);
        var question = Assert.IsType<ConfirmationViewModel>(arcadia.Main.Confirmation);

        arcadia.Press(UiAction.NextPage).Press(UiAction.PreviousPage, times: 2).Press(UiAction.NextTab).Press(UiAction.PreviousTab, times: 2);
        Assert.Same(question, arcadia.Main.Confirmation);
        Assert.Same(detail, arcadia.Detail);
        Assert.Equal(tab, arcadia.Detail!.TabName);
        Assert.Same(page, arcadia.Detail.Current);

        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Main.Confirmation);
        AssertKept(arcadia, game);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-22")]
    public async Task An_installation_finishing_and_the_game_running_update_rows_in_place_while_the_focus_stays_on_its_row()
    {
        var alpha = TestGames.Installed(KnownLaunchers.Gog, "Alpha", DateTimeOffset.UnixEpoch);
        var bravo = TestGames.NotInstalled(KnownLaunchers.Gog, "Bravo");
        var games = new List<Game> { alpha, bravo, TestGames.Installed(KnownLaunchers.Gog, "Charlie", DateTimeOffset.UnixEpoch) };
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(KnownLaunchers.Gog, games.Count, Library: games, Running: [])]);
        arcadia.FocusTile("Bravo");
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var uninstall = arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Uninstall);
        Assert.True(uninstall.IsGreyed);

        // The launcher installs it, then reports it installed: "Spielen", "Deinstallieren …" active, the focus stays.
        arcadia.Installs.Set(bravo, new InstallProgress(InstallPhase.Downloading, 0.5));
        await arcadia.FollowInstallsAsync();
        AssertOnUninstall(arcadia, "Bravo", greyed: true);

        arcadia.Installs.Set(bravo, null);
        await arcadia.FollowInstallsAsync();
        games[1] = TestGames.Installed(KnownLaunchers.Gog, "Bravo", DateTimeOffset.UnixEpoch) with { Uninstall = new LaunchTarget.ProtocolUri("goggalaxy://uninstall/bravo") };
        await arcadia.RescanAsync();
        var page = AssertOnUninstall(arcadia, "Bravo", greyed: false);
        Assert.Equal(AppTexts.GameTypePlay, page.Buttons[0].ButtonLabel);

        // It starts in the launcher: nothing to uninstall while it runs; it ends: active again. The focus stays.
        arcadia.RunInLauncher(page.Tile.Game);
        await arcadia.FollowRunningGamesAsync();
        AssertOnUninstall(arcadia, "Bravo", greyed: true);
        arcadia.EndInLauncher(page.Tile.Game);
        await arcadia.FollowRunningGamesAsync();
        AssertOnUninstall(arcadia, "Bravo", greyed: false);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-21")]
    public async Task A_game_without_artwork_shows_its_backdrop()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, TestGames.Installed(KnownLaunchers.Gog, "Alpha", DateTimeOffset.UnixEpoch))]);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        Assert.Null(page.Tile.Hero);
        Assert.NotNull(page.Backdrop);
        Assert.NotNull(page.Ring);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T499-24")]
    public async Task The_context_menu_offers_the_actions_the_detail_page_offers()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(settings: new AppSettings
        {
            ActiveLaunchers = ["gog", "epic", "xbox", "amazon"],
            PanEnabled = true,
            MouseMode = new MouseModeSettings { Activation = MouseModeActivation.Manual, InGames = MouseModeInGames.PerGame },
        });
        foreach (var tab in Enumerable.Range(0, 2))
        {
            arcadia.Press(UiAction.Options);
            var menu = arcadia.Main.GameMenu!.Actions.Where(action => action.Kind != GameActionKind.Details && action.IsEnabled)
                .Select(action => action.Kind).Order().ToList();
            arcadia.Press(UiAction.Back).Hold(UiAction.Accept, PastHoldTime);
            var page = arcadia.Detail!.Current;
            var onPage = page.Buttons.Where(button => button.IsEnabled).Select(button => button.Kind)
                .Concat(page.Areas.SelectMany(area => area.Rows).Where(row => !row.IsGreyed).Select(row => row.Kind))
                .Order().ToList();
            Assert.Equal(menu, onPage);
            arcadia.Press(UiAction.Back).Press(UiAction.NextTab);
        }
    }

    private static void AssertKept(ArcadiaDriver arcadia, string game)
    {
        var page = arcadia.Detail!.Current;
        Assert.Equal(game, page.Tile.Game.Key);
        Assert.Equal(DetailAreaViewModel.ManageKey, page.OpenArea.Key);
        Assert.Equal(DetailZone.Rows, page.Zone);
        Assert.Equal(GameActionKind.Uninstall, page.FocusedRow?.Kind);
    }

    private static GameDetailPageViewModel AssertOnUninstall(ArcadiaDriver arcadia, string title, bool greyed)
    {
        var page = arcadia.Detail!.Current;
        Assert.Equal(title, page.Tile.Title);
        Assert.Equal(DetailZone.Rows, page.Zone);
        Assert.Equal(GameActionKind.Uninstall, page.FocusedRow?.Kind);
        Assert.True(greyed == page.FocusedRow!.IsGreyed, $"ausgegraut {page.FocusedRow.IsGreyed}: {page.FocusedRow.Note}; installiert {page.Tile.IsInstalled}");
        return page;
    }
}
