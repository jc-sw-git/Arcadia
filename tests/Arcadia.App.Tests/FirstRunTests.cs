using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>The first start asks which launchers Arcadia uses (#139).</summary>
public sealed class FirstRunTests
{
    private static readonly IReadOnlyList<LauncherSpec> TwoFoundOneMissing =
    [
        new(KnownLaunchers.Gog, 31),
        new(KnownLaunchers.Xbox, 62),
        new(KnownLaunchers.Amazon, 6, IsInstalled: false),
    ];

    [AvaloniaFact]
    [Trait("TestCase", "T139-1")]
    public async Task A_fresh_install_asks_first_with_the_launchers_found_switched_on()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(TwoFoundOneMissing, new AppSettings());

        var firstRun = Assert.IsType<Arcadia.App.Setup.FirstRunViewModel>(arcadia.Main.FirstRun);
        Assert.True(firstRun.IsReady);
        Assert.Equal(["GOG Galaxy", "Xbox"], firstRun.Found.Select(row => row.Name));
        Assert.All(firstRun.Found, row => Assert.Equal("An", row.ValueText));
        Assert.Equal(["Gefunden · 31 Spiele", "Gefunden · 62 Spiele"], firstRun.Found.Select(row => row.StatusText));
        Assert.Equal(["Amazon Games"], firstRun.Missing.Select(row => row.Name));
        Assert.All(firstRun.Missing, row => Assert.False(row.IsOn));
        Assert.Equal(["Nicht installiert"], firstRun.Missing.Select(row => row.StateText));
        Assert.Equal(["Auf diesem PC nicht gefunden"], firstRun.Missing.Select(row => row.StatusText));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T139-1")]
    public async Task A_launcher_not_found_cannot_be_switched_on()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(TwoFoundOneMissing, new AppSettings());
        var firstRun = arcadia.Main.FirstRun!;

        arcadia.Press(UiAction.Down, times: 2);
        Assert.Equal("Amazon Games", firstRun.FocusedRow?.Name);

        // Each switch action on its own: two that worked would cancel each other out.
        foreach (var action in new[] { UiAction.Right, UiAction.Left, UiAction.Accept })
        {
            arcadia.Press(action);
            Assert.False(firstRun.Missing.Single().IsOn, $"{action} hat den nicht gefundenen Launcher eingeschaltet.");
            Assert.Equal("Amazon Games", firstRun.FocusedRow?.Name);
        }

        arcadia.Press(UiAction.Settings);
        await arcadia.WaitUntilAsync(() => !arcadia.Main.IsFirstRunShown, "die Bibliothek soll nach \"Fertig\" erscheinen");
        Assert.Equal(["GOG Galaxy", "Xbox"], arcadia.Main.LauncherTabs.Items.Select(tab => tab.Name));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T139-3")]
    public async Task Only_the_launchers_left_on_become_tabs_also_after_a_restart_and_the_question_does_not_come_again()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(TwoFoundOneMissing, new AppSettings());

        // Xbox (second row) off, then "Fertig" with ≡.
        arcadia.Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Settings);
        await arcadia.WaitUntilAsync(() => !arcadia.Main.IsFirstRunShown && arcadia.Main.LauncherTabs.Items.Count > 0, "die Bibliothek soll nach \"Fertig\" erscheinen");

        Assert.Equal(["GOG Galaxy"], arcadia.Main.LauncherTabs.Items.Select(tab => tab.Name));

        using var restarted = await arcadia.RestartAsync();
        Assert.False(restarted.Main.IsFirstRunShown);
        Assert.Equal(["GOG Galaxy"], restarted.Main.LauncherTabs.Items.Select(tab => tab.Name));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T139-2")]
    public async Task The_preview_shows_the_launchers_switched_on_as_tabs_and_follows_every_switch()
    {
        IReadOnlyList<LauncherSpec> launchers =
        [
            LauncherSpec.With(
                KnownLaunchers.Gog,
                Installed(KnownLaunchers.Gog, "Hollow Knight", ArcadiaDriver.Now.AddDays(-30)),
                NotInstalled(KnownLaunchers.Gog, "Iron Harbor")),
            new(KnownLaunchers.Xbox, 62),
            new(KnownLaunchers.Amazon, 6, IsInstalled: false),
        ];
        using var arcadia = await ArcadiaDriver.StartAsync(launchers, new AppSettings());
        var firstRun = arcadia.Main.FirstRun!;

        Assert.Equal("LAUNCHER-REIHENFOLGE", firstRun.TabBarTitle);
        Assert.Equal(["GOG Galaxy", "Xbox"], firstRun.TabBar.Tabs.Select(tab => tab.Name));
        Assert.Equal(["GOG Galaxy"], firstRun.TabBar.Tabs.Where(tab => tab.IsHighlighted).Select(tab => tab.Name));
        var gog = firstRun.FocusedRow!;
        Assert.Equal(("An · wird Tab 1", "2 · 1 installiert", "Test", "/gog"), (gog.StateText, gog.GamesText, gog.LocationText, gog.Keyword));

        arcadia.Press(UiAction.Down);
        Assert.Equal(["Xbox"], firstRun.TabBar.Tabs.Where(tab => tab.IsHighlighted).Select(tab => tab.Name));
        Assert.Equal("An · wird Tab 2", firstRun.FocusedRow?.StateText);

        arcadia.Press(UiAction.Accept);
        Assert.Equal(["GOG Galaxy"], firstRun.TabBar.Tabs.Select(tab => tab.Name));
        Assert.Equal("Aus · bekommt keinen Tab", firstRun.FocusedRow?.StateText);

        // No launcher on: the preview says the library stays empty.
        arcadia.Press(UiAction.Up).Press(UiAction.Left);
        Assert.True(firstRun.TabBar.IsEmpty);

        arcadia.Press(UiAction.Down).Press(UiAction.Right);
        Assert.Equal(["Xbox"], firstRun.TabBar.Tabs.Select(tab => tab.Name));
        Assert.Equal("An · wird Tab 1", firstRun.FocusedRow?.StateText);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T139-4")]
    public async Task Settings_of_a_version_without_a_choice_of_launchers_bring_the_question_and_keep_everything_else()
    {
        var beforeTheQuestion = new AppSettings { KeyboardLayout = "fr", PreferredPageSize = 50 };
        using var arcadia = await ArcadiaDriver.StartAsync(settings: beforeTheQuestion);

        Assert.True(arcadia.Main.IsFirstRunShown);
        arcadia.Press(UiAction.Settings);
        await arcadia.WaitUntilAsync(() => !arcadia.Main.IsFirstRunShown && arcadia.Main.LauncherTabs.Items.Count > 0, "die Bibliothek soll nach \"Fertig\" erscheinen");

        var saved = new SettingsStore(arcadia.SettingsPath).Load();
        Assert.Equal(["gog", "xbox", "amazon"], saved.ActiveLaunchers);
        Assert.Equal(("fr", 50), (saved.KeyboardLayout, saved.PreferredPageSize));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T139-5")]
    public async Task Demo_skips_the_question_with_every_launcher_active_and_saves_no_choice()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(settings: new AppSettings(), arguments: new StartArguments(Demo: true));

        Assert.False(arcadia.Main.IsFirstRunShown);
        Assert.Equal(["GOG Galaxy", "Xbox", "Amazon Games"], arcadia.Main.LauncherTabs.Items.Select(tab => tab.Name));
        Assert.Null(new SettingsStore(arcadia.SettingsPath).Load().ActiveLaunchers);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T139-5")]
    public async Task First_run_asks_again_also_with_a_saved_choice()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(arguments: new StartArguments(FirstRun: true));

        Assert.True(arcadia.Main.IsFirstRunShown);
        Assert.Equal(["GOG Galaxy", "Xbox", "Amazon Games"], arcadia.Main.FirstRun!.Found.Select(row => row.Name));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T139-5")]
    public async Task Demo_with_first_run_keeps_the_answer_only_until_Arcadia_closes()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(settings: new AppSettings(), arguments: new StartArguments(Demo: true, FirstRun: true));
        Assert.True(arcadia.Main.IsFirstRunShown);

        arcadia.Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Settings);
        await arcadia.WaitUntilAsync(() => !arcadia.Main.IsFirstRunShown && arcadia.Main.LauncherTabs.Items.Count > 0, "die Bibliothek soll nach \"Fertig\" erscheinen");
        Assert.Equal(["GOG Galaxy", "Amazon Games"], arcadia.Main.LauncherTabs.Items.Select(tab => tab.Name));
        Assert.Null(new SettingsStore(arcadia.SettingsPath).Load().ActiveLaunchers);

        using var restarted = await arcadia.RestartAsync(arguments: new StartArguments(Demo: true));
        Assert.Equal(["GOG Galaxy", "Xbox", "Amazon Games"], restarted.Main.LauncherTabs.Items.Select(tab => tab.Name));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T139-7")]
    public async Task Done_with_no_launcher_switched_on_shows_no_launcher_active()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(TwoFoundOneMissing, new AppSettings());

        arcadia.Press(UiAction.Accept).Press(UiAction.Down).Press(UiAction.Accept).Press(UiAction.Settings);

        Assert.False(arcadia.Main.IsFirstRunShown);
        Assert.Empty(arcadia.Main.LauncherTabs.Items);
        Assert.Equal("Kein Launcher aktiv", arcadia.Main.EmptyTab?.Title);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T139-7")]
    public async Task Done_with_no_launcher_found_shows_no_launcher_active()
    {
        IReadOnlyList<LauncherSpec> noneFound = [new(KnownLaunchers.Gog, 31, IsInstalled: false), new(KnownLaunchers.Xbox, 62, IsInstalled: false)];
        using var arcadia = await ArcadiaDriver.StartAsync(noneFound, new AppSettings());
        Assert.Empty(arcadia.Main.FirstRun!.Found);

        arcadia.Press(UiAction.Settings);

        Assert.False(arcadia.Main.IsFirstRunShown);
        Assert.Equal("Kein Launcher aktiv", arcadia.Main.EmptyTab?.Title);
    }
}
