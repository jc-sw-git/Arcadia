using System.Runtime.Versioning;

namespace Arcadia.Pan.Windows;

/// <summary>
/// <c>Pan.exe --restore</c>: the manual way out when the service hangs (review point 15). Undoes what the journal
/// lists, in this process, without the service. Runs elevated from Program Files. Returns whether nothing is left to
/// undo; otherwise the journal keeps what failed, for the next attempt.
/// </summary>
[SupportedOSPlatform("windows")]
public static class Restore
{
    public static async Task<bool> RunAsync(Action<string> log)
    {
        var journal = new JournalFile(PanPaths.JournalPath);
        var guardian = new Guardian(new UnbiasedClock(), journal, new WindowsCleanup(log), log);
        // Recovery loads the journal itself; a fallback it had to use is the journal from then on, so this look doesn't
        // cost the recovery its state (review point 3).
        var before = journal.Load();
        if (!before.State.HasWork && before.Problem is null)
        {
            log("Nothing to restore: the journal lists no open emulation");
            return true;
        }

        var complete = await guardian.RecoverAsync();
        log(complete ? "Restore complete" : $"Restore incomplete; see the lines above. What is left stays in {journal.Path} for the next run");
        return complete;
    }
}
