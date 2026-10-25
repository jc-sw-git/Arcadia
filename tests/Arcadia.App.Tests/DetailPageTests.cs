using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The detail page as a carousel over the tab's games (#11).</summary>
public sealed class DetailPageTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T11-2")]
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
    [Trait("TestCase", "T11-6")]
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

    [AvaloniaFact]
    [Trait("TestCase", "T11-3")]
    public async Task With_two_games_the_other_one_stands_on_both_sides_and_with_one_game_there_are_no_neighbours()
    {
        using var two = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, Installed("Alpha"), Installed("Bravo"))]);
        two.Hold(UiAction.Accept, PastHoldTime);
        var detail = two.Main.Detail!;
        Assert.True(detail.HasNeighbours);
        Assert.Equal("Alpha", detail.Current.Tile.Title);
        Assert.Equal("Bravo", Side(detail, -1));
        Assert.Equal("Bravo", Side(detail, 1));

        two.Press(UiAction.NextPage);
        Assert.Equal("Bravo", detail.Current.Tile.Title);
        Assert.Equal("Alpha", Side(detail, -1));
        Assert.Equal("Alpha", Side(detail, 1));

        using var one = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, Installed("Solo"))]);
        one.Hold(UiAction.Accept, PastHoldTime);
        detail = one.Main.Detail!;
        Assert.False(detail.HasNeighbours);
        Assert.Equal(["Solo"], detail.Pages.Select(page => page.Tile.Title));

        // LT/RT have nothing to turn to.
        one.Press(UiAction.NextPage).Press(UiAction.PreviousPage);
        Assert.Equal("Solo", detail.Current.Tile.Title);
        Assert.Equal("1 von 1", detail.PositionText);
        Assert.Single(detail.Pages);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T11-4")]
    public async Task LB_and_RB_switch_the_launcher_and_the_detail_page_stays_open_on_the_first_game_of_the_tab()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        arcadia.Press(UiAction.NextTab);
        var firstOfXbox = arcadia.Main.PageGames[0].Game.Key;
        arcadia.Press(UiAction.PreviousTab).Press(UiAction.Right, times: 2).Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal("3 von 31", arcadia.Main.Detail!.PositionText);

        arcadia.Press(UiAction.NextTab);
        var detail = Assert.IsType<GameDetailViewModel>(arcadia.Main.Detail);
        Assert.Equal("Xbox", detail.TabName);
        Assert.Equal("Xbox", arcadia.Main.SelectedTab?.Name);
        Assert.Equal(firstOfXbox, detail.Current.Tile.Game.Key);
        Assert.Equal("1 von 62", detail.PositionText);

        // LB from the first tab wraps around to the last one.
        arcadia.Press(UiAction.PreviousTab, times: 2);
        Assert.True(arcadia.Main.IsDetailOpen);
        Assert.Equal("Amazon Games", arcadia.Main.Detail!.TabName);
        Assert.Equal("1 von 6", arcadia.Main.Detail.PositionText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T11-5")]
    public async Task Y_on_the_detail_page_says_it_shows_one_game_and_another_sort_keeps_the_game_while_neighbours_and_position_follow()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([FiveGames]);
        arcadia.Press(UiAction.Right).Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Main.Detail!;
        Assert.Equal("bravo", detail.Current.Tile.Title);
        Assert.Equal("2 von 5", detail.PositionText);

        arcadia.Press(UiAction.ViewOptions);
        var options = Assert.IsType<ViewOptionsViewModel>(arcadia.Main.ViewOptions);
        Assert.Equal("Gilt für die Bibliothek; die Detailseite zeigt immer ein Spiel. Das aktuelle Spiel bleibt in der Mitte, die Nachbarn ordnen sich nach der neuen Sortierung.", options.PageSizeNote);

        // Name Z → A: Echo, Delta, Charlie, bravo, Alpha.
        arcadia.Press(UiAction.Right).Press(UiAction.Accept).Press(UiAction.Back);
        Assert.False(arcadia.Main.IsViewOptionsOpen);
        Assert.Same(detail, arcadia.Main.Detail);
        Assert.Equal("bravo", detail.Current.Tile.Title);
        Assert.Equal("4 von 5", detail.PositionText);
        Assert.Equal("Charlie", Side(detail, -1));
        Assert.Equal("Alpha", Side(detail, 1));
        Assert.Equal("Name Z–A", detail.SortLabel);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T11-8")]
    public async Task An_update_keeps_Spielen_with_a_note_and_a_game_not_installed_offers_Installieren_and_its_progress()
    {
        var withUpdate = Installed("Alpha") with { UpdateAvailable = true };
        var notInstalled = TestGames.NotInstalled(KnownLaunchers.Gog, "Bravo");
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, withUpdate, notInstalled, Installed("Charlie"))]);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Main.Detail!.Current;

        Assert.Equal("Alpha", page.Tile.Title);
        Assert.True(page.Tile.UpdateAvailable);
        var play = page.TopRow[0];
        Assert.Equal((GameActionKind.Play, "Spielen", true), (play.Kind, play.ButtonLabel, play.IsEnabled));
        Assert.Equal("GOG Galaxy installiert das Update eventuell, bevor das Spiel startet.", page.UpdateNote);

        arcadia.Press(UiAction.NextPage);
        page = arcadia.Main.Detail!.Current;
        Assert.Equal("Bravo", page.Tile.Title);
        Assert.False(page.Tile.IsInstalled);
        Assert.Equal((GameActionKind.Play, "Installieren", true), (page.TopRow[0].Kind, page.TopRow[0].ButtonLabel, page.TopRow[0].IsEnabled));
        Assert.Equal("–", page.InstalledAt);
        Assert.Equal("Nicht installiert", page.Tile.LocationText);
        Assert.False(page.HasUpdateNote);

        // Only the first row: no settings for the game, nothing to uninstall.
        Assert.Empty(page.GameSettingsRow);
        Assert.Null(page.Uninstall);

        // The launcher installs it: the first button shows how far, and can't be pressed.
        arcadia.Installs.Set(notInstalled, new InstallProgress(InstallPhase.Downloading, 0.42));
        await arcadia.FollowInstallsAsync();
        page = arcadia.Main.Detail!.Current;
        Assert.Equal("Bravo", page.Tile.Title);
        Assert.Equal((GameActionKind.Play, "Wird installiert · 42\u00A0%", false), (page.TopRow[0].Kind, page.TopRow[0].ButtonLabel, page.TopRow[0].IsEnabled));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T11-9")]
    public async Task The_figures_show_last_played_with_date_and_time_playtime_by_the_launcher_install_date_with_path_and_size_with_drive()
    {
        var lastPlayed = new DateTimeOffset(2026, 10, 23, 12, 30, 0, TimeSpan.Zero);
        var game = Installed("Alpha") with
        {
            InstalledAt = new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero),
            LastPlayedAt = lastPlayed,
            Playtime = TimeSpan.FromHours(12.5),
            InstallSizeBytes = 38_118_773_964, // 35.5 GiB
            InstallDirectory = @"D:\Spiele\Alpha",
        };
        var withoutFigures = Installed("Bravo") with { InstalledAt = null };
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, game, withoutFigures)]);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Main.Detail!.Current;

        Assert.Equal("Gestern", page.Tile.LastPlayedText);
        Assert.Equal($"23.10.2026, {lastPlayed.ToLocalTime():HH:mm}", page.LastPlayedAt);
        Assert.Equal("12 Std.", page.Tile.PlaytimeText);
        Assert.Equal("Laut GOG Galaxy", page.PlaytimeSource);
        Assert.Equal("10.01.2026", page.InstalledAt);
        Assert.Equal(@"D:\Spiele\Alpha", page.Tile.LocationText);
        Assert.Equal("35,5 GB", page.Tile.SizeText);
        Assert.Equal("Laufwerk D:", page.DriveText);

        // What the launcher doesn't report is said so, not left blank.
        arcadia.Press(UiAction.NextPage);
        page = arcadia.Main.Detail!.Current;
        Assert.Equal("Nie gespielt", page.Tile.LastPlayedText);
        Assert.Equal("–", page.LastPlayedAt);
        Assert.Equal("–", page.Tile.PlaytimeText);
        Assert.Equal("GOG Galaxy meldet keine", page.PlaytimeSource);
        Assert.Equal("–", page.InstalledAt);
        Assert.Equal("–", page.Tile.SizeText);
        Assert.Equal("–", page.DriveText);
    }

    /// <summary>Five games whose order is known by name: A → Z Alpha, bravo, Charlie, Delta, Echo.</summary>
    private static readonly LauncherSpec FiveGames = LauncherSpec.With(
        KnownLaunchers.Gog, Installed("Echo"), Installed("Delta"), Installed("Charlie"), Installed("bravo"), Installed("Alpha"));

    private static Game Installed(string title) =>
        TestGames.Installed(KnownLaunchers.Gog, title, installedAt: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    /// <summary>The title shown beside the middle: -1 left, 1 right.</summary>
    private static string Side(GameDetailViewModel detail, int offset) => detail.Pages.Single(page => page.Offset == offset).Tile.Title;
}
