namespace Arcadia.Launchers.Abstractions;

/// <summary>
/// Everything Arcadia needs from one launcher, registered once. Built-in launchers implement it
/// exactly as a plugin would; there is no other way for a launcher into Arcadia.
/// </summary>
public interface ILauncherIntegration
{
    /// <summary>
    /// Identity of the launcher; <see cref="Scanner"/>, <see cref="Client"/>, <see cref="InstallMonitor"/> and
    /// <see cref="RunningGames"/> belong to it.
    /// </summary>
    LauncherInfo Launcher { get; }

    ILauncherScanner Scanner { get; }

    /// <summary>Starts the launcher's games, or null if they are started as they are (<see cref="Game.Launch"/>).</summary>
    ILauncherClient? Client { get; }

    /// <summary>Follows installations in the launcher, or null if Arcadia can't watch them.</summary>
    IInstallMonitor? InstallMonitor { get; }

    /// <summary>
    /// Reports the launcher's running games, or null if the launcher doesn't tell; Arcadia then looks for processes
    /// from the games' install folders.
    /// </summary>
    IRunningGamesMonitor? RunningGames { get; }

    /// <summary>
    /// The file names of the launcher's own programs, e.g. <c>steam.exe</c>. When a game started from Arcadia ends and
    /// one of them is in front (say, the launcher asks to rate the game), Arcadia comes to the front over it.
    /// </summary>
    IReadOnlyCollection<string> ProgramNames { get; }

    /// <summary>
    /// Looks for the launcher on this PC, for the launcher selection on first start. May read the
    /// registry or disk; call it off the UI thread.
    /// </summary>
    /// <returns>Where the launcher was found, as shown to the user, or null if it isn't installed.</returns>
    string? Locate();

    /// <summary>Lets the user pick the launcher's folder when <see cref="Locate"/> doesn't find it; null if it can't (#138).</summary>
    ILauncherFolderChoice? FolderChoice => null;
}
