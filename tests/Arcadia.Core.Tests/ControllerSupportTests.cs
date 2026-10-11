using Arcadia.ControllerSupport;
using Arcadia.Core.Models;
using Arcadia.Core.Settings;

namespace Arcadia.Core.Tests;

public class ControllerSupportTests
{
    private static Game G(string launcher) => new()
    {
        Id = "x",
        LauncherId = launcher,
        Title = "X",
        Launch = new LaunchTarget.None(),
    };

    [Fact]
    public void Off_means_off_for_every_game()
    {
        var settings = new AppSettings { SteamControllerSupport = false };

        Assert.False(settings.ControllerSupportFor(G("epic")));
        Assert.False(settings.ControllerSupportFor(G("steam")));
    }

    [Fact]
    public void By_default_SISR_runs_for_everything_but_steam_games()
    {
        var settings = new AppSettings { SteamControllerSupport = true };

        Assert.True(settings.ControllerSupportFor(G("epic")));
        Assert.False(settings.ControllerSupportFor(G("steam")));
    }

    [Fact]
    public void Per_game_overrides_win_and_can_be_reset()
    {
        var steamGame = G("steam");
        var epicGame = G("epic");
        var settings = new AppSettings { SteamControllerSupport = true }
            .WithControllerSupportOverride(steamGame, true)
            .WithControllerSupportOverride(epicGame, false);

        Assert.True(settings.ControllerSupportFor(steamGame));
        Assert.False(settings.ControllerSupportFor(epicGame));

        settings = settings.WithControllerSupportOverride(epicGame, null);
        Assert.Null(settings.ControllerSupportOverrideFor(epicGame));
        Assert.True(settings.ControllerSupportFor(epicGame));
    }

    [Fact]
    public void Settings_round_trip_through_json()
    {
        var path = Path.Combine(Path.GetTempPath(), "arcadia-settings-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new SettingsStore(path);
            var game = G("epic");
            store.Save(new AppSettings { SteamControllerSupport = true }.WithControllerSupportOverride(game, false));

            var loaded = store.Load();

            Assert.True(loaded.SteamControllerSupport);
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
            File.WriteAllText(path, "{ \"SteamControllerSupport\": tru");

            var loaded = new SettingsStore(path).Load();

            Assert.False(loaded.SteamControllerSupport);
            Assert.Equal("{ \"SteamControllerSupport\": tru", File.ReadAllText(path + ".broken"));
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".broken");
        }
    }
}
