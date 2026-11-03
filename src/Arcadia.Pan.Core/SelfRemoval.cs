namespace Arcadia.Pan;

/// <summary>
/// At the service's start (#422): whether Pan goes on serving Arcadia or finishes an uninstall. It finishes one that
/// is pending (<see cref="PendingUninstall"/>), and removes itself when no Arcadia is left on the PC, but only once
/// the journal is empty (everything given back) and Arcadia is absent at two looks <see cref="SecondLookDelay"/> apart,
/// so a Velopack update that swaps Arcadia's folder at that moment can't make Pan go. Any Arcadia found cancels a
/// pending uninstall: the player installed it again.
/// </summary>
public static class SelfRemoval
{
    public static readonly TimeSpan SecondLookDelay = TimeSpan.FromSeconds(10);

    /// <param name="undoComplete">The start's recovery left nothing in the journal.</param>
    /// <param name="look">Looks for Arcadia (<see cref="ArcadiaInstallations.Look"/>).</param>
    /// <param name="wait">Waits <see cref="SecondLookDelay"/>.</param>
    /// <param name="clientCame">A client connected meanwhile: an Arcadia Pan doesn't know (a build that moved), so Pan stays.</param>
    /// <returns>True when Pan is to finish the uninstall (<see cref="UninstallOrder"/>) instead of serving.</returns>
    public static async Task<bool> ShouldFinishAsync(
        bool undoComplete, IPendingUninstallStore marker, Func<ArcadiaPresence> look, Func<Task> wait, Func<bool> clientCame, Action<string> log)
    {
        var pending = marker.Load();
        if (!StillAbsent(look(), pending is not null, marker, log))
        {
            return false;
        }

        if (!undoComplete)
        {
            log(pending is not null
                ? "Uninstall pending, but the journal still lists something: trying again at the next start"
                : "No Arcadia found, but the journal still lists something: Pan stays until it is undone");
            return false;
        }

        await wait();
        if (clientCame())
        {
            if (pending is not null && marker.Delete())
            {
                log("A client connected: the pending uninstall is cancelled");
            }
            else
            {
                log("No Arcadia found, but a client connected: Pan stays");
            }

            return false;
        }

        if (!StillAbsent(look(), pending is not null, marker, log))
        {
            return false;
        }

        log(pending is not null
            ? $"Finishing the pending uninstall ({(pending.RemoveDrivers ? "with" : "without")} the drivers)"
            : "No Arcadia installation found: Pan removes itself; the drivers stay");
        return true;
    }

    private static bool StillAbsent(ArcadiaPresence presence, bool pending, IPendingUninstallStore marker, Action<string> log)
    {
        switch (presence)
        {
            case ArcadiaPresence.Found:
                if (pending && marker.Delete())
                {
                    log("Arcadia is installed again: the pending uninstall is cancelled");
                }

                return false;
            case ArcadiaPresence.Unknown:
                log("Pan can't tell whether Arcadia is still installed (a place it may be can't be read): Pan stays");
                return false;
            default:
                return true;
        }
    }
}
