using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.Tests.Fixtures;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// A launcher's install dialog that the player cancels (#343): no launcher reports it, so "Bitte in … bestätigen" ends after a
/// minute, A on a waiting game opens the dialog again, and a confirm after the minute still shows its progress.
/// </summary>
public sealed class CancelledInstallDialogTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);
    private static readonly TimeSpan SignInLimit = TimeSpan.FromSeconds(30);

    private static readonly SteamApp InstalledSteamGame = new("930001", "Salzwiesen");
    private static readonly SteamApp Leuchtturm = new("930002", "Leuchtturm") { StateFlags = 1026, BuildId = 0, BytesToDownload = 1000 };

    private static readonly EpicApp Tidenhub = new("bc01", "Tidenhub") { InstallSize = 1000 };
    private static readonly EpicApp Engine = new("UE_5.9", "Unreal Engine 5.9") { Namespace = "ue", Categories = ["engines"] };

    private static readonly LaunchTarget BravoInstall = new LaunchTarget.ProtocolUri("goggalaxy://openGameView/bravo");
    private static readonly Game Bravo = TestGames.NotInstalled(KnownLaunchers.Gog, "Bravo") with { Install = BravoInstall };
    private static readonly Game Charlie = TestGames.NotInstalled(KnownLaunchers.Gog, "Charlie") with { Install = new LaunchTarget.ProtocolUri("goggalaxy://openGameView/charlie") };

    /// <summary>Alpha installed, Bravo and Charlie not, in this order; the focus starts on Alpha.</summary>
    private static readonly LauncherSpec Gog = LauncherSpec.With(
        KnownLaunchers.Gog,
        TestGames.Installed(KnownLaunchers.Gog, "Alpha", installedAt: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)),
        Bravo,
        Charlie);

    [AvaloniaFact]
    [Trait("TestCase", "T343-1")]
    public async Task A_Steam_install_nobody_confirms_waits_one_minute_then_offers_Installieren_again_on_the_tile_and_the_detail_page()
    {
        using var files = SteamWithOwnedGame();
        using var arcadia = await StartSignedInToSteamAsync(files);
        var tile = arcadia.FocusTile(Leuchtturm.Name);

        arcadia.Press(UiAction.Accept);
        Assert.Equal("Bitte in Steam bestätigen", tile.InstallStatusText);

        await PollForAsync(arcadia, TimeSpan.FromSeconds(58));
        Assert.Equal("Bitte in Steam bestätigen", tile.InstallStatusText);

        // "After 1 minute" allows one poll (2 s) on top.
        await PollForAsync(arcadia, TimeSpan.FromSeconds(4));
        Assert.Null(tile.InstallState);
        Assert.Equal(AppTexts.Install, tile.PrimaryActionText);
        Assert.True(tile.CanActivate);

        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.Equal(AppTexts.Install, arcadia.Detail!.Current.Buttons[0].ButtonLabel);
        Assert.True(arcadia.Detail.Current.Buttons[0].IsEnabled);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T343-2")]
    public async Task An_Epic_game_and_an_Epic_engine_each_offer_Installieren_again_after_one_minute()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        epic.WriteCatalog((Tidenhub, ["games", "applications"], null), (Engine, ["engines", "engines/ue5"], null));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, new Core.Settings.AppSettings
        {
            ActiveLaunchers = [KnownLaunchers.Epic.Id],
            Launchers = new Dictionary<string, Core.Settings.LauncherPreferences> { [KnownLaunchers.Epic.Id] = new(LibraryOptions: ["software"]) },
        });
        await arcadia.FollowInstallsAsync();

        var game = arcadia.FocusTile(Tidenhub.Title);
        arcadia.Press(UiAction.Accept);
        var engine = arcadia.FocusTile("Unreal Engine");
        arcadia.Press(UiAction.Accept);
        Assert.Equal("Bitte in Epic Games bestätigen", game.InstallStatusText);
        Assert.Equal("Bitte in Epic Games starten", engine.InstallStatusText);

        await PollForAsync(arcadia, TimeSpan.FromSeconds(58));
        Assert.Equal("Bitte in Epic Games bestätigen", game.InstallStatusText);
        Assert.Equal("Bitte in Epic Games starten", engine.InstallStatusText);

        await PollForAsync(arcadia, TimeSpan.FromSeconds(4));
        Assert.Equal((AppTexts.Install, AppTexts.Install), (game.PrimaryActionText, engine.PrimaryActionText));
        Assert.Null(game.InstallState);
        Assert.Null(engine.InstallState);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T343-3")]
    public async Task A_on_a_waiting_game_in_the_library_opens_the_install_again_with_the_first_toast_and_restarts_the_minute()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([Gog]);
        var tile = arcadia.FocusTile(Bravo.Title);
        arcadia.Press(UiAction.Accept);
        var firstToast = arcadia.Main.Toast.Text;

        await PollForAsync(arcadia, TimeSpan.FromSeconds(50));
        arcadia.Press(UiAction.Accept);

        Assert.Equal([BravoInstall, BravoInstall], arcadia.Opened);
        Assert.Empty(arcadia.Starts.Keys);
        Assert.Equal(firstToast, arcadia.Main.Toast.Text);
        Assert.Equal("Bitte in GOG Galaxy bestätigen", tile.InstallStatusText);

        // A at 50 s: still waiting at 1:40, "Installieren" after 1:50 (+ one poll).
        await PollForAsync(arcadia, TimeSpan.FromSeconds(50));
        Assert.Equal("Bitte in GOG Galaxy bestätigen", tile.InstallStatusText);
        await PollForAsync(arcadia, TimeSpan.FromSeconds(12));
        Assert.Equal(AppTexts.Install, tile.PrimaryActionText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T343-3")]
    public async Task A_on_a_waiting_game_on_its_detail_page_opens_the_install_again_and_B_still_goes_back()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([Gog]);
        var tile = arcadia.FocusTile(Bravo.Title);
        arcadia.Press(UiAction.Accept);
        var firstToast = arcadia.Main.Toast.Text;
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        Assert.True(arcadia.Main.IsDetailOpen);

        // The focus starts on the primary button; A on it is what the player does.
        Assert.Equal(GameActionKind.Play, arcadia.Detail!.Current.FocusedKind);
        arcadia.Press(UiAction.Accept);

        Assert.Equal([BravoInstall, BravoInstall], arcadia.Opened);
        Assert.Empty(arcadia.Starts.Keys);
        Assert.Equal(firstToast, arcadia.Main.Toast.Text);
        Assert.Equal("Bitte in GOG Galaxy bestätigen", tile.InstallStatusText);

        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsDetailOpen);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T343-4")]
    public async Task A_Steam_confirm_after_the_minute_ended_shows_the_progress_and_ends_installed()
    {
        using var files = SteamWithOwnedGame();
        using var arcadia = await StartSignedInToSteamAsync(files);
        arcadia.FocusTile(Leuchtturm.Name);
        arcadia.Press(UiAction.Accept);
        await PollForAsync(arcadia, TimeSpan.FromSeconds(62));
        Assert.Equal(AppTexts.Install, TileOf(arcadia, Leuchtturm).PrimaryActionText);

        // Confirmed in Steam's still open dialog: the new install's manifest appears and downloads.
        files.Steam.Write(Leuchtturm);
        files.Steam.Write(Leuchtturm with { BytesDownloaded = 400 });
        await arcadia.FollowInstallsAsync();
        Assert.NotNull(TileOf(arcadia, Leuchtturm).InstallState);
        Assert.NotEqual(InstallPhase.AwaitingConfirmation, TileOf(arcadia, Leuchtturm).InstallState!.Phase);

        files.Steam.Write(Leuchtturm with { StateFlags = 4, BuildId = 1234567 });
        await arcadia.FollowInstallsAsync();
        Assert.Equal("„Leuchtturm“ ist jetzt installiert.", arcadia.Main.Toast.Text);
        Assert.True(TileOf(arcadia, Leuchtturm).IsInstalled);
        Assert.Equal("Spielen", TileOf(arcadia, Leuchtturm).PrimaryActionText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T343-5")]
    public async Task A_quick_Steam_confirm_goes_from_waiting_straight_to_the_progress()
    {
        using var files = SteamWithOwnedGame();
        using var arcadia = await StartSignedInToSteamAsync(files);
        var tile = arcadia.FocusTile(Leuchtturm.Name);
        var shown = new List<string>();
        tile.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameTileViewModel.PrimaryActionText))
            {
                shown.Add(tile.PrimaryActionText);
            }
        };

        arcadia.Press(UiAction.Accept);
        files.Steam.Write(Leuchtturm with { BytesDownloaded = 100 });
        await arcadia.FollowInstallsAsync();

        Assert.NotNull(tile.InstallState);
        Assert.NotEqual(InstallPhase.AwaitingConfirmation, tile.InstallState!.Phase);
        Assert.DoesNotContain(AppTexts.Install, shown);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T343-6")]
    public async Task Two_waiting_games_end_after_their_own_minute_and_A_restarts_only_its_own()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([Gog]);
        var bravo = arcadia.FocusTile(Bravo.Title);
        arcadia.Press(UiAction.Accept);
        await PollForAsync(arcadia, TimeSpan.FromSeconds(20));
        var charlie = arcadia.FocusTile(Charlie.Title);
        arcadia.Press(UiAction.Accept);
        await PollForAsync(arcadia, TimeSpan.FromSeconds(20));

        // A on Charlie again at 0:40 (its 0:20): Bravo's minute stays as it was.
        arcadia.Press(UiAction.Accept);
        await PollForAsync(arcadia, TimeSpan.FromSeconds(24));
        Assert.Null(bravo.InstallState);
        Assert.Equal("Bitte in GOG Galaxy bestätigen", charlie.InstallStatusText);

        await PollForAsync(arcadia, TimeSpan.FromSeconds(38));
        Assert.Null(charlie.InstallState);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T343-7")]
    public async Task After_a_restart_while_waiting_the_game_offers_Installieren()
    {
        var arcadia = await ArcadiaDriver.StartAsync([Gog]);
        arcadia.FocusTile(Bravo.Title);
        arcadia.Press(UiAction.Accept);
        Assert.Equal("Bitte in GOG Galaxy bestätigen", arcadia.FocusedGame!.InstallStatusText);

        using var restarted = await arcadia.RestartAsync();
        arcadia.Dispose();
        var tile = restarted.PageGames.Single(t => t.Title == Bravo.Title);
        Assert.Null(tile.InstallState);
        Assert.Equal(AppTexts.Install, tile.PrimaryActionText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T343-8")]
    public async Task A_on_a_waiting_game_whose_launcher_cant_be_opened_says_why_and_keeps_the_first_minute()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([Gog]);
        var tile = arcadia.FocusTile(Bravo.Title);
        arcadia.Press(UiAction.Accept);
        await PollForAsync(arcadia, TimeSpan.FromSeconds(50));

        arcadia.OpenFailure = new Win32Exception("Das System kann die angegebene Datei nicht finden");
        arcadia.Press(UiAction.Accept);
        Assert.Equal("Installation von „Bravo“ konnte nicht geöffnet werden: Das System kann die angegebene Datei nicht finden", arcadia.Main.Toast.Text);
        Assert.Equal("Bitte in GOG Galaxy bestätigen", tile.InstallStatusText);

        // The minute of the first A runs on: "Installieren" at 1:00 (+ one poll), not at 1:50.
        await PollForAsync(arcadia, TimeSpan.FromSeconds(12));
        Assert.Null(tile.InstallState);
        Assert.Equal(AppTexts.Install, tile.PrimaryActionText);
    }

    /// <summary>The install timer ticks every 2 s for <paramref name="duration"/>.</summary>
    private static async Task PollForAsync(ArcadiaDriver arcadia, TimeSpan duration)
    {
        for (var passed = TimeSpan.Zero; passed < duration; passed += TimeSpan.FromSeconds(2))
        {
            await arcadia.FollowInstallsAsync();
        }
    }

    /// <summary>Steam with one installed game; the account owns it and Leuchtturm, which isn't installed.</summary>
    private static LauncherFiles SteamWithOwnedGame()
    {
        var files = new LauncherFiles();
        files.Steam.Install().Write(InstalledSteamGame);
        files.SteamWeb.OwnedGames.AddRange(
        [
            (930001, InstalledSteamGame.Name, 30, 1_700_000_000),
            (930002, Leuchtturm.Name, 0, 0),
        ]);
        return files;
    }

    /// <summary>Signed in on Steam's page, so the owned games that aren't installed show; back in the library.</summary>
    private static async Task<ArcadiaDriver> StartSignedInToSteamAsync(LauncherFiles files)
    {
        var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        arcadia.OpenLauncherPage(KnownLaunchers.Steam.Id);
        var row = Assert.IsType<Settings.ActionRowViewModel>(arcadia.FocusRow("launcher-account"));
        files.SteamWeb.IsApproved = true;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => row.ActionText == "Abmelden" && arcadia.PageGames.Any(t => t.Title == Leuchtturm.Name), "angemeldet", SignInLimit);
        files.SteamWeb.IsApproved = false;
        arcadia.CloseSettings();
        await arcadia.FollowInstallsAsync();
        return arcadia;
    }

    private static GameTileViewModel TileOf(ArcadiaDriver arcadia, SteamApp app) =>
        arcadia.PageGames.Single(tile => tile.Title == app.Name);
}
