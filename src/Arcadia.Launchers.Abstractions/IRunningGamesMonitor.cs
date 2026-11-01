namespace Arcadia.Launchers.Abstractions;

/// <summary>A game its launcher reports as running.</summary>
/// <param name="GameId">The <see cref="Game.Id"/>.</param>
/// <param name="StartedAt">When it started, or null if the launcher doesn't tell.</param>
public sealed record RunningGame(string GameId, DateTimeOffset? StartedAt);

/// <summary>
/// Tells which games of one launcher run, as the launcher itself reports it, also the ones started in the launcher.
/// Arcadia finds the games of a launcher without one through the processes from their install folders.
/// <see cref="Poll"/> is called from a background thread every few seconds while Arcadia is in front, and in the
/// background while a game started from Arcadia runs, so it should only read what changed since the last call.
/// </summary>
public interface IRunningGamesMonitor
{
    string LauncherId { get; }

    /// <summary>
    /// The games it reports don't really run on this PC (Arcadia's demo data). They show "Läuft" on their tile and
    /// nothing else: they can't be ended, so they must never lock Pan's switch or steer the Steam Controller (#307).
    /// </summary>
    bool ReportsSimulatedGames => false;

    /// <summary>
    /// The games running now. Arcadia waits a few seconds at most: a poll that throws or is late keeps the games it
    /// reported before, and a late one isn't called again until it returns; its answer is used then.
    /// </summary>
    /// <param name="cancellationToken">Ends at Arcadia's deadline. Stop only where the monitor's own state stays whole,
    /// e.g. before changing it; a canceled poll counts as failed.</param>
    IReadOnlyList<RunningGame> Poll(CancellationToken cancellationToken = default);

    /// <summary>
    /// The starts the launcher aborted since the last call (#277), asked right after <see cref="Poll"/> on the same thread.
    /// Only an explicit abort counts, never a missing signal: a start nothing is heard of ends after Arcadia's own timeout.
    /// </summary>
    IReadOnlyList<RefusedStart> RefusedStarts() => [];
}
