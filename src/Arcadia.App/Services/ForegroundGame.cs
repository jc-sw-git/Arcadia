using Arcadia.Core.Library;
using Arcadia.Launchers;

namespace Arcadia.App.Services;

/// <summary>Whether a game of the library owns the foreground window, however it was started.</summary>
public static class ForegroundGame
{
    /// <summary>A process from a game's install folder owns the foreground window; Arcadia itself never counts.</summary>
    public static bool IsInFront(GameFolders games) =>
        ProcessWatch.ForegroundProcessId() is { } id
        && id != Environment.ProcessId
        && games.Contain(ProcessWatch.PathOf(id));
}
