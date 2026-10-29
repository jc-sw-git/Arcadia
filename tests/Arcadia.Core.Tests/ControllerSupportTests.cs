using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Demo;

namespace Arcadia.Core.Tests;

public class ControllerSupportTests
{
    // Steam and Epic as registered: Steam says its games use Steam Input, Epic doesn't.
    private static readonly Func<string, bool> Builtin =
        FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Steam), new FakeIntegration(KnownLaunchers.Epic)).GamesUseSteamInput;

    private static Game G(string launcher) => new()
    {
        Id = "x",
        LauncherId = launcher,
        Title = "X",
        Launch = new LaunchTarget.None(),
    };

    [Fact]
    public void The_mode_per_game_needs_no_switch_and_is_read_by_key_also_for_games_outside_the_library()
    {
        var settings = new AppSettings();

        Assert.True(settings.UsesArcadiaPad("epic:x", Builtin));
        Assert.False(settings.UsesArcadiaPad("steam:570", Builtin));

        settings = settings.WithControllerModeChoice(G("steam").Key, true);
        Assert.True(settings.UsesArcadiaPad("steam:x", Builtin));
        Assert.False(settings.UsesArcadiaPad("steam:570", Builtin));
    }

    [Fact]
    public void By_default_every_game_but_Steam_s_starts_on_Arcadia()
    {
        var settings = new AppSettings();

        Assert.True(settings.UsesArcadiaPad(G("epic").Key, Builtin));
        Assert.False(settings.UsesArcadiaPad(G("steam").Key, Builtin));
        Assert.True(settings.LauncherUsesArcadiaPad("epic", Builtin));
        Assert.False(settings.LauncherUsesArcadiaPad("steam", Builtin));
    }

    [Fact]
    public void A_games_own_choice_wins_and_Standard_follows_the_launcher_again()
    {
        var steamGame = G("steam");
        var epicGame = G("epic");
        var settings = new AppSettings()
            .WithControllerModeChoice(steamGame.Key, true)
            .WithControllerModeChoice(epicGame.Key, false);

        Assert.True(settings.UsesArcadiaPad(steamGame.Key, Builtin));
        Assert.False(settings.UsesArcadiaPad(epicGame.Key, Builtin));

        settings = settings.WithControllerModeChoice(epicGame.Key, null);
        Assert.Null(settings.ControllerModeChoiceFor(epicGame.Key));
        Assert.True(settings.UsesArcadiaPad(epicGame.Key, Builtin));
    }

    /// <summary>#178: "Arcadia" chosen for an Epic game is its own choice, although it is Epic's default too.</summary>
    [Fact]
    public void Choosing_the_mode_the_launcher_gives_anyway_is_still_a_games_own_choice()
    {
        var epicGame = G("epic");
        var settings = new AppSettings().WithControllerModeChoice(epicGame.Key, true);
        Assert.True(settings.ControllerModeChoiceFor(epicGame.Key));

        // Epic's games switched to Steam-Eingabe: this one keeps its own choice.
        settings = settings.WithLauncherControllerMode("epic", useArcadiaPad: false, Builtin);
        Assert.True(settings.UsesArcadiaPad(epicGame.Key, Builtin));
        Assert.False(settings.UsesArcadiaPad("epic:other", Builtin));
    }

    [Fact]
    public void A_launchers_default_moves_its_games_on_Standard_and_no_other_launchers()
    {
        var settings = new AppSettings().WithLauncherControllerMode("steam", useArcadiaPad: true, Builtin);

        Assert.True(settings.LauncherUsesArcadiaPad("steam", Builtin));
        Assert.True(settings.UsesArcadiaPad("steam:570", Builtin));
        Assert.True(settings.PreferencesOf("steam").UsesArcadiaPad);
        Assert.True(settings.UsesArcadiaPad("epic:x", Builtin));
        Assert.Null(settings.PreferencesOf("epic").UsesArcadiaPad);
    }

    /// <summary>#178: the launcher's own default is stored as none, so its entry goes when nothing else was changed.</summary>
    [Fact]
    public void Choosing_the_launchers_own_default_again_stores_nothing()
    {
        var settings = new AppSettings()
            .WithLauncherControllerMode("steam", useArcadiaPad: true, Builtin)
            .WithLauncherControllerMode("epic", useArcadiaPad: true, Builtin);
        Assert.Equal(["steam"], settings.Launchers.Keys);

        settings = settings.WithLauncherControllerMode("steam", useArcadiaPad: false, Builtin);
        Assert.Empty(settings.Launchers);
        Assert.False(settings.UsesArcadiaPad("steam:570", Builtin));
    }

    [Fact]
    public void A_launchers_default_keeps_the_other_things_set_for_it()
    {
        var settings = new AppSettings()
            .WithLauncherPreferences("epic", p => p with { Name = "Epic" })
            .WithLauncherControllerMode("epic", useArcadiaPad: false, Builtin)
            .WithLauncherControllerMode("epic", useArcadiaPad: true, Builtin);

        Assert.Equal(new LauncherPreferences(Name: "Epic"), settings.PreferencesOf("epic"));
    }

    /// <summary>#178 "Alle Spiele auf die Vorauswahl setzen": this launcher's games, also those no longer in the library.</summary>
    [Fact]
    public void Resetting_a_launcher_clears_its_games_own_choices_and_only_those()
    {
        var settings = new AppSettings()
            .WithControllerModeChoice("epic:hades", false)
            .WithControllerModeChoice("epic:uninstalled-long-ago", true)
            .WithControllerModeChoice("steam:570", true)
            .WithControllerModeChoice("epicx:1", false);

        settings = settings.WithoutControllerModeChoices("epic");

        Assert.Equal(new Dictionary<string, bool> { ["steam:570"] = true, ["epicx:1"] = false }, settings.ControllerSupportOverrides);
        Assert.True(settings.UsesArcadiaPad("epic:hades", Builtin));
    }

    [Fact]
    public void A_launcher_that_says_its_games_use_Steam_Input_starts_them_on_Steam_Eingabe()
    {
        var plugin = FakeIntegration.LauncherOf("plugin") with { GamesUseSteamInput = true };
        var registry = FakeIntegration.Registry(new FakeIntegration(plugin), new FakeIntegration(FakeIntegration.LauncherOf("other")));
        var settings = new AppSettings();

        Assert.False(settings.UsesArcadiaPad("plugin:1", registry.GamesUseSteamInput));
        Assert.True(settings.UsesArcadiaPad("other:1", registry.GamesUseSteamInput));

        // Its default is what the launcher says, so choosing it on the launcher's page stores nothing.
        Assert.Empty(settings.WithLauncherControllerMode("plugin", useArcadiaPad: false, registry.GamesUseSteamInput).Launchers);
        Assert.True(settings.WithLauncherControllerMode("plugin", useArcadiaPad: true, registry.GamesUseSteamInput).UsesArcadiaPad("plugin:1", registry.GamesUseSteamInput));
    }

    [Fact]
    public void A_game_of_a_launcher_that_is_not_registered_starts_on_Arcadia()
    {
        // E.g. a game key left in settings.json by a launcher plugin that was removed since.
        var registry = FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Steam));
        var settings = new AppSettings();

        Assert.False(registry.GamesUseSteamInput("removed"));
        Assert.True(settings.UsesArcadiaPad("removed:1", registry.GamesUseSteamInput));
        Assert.True(settings.UsesArcadiaPad(G("removed").Key, registry.GamesUseSteamInput));
    }

    [Fact]
    public void Of_the_built_in_launchers_only_Steam_says_its_games_use_Steam_Input()
    {
        var registry = FakeIntegration.Registry([.. BuiltInLaunchers.Create(TimeProvider.System, demo: true)], _ => true);

        Assert.Equal(["steam"], registry.All.Where(i => i.Launcher.GamesUseSteamInput).Select(i => i.Launcher.Id));
        Assert.Contains(registry.All, i => i is DemoIntegration);
    }

    [Fact]
    public void Settings_round_trip_through_json()
    {
        var path = Path.Combine(Path.GetTempPath(), "arcadia-settings-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new SettingsStore(path);
            var game = G("epic");
            store.Save(new AppSettings().WithControllerModeChoice(game.Key, false).WithLauncherControllerMode("steam", useArcadiaPad: true, Builtin));

            var loaded = store.Load();

            Assert.False(loaded.ControllerModeChoiceFor(game.Key));
            Assert.True(loaded.LauncherUsesArcadiaPad("steam", Builtin));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// #178 migration: a settings.json from before stored only choices against the fixed default; they become the games'
    /// own choices, every other game is on "Standard", and a launcher without a default of its own keeps the factory one.
    /// </summary>
    [Fact]
    public void Settings_from_before_the_launchers_default_keep_every_games_mode()
    {
        var path = Path.Combine(Path.GetTempPath(), "arcadia-settings-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, """
                {
                  "ControllerSupportOverrides": { "steam:570": true, "epic:fortnite": false },
                  "Launchers": { "steam": { "Name": "Valve" } }
                }
                """);

            var loaded = new SettingsStore(path).Load();

            Assert.True(loaded.ControllerModeChoiceFor("steam:570"));
            Assert.False(loaded.ControllerModeChoiceFor("epic:fortnite"));
            Assert.Null(loaded.ControllerModeChoiceFor("steam:440"));
            Assert.Equal((true, false, false, true), (loaded.UsesArcadiaPad("steam:570", Builtin), loaded.UsesArcadiaPad("epic:fortnite", Builtin),
                loaded.UsesArcadiaPad("steam:440", Builtin), loaded.UsesArcadiaPad("epic:hades", Builtin)));
            Assert.Null(loaded.PreferencesOf("steam").UsesArcadiaPad);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>#178: a value written as null by hand is no choice: the launcher's factory default, the game on "Standard".</summary>
    [Fact]
    public void Null_written_by_hand_means_the_factory_default()
    {
        var path = Path.Combine(Path.GetTempPath(), "arcadia-settings-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, """{ "ControllerSupportOverrides": null, "Launchers": { "steam": { "UsesArcadiaPad": null } } }""");

            var loaded = new SettingsStore(path).Load();

            Assert.False(loaded.UsesArcadiaPad("steam:570", Builtin));
            Assert.True(loaded.UsesArcadiaPad("epic:x", Builtin));
            Assert.Null(loaded.ControllerModeChoiceFor("steam:570"));
            Assert.Empty(loaded.WithoutControllerModeChoices("steam").ControllerSupportOverrides);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("v0.6.1", "0.6.1")]
    [InlineData("v.0.9.8.1", "0.9.8.1")]
    [InlineData("0.7.0-rc1", "0.7.0")]
    public void Release_tags_parse_to_versions(string tag, string expected)
    {
        Assert.Equal(Version.Parse(expected), GitHubReleaseClient.ParseVersion(tag));
    }

    [Theory]
    [InlineData("dev-snapshot")]
    [InlineData("")]
    [InlineData(null)]
    public void Unparsable_tags_have_no_version(string? tag)
    {
        Assert.Null(GitHubReleaseClient.ParseVersion(tag));
    }

    [Fact]
    public void Unparsable_settings_yield_defaults_and_are_kept_aside()
    {
        var path = Path.Combine(Path.GetTempPath(), "arcadia-settings-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{ \"PanEnabled\": fals");

            var loaded = new SettingsStore(path).Load();

            Assert.True(loaded.PanEnabled);
            Assert.Equal("{ \"PanEnabled\": fals", File.ReadAllText(path + ".broken"));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".broken");
        }
    }
}
