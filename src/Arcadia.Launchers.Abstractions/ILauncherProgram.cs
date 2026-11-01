namespace Arcadia.Launchers.Abstractions;

/// <summary>
/// The launcher's own program on this PC, for the parts of Arcadia that have to close and restart it: the Steam
/// Controller's drivers can't be installed while the launcher that owns Steam Input runs (<see cref="LauncherInfo.OwnsSteamInput"/>).
/// Every member is safe to call when the launcher isn't installed; none throws.
/// </summary>
public interface ILauncherProgram
{
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
}

/// <summary>A game the launcher reports as running.</summary>
/// <param name="Id">The game's native id (<see cref="Game.Id"/>).</param>
/// <param name="Name">Its name as the launcher knows it, for messages.</param>
public sealed record RunningLauncherGame(string Id, string Name);
