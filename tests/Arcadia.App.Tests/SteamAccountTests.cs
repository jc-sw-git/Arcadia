using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.Tests.Fixtures;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Tests;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The Steam account on Steam's launcher page (#83): a QR sign-in approved on the phone, the owned games that aren't
/// installed in the Steam tab and the search, another account in Steam, offline with the cached list, and signing out.
/// Steam's web API is <see cref="FakeSteamWeb"/>; the phone is the test approving or rejecting the code.
/// </summary>
public sealed class SteamAccountTests
{
    private const string AccountRow = "launcher-account";

    private static readonly SteamApp Installed = new("910001", "Salzwiesen");

    private static readonly TimeSpan SignInLimit = TimeSpan.FromSeconds(30);

    [AvaloniaFact]
    [Trait("TestCase", "T83-1")]
    public async Task Sign_in_by_controller_shows_a_code_and_once_approved_the_name_and_the_uninstalled_games_which_A_installs()
    {
        using var files = SteamWithOwnedGames();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        Assert.Equal([Installed.Name], TitlesOf(arcadia));

        var row = OpenAccountRow(arcadia);
        Assert.Equal("KONTO", row.Group);
        Assert.Equal("Steam-Konto", row.Title);
        Assert.Equal("Anmelden", row.ActionText);
        Assert.Equal("Melde dich an, um auch nicht installierte Spiele zu sehen", row.Subtitle);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => row.Info.OfType<InfoQrCode>().Any(), "der QR-Code im Infobereich", SignInLimit);
        Assert.Contains(row.Info.OfType<InfoText>(), text => text.Text.Contains("QR-Code scannen", StringComparison.Ordinal));

        files.SteamWeb.IsApproved = true;
        await arcadia.WaitOnClockUntilAsync(() => TitlesOf(arcadia).Count == 3, "die nicht installierten Spiele im Steam-Tab", SignInLimit);

        Assert.Equal(FakeSteamWeb.ProfileName, row.Subtitle);
        Assert.Equal("Abmelden", row.ActionText);
        Assert.Empty(row.Info.OfType<InfoQrCode>());
        Assert.Equal(["Leuchtturm", "Salzwiesen", "Tidenhub"], TitlesOf(arcadia));
        Assert.False(TabOf(arcadia).Games.Single(game => game.Title == "Leuchtturm").IsInstalled);

        arcadia.CloseSettings();
        arcadia.FocusTile("Leuchtturm");
        arcadia.Press(UiAction.Accept);
        Assert.Equal(new LaunchTarget.ProtocolUri("steam://install/920001"), Assert.Single(arcadia.Opened));

        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("tiden");
        Assert.Equal(["Tidenhub"], arcadia.Search!.Hits.Select(hit => hit.Title));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-1")]
    public async Task B_drops_the_code_and_stays_on_the_page()
    {
        using var files = SteamWithOwnedGames();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        var row = OpenAccountRow(arcadia);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => row.Info.OfType<InfoQrCode>().Any(), "der QR-Code", SignInLimit);

        arcadia.Press(UiAction.Back);
        Dispatcher();

        Assert.Equal(KnownLaunchers.Steam.Id, ((LauncherSettingsViewModel)arcadia.Main.Settings!.Selected).PageLauncherId);
        Assert.Equal("Anmelden", row.ActionText);
        Assert.Empty(row.Info.OfType<InfoQrCode>());

