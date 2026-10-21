using System.Diagnostics;
using Arcadia.Core.Library;
using Arcadia.Launchers;

namespace Arcadia.App.Services;

/// <summary>Whether a game of the library owns the foreground window, however it was started.</summary>
public static class ForegroundGame
{
    /// <summary>
    /// A process from a game's install folder owns the foreground window, or one whose path can't be
    /// read that started after Arcadia started a game (<see cref="LaunchedGames"/>); Arcadia itself never counts.
    /// </summary>
    public static bool IsInFront(GameFolders games, LaunchedGames launched) => InFront(games, launched) is not null;

    /// <summary>The <c>Game.Key</c> of the game in front as <see cref="IsInFront"/> tells it, or null.</summary>
    public static string? InFront(GameFolders games, LaunchedGames launched)
    {
        if (ProcessWatch.ForegroundProcessId() is not { } id || id == Environment.ProcessId)
        {
            return null;
        }

        var path = ProcessWatch.PathOf(id);
        return path is null ? launched.GameStartedBefore(id) : games.GameOf(path);
    }

    /// <summary>The ids of the running processes, for <see cref="LaunchedGames.Starting"/>.</summary>
    public static IReadOnlyList<int> RunningProcessIds()
    {
        var processes = Process.GetProcesses();
        var ids = new int[processes.Length];
        for (var i = 0; i < processes.Length; i++)
        {
            ids[i] = processes[i].Id;
            processes[i].Dispose();
        }

        return ids;
    }
}
