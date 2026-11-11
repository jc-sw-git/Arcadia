using System.Runtime.Versioning;

namespace Arcadia.Pan.Windows;

/// <summary>
/// <c>Pan.exe --restore [--run id --client pid]</c>. Started by Arcadia with a run (#471), Pan waits for Arcadia's
/// <c>go</c> before it restores: Arcadia lets go of Pan only once the Windows prompt was accepted, and a declined prompt
/// starts no Pan at all. <c>abort</c>, Arcadia's end, a broken pipe or no <c>go</c> within
/// <see cref="PanTimeouts.RestoreHandOver"/> end Pan with exit 9 and nothing restored. By hand, without a run, it
/// restores at once.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class RestoreCommand
{
    public static int Run(string[] args, PanLog log)
    {
        // Read before anything is touched: a command line Pan doesn't understand changes nothing (exit 3).
        var run = RestoreRun.Parse(args);
        if (ServiceSetup.CheckDataDirectory() is { } problem)
        {
            Console.Error.WriteLine($"Not restoring: {problem}; run Pan.exe --install again.");
            return Program.FailedExitCode;
        }

        log.TrustFolder();
        if (run is not null)
        {
            log.Write($"Waiting for Arcadia's go to restore (run {run.Id})");
            var handOver = HandOverPipe.WaitFor(RestoreHandOver.PipeName(run.Id), run.ClientProcessId, PanTimeouts.RestoreHandOver, RestoreHandOver.Read, log.Write);
            if (handOver.End == HandOverEnd.Refused)
            {
                log.Write($"Go refused: {handOver.Reason}; nothing restored");
                return Program.FailedExitCode;
            }

            if (handOver.End != HandOverEnd.Go)
            {
                log.Write($"Ended before Arcadia's go: {HandOverPipe.Why(handOver.End)}; nothing restored");
                return InstallHandOver.EndedExitCode;
            }

            log.Write($"Go received (run {run.Id})");
        }

        return Restore.RunAsync(log.Write).GetAwaiter().GetResult() ? 0 : Program.RestoreIncompleteExitCode;
    }
}