        // Approved on the phone too late: Arcadia stays signed out.
        files.SteamWeb.IsApproved = true;
        arcadia.Wait(TimeSpan.FromSeconds(15));
        await arcadia.SteamAccount!.WhenIdleAsync();
        Dispatcher();
        Assert.Equal("Anmelden", row.ActionText);
        Assert.Equal([Installed.Name], TitlesOf(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-2")]
    public async Task An_expired_or_rejected_code_says_so_and_A_gets_a_new_one()
    {
        using var files = SteamWithOwnedGames();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        var row = OpenAccountRow(arcadia);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => row.Info.OfType<InfoQrCode>().Any(), "der QR-Code", SignInLimit);

        // Nobody approves it: after 2 minutes it is gone.
        await arcadia.WaitOnClockUntilAsync(() => row.Subtitle == "Abgelaufen – [A] für neuen Code", "„Abgelaufen“", TimeSpan.FromMinutes(3));
        Assert.Empty(row.Info.OfType<InfoQrCode>());
        Assert.Equal("Neuer Code", row.ActionText);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => row.Info.OfType<InfoQrCode>().Any(), "der neue QR-Code", SignInLimit);

        files.SteamWeb.IsRejected = true;
        await arcadia.WaitOnClockUntilAsync(() => row.Subtitle == "Abgelehnt – [A] für neuen Code", "„Abgelehnt“", SignInLimit);
        Assert.Empty(row.Info.OfType<InfoQrCode>());

        files.SteamWeb.IsRejected = false;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => row.Info.OfType<InfoQrCode>().Any(), "noch ein QR-Code", SignInLimit);
        Assert.Equal(3, files.SteamWeb.CodesIssued);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-5")]
    public async Task Another_account_in_Steam_shows_no_uninstalled_games_and_an_info_on_the_launcher_page()
    {
        using var files = SteamWithOwnedGames();
        files.Steam.AddAccount(SteamFolder.Account + 7, "Jemand anderes", timestamp: 1_800_000_000, mostRecent: true);
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);

        var row = await SignInAsync(arcadia, files);
        await arcadia.WaitOnClockUntilAsync(() => row.Severity == HealthSeverity.Info, "die Info zum anderen Konto", SignInLimit);

