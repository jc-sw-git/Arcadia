using System.Runtime.Versioning;

namespace Arcadia.Pan.Windows;

/// <summary>
/// <c>Pan.exe --install</c>: secures the data folder, waits for Arcadia's hand-over when the command line announces a run
/// (#413: the Windows prompt comes first, the installers after it), then installs usbip-win2, HidHide and Pan in this
/// order and reports each step. Nothing on the machine changes before the hand-over, so a run that ends there (exit 9)
/// leaves only <c>pan.log</c> lines. Without a run (Pan's update, #326) Pan alone is installed at once.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class InstallCommand
{
    public static int Run(string[] args, PanLog log, string ownPath)
    {
        // Read before anything is touched: a command line Pan doesn't understand changes nothing (exit 3).
        var run = InstallRun.Parse(args);

        // The log lives in the data folder: secured before anything is written there (review, bug 1). A failure here
        // can't be logged, so its own exit code tells Arcadia what to say.
        IReadOnlyList<string> setAside;
        try
        {
            setAside = ServiceSetup.SecureDataDirectory();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine($"The data folder could not be secured: {ex.Message}");
            return Program.DataFolderExitCode;
        }

        log.TrustFolder();
        foreach (var folder in setAside)
        {
            log.Write($"Data folder not trusted: {folder}; made anew, its journal not used");
        }

        HandedOverInstallers? installers = null;
        if (run is not null)
        {
            var handOver = WaitForHandOver(run, log);
            if (handOver.End == HandOverEnd.Refused)
            {
                log.Write($"Hand-over refused: {handOver.Reason}; nothing installed");
                return Program.FailedExitCode;
            }

            if (handOver.End != HandOverEnd.Go)
            {
                log.Write($"Ended before the hand-over: {Why(handOver.End)}; nothing changed");
                return InstallHandOver.EndedExitCode;
            }

            log.Write($"Hand-over received (run {run.Id})");
            installers = handOver.Installers;
        }

        // The player wants Arcadia again: an uninstall still pending goes before anything is installed (#422).
        if (PanUninstall.Marker.Delete())
        {
            log.Write("Pending uninstall cancelled: Pan is being installed again");
        }

        if (ArcadiaInstallations.BesideBundledPan(ownPath) is { } arcadia && File.Exists(arcadia))
        {
            PanUninstall.RecordArcadia(arcadia, log.Write);
        }

        UninstallHandOff.RemoveLeftover(ownPath, PanPaths.HandOffDirectory, log.Write);

        var report = run is null ? null : new InstallReportWriter(PanPaths.DataDirectory, run.Id, log.Write);
        if (installers?.Usbip is { } usbip)
        {
            Step(report, PanInstallStep.Usbip, () => UsbipInstall.RunAsync(usbip, installers.UsbipSha256!, log.Write).GetAwaiter().GetResult());
        }

        // HidHide matters only for showing Steam a copy of the puck (#242); a failed HidHide must not cost the player Pan
        // and with it the Xbox pad, so Pan is set up anyway and the exit code says so.
        var hidHideFailed = false;
        if (installers?.HidHide is { } hidHide)
        {
            try
            {
                Step(report, PanInstallStep.HidHide, () => HidHideInstall.RunAsync(hidHide, run!.HidHideRepair, log.Write).GetAwaiter().GetResult());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                log.Write($"HidHide not installed: {ex.Message}");
                Console.Error.WriteLine(ex.Message);
                hidHideFailed = true;
            }
        }

        Step(report, PanInstallStep.Pan, () => ServiceSetup.Install(ownPath, Option(args, "--expect-hash"), log.Write));
        return hidHideFailed ? Program.HidHideFailedExitCode : 0;
    }

    private static HandOverResult WaitForHandOver(InstallRun run, PanLog log)
    {
        log.Write($"Waiting for Arcadia's hand-over (run {run.Id})");
        // Closed once the wait ends, whatever its end: the pipe carries only this one message.
        using var pipe = InstallHandOverPipe.Create(run, log.Write, out var failure);
        if (pipe is null)
        {
            return new HandOverResult(failure ?? HandOverEnd.PipeTaken);
        }

        return InstallHandOver.WaitAsync(pipe, run, PanTimeouts.HandOver, TimeProvider.System, IsReparsePoint).GetAwaiter().GetResult();
    }

    private static void Step(InstallReportWriter? report, PanInstallStep step, Action work)
    {
        report?.Write(step, PanStepState.Running);
        try
        {
            work();
        }
        catch
        {
            report?.Write(step, PanStepState.Failed);
            throw;
        }

        report?.Write(step, PanStepState.Done);
    }

    private static string Why(HandOverEnd end) => end switch
    {
        HandOverEnd.Abort => "abort",
        HandOverEnd.ArcadiaGone => "Arcadia gone",
        HandOverEnd.Timeout => "timeout",
        _ => "pipe exists",
    };

    private static bool IsReparsePoint(string path) =>
        (File.Exists(path) || Directory.Exists(path)) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);

    private static string? Option(string[] args, string name)
    {
        for (var i = 1; i + 1 < args.Length; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
