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

        settings = settings.WithControllerSupportChoice(G("steam"), useArcadiaPad: true, Builtin);
        Assert.True(settings.UsesArcadiaPad("steam:x", Builtin));
        Assert.False(settings.UsesArcadiaPad("steam:570", Builtin));
    }

    [Fact]
    public void By_default_every_game_but_Steam_s_starts_on_Arcadia()
    {
        var settings = new AppSettings();

        Assert.True(settings.UsesArcadiaPad(G("epic").Key, Builtin));
        Assert.False(settings.UsesArcadiaPad(G("steam").Key, Builtin));
    }

    [Fact]
    public void Per_game_overrides_win_and_can_be_reset()
    {
        var steamGame = G("steam");
        var epicGame = G("epic");
        var settings = new AppSettings()
            .WithControllerSupportOverride(steamGame, true)
            .WithControllerSupportOverride(epicGame, false);

        Assert.True(settings.UsesArcadiaPad(steamGame.Key, Builtin));
        Assert.False(settings.UsesArcadiaPad(epicGame.Key, Builtin));

        settings = settings.WithControllerSupportOverride(epicGame, null);
        Assert.Null(settings.ControllerSupportOverrideFor(epicGame));
        Assert.True(settings.UsesArcadiaPad(epicGame.Key, Builtin));
    }

    [Fact]
    public void Choosing_a_games_default_mode_stores_nothing()
    {
        var steamGame = G("steam");
        var settings = new AppSettings().WithControllerSupportChoice(steamGame, useArcadiaPad: true, Builtin);
        Assert.True(settings.UsesArcadiaPad(steamGame.Key, Builtin));
        Assert.True(settings.ControllerSupportOverrideFor(steamGame));

        // Back to "Steam Input", the default for Steam games: the override is gone again.
        settings = settings.WithControllerSupportChoice(steamGame, useArcadiaPad: false, Builtin);
        Assert.False(settings.UsesArcadiaPad(steamGame.Key, Builtin));
        Assert.Null(settings.ControllerSupportOverrideFor(steamGame));
        Assert.Empty(settings.ControllerSupportOverrides);
    }

    [Fact]
    public void A_launcher_that_says_its_games_use_Steam_Input_starts_them_on_Steam_Eingabe()
    {
        var plugin = FakeIntegration.LauncherOf("plugin") with { GamesUseSteamInput = true };
        var registry = FakeIntegration.Registry(new FakeIntegration(plugin), new FakeIntegration(FakeIntegration.LauncherOf("other")));
        var settings = new AppSettings();

        Assert.False(settings.UsesArcadiaPad("plugin:1", registry.GamesUseSteamInput));
        Assert.True(settings.UsesArcadiaPad("other:1", registry.GamesUseSteamInput));

        // Its default is what the launcher says, so choosing it stores nothing.
        var game = G("plugin");
        Assert.Empty(settings.WithControllerSupportChoice(game, useArcadiaPad: false, registry.GamesUseSteamInput).ControllerSupportOverrides);
        Assert.True(settings.WithControllerSupportChoice(game, useArcadiaPad: true, registry.GamesUseSteamInput).UsesArcadiaPad(game.Key, registry.GamesUseSteamInput));
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
            store.Save(new AppSettings().WithControllerSupportOverride(game, false));

            var loaded = store.Load();

            Assert.False(loaded.ControllerSupportOverrideFor(game));
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
