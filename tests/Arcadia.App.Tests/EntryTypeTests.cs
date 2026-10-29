using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.App.Tests.Fixtures;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>
/// Software, tools and soundtracks in the library (#141): the launchers' switches on their pages, what A says, the count
/// line beside the tab's name and on the launcher's page.
/// </summary>
public sealed class EntryTypeTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly DateTimeOffset Installed = ArcadiaDriver.Now.AddDays(-30);

    private static readonly string[] EpicGame = ["games", "applications"];

    [AvaloniaFact]
    [Trait("TestCase", "T141-1")]
    public async Task Epic_software_and_engines_on_shows_one_Unreal_Engine_and_the_Fortnite_editor_as_tools_and_off_hides_them_at_once()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        var game = new EpicApp("ea01", "Leuchtturm");
        var older = new EpicApp("UE_5.3", "Unreal Engine 5.3") { Namespace = "ue", Categories = ["public", "engines"], Version = "5.3.2-29314046+++UE5+Release-5.3" };
        var newer = new EpicApp("UE_5.4", "Unreal Engine 5.4") { Namespace = "ue", Categories = ["public", "engines"], Version = "5.4.4-35576357+++UE5+Release-5.4" };
        var fortnite = new EpicApp("Fortnite", "Fortnite");
        var editor = new EpicApp("ea02", "Unreal Editor for Fortnite") { Namespace = fortnite.Namespace, Categories = ["addons", "addons/launchable"], MainGameAppName = fortnite.AppName };
        foreach (var app in new[] { game, older, newer, editor })
        {
            epic.WriteInstalled(app);
        }

        epic.WriteCatalog(
            (game, EpicGame, null),
            (older, ["engines", "applications"], null),
            (newer, ["engines", "applications"], null),
            (editor, ["addons", "addons/launchable"], fortnite));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        var scannedAt = arcadia.Main.Session.ScannedAt(KnownLaunchers.Epic.Id);

        // Off at first: the tab shows the games only.
        Assert.Equal(["Leuchtturm"], TitlesOf(arcadia, KnownLaunchers.Epic.Id));

        arcadia.OpenLauncherPage(KnownLaunchers.Epic.Id);
        var row = Assert.IsType<SwitchRowViewModel>(arcadia.FocusRow("library-option:epic:software"));
        Assert.Equal(("Software und Engines einbeziehen", "Aus"), (row.Title, row.ValueText));
        arcadia.Press(UiAction.Accept);

        Assert.Equal("An", row.ValueText);
        var tools = TabOf(arcadia, KnownLaunchers.Epic.Id).Games.Where(entry => entry.Type == GameType.Tool).ToList();
        Assert.Equal(["Unreal Editor for Fortnite", "Unreal Engine"], tools.Select(entry => entry.Title).Order(StringComparer.Ordinal));
        Assert.Equal(["Leuchtturm", "Unreal Editor for Fortnite", "Unreal Engine"], TitlesOf(arcadia, KnownLaunchers.Epic.Id));

        // One entry for both versions; it starts the newest.
        var engine = tools.Single(entry => entry.Title == "Unreal Engine");
        Assert.Contains("UE_5.4", Assert.IsType<LaunchTarget.ProtocolUri>(engine.Launch).Uri, StringComparison.Ordinal);

        // Back in the library: both with the wrench.
        arcadia.CloseSettings();
        foreach (var title in new[] { "Unreal Engine", "Unreal Editor for Fortnite" })
        {
            var tile = arcadia.FocusTile(title);
            Assert.True(tile.HasTypeIcon, $"{title} zeigt sein Symbol");
            Assert.Equal(("IconTypeTool", "Werkzeug"), (tile.TypeIconKey, tile.TypeName));
        }

        // Off again: gone at once, from what was read, without reading Epic again.
        arcadia.OpenLauncherPage(KnownLaunchers.Epic.Id);
        arcadia.FocusRow("library-option:epic:software");
        arcadia.Press(UiAction.Accept);

        Assert.Equal(["Leuchtturm"], TitlesOf(arcadia, KnownLaunchers.Epic.Id));
        Assert.Equal(scannedAt, arcadia.Main.Session.ScannedAt(KnownLaunchers.Epic.Id));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-3")]
    public async Task Each_Steam_switch_shows_and_hides_only_its_type_demos_are_games_and_runtimes_never_appear()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        var game = new SteamApp("910001", "Salzwiesen");
        var demo = new SteamApp("910002", "Leuchtturm Demo");
        var tool = new SteamApp("910003", "Kartenbauer SDK");
        var software = new SteamApp("910004", "Tapetenmaler");
        var soundtrack = new SteamApp("910005", "Salzwiesen Klänge");
        var proton = new SteamApp("910006", "Proton 9.0");
        var runtime = new SteamApp("910007", "Steam Linux Runtime 3.0 (sniper)");
        var redistributables = new SteamApp("910008", "Steamworks Common Redistributables");
        var video = new SteamApp("910009", "Salzwiesen Making-of");
        (SteamApp App, string Type)[] apps =
        [
            (game, "Game"), (demo, "Demo"), (tool, "Tool"), (software, "Application"), (soundtrack, "Music"),
            (proton, "Tool"), (runtime, "Tool"), (redistributables, "Tool"), (video, "Video"),
        ];
        foreach (var (app, _) in apps)
        {
            steam.Write(app);
        }

        steam.WriteAppInfo(apps);
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files);
        string[] games = ["Leuchtturm Demo", "Salzwiesen"];
        Assert.Equal(games, TitlesOf(arcadia, KnownLaunchers.Steam.Id));

        arcadia.OpenLauncherPage(KnownLaunchers.Steam.Id);
        var switches = new[] { ("tools", tool.Name), ("software", software.Name), ("soundtracks", soundtrack.Name) };
        foreach (var (option, title) in switches)
        {
            Switch(arcadia, KnownLaunchers.Steam.Id, option);
            Assert.Equal(games.Append(title).Order(StringComparer.Ordinal), TitlesOf(arcadia, KnownLaunchers.Steam.Id));
            Switch(arcadia, KnownLaunchers.Steam.Id, option);
            Assert.Equal(games, TitlesOf(arcadia, KnownLaunchers.Steam.Id));
        }

        // All three on: each entry has its type; runtimes, redistributables and videos still never appear.
        foreach (var (option, _) in switches)
        {
            Switch(arcadia, KnownLaunchers.Steam.Id, option);
        }

        var entries = TabOf(arcadia, KnownLaunchers.Steam.Id).Games.ToDictionary(entry => entry.Title, entry => entry.Type);
        Assert.Equal(
            new Dictionary<string, GameType>
            {
                [game.Name] = GameType.Game,
                [demo.Name] = GameType.Game,
                [tool.Name] = GameType.Tool,
                [software.Name] = GameType.Software,
                [soundtrack.Name] = GameType.Soundtrack,
            },
            entries);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-5")]
    public async Task A_says_Spielen_Tueftln_or_Abrocken_by_type_on_preview_detail_page_and_menu_and_Installieren_when_not_installed()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(
            KnownLaunchers.Gog,
            Installed(KnownLaunchers.Gog, "Hollow Knight", Installed),
            Installed(KnownLaunchers.Gog, "Pixel Forge", Installed) with { Type = GameType.Software },
            Installed(KnownLaunchers.Gog, "Mapping Kit", Installed) with { Type = GameType.Tool },
            Installed(KnownLaunchers.Gog, "Neon Drift Soundtrack", Installed) with { Type = GameType.Soundtrack },
            NotInstalled(KnownLaunchers.Gog, "Dedicated Server") with { Type = GameType.Tool },
            NotInstalled(KnownLaunchers.Gog, "Hollow Crown Soundtrack") with { Type = GameType.Soundtrack })]);

        foreach (var (title, action) in new[]
        {
            ("Hollow Knight", "Spielen"),
            ("Pixel Forge", "Tüfteln"),
            ("Mapping Kit", "Tüfteln"),
            ("Neon Drift Soundtrack", "Abrocken"),
            ("Dedicated Server", "Installieren"),
            ("Hollow Crown Soundtrack", "Installieren"),
        })
        {
            // Preview button and the footer's hint both read the tile's text.
            var tile = arcadia.FocusTile(title);
            Assert.Equal(action, tile.PrimaryActionText);

            arcadia.Press(UiAction.Options);
            var menu = arcadia.Main.GameMenu!;
            Assert.Equal(action, menu.Actions.Single(entry => entry.Kind == GameActionKind.Play).Label);
            arcadia.Press(UiAction.Back);

            arcadia.Hold(UiAction.Accept, PastHoldTime);
            var page = arcadia.Detail!.Current;
            Assert.Equal(title, page.Tile.Title);
            Assert.Equal(action, page.TopRow[0].ButtonLabel);
            arcadia.Press(UiAction.Back);
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-5")]
    public async Task While_software_or_a_tool_runs_A_says_Weiter_tueftln_and_on_a_game_still_Zurueck_ins_Spiel()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(
            KnownLaunchers.Gog,
            Installed(KnownLaunchers.Gog, "Hollow Knight", Installed),
            Installed(KnownLaunchers.Gog, "Pixel Forge", Installed) with { Type = GameType.Software },
            Installed(KnownLaunchers.Gog, "Mapping Kit", Installed) with { Type = GameType.Tool }) with
        {
            Running = ["Hollow Knight", "Pixel Forge", "Mapping Kit"],
        }]);

        foreach (var (title, action) in new[] { ("Hollow Knight", "Zurück ins Spiel"), ("Pixel Forge", "Weiter tüfteln"), ("Mapping Kit", "Weiter tüfteln") })
        {
            var tile = arcadia.FocusTile(title);
            Assert.True(tile.IsRunning, $"{title} läuft");
            Assert.Equal(action, tile.PrimaryActionText);

            arcadia.Press(UiAction.Options);
            Assert.Equal(action, arcadia.Main.GameMenu!.Actions.Single(entry => entry.Kind == GameActionKind.Play).Label);
            arcadia.Press(UiAction.Back);

            arcadia.Hold(UiAction.Accept, PastHoldTime);
            Assert.Equal(action, arcadia.Detail!.Current.TopRow[0].ButtonLabel);
            arcadia.Press(UiAction.Back);
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-13")]
    public async Task Installieren_on_a_not_installed_Unreal_Engine_opens_the_Unreal_Engine_library_in_Epic_and_asks_to_start_it_there()
    {
        using var files = new LauncherFiles();
        var epic = files.Epic.Install();
        var engine = new EpicApp("UE_5.8", "Unreal Engine 5.8") { Namespace = "ue", Categories = ["engines"] };
        epic.WriteCatalog((engine, ["engines", "engines/ue5"], null));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, new AppSettings
        {
            ActiveLaunchers = [KnownLaunchers.Epic.Id],
            Launchers = new Dictionary<string, LauncherPreferences> { [KnownLaunchers.Epic.Id] = new(LibraryOptions: ["software"]) },
        });

        var tile = arcadia.FocusTile("Unreal Engine");
        Assert.Equal("Installieren", tile.PrimaryActionText);
        arcadia.Press(UiAction.Accept);

        // Epic's install link for an engine opened no dialog on the owner's PC; its Unreal Engine library does.
        Assert.Equal([new LaunchTarget.ProtocolUri("com.epicgames.launcher://ue/library")], arcadia.Opened);
        // No dialog opens by itself there: Arcadia says so and waits for the start in Epic, not for a confirmation.
        Assert.Equal("Epic Games wird geöffnet – starte dort selbst die Installation von „Unreal Engine“.", arcadia.Main.Toast.Text);
        Assert.Equal(("Bitte in Epic Games starten", "Wartet"), (tile.InstallStatusText, tile.InstallShortText));
        arcadia.Press(UiAction.Accept);
        Assert.Equal("Die Installation von „Unreal Engine“ wartet darauf, dass du sie in Epic Games startest.", arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-5")]
    public async Task A_on_an_installed_Steam_soundtrack_plays_it_and_shows_its_page_in_Steam_with_a_toast_and_its_page_says_Steam_plays_it()
    {
        using var files = new LauncherFiles();
        var steam = files.Steam.Install();
        var soundtrack = new SteamApp("910101", "Salzwiesen Klänge");
        steam.Write(soundtrack);
        steam.WriteAppInfo((soundtrack, "Music"));
        using var arcadia = await ArcadiaDriver.StartWithLaunchersAsync(files, new AppSettings
        {
            ActiveLaunchers = [KnownLaunchers.Steam.Id],
            Launchers = new Dictionary<string, LauncherPreferences> { [KnownLaunchers.Steam.Id] = new(LibraryOptions: ["soundtracks"]) },
        });

        arcadia.FocusTile(soundtrack.Name);
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        var page = arcadia.Detail!.Current;
        Assert.Equal("Abrocken", page.TopRow[0].ButtonLabel);
        Assert.Equal("Steam spielt den Soundtrack ab.", page.SoundtrackNote);

        arcadia.Press(UiAction.Accept);

        Assert.Equal("„Salzwiesen Klänge“ wird in Steam geöffnet …", arcadia.Main.Toast.Text);
        // Steam plays it and shows its page, as "In Steam öffnen" does (owner, 2026-10-27); the page only after a gap,
        // because Steam lost the first of two links opened at once.
        var play = new LaunchTarget.ProtocolUri("steam://rungameid/910101");
        Assert.Equal([play], arcadia.Opened);
        await arcadia.WaitOnClockUntilAsync(() => arcadia.Opened.Count == 2, "Seite in Steam geöffnet", TimeSpan.FromSeconds(5));
        Assert.Equal([play, new LaunchTarget.ProtocolUri("steam://nav/games/details/910101")], arcadia.Opened);
        Assert.False(page.Tile.IsRunningOrStarting, "Kein „Startet“ oder „Läuft“");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-7")]
    public async Task The_count_line_has_one_part_per_type_in_order_with_installed_brackets_a_tooltip_and_no_page_number()
    {
        // 1,234 games (1,233 not installed), no software, two tools (both installed), one soundtrack not installed.
        var games = Enumerable.Range(1, 1234)
            .Select(i => i == 1 ? Installed(KnownLaunchers.Gog, "Spiel 0001", Installed) : NotInstalled(KnownLaunchers.Gog, $"Spiel {i:D4}"))
            .Concat(
            [
                Installed(KnownLaunchers.Gog, "Mapping Kit", Installed) with { Type = GameType.Tool },
                Installed(KnownLaunchers.Gog, "Server Kit", Installed) with { Type = GameType.Tool },
                NotInstalled(KnownLaunchers.Gog, "Neon Drift Soundtrack") with { Type = GameType.Soundtrack },
            ])
            .ToArray();
        using var arcadia = await ArcadiaDriver.StartAsync([LauncherSpec.With(KnownLaunchers.Gog, games), new(KnownLaunchers.Amazon, 0)]);

        var line = arcadia.Main.Library.Counts;
        Assert.Equal(
            [
                new TypeCountPart(GameType.Game, "1.234", "1", ShowsInstalled: true, HasDivider: false),
                new TypeCountPart(GameType.Tool, "2", "2", ShowsInstalled: false, HasDivider: true),
                new TypeCountPart(GameType.Soundtrack, "1", "0", ShowsInstalled: true, HasDivider: true),
            ],
            line.Parts);
        Assert.Equal("1.234 Spiele, davon 1 installiert · 2 Werkzeuge · 1 Soundtrack, davon 0 installiert", line.Tip);
        Assert.Equal(string.Empty, arcadia.Main.Library.CountText);
        Assert.DoesNotContain("Seite", line.Tip, StringComparison.Ordinal);

        // The tab's number counts every entry shown.
        Assert.Equal(1237, arcadia.SelectedTab!.Count);

        // An empty tab keeps its text and has no count line.
        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Amazon Games", arcadia.SelectedTab!.Name);
        Assert.False(arcadia.Main.Library.Counts.HasParts);
        Assert.NotEqual(string.Empty, arcadia.Main.Library.CountText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-8")]
    public async Task The_launcher_page_shows_the_tabs_count_line_on_Neu_einlesen_and_in_the_launcher_details()
    {
        LibraryTypeOption tools = new("tools", "Werkzeuge einbeziehen", "Testwerkzeuge", "Zeigt die Werkzeuge.", [], [GameType.Tool]);
        using var arcadia = await ArcadiaDriver.StartAsync([new LauncherSpec(
            KnownLaunchers.Gog,
            3,
            Library:
            [
                Installed(KnownLaunchers.Gog, "Hollow Knight", Installed),
                NotInstalled(KnownLaunchers.Gog, "Iron Harbor"),
                Installed(KnownLaunchers.Gog, "Mapping Kit", Installed) with { Type = GameType.Tool },
            ],
            Options: [tools])]);

        arcadia.OpenLauncherPage(KnownLaunchers.Gog.Id);
        Switch(arcadia, KnownLaunchers.Gog.Id, "tools");
        var rescan = arcadia.FocusRow("launcher-rescan");
        var tab = arcadia.Main.Library.Counts;

        Assert.Equal(["2 (1)", "1"], tab.Parts.Select(part => part.ShowsInstalled ? $"{part.TotalText} ({part.InstalledText})" : part.TotalText));
        Assert.True(rescan.HasCounts, "„Neu einlesen“ zeigt die Zeile");
        Assert.Equal(tab.Parts, rescan.Counts!.Parts);
        Assert.Equal(tab.Tip, rescan.Counts.Tip);
        Assert.StartsWith("· zuletzt ", rescan.Subtitle, StringComparison.Ordinal);

        // "Launcher-Details": the info panel of the launcher's row in the list.
        arcadia.Press(UiAction.Back);
        var fact = arcadia.FocusRow(KnownLaunchers.Gog.Id).Info.OfType<InfoFacts>().SelectMany(facts => facts.Facts).Single(fact => fact.Label == "Einträge");
        Assert.True(fact.HasCounts, "„Launcher-Details“ zeigen die Zeile");
        Assert.Equal(tab.Parts, fact.Counts!.Parts);
        Assert.Equal("2 Spiele, davon 1 installiert · 1 Werkzeug", fact.Value);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T141-9")]
    public async Task The_switches_survive_a_restart_and_all_off_leave_no_LibraryOptions_in_the_settings()
    {
        LibraryTypeOption tools = new("tools", "Werkzeuge einbeziehen", "Testwerkzeuge", "Zeigt die Werkzeuge.", [], [GameType.Tool]);
        LibraryTypeOption soundtracks = new("soundtracks", "Soundtracks einbeziehen", "Testsoundtracks", "Zeigt die Soundtracks.", [], [GameType.Soundtrack]);
        IReadOnlyList<LauncherSpec> launchers = [new LauncherSpec(
            KnownLaunchers.Gog,
            3,
            Library:
            [
                Installed(KnownLaunchers.Gog, "Hollow Knight", Installed),
                Installed(KnownLaunchers.Gog, "Mapping Kit", Installed) with { Type = GameType.Tool },
                Installed(KnownLaunchers.Gog, "Neon Drift Soundtrack", Installed) with { Type = GameType.Soundtrack },
            ],
            Options: [tools, soundtracks])];
        // A keyword of its own keeps the launcher's entry in settings.json when all switches are off.
        using var arcadia = await ArcadiaDriver.StartAsync(launchers, new AppSettings
        {
            ActiveLaunchers = [KnownLaunchers.Gog.Id],
            Launchers = new Dictionary<string, LauncherPreferences> { [KnownLaunchers.Gog.Id] = new(Keyword: "galaxy") },
        });
        arcadia.OpenLauncherPage(KnownLaunchers.Gog.Id);
        Switch(arcadia, KnownLaunchers.Gog.Id, "tools");
        Switch(arcadia, KnownLaunchers.Gog.Id, "soundtracks");
        Switch(arcadia, KnownLaunchers.Gog.Id, "soundtracks");

        using var restarted = await arcadia.RestartAsync();

        Assert.Equal(["Hollow Knight", "Mapping Kit"], TitlesOf(restarted, KnownLaunchers.Gog.Id));
        restarted.OpenLauncherPage(KnownLaunchers.Gog.Id);
        Assert.Equal("An", Assert.IsType<SwitchRowViewModel>(restarted.FocusRow("library-option:gog:tools")).ValueText);
        Assert.Equal("Aus", Assert.IsType<SwitchRowViewModel>(restarted.FocusRow("library-option:gog:soundtracks")).ValueText);
        Assert.Equal("[\"tools\"]", SavedOptions(restarted, KnownLaunchers.Gog.Id));

        // All off: the launcher's entry holds no LibraryOptions any more.
        Switch(restarted, KnownLaunchers.Gog.Id, "tools");
        Assert.Equal(["Hollow Knight"], TitlesOf(restarted, KnownLaunchers.Gog.Id));
        Assert.Null(SavedOptions(restarted, KnownLaunchers.Gog.Id));
    }

    /// <summary>A on the launcher page's switch <paramref name="option"/>, as the user would reach it with the D-pad.</summary>
    private static void Switch(ArcadiaDriver arcadia, string launcherId, string option)
    {
        var row = Assert.IsType<SwitchRowViewModel>(arcadia.FocusRow($"library-option:{launcherId}:{option}"));
        var wasOn = row.IsOn;
        arcadia.Press(UiAction.Accept);
        Assert.NotEqual(wasOn, row.IsOn);
    }

    /// <summary>The launcher's <c>LibraryOptions</c> in settings.json as JSON; null when it holds none (missing or null).</summary>
    private static string? SavedOptions(ArcadiaDriver arcadia, string launcherId)
    {
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(arcadia.SettingsPath));
        var launcher = json.RootElement.GetProperty("Launchers").GetProperty(launcherId);
        Assert.Equal("galaxy", launcher.GetProperty("Keyword").GetString());
        return launcher.TryGetProperty("LibraryOptions", out var options) && options.ValueKind != System.Text.Json.JsonValueKind.Null
            ? System.Text.Json.JsonSerializer.Serialize(options)
            : null;
    }

    private static LauncherTabViewModel TabOf(ArcadiaDriver arcadia, string launcherId) =>
        arcadia.Main.LauncherTabs.Items.Single(tab => tab.Launcher.Id == launcherId);

    private static IReadOnlyList<string> TitlesOf(ArcadiaDriver arcadia, string launcherId) =>
        [.. TabOf(arcadia, launcherId).Games.Select(entry => entry.Title).Order(StringComparer.Ordinal)];
}

internal static class EntryTypeDriving
{
    /// <summary>≡, then A on the launcher in the list: its page in the settings (#137).</summary>
    public static LauncherSettingsViewModel OpenLauncherPage(this ArcadiaDriver arcadia, string launcherId)
    {
        var settings = arcadia.Main.Settings?.Selected as LauncherSettingsViewModel ?? arcadia.OpenLauncherSettings();
        if (settings.PageLauncherId != launcherId)
        {
            arcadia.FocusRow(launcherId);
            arcadia.Press(UiAction.Accept);
        }

        Assert.Equal(launcherId, settings.PageLauncherId);
        return settings;
    }

    /// <summary>B until the settings are closed and the library shows.</summary>
    public static ArcadiaDriver CloseSettings(this ArcadiaDriver arcadia)
    {
        for (var steps = 0; arcadia.Main.Settings is not null; steps++)
        {
            Assert.True(steps < 5, "Die Einstellungen schließen sich nicht");
            arcadia.Press(UiAction.Back);
        }

        return arcadia;
    }

    /// <summary>The D-pad over the shown page (8 covers a row) until the cover of <paramref name="title"/> has the focus.</summary>
    public static GameTileViewModel FocusTile(this ArcadiaDriver arcadia, string title)
    {
        const int columns = 8;
        var target = arcadia.PageGames.ToList().FindIndex(tile => tile.Title == title);
        Assert.True(target >= 0, $"{title} steht nicht auf der Seite");
        var at = arcadia.PageGames.ToList().IndexOf(arcadia.FocusedGame!);
        var rows = target / columns - at / columns;
        arcadia.Press(rows > 0 ? UiAction.Down : UiAction.Up, Math.Abs(rows));
        at = arcadia.PageGames.ToList().IndexOf(arcadia.FocusedGame!);
        arcadia.Press(target > at ? UiAction.Right : UiAction.Left, Math.Abs(target - at));
        Assert.Equal(title, arcadia.FocusedGame?.Title);
        return arcadia.FocusedGame!;
    }
}
