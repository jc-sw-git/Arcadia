using System.Runtime.Versioning;

namespace Arcadia.Pan.Windows;

/// <summary>
/// <c>Pan.exe --restore</c>: the manual way out when the service hangs (review point 15). Undoes what the journal
/// lists, in this process, without the service. Runs elevated from Program Files.
/// </summary>
[SupportedOSPlatform("windows")]
public static class Restore
{
    public static async Task RunAsync(Action<string> log)
    {
        var journal = new JournalFile(PanPaths.JournalPath);
        var guardian = new Guardian(new UnbiasedClock(), journal, new WindowsCleanup(log), log);
        var before = journal.Load();
        if (!before.State.HasWork && before.Problem is null)
        {
            log("Nothing to restore: the journal lists no open emulation");
            return;
        }

        await guardian.RecoverAsync();
        log(journal.Load().State.HasWork ? "Restore incomplete; see the lines above" : "Restore complete");
    }
}
