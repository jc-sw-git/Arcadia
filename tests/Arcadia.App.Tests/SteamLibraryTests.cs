using System.Globalization;
using Arcadia.App.Input;
using Arcadia.App.Tests.Fixtures;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>The Steam tab from Steam's own files (#1), read by the real Steam integration.</summary>
public sealed class SteamLibraryTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    [AvaloniaFact]
    [Trait("TestCase", "T1-1")]
    public async Task The_Steam_tab_shows_the_games_of_all_libraries_with_demos_but_without_tools_redistributables_and_soundtracks()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        var otherDrive = steam.AddLibrary(files.PathFor("D"));
        steam.Write(new SteamApp("9100001", "Sternenhafen"));
        steam.Write(new SteamApp("9100002", "Nebelpfad Demo"));
        steam.Write(new SteamApp("9100003", "Kupferwald"), otherDrive);
        steam.Write(new SteamApp("9100004", "Kupferwald Soundtrack"), otherDrive);

        // Steam's own tools, by their real ids and names: Arcadia knows them by both.
        steam.Write(new SteamApp("228980", "Steamworks Common Redistributables"));
        steam.Write(new SteamApp("243750", "Source SDK Base 2013 Multiplayer"), otherDrive);
        steam.Write(new SteamApp("9100005", "Proton 9.0"));
        steam.Write(new SteamApp("9100006", "Steam Linux Runtime 4.0"), otherDrive);

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);

        var tab = Assert.Single(arcadia.Main.LauncherTabs.Items, tab => tab.Name == "Steam");
        Assert.Equal(["Kupferwald", "Nebelpfad Demo", "Sternenhafen"], tab.Games.Select(game => game.Title).Order(StringComparer.Ordinal));
        Assert.Equal(3, arcadia.PageGames.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T1-2")]
    public async Task Size_last_played_and_playtime_are_Steams_and_the_install_date_is_the_game_folders_creation()
    {
        var startedFromSteam = new DateTimeOffset(2026, 10, 20, 18, 30, 0, TimeSpan.Zero);
        var endedLater = new DateTimeOffset(2026, 10, 23, 21, 15, 0, TimeSpan.Zero);
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.AddAccount(SteamFolder.Account, "Spieler", timestamp: 1_790_000_000, mostRecent: true);
        var game = new SteamApp("9100011", "Sternenhafen") { SizeOnDisk = 13_421_772_800, LastPlayed = startedFromSteam.ToUnixTimeSeconds() };
        steam.Write(game, installedAt: new DateTime(2026, 3, 14, 12, 0, 0, DateTimeKind.Utc));

        // Steam writes the account's localconfig.vdf after the game ended: the later date of the two counts.
        steam.WritePlayStats(SteamFolder.Account, (game.AppId, endedLater.ToUnixTimeSeconds(), PlaytimeMinutes: 2_530));

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        var tile = arcadia.FocusedGame!;
        Assert.Equal("Sternenhafen", tile.Title);
        Assert.Equal("12,5 GB", tile.SizeText);
        Assert.Equal("42 Std.", tile.PlaytimeText);

        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var detail = arcadia.Detail!.Current;
        Assert.Equal(Local(endedLater, "dd.MM.yyyy, HH:mm"), detail.LastPlayedAt);
        Assert.Equal("Laut Steam", detail.PlaytimeSource);

        // Where the file system keeps no creation time of its own (Linux), it counts what it has.
        Assert.Equal(Local(Directory.GetCreationTimeUtc(steam.GameFolder(game)), "dd.MM.yyyy"), detail.InstalledAt);
        if (!OperatingSystem.IsLinux())
        {
            Assert.Equal(Local(new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero), "dd.MM.yyyy"), detail.InstalledAt);
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T1-5")]
    public async Task Without_Steam_there_is_no_Steam_tab_and_no_error()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        epic.WriteInstalled(new EpicApp("aa01", "Leuchtturm"));

        // A fresh install: the first start asks, Steam is among the launchers not found and stays off.
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, new AppSettings());
        var firstRun = arcadia.Main.FirstRun!;
        Assert.Equal(["Epic Games"], firstRun.Found.Select(row => row.Name));
        Assert.Contains(firstRun.Missing, row => row.Name == "Steam" && !row.IsOn);

        arcadia.Press(UiAction.Settings);
        await arcadia.WaitUntilAsync(() => !arcadia.Main.IsFirstRunShown && arcadia.Main.LauncherTabs.Items.Count > 0, "die Bibliothek soll nach \"Fertig\" erscheinen");

        Assert.Equal(["Epic Games"], arcadia.Main.LauncherTabs.Items.Select(tab => tab.Name));
        Assert.Null(arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T1-6")]
    public async Task With_demo_the_Steam_tab_shows_the_real_games_and_only_the_others_are_demo_libraries()
    {
        using var files = new LauncherFiles { Demo = true };
        files.Steam.Install().Write(new SteamApp("9100021", "Sternenhafen"));

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, new AppSettings());

        Assert.Equal("Steam", arcadia.Main.LauncherTabs.Items[0].Name);
        Assert.Equal(["Sternenhafen"], arcadia.Main.LauncherTabs.Items[0].Games.Select(game => game.Title));
        Assert.Contains(arcadia.Main.LauncherTabs.Items, tab => tab.Name == "GOG Galaxy" && tab.Count == 31);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T1-9")]
    public async Task A_library_whose_drive_is_gone_leaves_out_its_games_without_an_error_and_they_come_back_with_the_drive()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(new SteamApp("9100031", "Sternenhafen"));
        var drive = steam.AddLibrary(files.PathFor("E"));
        steam.Write(new SteamApp("9100032", "Kupferwald"), drive);
        var unplugged = files.PathFor("E-unplugged");
        Directory.Move(drive, unplugged);

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        Assert.Equal(["Sternenhafen"], SteamTitles(arcadia));
        Assert.Null(arcadia.Main.Toast.Text);

        Directory.Move(unplugged, drive);
        await arcadia.ActivateWindowAsync();

        Assert.Equal(["Kupferwald", "Sternenhafen"], SteamTitles(arcadia));
        Assert.Null(arcadia.Main.Toast.Text);
    }

    private static IEnumerable<string> SteamTitles(ArcadiaDriver arcadia) =>
        arcadia.Main.LauncherTabs.Items.Single(tab => tab.Name == "Steam").Games.Select(game => game.Title).Order(StringComparer.Ordinal);

    private static string Local(DateTimeOffset value, string format) => value.ToLocalTime().ToString(format, CultureInfo.InvariantCulture);

    private static string Local(DateTime utc, string format) => Local(new DateTimeOffset(utc, TimeSpan.Zero), format);
}
