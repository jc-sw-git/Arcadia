namespace Arcadia.Launchers.Abstractions;

/// <summary>
/// The launcher's own program on this PC, for the parts of Arcadia that have to close and restart it: the Steam
/// Controller's drivers can't be installed while the launcher that owns Steam Input runs (<see cref="LauncherInfo.OwnsSteamInput"/>).
/// Every member is safe to call when the launcher isn't installed; only <see cref="RemoveShortcutsTo"/> throws, when a
/// list can't be written.
/// </summary>
public interface ILauncherProgram
{
    /// <summary>The launcher's folder on this PC, or null if it isn't installed.</summary>
    string? Folder { get; }

    /// <summary>The launcher's program runs.</summary>
    bool IsRunning { get; }

    /// <summary>The launcher is back after a restart: running, signed in and its window ready, not just its process.</summary>
    bool IsReady { get; }

    /// <summary>The launcher's game running right now, also one started outside Arcadia, or null.</summary>
    RunningLauncherGame? RunningGame();

    /// <summary>Asks the launcher to exit and waits for it, at most a timeout of its own.</summary>
    /// <returns>True when it no longer runs (or never ran), false if it is still running after the timeout.</returns>
    Task<bool> CloseAsync(TimeProvider time, CancellationToken cancellationToken = default);

    /// <summary>Starts the launcher in the background (minimised to the tray); does nothing if it isn't installed.</summary>
    void Start();

    /// <summary>
    /// How many of the launcher's accounts on this PC have an entry in their library that starts <paramref name="executable"/>
    /// (Steam's non-Steam games); an unreadable list counts as none. None for a launcher without such entries.
    /// </summary>
    int ShortcutsTo(string executable) => 0;

    /// <summary>
    /// Removes the entries that start <paramref name="executable"/> from every account's library. Only while the launcher
    /// doesn't run: it writes its entries back when it exits.
    /// </summary>
    /// <exception cref="IOException">A list couldn't be written.</exception>
    void RemoveShortcutsTo(string executable)
    {
    }
}

/// <summary>A game the launcher reports as running.</summary>
/// <param name="Id">The game's native id (<see cref="Game.Id"/>).</param>
/// <param name="Name">Its name as the launcher knows it, for messages.</param>
public sealed record RunningLauncherGame(string Id, string Name);
