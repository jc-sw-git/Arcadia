using Arcadia.App.Input;
using Arcadia.App.Tests.Fixtures;
using Arcadia.App.ViewModels;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The update icon of a Steam game as Steam's manifests say it (#288): only a flag with a new build or bytes left to
/// download is an update, and the real install monitor shows it and takes it away live, without a rescan
/// (<c>docs/launchers/steam/library-and-installs.md</c>).
/// </summary>
public sealed class SteamUpdateTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    // As a real update: Steam flags it, names the new build and has bytes to download.
    private static readonly SteamApp Kupferwald = new("9200101", "Kupferwald") { BuildId = 1234567, TargetBuildId = 1234567 };
    private static readonly SteamApp KupferwaldUpdate = Kupferwald with { StateFlags = 6, TargetBuildId = 1299999, BytesToDownload = 129_207_952 };

    // As Half-Life 2 at a Steam start: flagged with the same build and a finished update's counters left in the manifest.
    private static readonly SteamApp Sternenhafen = new("9200102", "Sternenhafen")
    {
        BuildId = 7654321, TargetBuildId = 7654321, BytesToDownload = 28_153_264, BytesDownloaded = 28_153_264,
    };

    [AvaloniaFact]
    [Trait("TestCase", "T288-2")]
    [Trait("TestCase", "T288-7")]
    public async Task At_the_start_a_real_Steam_update_shows_on_the_cover_and_the_detail_page_and_a_flag_with_nothing_new_doesnt()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(KupferwaldUpdate);
        steam.Write(Sternenhafen with { StateFlags = 6 });
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);

        Assert.True(TileOf(arcadia, Kupferwald).UpdateAvailable);
        Assert.False(TileOf(arcadia, Sternenhafen).UpdateAvailable);
        await arcadia.FollowInstallsAsync();
        Assert.True(TileOf(arcadia, Kupferwald).UpdateAvailable);
        Assert.False(TileOf(arcadia, Sternenhafen).UpdateAvailable);

        var page = OpenDetail(arcadia, Kupferwald);
        Assert.True(page.HasUpdateNote);
        Assert.Equal("Steam installiert das Update eventuell, bevor das Spiel startet.", page.UpdateNote);
        Assert.Equal((GameActionKind.Play, "Spielen"), (page.TopRow[0].Kind, page.TopRow[0].ButtonLabel));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T288-1")]
    [Trait("TestCase", "T288-3")]
    [Trait("TestCase", "T288-4")]
    public async Task While_Arcadia_is_open_a_Steam_update_appears_and_goes_without_a_rescan_or_a_toast_and_a_brief_flag_with_nothing_new_never_shows()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(Kupferwald);
        steam.Write(Sternenhafen);
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await arcadia.FollowInstallsAsync();
        var scannedAt = arcadia.Main.Session.ScannedAt(KnownLaunchers.Steam.Id);

        // Steam starts and flags Sternenhafen for a few seconds, then drops the flag.
        steam.Write(Sternenhafen with { StateFlags = 6 });
        await arcadia.FollowInstallsAsync();
        Assert.False(TileOf(arcadia, Sternenhafen).UpdateAvailable);
        steam.Write(Sternenhafen);
        await arcadia.FollowInstallsAsync();
        Assert.False(TileOf(arcadia, Sternenhafen).UpdateAvailable);

        // Steam finds a new build of Kupferwald.
        steam.Write(KupferwaldUpdate);
        await arcadia.FollowInstallsAsync();
        Assert.True(TileOf(arcadia, Kupferwald).UpdateAvailable);

        // Steam installs it in the background and leaves the counters in the manifest.
        steam.Write(Kupferwald with { BuildId = 1299999, TargetBuildId = 1299999, BytesToDownload = 129_207_952, BytesDownloaded = 129_207_952 });
        await arcadia.FollowInstallsAsync();
        Assert.False(TileOf(arcadia, Kupferwald).UpdateAvailable);

        Assert.Equal(scannedAt, arcadia.Main.Session.ScannedAt(KnownLaunchers.Steam.Id));
        Assert.Null(arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T288-5")]
    public async Task On_the_detail_page_the_update_note_appears_and_goes_while_page_and_focus_stay()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        steam.Write(Kupferwald);
        steam.Write(Sternenhafen);
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await arcadia.FollowInstallsAsync();
        var page = OpenDetail(arcadia, Kupferwald);
        // Away from "Spielen", the button the page falls back to.
        arcadia.Press(UiAction.Right);
        var focused = page.FocusedKind;
        Assert.NotEqual(GameActionKind.Play, focused);
        Assert.False(page.HasUpdateNote);
        // The page shows only what it says changed.
        var told = new List<string?>();
        page.PropertyChanged += (_, e) => told.Add(e.PropertyName);

        steam.Write(KupferwaldUpdate);
        await arcadia.FollowInstallsAsync();
        Assert.Same(page, arcadia.Detail!.Current);
        Assert.True(page.HasUpdateNote);
        Assert.Contains(nameof(page.HasUpdateNote), told);
        Assert.True(page.Tile.UpdateAvailable);
        Assert.Equal(focused, page.FocusedKind);

        told.Clear();
        steam.Write(Kupferwald);
        await arcadia.FollowInstallsAsync();
        Assert.Same(page, arcadia.Detail!.Current);
        Assert.False(page.HasUpdateNote);
        Assert.Contains(nameof(page.HasUpdateNote), told);
        Assert.Equal(focused, page.FocusedKind);

        arcadia.Press(UiAction.Back);
        Assert.Null(arcadia.Detail);
    }

    private static GameDetailPageViewModel OpenDetail(ArcadiaDriver arcadia, SteamApp app)
    {
        for (var step = 0; step < 4 && arcadia.FocusedGame?.Title != app.Name; step++)
        {
            arcadia.Press(UiAction.Right);
        }

        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        Assert.Equal(app.Name, page.Tile.Title);
        return page;
    }

    private static GameTileViewModel TileOf(ArcadiaDriver arcadia, SteamApp app) =>
        arcadia.PageGames.Single(tile => tile.Title == app.Name);
}
