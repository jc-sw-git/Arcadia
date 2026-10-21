using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

public sealed class ProcessGamesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 18, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlySet<string> NoneFromArcadia = new HashSet<string>();

    private readonly FakeProcessTable _table = new();
    private readonly LaunchedGames _launched = new();

    private static Game GameIn(string id, string folder) => new()
    {
        Id = id,
        LauncherId = "gog",
        Title = id,
        Launch = new LaunchTarget.None(),
        IsInstalled = true,
        InstallDirectory = folder,
    };

    private ProcessGames CreateGames(params Game[] games)
    {
        var processGames = new ProcessGames(_table, _launched);
        processGames.Watch(GameFolders.Of(games));
        return processGames;
    }

    [Fact]
    public void A_process_from_a_games_folder_is_that_game_running_since_the_process_started()
    {
        var games = CreateGames(GameIn("witcher", @"D:\Games\Witcher 3"));
        _table.Start(10, @"C:\Windows\explorer.exe", Now.AddHours(-2));
        _table.Start(20, @"D:\Games\Witcher 3\bin\x64\witcher3.exe", Now);

        Assert.Equal(new Dictionary<string, DateTimeOffset?> { ["gog:witcher"] = Now }, games.Poll(NoneFromArcadia));
    }

    [Fact]
    public void Of_several_processes_of_a_game_the_earliest_start_counts()
    {
        var games = CreateGames(GameIn("witcher", @"D:\Games\Witcher 3"));
        _table.Start(20, @"D:\Games\Witcher 3\launcher.exe", Now);
        _table.Start(21, @"D:\Games\Witcher 3\bin\x64\witcher3.exe", Now.AddSeconds(5));

        Assert.Equal(Now, games.Poll(NoneFromArcadia)["gog:witcher"]);
    }

    [Fact]
    public void The_game_ends_with_its_last_process()
    {
        var games = CreateGames(GameIn("witcher", @"D:\Games\Witcher 3"));
        _table.Start(20, @"D:\Games\Witcher 3\launcher.exe", Now);
        _table.Start(21, @"D:\Games\Witcher 3\bin\x64\witcher3.exe", Now.AddSeconds(5));
        games.Poll(NoneFromArcadia);

        _table.End(20);
        Assert.Equal(Now.AddSeconds(5), games.Poll(NoneFromArcadia)["gog:witcher"]);

        _table.End(21);
        Assert.Empty(games.Poll(NoneFromArcadia));
    }

    [Fact]
    public void Each_round_looks_only_at_processes_that_are_new_since_the_round_before()
    {
        var games = CreateGames(GameIn("witcher", @"D:\Games\Witcher 3"));
        for (var id = 1; id <= 100; id++)
        {
            _table.Start(id, $@"C:\Programs\tool{id}.exe", Now);
        }

        games.Poll(NoneFromArcadia);
        Assert.Equal(100, _table.Descriptions);

        _table.Start(200, @"D:\Games\Witcher 3\witcher3.exe", Now);
        games.Poll(NoneFromArcadia);

        // The new process, and the game's process again in every later round to notice a reused id.
        Assert.Equal(101, _table.Descriptions);
        games.Poll(NoneFromArcadia);
        Assert.Equal(102, _table.Descriptions);
    }

    [Fact]
    public void A_reused_process_id_is_looked_at_anew()
    {
        var games = CreateGames(GameIn("witcher", @"D:\Games\Witcher 3"));
        _table.Start(20, @"D:\Games\Witcher 3\witcher3.exe", Now);
        games.Poll(NoneFromArcadia);

        // Between two rounds the game ended and another program got its id.
        _table.Start(20, @"C:\Programs\notepad.exe", Now.AddMinutes(1));

        Assert.Empty(games.Poll(NoneFromArcadia));
    }

    [Fact]
    public void Services_are_no_games()
    {
        var games = CreateGames(GameIn("valorant", @"C:\Riot Games\VALORANT"));
        _table.Start(30, @"C:\Riot Games\VALORANT\vgc.exe", Now, isService: true);

        Assert.Empty(games.Poll(NoneFromArcadia));
    }

    [Fact]
    public void A_process_whose_path_cant_be_read_is_the_game_arcadia_started_before_it()
    {
        var games = CreateGames(GameIn("valorant", @"C:\Riot Games\VALORANT"));
        _table.Start(5, path: null, Now.AddHours(-1));
        _launched.Starting("gog:valorant", _table.Ids());
        _table.Start(40, path: null, Now);

        Assert.Equal(new Dictionary<string, DateTimeOffset?> { ["gog:valorant"] = Now }, games.Poll(new HashSet<string> { "gog:valorant" }));
    }

    [Fact]
    public void A_process_whose_path_cant_be_read_is_no_game_unless_it_was_started_from_arcadia_and_still_awaited()
    {
        var games = CreateGames(GameIn("valorant", @"C:\Riot Games\VALORANT"));
        _launched.Starting("gog:valorant", _table.Ids());
        _table.Start(40, path: null, Now);

        // The game started from Arcadia ended already, so the next locked process isn't it.
        Assert.Empty(games.Poll(NoneFromArcadia));
    }

    [Fact]
    public void Watching_other_games_looks_at_every_process_again()
    {
        var games = CreateGames();
        _table.Start(20, @"D:\Games\Witcher 3\witcher3.exe", Now);
        Assert.Empty(games.Poll(NoneFromArcadia));

        // The game was installed meanwhile; its process runs already.
        games.Watch(GameFolders.Of([GameIn("witcher", @"D:\Games\Witcher 3")]));

        Assert.Equal(["gog:witcher"], games.Poll(NoneFromArcadia).Keys);
    }

    private sealed class FakeProcessTable : IProcessTable
    {
        private readonly Dictionary<int, (ProcessDetails Details, bool IsService)> _processes = [];

        public int Descriptions { get; private set; }

        public void Start(int id, string? path, DateTimeOffset startedAt, bool isService = false) =>
            _processes[id] = (new ProcessDetails(path, startedAt), isService);

        public void End(int id) => _processes.Remove(id);

        public IReadOnlyCollection<int> Ids() => [.. _processes.Keys];

        public ProcessDetails? Describe(int processId)
        {
            Descriptions++;
            return _processes.TryGetValue(processId, out var process) && !process.IsService ? process.Details : null;
        }
    }
}
