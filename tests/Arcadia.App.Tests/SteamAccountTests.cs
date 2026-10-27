using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.Tests.Fixtures;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// The Steam account on Steam's launcher page (#83): a sign-in on Steam's own login page in Arcadia's browser, the owned
/// games that aren't installed in the Steam tab and the search, another account in Steam, offline with the cached list,
/// and signing out. Steam's web API and pages are <see cref="FakeSteamWeb"/>; the user signing in there is the test.
/// </summary>
public sealed class SteamAccountTests
{
    private const string AccountRow = "launcher-account";

    private static readonly SteamApp Installed = new("910001", "Salzwiesen");

    private static readonly TimeSpan SignInLimit = TimeSpan.FromSeconds(30);

    private const string OtherAccountUninstalledNotice = "In Steam ist derzeit ein anderes Konto angemeldet als in Arcadia. Installationen aus Arcadia heraus sind so lange nicht möglich.";
    private const string OtherAccountInstalledNotice = "In Steam ist derzeit ein anderes Konto angemeldet als in Arcadia. Das Spiel könnte daher entweder nicht starten oder falsche Spielstände verwenden.";

    [AvaloniaFact]
    [Trait("TestCase", "T83-1")]
    public async Task Sign_in_by_controller_opens_Steams_login_page_and_once_signed_in_there_shows_the_name_and_the_uninstalled_games_which_A_installs()
    {
        using var files = SteamWithOwnedGames();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        Assert.Equal([Installed.Name], TitlesOf(arcadia));

        var row = OpenAccountRow(arcadia);
        Assert.Equal("KONTO", row.Group);
        Assert.Equal("Steam-Konto", row.Title);
        Assert.Equal("Anmelden", row.ActionText);
        Assert.Equal("Melde dich an, um vollen Zugriff auf deine Spielebibliothek zu haben", row.Subtitle);
        Assert.Equal(2, row.Info.OfType<InfoText>().Count());
        Assert.StartsWith("Die Anmeldung erfolgt auf der Anmeldeseite von Steam", row.Info.OfType<InfoText>().Last().Text, StringComparison.Ordinal);
        Assert.Empty(row.Info.OfType<InfoFootnote>());

        // Steam's own page over the whole window, its address shown like in a browser.
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => arcadia.Main.IsWebSignInOpen && files.SteamWeb.IsSignInPageShown, "Steams Anmeldeseite", SignInLimit);
        Assert.Equal("Bei Steam anmelden", arcadia.Main.WebPage!.Title);
        Assert.Equal("https://store.steampowered.com", arcadia.Main.WebPage.Address);
        Assert.Equal("Anmeldung auf der Seite von Steam …", row.Subtitle);

        files.SteamWeb.IsApproved = true;
        await arcadia.WaitOnClockUntilAsync(() => TitlesOf(arcadia).Count == 3, "die nicht installierten Spiele im Steam-Tab", SignInLimit);

        Assert.False(arcadia.Main.IsWebSignInOpen);
        Assert.Equal(FakeSteamWeb.ProfileName, row.Subtitle);
        Assert.Equal("Abmelden", row.ActionText);
        Assert.Equal($"Angemeldet als {FakeSteamWeb.ProfileName}. Arcadia zeigt neben deinen installierten Spielen auch die Spiele, die im Besitz von {FakeSteamWeb.ProfileName} sind.", row.Info.OfType<InfoText>().First().Text);
        Assert.Equal("Meldest du dich ab, werden nur noch die installierten Spiele gelistet.", Assert.Single(row.Info.OfType<InfoFootnote>()).Text);
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
    public async Task B_closes_the_login_page_and_stays_on_the_launcher_page()
    {
        using var files = SteamWithOwnedGames();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        var row = OpenAccountRow(arcadia);
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => arcadia.Main.IsWebSignInOpen && files.SteamWeb.IsSignInPageShown, "Steams Anmeldeseite", SignInLimit);

