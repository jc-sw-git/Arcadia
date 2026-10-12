using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class InstallTrackerTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly FakeMonitor _epic = new("epic");

    private static Game GameOf(string launcherId, string id, bool installed = false) => new()
    {
        Id = id,
        LauncherId = launcherId,
        Title = id,
        Launch = new LaunchTarget.None(),
        IsInstalled = installed,
    };

    private InstallTracker CreateTracker() =>
        new(FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Epic) { InstallMonitor = _epic }), _time);

    [Fact]
    public async Task Waits_for_confirmation_after_opening_the_install_dialog()
    {
        var tracker = CreateTracker();
        var game = GameOf("epic", "a");

        Assert.Equal(InstallPhase.AwaitingConfirmation, tracker.Started(game)?.Phase);
        Assert.Equal(InstallPhase.AwaitingConfirmation, tracker.StateOf(game.Key)?.Phase);

        Assert.Empty((await tracker.PollAsync())!);
        Assert.Equal(InstallPhase.AwaitingConfirmation, tracker.StateOf(game.Key)?.Phase);
    }

    [Fact]
    public async Task Gives_up_waiting_for_confirmation_after_the_timeout()
    {
        var tracker = CreateTracker();
        var game = GameOf("epic", "a");
        tracker.Started(game);

        _time.Advance(InstallTracker.ConfirmationTimeout);
        await tracker.PollAsync();
        Assert.NotNull(tracker.StateOf(game.Key));

        _time.Advance(TimeSpan.FromSeconds(1));
        await tracker.PollAsync();
        Assert.Null(tracker.StateOf(game.Key));
    }

    [Fact]
    public async Task Follows_progress_until_the_install_completes()
    {
        var tracker = CreateTracker();
        var game = GameOf("epic", "a");
        tracker.Started(game);

        _epic.Active["a"] = new InstallProgress(InstallPhase.Downloading, 0.5);
        await tracker.PollAsync();
        Assert.Equal(new InstallProgress(InstallPhase.Downloading, 0.5), tracker.StateOf(game.Key));

        _epic.Active.Clear();
        _epic.Finished["a"] = InstallOutcome.Completed;
        var results = await tracker.PollAsync();

        Assert.Equal([new InstallResult(game.Key, InstallOutcome.Completed)], results);
        Assert.Null(tracker.StateOf(game.Key));
    }

    [Fact]
    public async Task A_confirmed_install_no_longer_waits_after_it_ends()
    {
        var tracker = CreateTracker();
        var game = GameOf("epic", "a");
        tracker.Started(game);

        // Cancelled in the launcher before Arcadia ever saw it running.
        _epic.Finished["a"] = InstallOutcome.Cancelled;
        Assert.Equal([new InstallResult(game.Key, InstallOutcome.Cancelled)], await tracker.PollAsync());

        _epic.Finished.Clear();
        Assert.Empty((await tracker.PollAsync())!);
        Assert.Null(tracker.StateOf(game.Key));
    }

    [Fact]
    public async Task Reports_installs_and_uninstalls_started_in_the_launcher()
    {
        var tracker = CreateTracker();

        _epic.Active["b"] = new InstallProgress(InstallPhase.Uninstalling);
        await tracker.PollAsync();
        Assert.Equal(InstallPhase.Uninstalling, tracker.StateOf(Game.KeyOf("epic", "b"))?.Phase);

        _epic.Active.Clear();
        _epic.Finished["b"] = InstallOutcome.Uninstalled;
        _epic.Finished["c"] = InstallOutcome.Failed;
        var results = await tracker.PollAsync();

        Assert.Equal(
            [new InstallResult("epic:b", InstallOutcome.Uninstalled), new InstallResult("epic:c", InstallOutcome.Failed)],
            results);
    }

    [Fact]
    public void Unwatched_launchers_stay_pending_until_a_scan_shows_the_game_installed()
    {
        var tracker = CreateTracker();
        var game = GameOf("gog", "x");

        Assert.Null(tracker.Started(game));
        Assert.True(tracker.HasPending);
        Assert.Null(tracker.StateOf(game.Key));

        Assert.Empty(tracker.CompletePending([game, GameOf("gog", "other", installed: true)]));
        Assert.True(tracker.HasPending);

        var installed = game with { IsInstalled = true };
        Assert.Equal([installed], tracker.CompletePending([installed]));
        Assert.False(tracker.HasPending);
    }

    [Fact]
    public async Task Does_not_poll_without_monitors()
    {
        var tracker = new InstallTracker(FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Epic)), _time);
        Assert.Null(await tracker.PollAsync());
    }

    [Fact]
    public async Task Skips_a_poll_while_the_previous_one_runs()
    {
        var blocking = new FakeMonitor("epic") { Gate = new ManualResetEventSlim() };
        var tracker = new InstallTracker(FakeIntegration.Registry(new FakeIntegration(KnownLaunchers.Epic) { InstallMonitor = blocking }), _time);

        var first = tracker.PollAsync();
        Assert.Null(await tracker.PollAsync());

        blocking.Gate.Set();
        Assert.NotNull(await first);
    }
}
