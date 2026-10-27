using System.Diagnostics;
using System.Runtime.Versioning;

namespace Arcadia.Pan.Windows;

/// <summary>
/// Runs the HidHide installer Arcadia downloaded, inside Pan's elevated <c>--install</c>, so HidHide costs no UAC
/// prompt of its own (#243 part B2). As for usbip-win2 (<see cref="UsbipInstall"/>), the signature is checked on a
/// copy in Pan's data folder, which only administrators can change.
/// </summary>
[SupportedOSPlatform("windows")]
public static class HidHideInstall
{
    private static TimeSpan PuckRestartWait => PanTimeouts.PuckRestart;

    /// <exception cref="UntrustedInstallerException">Not the pinned installer or not signed by HidHide's publisher; nothing was started.</exception>
    /// <exception cref="InvalidOperationException">The setup failed; the message says how.</exception>
    public static async Task RunAsync(string installer, Action<string> log)
    {
        if (!File.Exists(installer))
        {
            throw new FileNotFoundException("the HidHide installer is missing", installer);
        }

        var copy = Path.Combine(PanPaths.DataDirectory, "hidhide-installer.exe");
        File.Copy(installer, copy, overwrite: true);
        var stillRunning = false;
        try
        {
            stillRunning = await RunCopyAsync(copy, log);
        }
        finally
        {
            // A setup that still runs has the file open; it is overwritten at the next install.
            if (!stillRunning)
            {
                File.Delete(copy);
            }
        }

        await RestartPuckAsync(log);
    }

    /// <returns>True if the setup was left running (the driver is in place, its last step hangs).</returns>
    private static Task<bool> RunCopyAsync(string installer, Action<string> log) =>
        InstallerSignature.RunIfTrustedAsync(installer, "Der HidHide-Installer", HidHideRelease.Publisher, HidHideRelease.ManualInstallUrl,
            new WinTrustAuthenticode(), async () =>
            {
                log($"HidHide installer signed by {HidHideRelease.Publisher.Organization}, SHA-256 as pinned; running {installer} {string.Join(' ', HidHideRelease.SilentArguments)}");
                var start = new ProcessStartInfo(installer) { UseShellExecute = false };
                foreach (var argument in HidHideRelease.SilentArguments)
                {
                    start.ArgumentList.Add(argument);
                }

                using var process = Process.Start(start) ?? throw new InvalidOperationException("the HidHide installer couldn't be started");
                int? exitCode;
                using (var timeout = new CancellationTokenSource(HidHideSetup.Wait))
                {
                    try
                    {
                        await process.WaitForExitAsync(timeout.Token);
                        exitCode = process.ExitCode;
                    }
                    catch (OperationCanceledException)
                    {
                        exitCode = null;
                    }
                }

                var version = new HidHideDriver().InstalledVersion();
                switch (HidHideSetup.Judge(exitCode, version))
                {
                    case HidHideSetupOutcome.Installed:
                        log($"HidHide {version} installed");
                        return false;
                    case HidHideSetupOutcome.InstalledRestartPending:
                        log($"HidHide {version} installed; Windows wants a restart");
                        return false;
                    case HidHideSetupOutcome.InstalledSetupStillRunning:
                        log($"HidHide {version} is in place, but its setup still runs after {HidHideSetup.Wait.TotalMinutes:0} min (its last step hangs on some PCs); left running");
                        return true;
                    default:
                        throw new InvalidOperationException(exitCode is { } code
                            ? $"the HidHide installer reported error {code} (HidHide now: {version?.ToString() ?? "missing"})"
                            : $"the HidHide installer didn't finish within {HidHideSetup.Wait.TotalMinutes:0} min and HidHide isn't in place; it was left running");
                }
            }, HidHideRelease.Sha256);

    /// <summary>
    /// HidHide filters a HID device from its next start on: restarting a connected Steam Controller loads the filter
    /// for it (and with it HidHide's control device) without a reboot. Best effort, bounded.
    /// </summary>
    private static async Task RestartPuckAsync(Action<string> log)
    {
        try
        {
            await Task.Run(() => new PuckDevices().RestartAsync(CancellationToken.None)).WaitAsync(PuckRestartWait);
            log("Steam Controller restarted for HidHide's filter (if connected)");
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            log($"Steam Controller not restarted after HidHide's setup: {ex.Message}; HidHide filters it from its next start");
        }
    }
}
