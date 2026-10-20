using Arcadia.Core.Settings;

namespace Arcadia.Core.Tests;

public sealed class MouseModeInGamesTests
{
    private const string Game = "steam:620";

    [Fact]
    public void Outside_games_the_mouse_mode_is_always_allowed()
    {
        var settings = new MouseModeSettings { InGames = MouseModeInGames.Blocked };

        Assert.True(settings.AllowedIn(null));
    }

    [Theory]
    [InlineData(MouseModeInGames.Allowed, true)]
    [InlineData(MouseModeInGames.Blocked, false)]
    public void Nein_allows_it_in_every_game_and_ja_in_none(MouseModeInGames inGames, bool allowed)
    {
        Assert.Equal(allowed, new MouseModeSettings { InGames = inGames }.AllowedIn(Game));
    }

    [Theory]
    [InlineData(MouseModeActivation.Off)]
    [InlineData(MouseModeActivation.Automatic)]
    public void The_choice_counts_only_for_von_hand(MouseModeActivation activation)
    {
        var settings = new MouseModeSettings { Activation = activation, InGames = MouseModeInGames.Blocked };

        Assert.True(settings.AllowedIn(Game));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Individuell_uses_the_default_for_a_game_left_on_standard(bool defaultAllowed)
    {
        var settings = new MouseModeSettings { InGames = MouseModeInGames.PerGame, PerGameDefaultAllowed = defaultAllowed };

        Assert.Null(settings.GameChoiceFor(Game));
        Assert.Equal(defaultAllowed, settings.AllowedIn(Game));
    }

    [Fact]
    public void Individuell_follows_a_games_own_choice_over_the_default()
    {
        var settings = new MouseModeSettings { InGames = MouseModeInGames.PerGame, PerGameDefaultAllowed = true }
            .WithGameChoice(Game, false)
            .WithGameChoice("epic:hades", true);

        Assert.False(settings.AllowedIn(Game));
        Assert.True(settings.AllowedIn("epic:hades"));
        Assert.True(settings.AllowedIn("epic:other"));
    }

    [Fact]
    public void A_games_choice_counts_only_with_individuell()
    {
        var settings = new MouseModeSettings { InGames = MouseModeInGames.Allowed }.WithGameChoice(Game, false);

        Assert.True(settings.AllowedIn(Game));
    }

    [Fact]
    public void Back_to_standard_removes_the_games_choice()
    {
        var settings = new MouseModeSettings().WithGameChoice(Game, false).WithGameChoice(Game, null);

        Assert.Null(settings.GameChoiceFor(Game));
        Assert.Empty(settings.GameChoices);
    }
}
