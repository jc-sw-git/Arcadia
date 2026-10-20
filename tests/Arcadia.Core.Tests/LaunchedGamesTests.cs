using Arcadia.Core.Library;

namespace Arcadia.Core.Tests;

public sealed class LaunchedGamesTests
{
    [Fact]
    public void Before_arcadia_started_a_game_no_process_counts()
    {
        Assert.Null(new LaunchedGames().GameStartedBefore(42));
    }

    [Fact]
    public void A_process_that_started_after_the_game_start_is_that_game_one_that_ran_before_is_not()
    {
        var launched = new LaunchedGames();
        launched.Starting("steam:620", [4, 8, 15]);

        Assert.Equal("steam:620", launched.GameStartedBefore(16));
        Assert.Null(launched.GameStartedBefore(8));
    }

    [Fact]
    public void The_next_start_replaces_the_game_and_the_processes_running_before()
    {
        var launched = new LaunchedGames();
        launched.Starting("steam:620", [4]);
        launched.Starting("epic:hades", [4, 16]);

        Assert.Null(launched.GameStartedBefore(16));
        Assert.Equal("epic:hades", launched.GameStartedBefore(23));
    }
}