        // The page takes every press: A or the D-pad don't reach the row under it ("Abbrechen" there)…
        arcadia.Press(UiAction.Accept);
        arcadia.Press(UiAction.Down);
        Dispatcher();
        Assert.True(arcadia.Main.IsWebSignInOpen);
        Assert.Equal("Abbrechen", row.ActionText);
        Assert.True(row.IsFocused);

        // …B closes it and nothing else, so the settings stay open.
        arcadia.Press(UiAction.Back);
        await arcadia.WaitOnClockUntilAsync(() => !files.SteamWeb.IsSignInPageShown && row.ActionText == "Anmelden", "die Seite ist zu", SignInLimit);

        Assert.False(arcadia.Main.IsWebSignInOpen);
        Assert.Equal(KnownLaunchers.Steam.Id, ((LauncherSettingsViewModel)arcadia.Main.Settings!.Selected).PageLauncherId);

        // Signed in on Steam's page too late: Arcadia stays signed out.
        files.SteamWeb.IsApproved = true;
        arcadia.Wait(TimeSpan.FromSeconds(15));
        await arcadia.SteamAccount!.WhenIdleAsync();
        Dispatcher();
        Assert.Equal("Anmelden", row.ActionText);
        Assert.Equal([Installed.Name], TitlesOf(arcadia));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-2")]
    public async Task A_login_page_that_does_not_load_says_Steam_is_unreachable_and_A_opens_it_again()
    {
        using var files = SteamWithOwnedGames();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        var row = OpenAccountRow(arcadia);
        files.SteamWeb.IsOffline = true;

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => row.Subtitle == "Steam nicht erreichbar – [A] erneut versuchen", "„nicht erreichbar“", SignInLimit);
        Assert.False(arcadia.Main.IsWebSignInOpen);
        Assert.Equal("Erneut versuchen", row.ActionText);
        Assert.Contains(row.Info.OfType<InfoText>(), text => text.Text.StartsWith("Arcadia konnte die Anmeldeseite von Steam nicht laden.", StringComparison.Ordinal));

        files.SteamWeb.IsOffline = false;
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitOnClockUntilAsync(() => arcadia.Main.IsWebSignInOpen && files.SteamWeb.IsSignInPageShown, "Steams Anmeldeseite", SignInLimit);
        Assert.Equal(2, files.SteamWeb.SignInPagesShown);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-3")]
    public async Task Soundtracks_and_tools_only_the_store_lists_join_as_not_installed_with_their_switches_on()
    {
        using var files = SteamWithOwnedGames();
        var soundtrack = new SteamApp("930001", "Leuchtturm Klänge");
        var tool = new SteamApp("930002", "Leuchtturm Dedicated Server");
        var dlc = new SteamApp("930003", "Leuchtturm Bonus");
        files.SteamWeb.StoreApps.AddRange([930001u, 930002u, 930003u]);
        files.Steam.WriteAppInfo((Installed, "Game"), (soundtrack, "Music"), (tool, "Tool"), (dlc, "DLC"));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, new AppSettings
        {
            ActiveLaunchers = [KnownLaunchers.Steam.Id],
            Launchers = new Dictionary<string, LauncherPreferences> { [KnownLaunchers.Steam.Id] = new(LibraryOptions: ["soundtracks", "tools"]) },
        });

        await SignInAsync(arcadia, files);
        await arcadia.WaitOnClockUntilAsync(() => TitlesOf(arcadia).Count == 5, "Soundtrack und Werkzeug im Steam-Tab", SignInLimit);

