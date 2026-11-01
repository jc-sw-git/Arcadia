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
        return ProcessWatch.ForegroundProcessId() is { } id ? GameOf(id, ProcessWatch.PathOf(id), games, launched) : null;
    }

    /// <summary>The <c>Game.Key</c> of the game process <paramref name="processId"/> at <paramref name="path"/> belongs to, as <see cref="InFront"/> tells it.</summary>
    public static string? GameOf(int processId, string? path, GameFolders games, LaunchedGames launched)
    {
        if (processId == Environment.ProcessId)
        {
            return null;
        }

        return path is null ? launched.GameStartedBefore(processId) : games.GameOf(path);
    }

    /// <summary>
    /// What is in front a moment after <paramref name="gameKey"/>, started from Arcadia, ended (#94): only
    /// <see cref="ForegroundAfterGame.Other"/> keeps Arcadia behind. <paramref name="launcherPrograms"/> are the file
    /// names of the game's launcher (<c>ILauncherIntegration.ProgramNames</c>).
    /// </summary>
    public static ForegroundAfterGame InFrontAfter(string gameKey, GameFolders games, LaunchedGames launched, IReadOnlyCollection<string> launcherPrograms)
    {
        if (ProcessWatch.ForegroundProcessId() is not { } id)
        {
            return ForegroundAfterGame.Nothing;
        }

        if (id == Environment.ProcessId)
        {
            return ForegroundAfterGame.Arcadia;
        }

        if (ForegroundGuard.IsDesktopInFront())
        {
            return ForegroundAfterGame.Desktop;
        }

        var path = ProcessWatch.PathOf(id);
        if ((path is null ? launched.GameStartedBefore(id) : games.GameOf(path)) == gameKey)
        {
            return ForegroundAfterGame.TheGame;
        }

        return path is not null && launcherPrograms.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            ? ForegroundAfterGame.ItsLauncher
            : ForegroundAfterGame.Other;
    }
}

/// <summary>What is in front when a game started from Arcadia ended (#94).</summary>
public enum ForegroundAfterGame
{
    /// <summary>No window, e.g. right after the game's window closed.</summary>
    Nothing,

    Arcadia,

    Desktop,

    /// <summary>A window of the game that ended, e.g. its crash reporter.</summary>
    TheGame,

    /// <summary>A program of the game's launcher, e.g. Epic asking to rate the game.</summary>
    ItsLauncher,

    /// <summary>Another program, which the user switched to during the game; it keeps the front.</summary>
    Other,
}
