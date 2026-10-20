using Arcadia.Core.Library;

namespace Arcadia.Core.Tests;

public sealed class LaunchedGamesTests
{
    [Fact]
    public void Before_arcadia_started_a_game_no_process_counts()
    {
        Assert.False(new LaunchedGames().StartedSinceLastStart(42));
    }

    [Fact]
    public void A_process_that_started_after_the_game_start_counts_one_that_ran_before_does_not()
    {
        var launched = new LaunchedGames();
        launched.Starting([4, 8, 15]);

        Assert.True(launched.StartedSinceLastStart(16));
        Assert.False(launched.StartedSinceLastStart(8));
    }

    [Fact]
    public void The_next_start_replaces_the_processes_running_before()
    {
        var launched = new LaunchedGames();
        launched.Starting([4]);
        launched.Starting([4, 16]);

        Assert.False(launched.StartedSinceLastStart(16));
        Assert.True(launched.StartedSinceLastStart(23));
    }
}