        Assert.Equal(["Leuchtturm", "Leuchtturm Dedicated Server", "Leuchtturm Klänge", "Salzwiesen", "Tidenhub"], TitlesOf(arcadia));
        var games = TabOf(arcadia).Games;
        Assert.False(games.Single(game => game.Title == soundtrack.Name).IsInstalled);
        Assert.Equal(GameType.Soundtrack, games.Single(game => game.Title == soundtrack.Name).Type);
        Assert.Equal(GameType.Tool, games.Single(game => game.Title == tool.Name).Type);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-3")]
    public async Task Free_tools_and_soundtracks_only_a_package_holds_join_like_in_Steams_library()
    {
        using var files = SteamWithOwnedGames();
        var freeTool = new SteamApp("930011", "Leuchtturm SDK");
        var freeApplication = new SteamApp("930012", "Leuchtturm Filmstudio");
        var soundtrack = new SteamApp("930013", "Leuchtturm OST");
        var notMine = new SteamApp("930014", "Fremdes Benchmark-Werkzeug");
        files.SteamWeb.StorePackages.Add(7001);
        files.Steam.WritePackageInfo((0, [930011u, 930012u]), (7001, [930013u]), (7002, [930014u]));
        files.Steam.WriteAppInfo((Installed, "Game"), (freeTool, "Tool"), (freeApplication, "Application"), (soundtrack, "Music"), (notMine, "Tool"));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, new AppSettings
        {
            ActiveLaunchers = [KnownLaunchers.Steam.Id],
            Launchers = new Dictionary<string, LauncherPreferences> { [KnownLaunchers.Steam.Id] = new(LibraryOptions: ["soundtracks", "tools", "software"]) },
        });

        await SignInAsync(arcadia, files);
        await arcadia.WaitOnClockUntilAsync(() => TitlesOf(arcadia).Count == 5, "Werkzeug und Soundtrack aus Paketen im Steam-Tab", SignInLimit);

        // Package 0's application isn't in Steam's library either; package 7002 isn't the account's.
        Assert.Equal(["Leuchtturm", "Leuchtturm OST", "Leuchtturm SDK", "Salzwiesen", "Tidenhub"], TitlesOf(arcadia));
        var games = TabOf(arcadia).Games;
        Assert.Equal(GameType.Tool, games.Single(game => game.Title == freeTool.Name).Type);
        Assert.Equal(GameType.Soundtrack, games.Single(game => game.Title == soundtrack.Name).Type);
        Assert.All(games.Where(game => game.Title.StartsWith("Leuchtturm ", StringComparison.Ordinal)), game => Assert.False(game.IsInstalled));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-5")]
    public async Task Another_account_in_the_running_Steam_keeps_the_games_blocks_installing_and_says_so_on_preview_detail_and_page()
    {
        using var files = SteamWithOwnedGames();
        files.Steam.ActiveUser = SteamFolder.Account + 7;
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);

        var row = await SignInAsync(arcadia, files);
        await arcadia.WaitOnClockUntilAsync(() => row.Severity == HealthSeverity.Info && TitlesOf(arcadia).Count == 3, "die Info zum anderen Konto", SignInLimit);

        Assert.Equal($"{FakeSteamWeb.ProfileName} · Steam ist mit einem anderen Konto angemeldet", row.Subtitle);
        var note = Assert.Single(row.Info.OfType<InfoNote>());
        Assert.True(note.IsInfo);
        Assert.Equal("Steam ist momentan mit einem anderen Konto angemeldet als in Arcadia. Arcadia wird so lange keine Installationen starten können. Installierte Spiele könnten ggf. nicht starten oder falsche Spielstände verwenden.", note.Text);

        arcadia.CloseSettings();
        arcadia.FocusTile("Leuchtturm");
        var tile = arcadia.Main.Library.FocusedGame!;
        Assert.Equal(OtherAccountUninstalledNotice, tile.Notice);
        Assert.False(tile.CanActivate);
        Assert.True(tile.IsInstallBlocked);
        Assert.Equal(OtherAccountInstalledNotice, TabOf(arcadia).Games.Single(game => game.Title == Installed.Name).Notice);

        // A on the cover says why instead of opening Steam's install dialog.
        arcadia.Press(UiAction.Accept);
        Assert.Empty(arcadia.Opened);
        Assert.Equal("„Leuchtturm“ lässt sich gerade nicht installieren: In Steam ist ein anderes Konto angemeldet.", arcadia.Main.Toast.Text);

