using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

public sealed class RunningGamesTrackerTests
{
    private static readonly DateTimeOffset Started = new(2026, 10, 21, 18, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Started.AddMinutes(1));
    private readonly FakeRunningGames _steam = new("steam");
    private readonly FakeRunningGames _epic = new("epic");
    private readonly CountingProcessTable _processes = new();
    private readonly HashSet<string> _disabled = [];
    private readonly List<string> _log = [];
    private readonly HashSet<string> _installing = [];

    private RunningGamesTracker CreateTracker(params ILauncherIntegration[] extra)
    {
        ILauncherIntegration[] integrations =
        [
            new FakeIntegration(FakeIntegration.LauncherOf("steam")) { RunningGames = _steam },
            new FakeIntegration(FakeIntegration.LauncherOf("epic")) { RunningGames = _epic },
            .. extra,
        ];
        var registry = FakeIntegration.Registry(integrations, id => !_disabled.Contains(id));
        return new RunningGamesTracker(registry, new ProcessGames(_processes, new LaunchedGames()), _time)
        {
            Log = _log.Add,
            IsInstalling = _installing.Contains,
        };
    }

    [Fact]
    public async Task A_game_its_launcher_reports_runs_since_its_start()
    {
        var tracker = CreateTracker();
        _steam.Running.Add(new RunningGame("440", Started));

        var changes = await tracker.PollAsync();

        Assert.True(changes?.StatesChanged);
        Assert.Equal(new RunState(RunPhase.Running, Started), tracker.StateOf("steam:440"));
        Assert.False((await tracker.PollAsync())?.StatesChanged);
    }

    [Fact]
    public async Task A_game_started_in_its_launcher_ends_without_bringing_arcadia_back()
    {
        var tracker = CreateTracker();
        _steam.Running.Add(new RunningGame("440", Started));
        await tracker.PollAsync();

        _steam.Running.Clear();
        var changes = await tracker.PollAsync();

        Assert.Equal([new EndedGame("steam:440", StartedFromArcadia: false)], changes?.Ended);
        Assert.Null(tracker.StateOf("steam:440"));
    }

    [Fact]
    public async Task A_game_started_from_arcadia_is_starting_until_its_launcher_reports_it()
    {
        var tracker = CreateTracker();
        tracker.Starting("steam:440");

        Assert.Equal(new RunState(RunPhase.Starting, Started.AddMinutes(1)), tracker.StateOf("steam:440"));
        await tracker.PollAsync();
        Assert.Equal(RunPhase.Starting, tracker.StateOf("steam:440")?.Phase);

        _steam.Running.Add(new RunningGame("440", Started.AddMinutes(1)));
        var changes = await tracker.PollAsync();

        Assert.True(changes?.StatesChanged);
        Assert.Equal(RunPhase.Running, tracker.StateOf("steam:440")?.Phase);
        Assert.Equal(["steam:440"], tracker.StartedFromArcadia);
    }

    [Fact]
    public async Task The_end_of_a_game_started_from_arcadia_says_so()
    {
        var tracker = CreateTracker();
        tracker.Starting("steam:440");
        _steam.Running.Add(new RunningGame("440", Started));
        await tracker.PollAsync();

        _steam.Running.Clear();
        var changes = await tracker.PollAsync();

        Assert.Equal([new EndedGame("steam:440", StartedFromArcadia: true)], changes?.Ended);
        Assert.Empty(tracker.StartedFromArcadia);
    }

    [Fact]
    public async Task Starting_gives_up_after_the_timeout()
    {
        var tracker = CreateTracker();
        tracker.Starting("steam:440");

        _time.Advance(RunningGamesTracker.StartTimeout);
        await tracker.PollAsync();
        Assert.NotNull(tracker.StateOf("steam:440"));

        _time.Advance(TimeSpan.FromSeconds(1));
        var changes = await tracker.PollAsync();

        Assert.True(changes?.StatesChanged);
        Assert.Null(tracker.StateOf("steam:440"));
        Assert.Single(_log);
    }

    [Fact]
    public async Task A_game_its_launcher_updates_first_is_starting_for_as_long_as_the_update_runs()
    {
        var tracker = CreateTracker();
        tracker.Starting("steam:440");
        _installing.Add("steam:440");

        _time.Advance(RunningGamesTracker.StartTimeout * 10);
        await tracker.PollAsync();
        Assert.Equal(RunPhase.Starting, tracker.StateOf("steam:440")?.Phase);

        _installing.Clear();
        _steam.Running.Add(new RunningGame("440", _time.GetUtcNow()));
        await tracker.PollAsync();

        Assert.Equal(RunPhase.Running, tracker.StateOf("steam:440")?.Phase);
        Assert.Equal(["steam:440"], tracker.StartedFromArcadia);
    }

