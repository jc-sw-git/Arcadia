using Arcadia.App.Tests.Fixtures;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// Installations and updates in Steam as Arcadia follows them (#81): the app manifests and <c>content_log.txt</c> change
/// as they did on the owner's PC (<c>docs/launchers/steam/library-and-installs.md</c>), and the real install monitor reads them.
/// </summary>
public sealed class SteamInstallTests
{
    private static readonly SteamApp Kupferwald = new("9200001", "Kupferwald");
    private static readonly SteamApp Sternenhafen = new("9200002", "Sternenhafen");

    [AvaloniaFact]
    [Trait("TestCase", "T81-3")]
    public async Task A_Steam_download_shows_queue_progress_and_pause_and_a_new_install_ends_installed_with_a_note_without_a_restart()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(Kupferwald);
        using var arcadia = await StartAsync(files);

        // A download as Steam writes it into the manifest: accepted, waiting, downloading, paused.
        steam.Write(Kupferwald with { StateFlags = 1030, BytesToDownload = 1000, BytesToStage = 1000 });
        await arcadia.FollowInstallsAsync();
        Assert.Equal("In der Warteschlange", TileOf(arcadia, Kupferwald).InstallStatusText);
        Assert.True(TileOf(arcadia, Kupferwald).IsInstallQueued);

        // Running, the manifest's counters go stale at once, so no number (#278); paused they are exact.
        steam.Write(Kupferwald with { StateFlags = 1030, BytesToDownload = 1000, BytesDownloaded = 300, BytesToStage = 1000, BytesStaged = 300 });
        await arcadia.FollowInstallsAsync();
        Assert.Equal("Wird aktualisiert …", TileOf(arcadia, Kupferwald).InstallStatusText);
        Assert.True(TileOf(arcadia, Kupferwald).IsInstallIndeterminate);

        steam.Write(Kupferwald with { StateFlags = 1542, BytesToDownload = 1000, BytesDownloaded = 300, BytesToStage = 1000, BytesStaged = 300 });
        await arcadia.FollowInstallsAsync();
        Assert.Equal("Pausiert · 30 %", TileOf(arcadia, Kupferwald).InstallStatusText);

        // A new install has no build until it is done, so it has no cover before; at the end it is there.
        steam.Write(Sternenhafen with { StateFlags = 2, BuildId = 0 });
        await arcadia.FollowInstallsAsync();
        steam.Write(Sternenhafen with { StateFlags = 1026, BuildId = 0, BytesToDownload = 1000, BytesDownloaded = 500 });
        await arcadia.FollowInstallsAsync();
        steam.Write(Sternenhafen);
        await arcadia.FollowInstallsAsync();