        Assert.Equal([Installed.Name], TitlesOf(arcadia));
        Assert.Equal($"{FakeSteamWeb.ProfileName} · Steam ist mit einem anderen Konto angemeldet", row.Subtitle);
        var note = Assert.Single(row.Info.OfType<InfoNote>());
        Assert.True(note.IsInfo);
        Assert.StartsWith("Steam ist mit einem anderen Konto angemeldet.", note.Text, StringComparison.Ordinal);
        Assert.Null(arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-6")]
    public async Task Offline_the_cached_games_show_at_once_and_the_page_has_a_status_message_not_a_toast()
    {
        using var files = SteamWithOwnedGames();
        files.Steam.AddAccount(SteamFolder.Account, "Spieler Eins", timestamp: 1_800_000_000, mostRecent: true);
        using (var first = await ArcadiaDriver.StartWithLaunchersAsync(files))
        {
            await SignInAsync(first, files);
        }

        files.SteamWeb.IsOffline = true;
        files.SteamWeb.OwnedGames.Clear();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);

        // The cached list counts from the first scan, before any fetch could answer.
        Assert.Equal(["Leuchtturm", "Salzwiesen", "Tidenhub"], TitlesOf(arcadia));
        var row = OpenAccountRow(arcadia);
        await arcadia.WaitOnClockUntilAsync(() => row.Severity == HealthSeverity.Warning, "die Statusmeldung", SignInLimit);

        Assert.Equal($"{FakeSteamWeb.ProfileName} · Steam ist nicht erreichbar", row.Subtitle);
        Assert.Contains(row.Info.OfType<InfoNote>(), note => note.Text == "Steam ist nicht erreichbar. Arcadia zeigt die zuletzt geladene Liste deiner Spiele.");
        Assert.Null(arcadia.Main.Toast.Text);
        Assert.Equal(["Leuchtturm", "Salzwiesen", "Tidenhub"], TitlesOf(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-6")]
    public async Task Back_online_Neu_einlesen_clears_the_status_message()
    {
        using var files = SteamWithOwnedGames();
        using (var first = await ArcadiaDriver.StartWithLaunchersAsync(files))
        {
            await SignInAsync(first, files);
        }

        files.SteamWeb.IsOffline = true;
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        var row = OpenAccountRow(arcadia);
        await arcadia.WaitOnClockUntilAsync(() => row.Severity == HealthSeverity.Warning, "die Statusmeldung", SignInLimit);

        // The cable is back in; "Neu einlesen" right away.
        files.SteamWeb.IsOffline = false;
        arcadia.FocusRow("launcher-rescan");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => row.Severity is null, "die Statusmeldung verschwindet nach „Neu einlesen“", TimeSpan.FromSeconds(10));

        Assert.Equal(FakeSteamWeb.ProfileName, row.Subtitle);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-7")]
    public async Task Sign_out_asks_first_then_removes_the_uninstalled_games_and_the_cache_at_once()
    {
        using var files = SteamWithOwnedGames();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        var row = await SignInAsync(arcadia, files);
        var settings = (LauncherSettingsViewModel)arcadia.Main.Settings!.Selected;
        await arcadia.WaitOnClockUntilAsync(() => TitlesOf(arcadia).Count == 3, "die nicht installierten Spiele im Steam-Tab", SignInLimit);
        Assert.True(File.Exists(Path.Combine(files.AccountFolder, "steam-owned-games.json")));

        // B keeps the sign-in.
        arcadia.Press(UiAction.Accept);
        var question = Assert.IsType<ConfirmationViewModel>(settings.Dialog);
        Assert.Equal("Vom Steam-Konto abmelden?", question.Title);
        Assert.True(question.IsCancelFocused);
        arcadia.Press(UiAction.Back);
        Assert.Null(settings.Dialog);
        Assert.Equal("Abmelden", row.ActionText);

        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Left);
        arcadia.Press(UiAction.Accept);
        Assert.Null(settings.Dialog);
        await arcadia.WaitOnClockUntilAsync(() => TitlesOf(arcadia).Count == 1, "die nicht installierten Spiele verschwinden", SignInLimit);

        Assert.Equal([Installed.Name], TitlesOf(arcadia));
        Assert.Equal("Anmelden", row.ActionText);
        Assert.Empty(Directory.GetFiles(files.AccountFolder));
        arcadia.CloseSettings();
        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("tiden");
        Assert.Empty(arcadia.Search!.Hits);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-1")]
    public async Task The_account_group_shows_only_while_Steam_is_active()
    {
        using var files = SteamWithOwnedGames();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        var settings = arcadia.OpenLauncherPage(KnownLaunchers.Steam.Id);
        Assert.Contains(settings.Rows, r => r.Key == AccountRow);

        arcadia.FocusRow("launcher-active");
        arcadia.Press(UiAction.Left);

        Assert.DoesNotContain(settings.Rows, r => r.Key == AccountRow);
    }

    /// <summary>Steam with one installed game; the account owns it and two more, Leuchtturm (920001) and Tidenhub (920002).</summary>
    private static LauncherFiles SteamWithOwnedGames()
    {
        var files = new LauncherFiles();
        files.Steam.Install().Write(Installed);
        files.SteamWeb.OwnedGames.AddRange(
        [
            (910001, Installed.Name, 30, 1_700_000_000),
            (920001, "Leuchtturm", 125, 1_600_000_000),
            (920002, "Tidenhub", 0, 0),
        ]);
        return files;
    }

    /// <summary>Steam's page, the focus on "Steam-Konto".</summary>
    private static ActionRowViewModel OpenAccountRow(ArcadiaDriver arcadia)
    {
        arcadia.OpenLauncherPage(KnownLaunchers.Steam.Id);
        return Assert.IsType<ActionRowViewModel>(arcadia.FocusRow(AccountRow));
    }

    /// <summary>A on "Anmelden", the code approved on the phone; the account's games are fetched.</summary>
    private static async Task<ActionRowViewModel> SignInAsync(ArcadiaDriver arcadia, LauncherFiles files)
    {
        var row = OpenAccountRow(arcadia);
        files.SteamWeb.IsApproved = true;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => row.ActionText == "Abmelden" && arcadia.SteamAccount!.State.OwnedGames is not null, "angemeldet", SignInLimit);
        files.SteamWeb.IsApproved = false;
        return row;
    }

    private static void Dispatcher() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private static LauncherTabViewModel TabOf(ArcadiaDriver arcadia) =>
        arcadia.Main.LauncherTabs.Items.Single(tab => tab.Launcher.Id == KnownLaunchers.Steam.Id);

    private static IReadOnlyList<string> TitlesOf(ArcadiaDriver arcadia) =>
        [.. TabOf(arcadia).Games.Select(entry => entry.Title).Order(StringComparer.Ordinal)];
}