    [Fact]
    public async Task After_an_update_the_start_waits_the_start_timeout_again()
    {
        var tracker = CreateTracker();
        tracker.Starting("steam:440");
        _installing.Add("steam:440");
        _time.Advance(RunningGamesTracker.StartTimeout * 10);
        await tracker.PollAsync();

        _installing.Clear();
        _time.Advance(RunningGamesTracker.StartTimeout);
        await tracker.PollAsync();
        Assert.Equal(RunPhase.Starting, tracker.StateOf("steam:440")?.Phase);

        _time.Advance(TimeSpan.FromSeconds(1));
        await tracker.PollAsync();

        Assert.Null(tracker.StateOf("steam:440"));
    }

    [Fact]
    public async Task A_start_keeps_when_arcadia_asked_while_the_update_runs()
    {
        var tracker = CreateTracker();
        tracker.Starting("steam:440");
        var asked = _time.GetUtcNow();
        _installing.Add("steam:440");

        _time.Advance(TimeSpan.FromMinutes(5));
        await tracker.PollAsync();

        Assert.Equal(new RunState(RunPhase.Starting, asked), tracker.StateOf("steam:440"));
    }

    [Fact]
    public async Task An_update_doesnt_keep_a_start_of_a_switched_off_launcher()
    {
        var tracker = CreateTracker();
        tracker.Starting("steam:440");
        _installing.Add("steam:440");
        _disabled.Add("steam");

        await tracker.PollAsync();

        Assert.Null(tracker.StateOf("steam:440"));
    }

    [Fact]
    public void A_failed_start_shows_play_again()
    {
        var tracker = CreateTracker();
        tracker.Starting("steam:440");

        tracker.StartFailed("steam:440");

        Assert.Null(tracker.StateOf("steam:440"));
        Assert.Empty(tracker.StartedFromArcadia);
    }

    [Fact]
    public async Task Every_poll_asks_every_launcher_also_without_a_game_started_from_arcadia()
    {
        // A game started in its launcher while Arcadia is in the background, e.g. on a second screen (owner, 2026-10-21).
        var tracker = CreateTracker(new FakeIntegration(FakeIntegration.LauncherOf("gog")));
        _epic.Running.Add(new RunningGame("Chess", Started));

        await tracker.PollAsync();
        _epic.Running.Clear();
        var changes = await tracker.PollAsync();

        Assert.Equal(2, _steam.Polls);
        Assert.Equal(2, _epic.Polls);
        Assert.Equal(2, _processes.Rounds);
        Assert.Equal([new EndedGame("epic:Chess", StartedFromArcadia: false)], changes?.Ended);
    }

    [Fact]
    public async Task A_launcher_that_fails_keeps_its_games_and_is_logged()
    {
        var failing = new FailingRunningGames("gog");
        var tracker = CreateTracker(new FakeIntegration(FakeIntegration.LauncherOf("gog")) { RunningGames = failing });
        _steam.Running.Add(new RunningGame("440", Started));
        await tracker.PollAsync();
        Assert.Equal(RunPhase.Running, tracker.StateOf("gog:witcher")?.Phase);

        failing.Fails = true;
        _steam.Running.Clear();
        var changes = await tracker.PollAsync();

        Assert.Equal([new EndedGame("steam:440", StartedFromArcadia: false)], changes?.Ended);
        Assert.Equal(RunPhase.Running, tracker.StateOf("gog:witcher")?.Phase);
        Assert.Single(_log);
    }

    [Fact]
    public async Task A_start_the_launcher_stops_telling_is_kept()
    {
        var tracker = CreateTracker();
        _steam.Running.Add(new RunningGame("440", Started));
        await tracker.PollAsync();

        _steam.Running[0] = new RunningGame("440", null);
        await tracker.PollAsync();

        Assert.Equal(Started, tracker.StateOf("steam:440")?.Since);
    }

