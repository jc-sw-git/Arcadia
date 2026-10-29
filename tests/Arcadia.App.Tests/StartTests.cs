using Arcadia.App.Input;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

public sealed class StartTests
{
    [AvaloniaFact]
    [Trait("TestCase", "G-1")]
    public async Task Start_shows_a_tab_per_launcher_with_the_first_tab_selected_and_a_game_focused()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();

        Assert.Equal(["GOG Galaxy", "Xbox", "Amazon Games"], arcadia.Main.LauncherTabs.Items.Select(tab => tab.Name));
        Assert.Same(arcadia.Main.LauncherTabs.Items[0], arcadia.SelectedTab);
        Assert.NotNull(arcadia.FocusedGame);
        Assert.Same(arcadia.PageGames[0], arcadia.FocusedGame);
        Assert.True(arcadia.Main.AreGamesShown);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-2")]
    public async Task A_fresh_system_without_any_launcher_says_so_after_the_first_question_without_an_error()
    {
        IReadOnlyList<LauncherSpec> noneFound = ArcadiaDriver.DefaultLaunchers.Select(spec => spec with { IsInstalled = false }).ToList();
        using var arcadia = await ArcadiaDriver.StartAsync(noneFound, new AppSettings());
        Assert.Empty(arcadia.Main.FirstRun!.Found);

        // "Fertig" with ≡.
        arcadia.Press(UiAction.Settings);
        await arcadia.WaitUntilAsync(() => !arcadia.Main.IsFirstRunShown, "die Bibliothek soll nach \"Fertig\" erscheinen");

        var empty = Assert.IsType<Arcadia.App.ViewModels.EmptyTabViewModel>(arcadia.Main.Library.EmptyTab);
        Assert.True(empty.HasNoLauncher);
        Assert.Equal("Kein Launcher aktiv", empty.Title);
        Assert.Empty(arcadia.Main.LauncherTabs.Items);
        Assert.False(arcadia.Main.Toast.IsVisible, arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-2")]
    public async Task Launchers_without_games_show_their_tabs_saying_so_without_an_error()
    {
        using var arcadia = await ArcadiaDriver.StartAsync([new(KnownLaunchers.Gog, 0), new(KnownLaunchers.Xbox, 0)]);

        Assert.Equal(["GOG Galaxy", "Xbox"], arcadia.Main.LauncherTabs.Items.Select(tab => tab.Name));
        Assert.Equal("Keine Spiele in GOG Galaxy", arcadia.Main.Library.EmptyTab?.Title);
        Assert.Null(arcadia.FocusedGame);
        arcadia.Press(UiAction.NextTab);
        Assert.Equal("Keine Spiele in Xbox", arcadia.Main.Library.EmptyTab?.Title);
        Assert.False(arcadia.Main.Toast.IsVisible, arcadia.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-4")]
    public async Task A_broken_settings_file_is_kept_as_broken_and_Arcadia_starts_with_the_defaults()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        const string broken = "{ \"PreferredPageSize\": 50, kaputt";
        File.WriteAllText(arcadia.SettingsPath, broken);

        using var restarted = await arcadia.RestartAsync();

        Assert.Equal(broken, File.ReadAllText(arcadia.SettingsPath + ".broken"));
        var defaults = new AppSettings();
        Assert.Equal(defaults.PreferredPageSize, restarted.Settings.Current.PreferredPageSize);
        Assert.Equal(defaults.LibrarySort, restarted.Settings.Current.LibrarySort);

        // Without settings, Arcadia asks again which launchers to use, as on a first start.
        Assert.True(restarted.Main.IsFirstRunShown);
        Assert.False(restarted.Main.Toast.IsVisible, restarted.Main.Toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-5")]
    public async Task The_log_keeps_the_user_profile_out_of_what_it_writes()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var marker = Guid.NewGuid().ToString("N");
        arcadia.Starts.Failure = new NotSupportedException($"Nicht lesbar: {Path.Combine(profile, "Spiele", marker, "game.exe")}");

        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => ArcadiaDriver.Log.Contains(marker), "der Fehlstart soll im Log stehen");

        var line = ArcadiaDriver.Log.Split('\n').Single(line => line.Contains(marker));
        Assert.DoesNotContain(profile, line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine("Spiele", marker, "game.exe"), line);
    }

    [AvaloniaFact]
    [Trait("TestCase", "G-30")]
    public async Task The_clock_runs_and_no_status_symbol_shows_while_nothing_needs_one()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var status = arcadia.Main.Status;
        Assert.Equal("20:00", status.Clock);
        Assert.Equal("Sa., 24. Okt.", status.Date);

        arcadia.Wait(TimeSpan.FromSeconds(61));
        Assert.Equal("20:01", status.Clock);

        Assert.False(status.HasControllerStatus);
        Assert.False(status.IsControllerStarting);
        Assert.False(status.HasArcadiaUpdate);
        Assert.False(status.HasAnyStatus);
    }
}
