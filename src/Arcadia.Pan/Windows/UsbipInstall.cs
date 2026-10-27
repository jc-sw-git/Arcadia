using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace Arcadia.Pan.Windows;

/// <summary>
/// Runs the usbip-win2 installer Arcadia downloaded, inside Pan's elevated <c>--install</c>, so the driver and the
/// service cost one UAC prompt together (#243). The elevated process copies the installer into Pan's own folder
/// (administrators only) and checks the signature of the copy: the original lies in the user's temp folder, and what
/// Arcadia checked before the prompt could be swapped at any time (review, should-fix 7).
/// </summary>
[SupportedOSPlatform("windows")]
public static class UsbipInstall
{
    private static TimeSpan InstallerWait => PanTimeouts.UsbipInstaller;

    /// <exception cref="UntrustedInstallerException">Not signed by usbip-win2's publisher; nothing was started.</exception>
    /// <exception cref="TimeoutException">The setup still runs after the wait; it is left running, as killing an installer mid-driver-install could leave it half set up.</exception>
    public static async Task RunAsync(string installer, string expectedSha256, Action<string> log)
    {
        if (!File.Exists(installer))
        {
            throw new FileNotFoundException("the usbip-win2 installer is missing", installer);
        }

        var copy = Path.Combine(PanPaths.DataDirectory, "usbip-installer.exe");
        File.Copy(installer, copy, overwrite: true);
        var stillRunning = false;
        try
        {
            await RunCopyAsync(copy, expectedSha256, log);
        }
        catch (TimeoutException)
        {
            stillRunning = true;
            throw;
        }
        finally
        {
            // A setup that still runs has the file open; it is overwritten at the next install.
            if (!stillRunning)
            {
                File.Delete(copy);
            }
        }
    }

    private static async Task RunCopyAsync(string installer, string expectedSha256, Action<string> log)
    {
        await InstallerSignature.RunIfTrustedAsync(installer, "Der usbip-win2-Installer", UsbipRelease.Publisher, UsbipRelease.ManualInstallUrl,
            new WinTrustAuthenticode(), async () =>
            {
                log($"usbip-win2 installer signed by {UsbipRelease.Publisher.Organization}, SHA-256 as downloaded; running {installer} {string.Join(' ', UsbipRelease.SilentArguments)}");
                var start = new ProcessStartInfo(installer) { UseShellExecute = false };
                foreach (var argument in UsbipRelease.SilentArguments)
                {
                    start.ArgumentList.Add(argument);
                }

                using var process = Process.Start(start) ?? throw new InvalidOperationException("the usbip-win2 installer couldn't be started");
                using var timeout = new CancellationTokenSource(InstallerWait);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException($"the usbip-win2 installer didn't finish within {InstallerWait.TotalMinutes:0} min; it was left running");
                }

                switch (process.ExitCode)
                {
                    case 0:
                        log("usbip-win2 installed");
                        break;
                    case UsbipRelease.RestartNeededExitCode:
                        log("usbip-win2 installed; Windows wants a restart");
                        break;
                    default:
                        throw new Win32Exception(process.ExitCode, $"the usbip-win2 installer reported error {process.ExitCode}");
                }

                return true;
            }, expectedSha256);
    }
}