        Assert.Equal("„Sternenhafen“ ist jetzt installiert.", arcadia.Main.Toast.Text);
        var installed = TileOf(arcadia, Sternenhafen);
        Assert.True(installed.IsInstalled);
        Assert.Null(installed.InstallState);
        Assert.Equal("Spielen", installed.PrimaryActionText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T81-4")]
    public async Task Of_two_Steam_downloads_only_the_one_Steam_works_on_downloads_and_the_other_waits()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(Kupferwald);
        steam.Write(Sternenhafen);
        using var arcadia = await StartAsync(files);

        // Steam flags both alike; its content log names the one it works on.
        steam.Write(Kupferwald with { StateFlags = 1030, BytesToDownload = 1000, BytesDownloaded = 100 });
        steam.Write(Sternenhafen with { StateFlags = 1030, BytesToDownload = 1000, BytesDownloaded = 100 });
        steam.AppendContentLog($"AppID {Sternenhafen.AppId} state changed : Update Required,Update Running,Update Started,");
        await arcadia.FollowInstallsAsync();

        Assert.True(TileOf(arcadia, Sternenhafen).IsInstallSpinning);
        Assert.False(TileOf(arcadia, Sternenhafen).IsInstallQueued);
        Assert.True(TileOf(arcadia, Kupferwald).IsInstallQueued);
        Assert.False(TileOf(arcadia, Kupferwald).IsInstallSpinning);
        Assert.Equal("Wartet", TileOf(arcadia, Kupferwald).InstallShortText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T81-5")]
    public async Task Cancelling_a_new_Steam_install_shows_a_note()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(Kupferwald);
        using var arcadia = await StartAsync(files);

        steam.Write(Sternenhafen with { StateFlags = 1026, BuildId = 0, BytesToDownload = 1000, BytesDownloaded = 100 });
        await arcadia.FollowInstallsAsync();
        steam.Remove(Sternenhafen.AppId);
        await arcadia.FollowInstallsAsync();

        Assert.StartsWith("Installation von „", arcadia.Main.Toast.Text);
        Assert.EndsWith("“ abgebrochen.", arcadia.Main.Toast.Text);
        Assert.DoesNotContain(arcadia.PageGames, tile => tile.Title == Sternenhafen.Name);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T81-5")]
    public async Task Uninstalling_in_Steam_shows_a_note_and_the_game_leaves_the_tab()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(Kupferwald);
        steam.Write(Sternenhafen);
        using var arcadia = await StartAsync(files);

        steam.Remove(Kupferwald.AppId);
        Directory.Delete(steam.GameFolder(Kupferwald), recursive: true);
        await arcadia.FollowInstallsAsync();

        Assert.Equal("„Kupferwald“ wurde deinstalliert.", arcadia.Main.Toast.Text);
        Assert.Equal(["Sternenhafen"], arcadia.PageGames.Select(tile => tile.Title));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T81-5")]
    public async Task An_update_of_a_Steam_tool_gives_no_note()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(Kupferwald);
        var redistributables = new SteamApp("228980", "Steamworks Common Redistributables");
        steam.Write(redistributables);
        using var arcadia = await StartAsync(files);

        steam.Write(redistributables with { StateFlags = 1030, BytesToDownload = 1000, BytesDownloaded = 500 });
        await arcadia.FollowInstallsAsync();
        steam.Write(redistributables);
        await arcadia.FollowInstallsAsync();

        Assert.Null(arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T81-6")]
    public async Task A_running_Steam_update_shows_as_updating_not_as_waiting_even_before_its_bytes_move()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(Kupferwald);
        using var arcadia = await StartAsync(files);

        // Steam writes the byte counters only at the start and the end; its content log says at once that it works.
        steam.Write(Kupferwald with { StateFlags = 1030, BytesToDownload = 1000 });
        steam.AppendContentLog($"AppID {Kupferwald.AppId} state changed : Update Required,Update Running,Update Started,");
        await arcadia.FollowInstallsAsync();

        var tile = TileOf(arcadia, Kupferwald);
        Assert.Equal("Wird aktualisiert …", tile.InstallStatusText);
        Assert.Equal("Lädt", tile.InstallShortText);
        Assert.False(tile.IsInstallQueued);
    }

    // #278: the content log lines are the owner's of 2026-10-27 (docs/launchers/steam/library-and-installs.md); one poll of
    // the install timer (2 s) stands for "within 5 s".

    [AvaloniaFact]
    [Trait("TestCase", "T278-2")]
    public async Task A_pause_in_Steam_shows_paused_with_Steams_number_and_a_resume_runs_again_without_one()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(Kupferwald);
        using var arcadia = await StartAsync(files);
        steam.Write(Kupferwald with { StateFlags = 1030, BytesToDownload = 1000, BytesToStage = 1200 });
        steam.AppendContentLog(
            $"AppID {Kupferwald.AppId} update started : download 100/1000, store 0/0, reuse 0/0, delta 0/0, stage 120/1200 ",
            $"AppID {Kupferwald.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Running,Update Started,",
            $"AppID {Kupferwald.AppId} App update changed : Running Update,Downloading,Staging,");
        await arcadia.FollowInstallsAsync();
        AssertRunningWithoutNumber(TileOf(arcadia, Kupferwald), "Wird aktualisiert …");

        // Steam names the pause in the log at once and writes its bytes into the manifest, without the paused flag.
        steam.AppendContentLog(
            $"AppID {Kupferwald.AppId} update canceled : Disabled (Suspended)",
            $"AppID {Kupferwald.AppId} App update changed : Running Update,Downloading,Staging,Stopping,",
            $"AppID {Kupferwald.AppId} App update changed : None",
            $"AppID {Kupferwald.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Started, (Suspended)");
        steam.Write(Kupferwald with { StateFlags = 1030, BytesToDownload = 1000, BytesDownloaded = 420, BytesToStage = 1200, BytesStaged = 500 });
        await arcadia.FollowInstallsAsync();
        Assert.Equal("Pausiert · 42 %", TileOf(arcadia, Kupferwald).InstallStatusText);
        Assert.True(TileOf(arcadia, Kupferwald).IsInstallPaused);

        steam.AppendContentLog(
            $"AppID {Kupferwald.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Running,Update Started,",
            $"AppID {Kupferwald.AppId} update started : download 420/1000, store 0/0, reuse 0/0, delta 0/0, stage 500/1200 ",
            $"AppID {Kupferwald.AppId} App update changed : Running Update,Downloading,Staging,");
        await arcadia.FollowInstallsAsync();
        AssertRunningWithoutNumber(TileOf(arcadia, Kupferwald), "Wird aktualisiert …");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T278-3")]
    public async Task Of_two_Steam_downloads_the_one_another_took_over_waits_and_switching_back_swaps_them()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(Kupferwald);
        steam.Write(Sternenhafen);
        using var arcadia = await StartAsync(files);
        steam.Write(Kupferwald with { StateFlags = 1030, BytesToDownload = 1000, BytesToStage = 1200 });
        steam.Write(Sternenhafen with { StateFlags = 1030, BytesToDownload = 1000, BytesToStage = 1200 });
        steam.AppendContentLog(
            $"AppID {Kupferwald.AppId} update started : download 0/1000, store 0/0, reuse 0/0, delta 0/0, stage 0/1200 ",
            $"AppID {Kupferwald.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Running,Update Started,",
            $"AppID {Sternenhafen.AppId} state changed : Update Required,Fully Installed,Update Queued,");
        await arcadia.FollowInstallsAsync();

        // The owner moves Sternenhafen to the top of Steam's queue.
        steam.AppendContentLog(
            $"AppID {Kupferwald.AppId} update canceled : Priority (Suspended)",
            $"AppID {Kupferwald.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Started, (Suspended)",
            $"AppID {Sternenhafen.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Running,",
            $"AppID {Sternenhafen.AppId} App update changed : Running Update,Preallocating,");
        await arcadia.FollowInstallsAsync();
        AssertWaiting(TileOf(arcadia, Kupferwald));
        AssertRunningWithoutNumber(TileOf(arcadia, Sternenhafen), "Wird aktualisiert …");

        steam.AppendContentLog(
            $"AppID {Sternenhafen.AppId} update canceled : Priority (Suspended)",
            $"AppID {Sternenhafen.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Started, (Suspended)",
            $"AppID {Kupferwald.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Running,Update Started,",
            $"AppID {Kupferwald.AppId} update started : download 300/1000, store 0/0, reuse 0/0, delta 0/0, stage 350/1200 ");
        await arcadia.FollowInstallsAsync();
        AssertWaiting(TileOf(arcadia, Sternenhafen));
        AssertRunningWithoutNumber(TileOf(arcadia, Kupferwald), "Wird aktualisiert …");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T278-4")]
    public async Task A_Steam_update_shows_no_number_while_Steam_preallocates_and_downloads_then_ends_updated()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(Kupferwald);
        using var arcadia = await StartAsync(files);

        // Before the manifest says anything, the log knows Steam works on the update.
        steam.Write(Kupferwald with { StateFlags = 6 });
        steam.AppendContentLog(
            $"AppID {Kupferwald.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Running,",
            $"AppID {Kupferwald.AppId} App update changed : Running Update,Preallocating,");
        await arcadia.FollowInstallsAsync();
        AssertRunningWithoutNumber(TileOf(arcadia, Kupferwald), "Wird aktualisiert …");

        steam.Write(Kupferwald with { StateFlags = 1030, BytesToDownload = 1000, BytesDownloaded = 300, BytesToStage = 1200, BytesStaged = 350 });
        steam.AppendContentLog(
            $"AppID {Kupferwald.AppId} update started : download 300/1000, store 0/0, reuse 0/0, delta 0/0, stage 350/1200 ",
            $"AppID {Kupferwald.AppId} App update changed : Running Update,Downloading,Staging,");
        await arcadia.FollowInstallsAsync();
        AssertRunningWithoutNumber(TileOf(arcadia, Kupferwald), "Wird aktualisiert …");

        steam.AppendContentLog($"AppID {Kupferwald.AppId} App update changed : Running Update,Verifying Staged,");
        await arcadia.FollowInstallsAsync();
        Assert.Equal("Wird abgeschlossen …", TileOf(arcadia, Kupferwald).InstallStatusText);

        steam.AppendContentLog(
            $"AppID {Kupferwald.AppId} finished update, 1 mounted depots (BuildID 1234568) : 9200004 (1),",
            $"AppID {Kupferwald.AppId} state changed : Fully Installed,");
        steam.Write(Kupferwald with { BuildId = 1234568 });
        await arcadia.FollowInstallsAsync();
        Assert.Null(TileOf(arcadia, Kupferwald).InstallState);
        Assert.True(TileOf(arcadia, Kupferwald).IsInstalled);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T278-5")]
    public async Task After_a_Steam_account_switch_the_running_and_the_queued_download_are_paused_and_switching_back_resumes_them()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(Kupferwald);
        steam.Write(Sternenhafen);
        using var arcadia = await StartAsync(files);
        steam.Write(Kupferwald with { StateFlags = 1030, BytesToDownload = 1000, BytesDownloaded = 100, BytesToStage = 1200 });
        steam.Write(Sternenhafen with { StateFlags = 1030, BytesToDownload = 1000, BytesToStage = 1200 });
        steam.AppendContentLog(
            $"AppID {Sternenhafen.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Started,",
            $"AppID {Kupferwald.AppId} update started : download 100/1000, store 0/0, reuse 0/0, delta 0/0, stage 120/1200 ",
            $"AppID {Kupferwald.AppId} App update changed : Running Update,Downloading,Staging,");
        await arcadia.FollowInstallsAsync();
        AssertRunningWithoutNumber(TileOf(arcadia, Kupferwald), "Wird aktualisiert …");
        AssertWaiting(TileOf(arcadia, Sternenhafen));

        // The logoff suspends the running download; the other account's session doesn't mention either.
        steam.AppendContentLog(
            $"AppID {Kupferwald.AppId} update canceled : Disabled (Suspended)",
            $"AppID {Kupferwald.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Started, (Suspended)",
            "Client version: 1793056502",
            "Loaded 2 apps from install folder \"C:\\Steam\\steamapps\\appmanifest_*.acf\".");
        await arcadia.FollowInstallsAsync();
        Assert.True(TileOf(arcadia, Kupferwald).IsInstallPaused);
        Assert.StartsWith("Pausiert", TileOf(arcadia, Kupferwald).InstallStatusText);
        Assert.True(TileOf(arcadia, Sternenhafen).IsInstallPaused);
        Assert.StartsWith("Pausiert", TileOf(arcadia, Sternenhafen).InstallStatusText);

        steam.AppendContentLog(
            "Client version: 1793056502",
            $"AppID {Sternenhafen.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Started,",
            $"AppID {Kupferwald.AppId} state changed : Update Required,Fully Installed,Update Queued,Update Running,Update Started,",
            $"AppID {Kupferwald.AppId} App update changed : Running Update,Preallocating,");
        await arcadia.FollowInstallsAsync();
        AssertRunningWithoutNumber(TileOf(arcadia, Kupferwald), "Wird aktualisiert …");
        AssertWaiting(TileOf(arcadia, Sternenhafen));
    }

    private static void AssertRunningWithoutNumber(Arcadia.App.ViewModels.GameTileViewModel tile, string text)
    {
        Assert.Equal(text, tile.InstallStatusText);
        Assert.True(tile.IsInstallIndeterminate);
        Assert.True(tile.IsInstallSpinning);
    }

    private static void AssertWaiting(Arcadia.App.ViewModels.GameTileViewModel tile)
    {
        Assert.Equal("In der Warteschlange", tile.InstallStatusText);
        Assert.True(tile.IsInstallQueued);
    }

    /// <summary>Arcadia started a while ago: the install timer has read the manifests once, so what comes now is news.</summary>
    private static async Task<ArcadiaDriver> StartAsync(LauncherFiles files)
    {
        var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await arcadia.FollowInstallsAsync();
        return arcadia;
    }

    private static Arcadia.App.ViewModels.GameTileViewModel TileOf(ArcadiaDriver arcadia, SteamApp app) =>
        arcadia.PageGames.Single(tile => tile.Title == app.Name);
}
