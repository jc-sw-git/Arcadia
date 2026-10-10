namespace Arcadia.Core.Launchers;

/// <summary>Where a running installation stands, as far as the launcher reveals it.</summary>
public enum InstallPhase
{
    /// <summary>Arcadia opened the launcher's install dialog; the user hasn't confirmed it yet.</summary>
    AwaitingConfirmation,

    /// <summary>Waiting in the launcher's download queue behind other installations.</summary>
    Queued,

    Downloading,

    Paused,

    /// <summary>All data is there; the launcher moves and verifies files.</summary>
    Finishing,

    /// <summary>The launcher is removing the game.</summary>
    Uninstalling,
}

/// <param name="Fraction">0…1, or null when the launcher doesn't reveal the size.</param>
/// <param name="QueuePosition">1-based position in the launcher's queue; only for <see cref="InstallPhase.Queued"/>.</param>
public sealed record InstallProgress(InstallPhase Phase, double? Fraction = null, int? QueuePosition = null);

public enum InstallOutcome
{
    Completed,
    Cancelled,
    Failed,

    /// <summary>The game was uninstalled in the launcher.</summary>
    Uninstalled,
}

/// <param name="Active">Running installations by <see cref="Models.Game.Id"/>.</param>
/// <param name="Finished">Installations that ended since the previous poll, by game id.</param>
public sealed record InstallSnapshot(IReadOnlyDictionary<string, InstallProgress> Active, IReadOnlyDictionary<string, InstallOutcome> Finished)
{
    public static InstallSnapshot Empty { get; } = new(new Dictionary<string, InstallProgress>(), new Dictionary<string, InstallOutcome>());
}

/// <summary>
/// Watches the installations of one launcher — also the ones started in the launcher itself.
/// <see cref="Poll"/> is called periodically from a background thread.
/// </summary>
public interface IInstallMonitor
{
    string LauncherId { get; }

    InstallSnapshot Poll();
}
