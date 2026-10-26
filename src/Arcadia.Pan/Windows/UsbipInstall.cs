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
    private static readonly TimeSpan InstallerWait = TimeSpan.FromMinutes(5);

    /// <exception cref="UntrustedInstallerException">Not signed by usbip-win2's publisher; nothing was started.</exception>
    public static async Task RunAsync(string installer, Action<string> log)
    {
        if (!File.Exists(installer))
        {
            throw new FileNotFoundException("the usbip-win2 installer is missing", installer);
        }

        var copy = Path.Combine(PanPaths.DataDirectory, "usbip-installer.exe");
        File.Copy(installer, copy, overwrite: true);
        try
        {
            await RunCopyAsync(copy, log);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    private static async Task RunCopyAsync(string installer, Action<string> log)
    {
        await InstallerSignature.RunIfTrustedAsync(installer, "Der usbip-win2-Installer", UsbipRelease.Publisher, UsbipRelease.ManualInstallUrl,
            new WinTrustAuthenticode(), async () =>
            {
                log($"usbip-win2 installer signed by {UsbipRelease.Publisher.Organization}; running {installer} /S");
                using var process = Process.Start(new ProcessStartInfo(installer, "/S") { UseShellExecute = false })
                    ?? throw new InvalidOperationException("the usbip-win2 installer couldn't be started");
                using var timeout = new CancellationTokenSource(InstallerWait);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    process.Kill();
                    throw new TimeoutException($"the usbip-win2 installer didn't finish within {InstallerWait.TotalMinutes:0} min");
                }

                if (process.ExitCode != 0)
                {
                    throw new Win32Exception(process.ExitCode, $"the usbip-win2 installer reported error {process.ExitCode}");
                }

                log("usbip-win2 installed");
                return true;
            });
    }
}