    [Fact]
    public async Task Processes_are_only_searched_for_launchers_that_dont_report_their_games()
    {
        var tracker = CreateTracker();
        await tracker.PollAsync();
        Assert.Equal(0, _processes.Rounds);

        var withFallback = CreateTracker(new FakeIntegration(FakeIntegration.LauncherOf("gog")));
        await withFallback.PollAsync();
        Assert.Equal(1, _processes.Rounds);
    }

    [Fact]
    public async Task A_game_of_a_launcher_without_a_report_runs_while_a_process_from_its_folder_runs()
    {
        var tracker = CreateTracker(new FakeIntegration(FakeIntegration.LauncherOf("gog")));
        tracker.SetLibrary([GogGame("witcher", @"D:\Games\Witcher 3"), SteamGame("440", @"D:\Steam\common\TF2")]);
        _processes.Running[20] = new ProcessDetails(@"D:\Games\Witcher 3\witcher3.exe", Started);
        _processes.Running[21] = new ProcessDetails(@"D:\Steam\common\TF2\hl2.exe", Started);

        await tracker.PollAsync();

        // Steam's game counts only as Steam reports it.
        Assert.Equal(new RunState(RunPhase.Running, Started), tracker.StateOf("gog:witcher"));
        Assert.Null(tracker.StateOf("steam:440"));

        _processes.Running.Remove(20);
        var changes = await tracker.PollAsync();
        Assert.Equal([new EndedGame("gog:witcher", StartedFromArcadia: false)], changes?.Ended);
    }

    [Fact]
    public async Task Switching_a_launcher_off_ends_watching_its_games()
    {
        var tracker = CreateTracker();
        tracker.Starting("steam:440");
        _steam.Running.Add(new RunningGame("440", Started));
        await tracker.PollAsync();

        _disabled.Add("steam");
        var changes = await tracker.PollAsync();

        Assert.True(changes?.StatesChanged);
        Assert.Empty(changes!.Ended);
        Assert.Null(tracker.StateOf("steam:440"));
        Assert.Empty(tracker.StartedFromArcadia);
    }

    [Fact]
    public async Task A_poll_while_one_runs_asks_nobody()
    {
        var blocking = new BlockingRunningGames("gog");
        var tracker = CreateTracker(new FakeIntegration(FakeIntegration.LauncherOf("gog")) { RunningGames = blocking });
        var first = tracker.PollAsync();
        Assert.True(blocking.Entered.Wait(TimeSpan.FromSeconds(10)));

        Assert.Null(await tracker.PollAsync());

        blocking.Release.Set();
        Assert.NotNull(await first);
        Assert.Equal(1, _steam.Polls);
    }

    [Fact]
    public async Task An_update_restart_resumes_watching_the_game_the_arcadia_before_started()
    {
        var tracker = CreateTracker();
        _steam.Running.Add(new RunningGame("440", Started));
        tracker.ResumeStartedFromArcadia("steam:440");

        await tracker.PollAsync();
        _steam.Running.Clear();
        var changes = await tracker.PollAsync();

        Assert.Equal([new EndedGame("steam:440", StartedFromArcadia: true)], changes?.Ended);
    }

    private static Game GogGame(string id, string folder) => GameOf("gog", id, folder);

    private static Game SteamGame(string id, string folder) => GameOf("steam", id, folder);

    private static Game GameOf(string launcherId, string id, string folder) => new()
    {
        Id = id,
        LauncherId = launcherId,
        Title = id,
        Launch = new LaunchTarget.None(),
        IsInstalled = true,
        InstallDirectory = folder,
    };

    private sealed class CountingProcessTable : IProcessTable
    {
        public Dictionary<int, ProcessDetails> Running { get; } = [];

        public int Rounds { get; private set; }

        public IReadOnlyCollection<int> Ids()
        {
            Rounds++;
            return [.. Running.Keys];
        }

        public ProcessDetails? Describe(int processId) => Running.TryGetValue(processId, out var details) ? details : null;
    }

    private sealed class FailingRunningGames(string launcherId) : IRunningGamesMonitor
    {
        public bool Fails { get; set; }

        public string LauncherId => launcherId;

        public IReadOnlyList<RunningGame> Poll() =>
            Fails ? throw new InvalidOperationException("broken plugin") : [new RunningGame("witcher", Started)];
    }

    private sealed class BlockingRunningGames(string launcherId) : IRunningGamesMonitor
    {
        public ManualResetEventSlim Entered { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public string LauncherId => launcherId;

        public IReadOnlyList<RunningGame> Poll()
        {
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(10));
            return [];
        }
    }
}
