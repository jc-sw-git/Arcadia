using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Avalonia.Headless.XUnit;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>The category "Launcher": which launchers are active and in which order, and the library without games (#136).</summary>
public sealed class LauncherSettingsTests
{
    private static readonly DateTimeOffset Installed = ArcadiaDriver.Now.AddDays(-30);

    [AvaloniaFact]
    [Trait("TestCase", "T136-4")]
    public async Task Y_lifts_a_launcher_the_D_pad_moves_it_and_Y_puts_it_down_with_the_tabs_following_at_once()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var launchers = arcadia.OpenLauncherSettings();

        arcadia.Press(UiAction.ViewOptions);
        var gog = Assert.IsType<LauncherRowViewModel>(launchers.FocusedRow);
        Assert.Equal("gog", gog.Key);
        Assert.True(gog.IsLifted);
        Assert.True(launchers.IsReordering);

        arcadia.Press(UiAction.Down);
        Assert.Equal(["xbox", "gog", "amazon"], launchers.LauncherKeys());
        Assert.Equal(["GOG Galaxy", "Xbox", "Amazon Games"], arcadia.TabNames());

        arcadia.Press(UiAction.ViewOptions);
        Assert.False(gog.IsLifted);
        Assert.False(launchers.IsReordering);
        Assert.Equal(["Xbox", "GOG Galaxy", "Amazon Games"], arcadia.TabNames());
        Assert.Equal(["xbox", "gog", "amazon"], arcadia.Settings.Current.ActiveLaunchers);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-4")]
    public async Task B_while_a_launcher_is_lifted_puts_everything_back_as_it_was()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var launchers = arcadia.OpenLauncherSettings();

        arcadia.Press(UiAction.ViewOptions).Press(UiAction.Down, times: 2);
        Assert.Equal(["xbox", "amazon", "gog"], launchers.LauncherKeys());

        // Anything but ▲ ▼, Y and B waits until the launcher is put down.
        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Launcher", arcadia.Main.Settings!.Selected.Name);

        arcadia.Press(UiAction.Back);
        Assert.True(arcadia.Main.IsSettingsOpen);
        Assert.False(launchers.IsReordering);
        Assert.Equal(["gog", "xbox", "amazon"], launchers.LauncherKeys());
        Assert.Equal(["GOG Galaxy", "Xbox", "Amazon Games"], arcadia.TabNames());
        Assert.Equal(["gog", "xbox", "amazon"], arcadia.Settings.Current.ActiveLaunchers);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-4")]
    public async Task A_launcher_put_down_under_the_inactive_ones_is_off_and_its_tab_goes()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var launchers = arcadia.OpenLauncherSettings();

        arcadia.Press(UiAction.Down).Press(UiAction.ViewOptions).Press(UiAction.Down, times: 2);
        var xbox = Assert.IsType<LauncherRowViewModel>(launchers.FocusedRow);
        Assert.Equal(["gog", "amazon", "xbox"], launchers.LauncherKeys());
        Assert.StartsWith("NICHT AKTIV", xbox.Group, StringComparison.Ordinal);

        arcadia.Press(UiAction.ViewOptions);
        Assert.False(xbox.IsActive);
        Assert.Equal(["GOG Galaxy", "Amazon Games"], arcadia.TabNames());
        Assert.Equal(["gog", "amazon"], arcadia.Settings.Current.ActiveLaunchers);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-5")]
    public async Task Beside_the_list_the_tab_strip_shows_the_tabs_in_order_and_the_details_explain_the_focused_launcher()
    {
        IReadOnlyList<LauncherSpec> launchers =
        [
            LauncherSpec.With(
                KnownLaunchers.Gog,
                Installed(KnownLaunchers.Gog, "Hollow Knight", Installed),
                Installed(KnownLaunchers.Gog, "Iron Harbor", Installed),
                NotInstalled(KnownLaunchers.Gog, "Silent Tides")),
            new(KnownLaunchers.Xbox, 62),
            new(KnownLaunchers.Amazon, 6),
        ];
        using var arcadia = await ArcadiaDriver.StartAsync(launchers, new AppSettings { ActiveLaunchers = ["gog", "xbox"] });
        var settings = arcadia.OpenLauncherSettings();

        var gog = settings.FocusedRow!;
        Assert.Equal("LAUNCHER-REIHENFOLGE", gog.PreviewTitle);
        Assert.Equal("LAUNCHER-DETAILS", gog.InfoTitle);
        var tabs = Assert.IsType<InfoTabBar>(Assert.Single(gog.Preview));
        Assert.Equal(["GOG Galaxy", "Xbox"], tabs.Tabs.Select(tab => tab.Name));
        Assert.Equal(["GOG Galaxy"], tabs.Tabs.Where(tab => tab.IsHighlighted).Select(tab => tab.Name));
        var head = Assert.IsType<InfoLauncher>(gog.Info[0]);
        Assert.Equal(("GOG Galaxy", "Aktiv · Tab 1"), (head.Name, head.StateText));
        Assert.Equal("3 Spiele, davon 2 installiert", gog.Fact("Einträge"));
        Assert.Equal("gerade eben", gog.Fact("Zuletzt eingelesen"));
        Assert.Equal("Test", gog.Fact("Gefunden unter"));
        Assert.Equal("werden überwacht", gog.Fact("Installationen"));
        Assert.Equal("an ihren Prozessen erkannt", gog.Fact("Laufende Spiele"));

        // An inactive launcher is not in the tab strip.
        arcadia.Press(UiAction.Down, times: 2);
        var amazon = settings.FocusedRow!;
        Assert.Equal("amazon", amazon.Key);
        tabs = Assert.IsType<InfoTabBar>(Assert.Single(amazon.Preview));
        Assert.Equal(["GOG Galaxy", "Xbox"], tabs.Tabs.Select(tab => tab.Name));
        Assert.DoesNotContain(tabs.Tabs, tab => tab.IsHighlighted);
        Assert.Equal("Nicht aktiv", Assert.IsType<InfoLauncher>(amazon.Info[0]).StateText);
        Assert.Equal("nicht eingelesen", amazon.Fact("Spiele"));
        Assert.Equal("nicht überwacht", amazon.Fact("Installationen"));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-6")]
    public async Task X_reads_all_active_launchers_again_and_a_toast_says_so()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(LauncherSettingsDriving.WithoutFolders);
        arcadia.Time.Advance(TimeSpan.FromMinutes(10));
        var settings = arcadia.OpenLauncherSettings();
        Assert.Equal("vor 10 Min.", settings.FocusedRow!.Fact("Zuletzt eingelesen"));

        arcadia.Press(UiAction.Search);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == "Alle Launcher sind neu eingelesen.", "der Toast nach dem Einlesen");

        Assert.All(settings.Rows.OfType<LauncherRowViewModel>(), row => Assert.Equal("gerade eben", row.Fact("Zuletzt eingelesen")));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-14")]
    public async Task The_row_read_all_again_is_reached_with_the_D_pad_and_A_reads_like_X()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(LauncherSettingsDriving.WithoutFolders);
        arcadia.Time.Advance(TimeSpan.FromMinutes(10));
        var settings = arcadia.OpenLauncherSettings();
        Assert.Equal("vor 10 Min.", settings.FocusedRow!.Fact("Zuletzt eingelesen"));

        arcadia.Press(UiAction.Down, times: 3);
        var row = Assert.IsType<ActionRowViewModel>(settings.FocusedRow);
        Assert.Equal(("Alle neu einlesen", "BIBLIOTHEK"), (row.Title, row.Group));
        Assert.Equal(("Jetzt einlesen", "X"), (row.ActionText, row.ShortcutPad));
        Assert.True(row.CanActivate);

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == "Alle Launcher sind neu eingelesen.", "der Toast nach dem Einlesen");
        Assert.All(settings.Rows.OfType<LauncherRowViewModel>(), launcher => Assert.Equal("gerade eben", launcher.Fact("Zuletzt eingelesen")));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-14")]
    public async Task Without_an_active_launcher_the_row_read_all_again_is_greyed_out_and_A_does_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(settings: new AppSettings { ActiveLaunchers = [] });
        var settings = arcadia.OpenLauncherSettings();

        arcadia.Press(UiAction.Down, times: 3);
        var row = Assert.IsType<ActionRowViewModel>(settings.FocusedRow);
        Assert.Equal("Alle neu einlesen", row.Title);
        Assert.False(row.IsEnabled);
        Assert.Equal("Kein Launcher aktiv", row.Subtitle);

        arcadia.Press(UiAction.Accept);
        Assert.Null(arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-7")]
    public async Task A_tab_without_games_keeps_its_tab_and_its_buttons_read_again_or_open_its_launcher_settings()
    {
        IReadOnlyList<LauncherSpec> launchers = [new(KnownLaunchers.Gog, 31), new(KnownLaunchers.Xbox, 0)];
        using var arcadia = await ArcadiaDriver.StartAsync(launchers);

        arcadia.Press(UiAction.NextTab);
        Assert.Equal(["GOG Galaxy", "Xbox"], arcadia.TabNames());
        var empty = arcadia.Main.Library.EmptyTab!;
        Assert.Equal(("Keine Spiele in Xbox", "Neu einlesen"), (empty.Title, empty.RescanText));
        Assert.True(empty.IsSettingsFocused);

        arcadia.Press(UiAction.Right).Press(UiAction.Accept);
        Assert.True(empty.IsRescanFocused);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == "Weiterhin keine Spiele in Xbox.", "der Toast nach dem Einlesen");

        arcadia.Press(UiAction.Left).Press(UiAction.Accept);
        Assert.True(arcadia.Main.IsSettingsOpen);
        Assert.Equal("Launcher", arcadia.Main.Settings!.Selected.Name);
        Assert.Equal("xbox", arcadia.Main.Settings.Selected.FocusedRow?.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-7")]
    public async Task A_tab_of_a_launcher_no_longer_found_keeps_its_tab_and_looks_for_it_again()
    {
        IReadOnlyList<LauncherSpec> launchers = [new(KnownLaunchers.Gog, 31), new(KnownLaunchers.Xbox, 62, IsInstalled: false)];
        using var arcadia = await ArcadiaDriver.StartAsync(launchers);

        arcadia.Press(UiAction.NextTab);
        Assert.Equal(["GOG Galaxy", "Xbox"], arcadia.TabNames());
        var empty = arcadia.Main.Library.EmptyTab!;
        Assert.Equal(("Xbox nicht gefunden", "Erneut suchen"), (empty.Title, empty.RescanText));

        arcadia.Press(UiAction.Right).Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == "Xbox ist weiterhin nicht zu finden.", "der Toast nach dem Suchen");

        arcadia.Press(UiAction.Left).Press(UiAction.Accept);
        Assert.Equal("xbox", arcadia.Main.Settings?.Selected.FocusedRow?.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-7")]
    [Trait("TestCase", "T136-15")]
    public async Task With_every_launcher_off_the_library_says_so_and_offers_only_the_launcher_settings()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(settings: new AppSettings { ActiveLaunchers = [] });

        Assert.Empty(arcadia.Main.LauncherTabs.Items);
        var empty = arcadia.Main.Library.EmptyTab!;
        Assert.True(empty.HasNoLauncher);
        Assert.Equal("Kein Launcher aktiv", empty.Title);
        Assert.False(empty.CanRescan);
        Assert.DoesNotContain("demo", empty.Text, StringComparison.OrdinalIgnoreCase);

        arcadia.Press(UiAction.Right);
        Assert.True(empty.IsSettingsFocused);
        arcadia.Press(UiAction.Left);
        Assert.True(empty.IsSettingsFocused);

        arcadia.Press(UiAction.Accept);
        Assert.Equal("Launcher", arcadia.Main.Settings?.Selected.Name);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-16")]
    public async Task An_active_launcher_without_games_is_information_without_a_symbol_at_the_category()
    {
        IReadOnlyList<LauncherSpec> launchers = [new(KnownLaunchers.Gog, 31), new(KnownLaunchers.Xbox, 0)];
        using var arcadia = await ArcadiaDriver.StartAsync(launchers);
        var settings = arcadia.OpenLauncherSettings();

        arcadia.Press(UiAction.Down);
        var xbox = settings.FocusedRow!;
        Assert.Equal(("xbox", "Keine Spiele gefunden"), (xbox.Key, ((LauncherRowViewModel)xbox).StatusText));
        Assert.Equal(HealthSeverity.Info, xbox.Severity);
        Assert.True(Assert.Single(xbox.Info.OfType<InfoNote>()).IsInfo);
        Assert.Null(settings.Marker);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T136-16")]
    public async Task An_active_launcher_no_longer_found_stays_a_warning_with_a_symbol_at_the_category()
    {
        IReadOnlyList<LauncherSpec> launchers = [new(KnownLaunchers.Gog, 31), new(KnownLaunchers.Xbox, 62, IsInstalled: false)];
        using var arcadia = await ArcadiaDriver.StartAsync(launchers);
        var settings = arcadia.OpenLauncherSettings();

        arcadia.Press(UiAction.Down);
        var xbox = settings.FocusedRow!;
        Assert.Equal(("xbox", "Nicht gefunden"), (xbox.Key, ((LauncherRowViewModel)xbox).StatusText));
        Assert.Equal(HealthSeverity.Warning, xbox.Severity);
        Assert.False(Assert.Single(xbox.Info.OfType<InfoNote>()).IsInfo);
        Assert.Equal(HealthSeverity.Warning, settings.Marker);
    }
}

/// <summary>Ways into the launcher settings and what a test reads there, as the user sees it.</summary>
internal static class LauncherSettingsDriving
{
    /// <summary>≡ from the library: the settings open on the category "Launcher".</summary>
    public static LauncherSettingsViewModel OpenLauncherSettings(this ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings);
        return Assert.IsType<LauncherSettingsViewModel>(arcadia.Main.Settings?.Selected);
    }

    /// <summary>Text from a real keyboard into the text being written, as the window passes it on.</summary>
    public static ArcadiaDriver Type(this ArcadiaDriver arcadia, string text)
    {
        var editor = arcadia.Main.Settings?.Selected.Editor;
        Assert.NotNull(editor);
        editor.Type(text);
        return arcadia;
    }

    /// <summary>
    /// Three launchers whose games have no install folder: a demo game's folder doesn't exist, so focusing it starts a
    /// rescan in the background, which would move "zuletzt eingelesen" under a test that reads it.
    /// </summary>
    public static IReadOnlyList<LauncherSpec> WithoutFolders { get; } =
    [
        LauncherSpec.With(KnownLaunchers.Gog, Game(KnownLaunchers.Gog, "Hollow Knight"), Game(KnownLaunchers.Gog, "Iron Harbor")),
        LauncherSpec.With(KnownLaunchers.Xbox, Game(KnownLaunchers.Xbox, "Ember Hollow"), Game(KnownLaunchers.Xbox, "Silent Tides"), Game(KnownLaunchers.Xbox, "Night Rail")),
        LauncherSpec.With(KnownLaunchers.Amazon, Game(KnownLaunchers.Amazon, "Copper Sky")),
    ];

    private static Arcadia.Launchers.Abstractions.Game Game(Arcadia.Launchers.Abstractions.LauncherInfo launcher, string title) =>
        TestGames.Installed(launcher, title, ArcadiaDriver.Now.AddDays(-30));

    public static IReadOnlyList<string> TabNames(this ArcadiaDriver arcadia) => [.. arcadia.Main.LauncherTabs.Items.Select(tab => tab.Name)];

    public static IReadOnlyList<string> LauncherKeys(this LauncherSettingsViewModel settings) =>
        [.. settings.Rows.OfType<LauncherRowViewModel>().Select(row => row.Key)];

    /// <summary>A line of the info panel, e.g. "Einträge" → "3 Spiele, davon 2 installiert".</summary>
    public static string Fact(this SettingsRowViewModel row, string label) =>
        row.Info.OfType<InfoFacts>().SelectMany(facts => facts.Facts).Single(fact => fact.Label == label).Value;
}
