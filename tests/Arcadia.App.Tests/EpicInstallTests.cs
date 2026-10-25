using Arcadia.App.Input;
using Arcadia.App.Tests.Fixtures;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// Installations in the Epic Games Launcher as Arcadia follows them (#41, #81): Epic's log, its download queue and the
/// install folder change as they did on the owner's PC (<c>docs/launchers/epic.md</c>), and the real install monitor reads them.
/// </summary>
public sealed class EpicInstallTests
{
    private static readonly string[] Game = ["games", "applications"];

    private static readonly EpicApp Leuchtturm = new("ba01", "Leuchtturm") { InstallSize = 1000 };
    private static readonly EpicApp Salzwiesen = new("ba02", "Salzwiesen") { InstallSize = 1000 };

    [AvaloniaFact]
    [Trait("TestCase", "T41-3")]
    public async Task An_install_from_Arcadia_waits_for_confirmation_then_queues_downloads_and_ends_installed_without_a_restart()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        epic.WriteCatalog((Leuchtturm, Game, null));
        using var arcadia = await StartAsync(files);

        arcadia.Press(UiAction.Accept);
        Assert.Equal(new LaunchTarget.ProtocolUri($"com.epicgames.launcher://apps/{Leuchtturm.Namespace}%3A{Leuchtturm.CatalogItemId}%3A{Leuchtturm.AppName}?action=install"), Assert.Single(arcadia.Opened));
        Assert.Equal("Bitte in Epic Games bestätigen", Tile(arcadia).InstallStatusText);
        await arcadia.FollowInstallsAsync();
        Assert.Equal("Bitte in Epic Games bestätigen", Tile(arcadia).InstallStatusText);

        // Confirmed in Epic behind another download.
        epic.SetQueue(Salzwiesen, Leuchtturm);
        epic.Log(EpicFolder.Queued(Salzwiesen), EpicFolder.Started(Salzwiesen), EpicFolder.Queued(Leuchtturm));
        await arcadia.FollowInstallsAsync();
        Assert.Equal("In der Warteschlange · Platz 1", Tile(arcadia).InstallStatusText);
        Assert.True(Tile(arcadia).IsInstallQueued);

        epic.SetQueue(Leuchtturm);
        epic.Log(EpicFolder.Completed(Salzwiesen), EpicFolder.Started(Leuchtturm));
        epic.WritePending(Leuchtturm);
        epic.WriteGameBytes(Leuchtturm, 420);
        await arcadia.FollowInstallsAsync();
        Assert.Equal("Wird installiert · 42\u00A0%", Tile(arcadia).InstallStatusText);

        epic.SetQueue();
        epic.WriteInstalled(Leuchtturm);
        epic.Log(EpicFolder.Completed(Leuchtturm));
        await arcadia.FollowInstallsAsync();

