using Arcadia.App.Input;
using Arcadia.App.Tests.Fixtures;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The Epic tab from Epic's own files (#42, #81, #41), read by the real Epic integration.</summary>
public sealed class EpicLibraryTests
{
    private static readonly string[] Game = ["games", "applications"];

    [AvaloniaFact]
    [Trait("TestCase", "T42-1")]
    public async Task The_Epic_tab_shows_every_game_of_the_library_installed_or_not_but_no_Unreal_Engine_or_editor()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        var installed = new EpicApp("aa01", "Leuchtturm");
        var notInstalled = new EpicApp("aa02", "Salzwiesen");
        epic.WriteInstalled(installed);
        epic.WriteCatalog(
            (installed, Game, null),
            (notInstalled, Game, null),
            (new EpicApp("aa03", "Unreal Engine"), ["engines", "applications"], null),
            (new EpicApp("aa04", "Unreal Editor for Fortnite"), ["addons", "addons/launchable"], null));

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);

        var games = EpicTab(arcadia).Games;
        Assert.Equal(["Leuchtturm", "Salzwiesen"], games.Select(game => game.Title).Order(StringComparer.Ordinal));
        Assert.True(games.Single(game => game.Title == "Leuchtturm").IsInstalled);
        Assert.False(games.Single(game => game.Title == "Salzwiesen").IsInstalled);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T42-2")]
    public async Task No_DLC_season_pass_beta_soundtrack_or_preorder_bonus_shows_as_a_game()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        var game = new EpicApp("ab01", "Leuchtturm");
        var dlc = new EpicApp("ab02", "Leuchtturm: Die Insel");
        epic.WriteInstalled(game);
        epic.WriteInstalledDlc(dlc, game);
        epic.WriteCatalog(
            (game, Game, null),
            (dlc, ["games", "addons"], game),
            (new EpicApp("ab03", "Leuchtturm Season Pass"), Game, game),
            (new EpicApp("ab04", "Leuchtturm Beta"), Game, game),
            (new EpicApp("ab05", "Leuchtturm Soundtrack"), ["games", "digitaldownloads"], null),
            (new EpicApp("ab06", "Leuchtturm Vorbesteller-Bonus"), ["games", "addons"], null));

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);

        Assert.Equal(["Leuchtturm"], EpicTab(arcadia).Games.Select(g => g.Title));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T42-3")]
    public async Task Without_the_catalog_cache_the_tab_shows_the_installed_games_and_no_error()
    {
        using var files = new LauncherFiles();
        files.Epic.Install().WriteInstalled(new EpicApp("ac01", "Leuchtturm"));

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);

        Assert.Equal(["Leuchtturm"], EpicTab(arcadia).Games.Select(game => game.Title));
        Assert.Null(arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T42-4")]
    public async Task Titles_lose_the_question_marks_of_trademark_signs_and_umlauts_stay_broken()
    {
        // The cache is ASCII only: every other character is a question mark.
        using var files = new LauncherFiles();
        files.Epic.Install().WriteCatalog(
            (new EpicApp("ad01", "Sternfahrt? Chronik"), Game, null),
            (new EpicApp("ad02", "Leuchtturm??: Die Insel"), Game, null),
            (new EpicApp("ad03", "Br?ckenbauer"), Game, null));

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);

        Assert.Equal(["Br?ckenbauer", "Leuchtturm: Die Insel", "Sternfahrt Chronik"], EpicTab(arcadia).Games.Select(game => game.Title).Order(StringComparer.Ordinal));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T81-1")]
    public async Task The_update_icon_shows_while_Epic_reports_an_update_and_goes_once_the_update_is_installed()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        var game = new EpicApp("ae01", "Leuchtturm") { Version = "1.0.0" };
        epic.WriteInstalled(game);
        epic.Log(EpicFolder.InstalledApps((game, "1.0.0", HasUpdate: true)));

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        Assert.True(arcadia.FocusedGame!.UpdateAvailable);

        // Updated in Epic: the manifest has the new version; the launcher's report still names the old one.
        epic.WriteInstalled(game with { Version = "1.1.0" });
        await arcadia.ActivateWindowAsync();

        Assert.Equal("Leuchtturm", arcadia.FocusedGame!.Title);
        Assert.False(arcadia.FocusedGame.UpdateAvailable);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T81-2")]
    public async Task Last_played_comes_from_Epics_log_and_stays_after_Epic_deleted_that_log()
    {
        var played = ArcadiaDriver.Now.AddDays(-3);
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        var game = new EpicApp("af01", "Leuchtturm");
        epic.WriteInstalled(game);
        epic.Log(EpicFolder.PlaytimeStarted(game, played), EpicFolder.PlaytimeStopped(game, played.AddHours(2)));

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        Assert.Equal("Vor 3 Tagen", arcadia.FocusedGame!.LastPlayedText);

        // Two launcher starts later the log with the session is gone.
        epic.StartNewLog();
        epic.StartNewLog(deleteBackups: true);
        using var restarted = await arcadia.RestartAsync();

        Assert.Equal("Vor 3 Tagen", restarted.FocusedGame!.LastPlayedText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T41-6")]
    public async Task A_game_uninstalled_in_Epic_shows_as_not_installed_once_Arcadias_window_is_active_again()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        var game = new EpicApp("ag01", "Leuchtturm");
        epic.WriteInstalled(game);
        epic.WriteCatalog((game, Game, null));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        Assert.True(arcadia.FocusedGame!.IsInstalled);

        epic.Uninstall(game);
        await arcadia.ActivateWindowAsync();

        var tile = arcadia.FocusedGame!;
        Assert.Equal("Leuchtturm", tile.Title);
        Assert.False(tile.IsInstalled);
        Assert.Equal("Installieren", tile.PrimaryActionText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T41-6")]
    public async Task A_game_uninstalled_in_Epic_shows_as_not_installed_once_it_is_selected()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        var first = new EpicApp("ah01", "Alpenglühen");
        var second = new EpicApp("ah02", "Leuchtturm");
        epic.WriteInstalled(first);
        epic.WriteInstalled(second);
        epic.WriteCatalog((first, Game, null), (second, Game, null));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        var (gone, toward) = arcadia.FocusedGame!.Title == first.Title ? (second, UiAction.Right) : (first, UiAction.Left);

        // Arcadia stays in front, a while later the user moves to the game.
        epic.Uninstall(gone);
        arcadia.Wait(TimeSpan.FromSeconds(10)).Press(toward);

        Assert.Equal(gone.Title, arcadia.FocusedGame!.Title);
        await arcadia.WaitUntilAsync(
            () => arcadia.PageGames.Single(tile => tile.Title == gone.Title).IsInstalled == false,
            "das deinstallierte Spiel soll als nicht installiert erscheinen");
    }

    private static Arcadia.App.ViewModels.LauncherTabViewModel EpicTab(ArcadiaDriver arcadia) =>
        Assert.Single(arcadia.Main.LauncherTabs.Items, tab => tab.Name == "Epic Games");
}
