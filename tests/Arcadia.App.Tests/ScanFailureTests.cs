using System.Text.Json.Nodes;
using Arcadia.App.Input;
using Arcadia.App.Tests.Fixtures;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>A launcher whose files can't be read says so instead of looking empty (#369).</summary>
public sealed class ScanFailureTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T369-1")]
    public async Task A_locked_library_list_shows_a_failed_scan_on_the_Steam_tab_and_trying_again_reads_it()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(new SteamApp("9100001", "Sternenhafen"));
        var locked = new FileStream(Path.Combine(steam.Root, "steamapps", "libraryfolders.vdf"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);

        var empty = arcadia.Main.Library.EmptyTab!;
        Assert.True(empty.IsScanFailed);
        Assert.Equal(("Steam konnte nicht eingelesen werden", "Einlesen fehlgeschlagen", "Erneut versuchen"), (empty.Title, empty.Summary, empty.RescanText));
        Assert.Equal("Einlesen fehlgeschlagen", arcadia.Main.Library.CountText);

        // Tried again with the controller, while Steam still holds the file.
        arcadia.Press(UiAction.Right).Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == "Steam konnte nicht eingelesen werden.", "der Toast nach dem Einlesen");

        locked.Dispose();
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == "1 Spiel in Steam gefunden.", "der Toast nach dem Einlesen");
        Assert.Null(arcadia.Main.Library.EmptyTab);
        Assert.Equal("Sternenhafen", arcadia.FocusedGame?.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T369-2")]
    public async Task A_failed_rescan_keeps_the_games_shown_and_says_so_once()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(new SteamApp("9100001", "Sternenhafen"));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        Assert.Equal("Sternenhafen", arcadia.FocusedGame?.Title);

        using (new FileStream(Path.Combine(steam.Root, "steamapps", "libraryfolders.vdf"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await arcadia.ActivateWindowAsync();
            Assert.Equal("Steam konnte nicht neu eingelesen werden; die zuletzt gelesenen Spiele bleiben.", arcadia.Main.Toast.Text);
            Assert.Equal("Sternenhafen", arcadia.FocusedGame?.Title);
            Assert.Null(arcadia.Main.Library.EmptyTab);

            // Still failing: the toast has gone and no second one comes.
            await arcadia.ActivateWindowAsync();
            Assert.False(arcadia.Main.Toast.IsVisible, arcadia.Main.Toast.Text);
        }

        await arcadia.ActivateWindowAsync();
        Assert.False(arcadia.SelectedTab!.ScanFailed);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T369-1")]
    public async Task A_tab_without_games_whose_rescan_fails_changes_to_the_failed_state_and_back()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        Assert.Equal("Keine Spiele in Steam", arcadia.Main.Library.EmptyTab?.Title);

        using (new FileStream(Path.Combine(steam.Root, "steamapps", "libraryfolders.vdf"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await arcadia.ActivateWindowAsync();
            Assert.Equal(("Steam konnte nicht eingelesen werden", "Erneut versuchen"), (arcadia.Main.Library.EmptyTab?.Title, arcadia.Main.Library.EmptyTab?.RescanText));
        }

        await arcadia.ActivateWindowAsync();
        Assert.Equal("Keine Spiele in Steam", arcadia.Main.Library.EmptyTab?.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T369-1")]
    public async Task The_window_shows_the_failed_state_when_a_tab_without_games_fails_and_the_empty_state_when_it_reads_again()
    {
        using var window = await ArcadiaWindow.ShowAsync([new LauncherSpec(Core.Launchers.KnownLaunchers.Steam, 0, Library: [])]);
        var scanner = (FixedScanner)window.Arcadia.Launchers.Single().Scanner;
        window.OneWithText("Keine Spiele in Steam");

        scanner.Failure = new IOException("locked");
        await window.Arcadia.RescanAsync();
        window.Settle();
        window.OneWithText("Steam konnte nicht eingelesen werden");
        window.OneWithText("Erneut versuchen");

        scanner.Failure = null;
        await window.Arcadia.RescanAsync();
        window.Settle();
        window.OneWithText("Keine Spiele in Steam");
    }

    [AvaloniaTheory]
    [Trait("TestCase", "T369-2")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reading_again_by_hand_while_the_file_is_locked_says_the_games_stay(bool oneLauncher)
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(new SteamApp("9100001", "Sternenhafen"));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);

        using (new FileStream(Path.Combine(steam.Root, "steamapps", "libraryfolders.vdf"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // "Neu einlesen" on Steam's page, or "Alle neu einlesen" in the launcher settings.
            await arcadia.Main.Session.RescanAsync(oneLauncher ? arcadia.SelectedTab!.Launcher : null);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal("Sternenhafen", arcadia.FocusedGame?.Title);
            Assert.Equal("Steam konnte nicht neu eingelesen werden; die zuletzt gelesenen Spiele bleiben.", arcadia.Main.Toast.Text);
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T369-3")]
    public async Task A_broken_Epic_manifest_costs_only_its_own_game()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        var broken = new EpicApp("handmade", "Handarbeit");
        epic.WriteInstalled(broken);
        epic.WriteInstalled(new EpicApp("glimmer", "Glimmerbucht"));
        epic.WriteInstalled(new EpicApp("garbled", "Kauderwelsch"));
        var manifest = Path.Combine(epic.Folders.ManifestDirectory, "handmade.item");
        var json = JsonNode.Parse(File.ReadAllText(manifest))!;
        json["InstallSize"] = "1000";
        File.WriteAllText(manifest, json.ToJsonString());
        File.WriteAllText(Path.Combine(epic.Folders.ManifestDirectory, "garbled.item"), "{ not json");

        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);

        var tab = Assert.Single(arcadia.Main.LauncherTabs.Items);
        Assert.False(tab.ScanFailed);
        Assert.Equal(["Glimmerbucht", "Handarbeit"], tab.Games.Where(g => g.IsInstalled).Select(g => g.Title).Order(StringComparer.Ordinal));
        Assert.Null(tab.Games.Single(g => g.Title == "Handarbeit").InstallSizeBytes);
    }
}
