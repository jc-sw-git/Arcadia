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
    /// The games it reports don't really run on this PC (Arcadia's demo data). They show "Läuft", but nothing waits for
    /// their end: they can't be ended, so they never keep Pan's switch locked (#307).
    /// </summary>
    bool ReportsSimulatedGames => false;

    IReadOnlyList<RunningGame> Poll();
}
