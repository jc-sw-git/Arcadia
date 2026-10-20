namespace Arcadia.Core.Library;

/// <summary>
/// Tells a game Arcadia started even when its executable's path can't be read: an anti-cheat may
/// refuse even the limited query right, and then <see cref="GameFolders"/> can't recognise it. A
/// process whose path can't be read and that wasn't running yet when Arcadia last started a game
/// counts as that game (owner, 2026-10-20). Other programs nearly always have a readable path, also
/// when they run as administrator, so this rarely takes one for a game.
/// </summary>
public sealed class LaunchedGames
{
    private readonly Lock _lock = new();
    private HashSet<int>? _runningBeforeStart;

    /// <summary>Call just before Arcadia starts a game, with the ids of the processes running then.</summary>
    public void Starting(IEnumerable<int> runningProcessIds)
    {
        var running = runningProcessIds.ToHashSet();
        lock (_lock)
        {
            _runningBeforeStart = running;
        }
    }

    /// <summary>Whether a process whose path couldn't be read started after Arcadia last started a game.</summary>
    public bool StartedSinceLastStart(int processId)
    {
        lock (_lock)
        {
            return _runningBeforeStart is { } before && !before.Contains(processId);
        }
    }
}