        arcadia.Hold(UiAction.Accept, TimeSpan.FromSeconds(1.2));
        var page = arcadia.Detail!.Current;
        var install = page.TopRow[0];
        Assert.Equal("Installieren", install.ButtonLabel);
        Assert.False(install.IsEnabled);
        Assert.Equal("In Steam ist ein anderes Konto angemeldet", install.DisabledReason);
        Assert.Equal(OtherAccountUninstalledNotice, page.Tile.Notice);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-5")]
    public async Task Switching_the_account_in_Steam_shows_in_the_library_within_seconds_without_a_press()
    {
        using var files = SteamWithOwnedGames();
        files.Steam.ActiveUser = SteamFolder.Account;
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await SignInAsync(arcadia, files);
        arcadia.CloseSettings();
        await arcadia.WaitOnClockUntilAsync(() => TitlesOf(arcadia).Count == 3, "die nicht installierten Spiele", SignInLimit);
        Assert.All(TabOf(arcadia).Games, game => Assert.Null(game.Notice));

        files.Steam.ActiveUser = SteamFolder.Account + 7;
        await arcadia.WaitOnClockUntilAsync(() => TabOf(arcadia).Games.All(game => game.Notice is OtherAccountInstalledNotice or OtherAccountUninstalledNotice), "der Hinweis nach dem Kontowechsel", TimeSpan.FromSeconds(5));

        // Back to Arcadia's account, and Steam closed: no hint either way.
        files.Steam.ActiveUser = null;
        await arcadia.WaitOnClockUntilAsync(() => TabOf(arcadia).Games.All(game => game.Notice is null), "kein Hinweis bei geschlossenem Steam", TimeSpan.FromSeconds(5));
        Assert.Equal(3, TitlesOf(arcadia).Count);
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
        Assert.StartsWith($"Willst du {FakeSteamWeb.ProfileName} wirklich in Arcadia abmelden?", question.Message, StringComparison.Ordinal);
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

        // Steam's cookies in Arcadia's browser go too, unseen: the next sign-in asks Steam's page again.
        await arcadia.WaitOnClockUntilAsync(() => files.SteamWeb.CookiesForgotten == 1, "Steams Cookies gelöscht", SignInLimit);
        Assert.False(arcadia.Main.IsWebSignInOpen);
        arcadia.CloseSettings();
        arcadia.Press(UiAction.Search);
        arcadia.Main.TypeInSearch("tiden");
        Assert.Empty(arcadia.Search!.Hits);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T83-1")]
    public async Task The_account_group_comes_first_and_is_disabled_while_Steam_is_inactive()
    {
        using var files = SteamWithOwnedGames();
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        await SignInAsync(arcadia, files);
        var settings = (LauncherSettingsViewModel)arcadia.Main.Settings!.Selected;
        Assert.Equal(AccountRow, settings.Rows[0].Key);

        arcadia.FocusRow("launcher-active");
        arcadia.Press(UiAction.Left);

        var row = Assert.IsType<ActionRowViewModel>(settings.Rows[0]);
        Assert.Equal(AccountRow, row.Key);
        Assert.False(row.IsEnabled);
        Assert.Equal($"{FakeSteamWeb.ProfileName} · nur bei aktivem Steam", row.Subtitle);

        // Its info says why; A on it does nothing, and the sign-in stays stored.
        arcadia.FocusRow(AccountRow);
        Assert.Contains(row.Info.OfType<InfoNote>(), note => note.Text.StartsWith("Anmelden und Abmelden geht erst, wenn Steam aktiv ist.", StringComparison.Ordinal));
        arcadia.Press(UiAction.Accept);
        Assert.Null(settings.Dialog);
        Assert.Equal(AccountStep.SignedIn, arcadia.SteamAccount!.State.Step);
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

    /// <summary>A on "Anmelden", signed in on Steam's page; the account's games are fetched.</summary>
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
