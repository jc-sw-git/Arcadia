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