        Assert.Equal("„Leuchtturm“ ist jetzt installiert.", arcadia.Main.Toast.Text);
        Assert.True(Tile(arcadia).IsInstalled);
        Assert.Null(Tile(arcadia).InstallState);
        Assert.Equal("Spielen", Tile(arcadia).PrimaryActionText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T41-4")]
    public async Task Pause_resume_and_cancel_in_Epic_show_in_Arcadia_and_install_is_locked_while_one_runs()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        epic.WriteCatalog((Leuchtturm, Game, null));
        using var arcadia = await StartAsync(files);
        DownloadingHalf(epic, Leuchtturm);
        await arcadia.FollowInstallsAsync();
        Assert.Equal("Wird installiert · 50\u00A0%", Tile(arcadia).InstallStatusText);

        // "Installieren" is locked: A only says what is going on.
        arcadia.Press(UiAction.Accept);
        Assert.Empty(arcadia.Opened);
        Assert.False(Tile(arcadia).CanActivate);
        Assert.Equal("„Leuchtturm“ wird bereits installiert – Pausieren und Abbrechen in Epic Games.", arcadia.Main.Toast.Text);

        epic.Log(EpicFolder.Paused(Leuchtturm));
        await arcadia.FollowInstallsAsync();
        Assert.Equal("Pausiert · 50\u00A0%", Tile(arcadia).InstallStatusText);
        Assert.True(Tile(arcadia).IsInstallPaused);

        epic.Log(EpicFolder.Resumed(Leuchtturm));
        await arcadia.FollowInstallsAsync();
        Assert.Equal("Wird installiert · 50\u00A0%", Tile(arcadia).InstallStatusText);

        epic.SetQueue();
        epic.Log(EpicFolder.Cancelled(Leuchtturm));
        await arcadia.FollowInstallsAsync();
        Assert.Null(Tile(arcadia).InstallState);
        Assert.Equal("Installation von „Leuchtturm“ abgebrochen.", arcadia.Main.Toast.Text);
        Assert.Equal("Installieren", Tile(arcadia).PrimaryActionText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T41-5")]
    public async Task An_install_started_in_Epic_itself_shows_in_Arcadia_with_its_state()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        epic.WriteCatalog((Leuchtturm, Game, null));
        using var arcadia = await StartAsync(files);

        DownloadingHalf(epic, Leuchtturm);
        await arcadia.FollowInstallsAsync();

        Assert.Empty(arcadia.Opened);
        Assert.Equal("Wird installiert · 50\u00A0%", Tile(arcadia).InstallStatusText);
        Assert.Equal("50\u00A0%", Tile(arcadia).InstallShortText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T41-8")]
    public async Task A_paused_install_that_is_cancelled_loses_its_progress()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        epic.WriteCatalog((Leuchtturm, Game, null));
        using var arcadia = await StartAsync(files);
        DownloadingHalf(epic, Leuchtturm);
        epic.Log(EpicFolder.Paused(Leuchtturm));
        await arcadia.FollowInstallsAsync();
        Assert.Equal("Pausiert · 50\u00A0%", Tile(arcadia).InstallStatusText);

        // Epic leaves the pending manifest and the downloaded files behind; they are no state.
        epic.SetQueue();
        epic.Log(EpicFolder.Cancelled(Leuchtturm));
        await arcadia.FollowInstallsAsync();

        Assert.Null(Tile(arcadia).InstallState);
        Assert.Equal(0, Tile(arcadia).InstallPercent);
        Assert.Equal(string.Empty, Tile(arcadia).InstallStatusText);
        Assert.True(Tile(arcadia).ShowInstallBadge);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T41-10")]
    public async Task When_Epic_resumes_another_game_the_first_shows_as_paused_and_only_the_second_downloads()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        epic.WriteCatalog((Leuchtturm, Game, null), (Salzwiesen, Game, null));
        using var arcadia = await StartAsync(files);
        epic.SetQueue(Leuchtturm, Salzwiesen);
        epic.WritePending(Leuchtturm);
        epic.WritePending(Salzwiesen);
        epic.WriteGameBytes(Leuchtturm, 500);
        epic.WriteGameBytes(Salzwiesen, 200);
        epic.Log(EpicFolder.Queued(Salzwiesen), EpicFolder.Started(Salzwiesen), EpicFolder.Paused(Salzwiesen), EpicFolder.Queued(Leuchtturm), EpicFolder.Started(Leuchtturm));
        await arcadia.FollowInstallsAsync();
        Assert.Equal("Wird installiert · 50\u00A0%", TileOf(arcadia, Leuchtturm).InstallStatusText);
        Assert.Equal("Pausiert · 20\u00A0%", TileOf(arcadia, Salzwiesen).InstallStatusText);

        // Epic logs only the resume of the second; the first stops without a pause line.
        epic.Log(EpicFolder.Resumed(Salzwiesen));
        await arcadia.FollowInstallsAsync();

        Assert.Equal("Pausiert · 50\u00A0%", TileOf(arcadia, Leuchtturm).InstallStatusText);
        Assert.Equal("Wird installiert · 20\u00A0%", TileOf(arcadia, Salzwiesen).InstallStatusText);
        Assert.Single(arcadia.PageGames, tile => tile.IsInstallSpinning);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T81-6")]
    public async Task An_Epic_update_shows_as_updating_without_a_percentage_and_not_as_almost_done()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        epic.WriteInstalled(Leuchtturm);
        using var arcadia = await StartAsync(files);

        // An update downloads into the full install folder: its size says nothing.
        epic.SetQueue(Leuchtturm);
        epic.WritePending(Leuchtturm with { Version = "1.1.0" });
        epic.Log(EpicFolder.Queued(Leuchtturm), EpicFolder.Started(Leuchtturm with { Version = "1.1.0" }));
        await arcadia.FollowInstallsAsync();

        Assert.Equal("Wird aktualisiert …", Tile(arcadia).InstallStatusText);
        Assert.Equal("Lädt", Tile(arcadia).InstallShortText);
        Assert.True(Tile(arcadia).IsInstallIndeterminate);
    }

    /// <summary>Arcadia started a while ago: the install timer has read the log once, so what comes now is news.</summary>
    private static async Task<ArcadiaDriver> StartAsync(LauncherFiles files)
    {
        var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await arcadia.FollowInstallsAsync();
        return arcadia;
    }

    private static void DownloadingHalf(EpicFolder epic, EpicApp app)
    {
        epic.SetQueue(app);
        epic.WritePending(app);
        epic.WriteGameBytes(app, app.InstallSize / 2);
        epic.Log(EpicFolder.Queued(app), EpicFolder.Started(app));
    }

    private static Arcadia.App.ViewModels.GameTileViewModel Tile(ArcadiaDriver arcadia) => arcadia.FocusedGame!;

    private static Arcadia.App.ViewModels.GameTileViewModel TileOf(ArcadiaDriver arcadia, EpicApp app) =>
        arcadia.PageGames.Single(tile => tile.Title == app.Title);
}
