namespace Arcadia.Core.Library;

/// <summary>One process as <see cref="ProcessGames"/> needs it.</summary>
/// <param name="Path">Its executable, or null if it can't be read (e.g. an anti-cheat refuses even the limited query).</param>
/// <param name="StartedAt">When it started, or null if that can't be read.</param>
public readonly record struct ProcessDetails(string? Path, DateTimeOffset? StartedAt);

/// <summary>The processes of the PC; replaced in tests.</summary>
public interface IProcessTable
{
    /// <summary>The ids of the running processes; empty where Arcadia can't tell games by their processes.</summary>
    IReadOnlyCollection<int> Ids();

    /// <summary>
    /// What Arcadia can learn about the process, or null if it ended or is a service: services run apart from the
    /// user's programs, and an anti-cheat or update service in a game's folder never ends.
    /// </summary>
    ProcessDetails? Describe(int processId);
}

/// <summary>
/// Running games by their processes, for launchers that don't report them (#95): a process from a game's install folder
/// is that game (<see cref="GameFolders"/>); one whose path can't be read is a game only if it appeared after Arcadia started
/// that game (<see cref="LaunchedGames"/>). Each round looks only at the processes that are new since the round before,
/// so its cost depends on how many programs started meanwhile, not on the size of the library.
/// </summary>
public sealed class ProcessGames(IProcessTable processes, LaunchedGames launched)
{
    private readonly Dictionary<int, (string GameKey, DateTimeOffset? StartedAt)> _games = [];
    private HashSet<int> _seen = [];
    private GameFolders _folders = GameFolders.Empty;

    /// <summary>The games to look for; processes are looked at anew, since one of them may belong to a game now.</summary>
    public void Watch(GameFolders folders)
    {
        _folders = folders;
        _seen = [];
        _games.Clear();
    }

    /// <param name="startedFromArcadia">The games a process whose path can't be read may belong to.</param>
    /// <returns>The running games by key, each with the start of its earliest process if known.</returns>
    public IReadOnlyDictionary<string, DateTimeOffset?> Poll(IReadOnlySet<string> startedFromArcadia)
    {
        var current = new HashSet<int>(processes.Ids());
        foreach (var (id, game) in _games.ToList())
        {
            if (!current.Contains(id) || processes.Describe(id) is not { } details)
            {
                _games.Remove(id);
            }
            else if (details.StartedAt != game.StartedAt)
            {
                // Windows reuses ids: a process with the same id but another start is another program, looked at below.
                _games.Remove(id);
                _seen.Remove(id);
            }
        }

        foreach (var id in current)
        {
            if (!_seen.Contains(id) && !_games.ContainsKey(id) && processes.Describe(id) is { } details && GameOf(id, details, startedFromArcadia) is { } key)
            {
                _games[id] = (key, details.StartedAt);
            }
        }

        _seen = current;
        var running = new Dictionary<string, DateTimeOffset?>(StringComparer.Ordinal);
        foreach (var (key, startedAt) in _games.Values)
        {
            running[key] = running.TryGetValue(key, out var earlier) ? Earliest(earlier, startedAt) : startedAt;
        }

        return running;
    }

    private string? GameOf(int id, ProcessDetails details, IReadOnlySet<string> startedFromArcadia) =>
        details.Path is { } path
            ? _folders.GameOf(path)
            : launched.GameStartedBefore(id) is { } key && startedFromArcadia.Contains(key) ? key : null;

    private static DateTimeOffset? Earliest(DateTimeOffset? a, DateTimeOffset? b) =>
        a is { } x && b is { } y ? (x < y ? x : y) : a ?? b;
}
