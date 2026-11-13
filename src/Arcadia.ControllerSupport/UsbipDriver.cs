using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ServiceProcess;
using Arcadia.Pan;
using Microsoft.Win32;

namespace Arcadia.ControllerSupport;

/// <summary>
/// The usbip-win2 kernel driver. Arcadia's VIIPER attaches its virtual controllers through it.
/// </summary>
[SupportedOSPlatform("windows")]
public static class UsbipDriver
{
    /// <summary>The minimum version VIIPER's own installer requires.</summary>
    public static Version MinimumVersion => UsbipRelease.MinimumVersion;

    private const string ControllerService = "usbip2_ude";

    public static Version? InstalledVersion()
    {
        // Unreadable counts as not installed, as for HidHide (HidHideInstallation.InstalledVersion): a key a policy locks
        // or a driver file being replaced must not end the check that asks.
        try
        {
            foreach (var (displayVersion, _, _) in UninstallEntries())
            {
                if (Version.TryParse(displayVersion, out var version))
                {
                    return version;
                }
            }

            // Fallback: the version of the loaded driver file, wherever the driver store put it.
            if (DriverFile() is { } driver && FileVersionInfo.GetVersionInfo(driver).FileVersion is { } fileVersion
                && Version.TryParse(fileVersion.Split(' ')[0], out var parsed))
            {
                return parsed;
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>
    /// usbip-win2 was uninstalled and Windows removes its driver only at the next restart (#407): Windows marks such a
    /// service with <c>DeleteFlag</c>, as for HidHide (<see cref="HidHideWire.RemovalPending"/>).
    /// </summary>
    public static bool IsRemovalPending()
    {
        try
        {
            using var service = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{ControllerService}");
            return service is not null && HidHideWire.RemovalPending(service.GetValue("DeleteFlag") as int?, null, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>The command of usbip-win2's own uninstaller, or null if it isn't installed.</summary>
    public static string? UninstallCommand() =>
        UninstallEntries().Select(e => e.UninstallString).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

    private static IEnumerable<(string? DisplayVersion, string? UninstallString, string? InstallLocation)> UninstallEntries()
    {
        foreach (var root in new[] { @"Software\Microsoft\Windows\CurrentVersion\Uninstall", @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
        {
            using var uninstall = Registry.LocalMachine.OpenSubKey(root);
            if (uninstall is null)
            {
                continue;
            }

            foreach (var name in uninstall.GetSubKeyNames())
            {
                // The display name changed between releases ("USBip version 0.9.7.7", "USBip 0.9.8.1").
                using var entry = uninstall.OpenSubKey(name);
                if (entry?.GetValue("DisplayName") is string display
                    && display.StartsWith("USBip", StringComparison.OrdinalIgnoreCase))
                {
                    yield return (entry.GetValue("DisplayVersion") as string, entry.GetValue("UninstallString") as string, entry.GetValue("InstallLocation") as string);
                }
            }
        }
    }

    private static string? DriverFile()
    {
        using var service = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{ControllerService}");
        if (service?.GetValue("ImagePath") is not string imagePath)
        {
            return null;
        }

        // ImagePath looks like "\SystemRoot\System32\DriverStore\...\usbip2_ude.sys".
        var path = imagePath.Replace(@"\SystemRoot", Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase)
            .Replace(@"\??\", string.Empty, StringComparison.Ordinal);
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), path);
        }

        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Detaches the controllers VIIPER attached. VIIPER has no detach of its own: the driver drops a
    /// device when VIIPER's connection closes. This is the safety net for when Arcadia killed SISR's VIIPER
    /// (#214) and a device might be left behind. Returns how many
    /// were detached; never throws.
    /// </summary>
    public static async Task<int> DetachViiperDevicesAsync(CancellationToken cancellationToken = default)
    {
        if (CommandLineTool() is not { } usbip)
        {
            return 0;
        }

        var listing = await RunAsync(usbip, ["port"], cancellationToken);
        if (listing is null)
        {
            return 0;
        }

        var detached = 0;
        foreach (var port in UsbipPorts.FromLocalServer(listing, UsbipPorts.ViiperPort))
        {
            if (await RunAsync(usbip, ["detach", "-p", port.ToString(System.Globalization.CultureInfo.InvariantCulture)], cancellationToken) is not null)
            {
                detached++;
            }
        }

        return detached;
    }

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);

    /// <summary>usbip.exe from usbip-win2's install folder (default <c>C:\Program Files\USBip</c>), or null.</summary>
    private static string? CommandLineTool()
    {
        var folders = UninstallEntries().Select(e => e.InstallLocation)
            .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "USBip"));
        return folders.Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => Path.Combine(f!.Trim('"'), "usbip.exe"))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>Runs usbip.exe; its output, or null if it failed, timed out or exited with an error.</summary>
    private static async Task<string?> RunAsync(string usbip, string[] arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(usbip) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CommandTimeout);
        try
        {
            using var process = Process.Start(info);
            if (process is null)
            {
                return null;
            }

            try
            {
                var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                return process.ExitCode == 0 ? output : null;
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                return null;
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>True if the emulated host controller driver is loaded.</summary>
    public static bool IsRunning()
    {
        try
        {
            var devices = ServiceController.GetDevices();
            try
            {
                return devices.Any(d =>
                    string.Equals(d.ServiceName, ControllerService, StringComparison.OrdinalIgnoreCase)
                    && d.Status == ServiceControllerStatus.Running);
            }
            finally
            {
                foreach (var device in devices)
                {
                    device.Dispose();
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Downloads the latest usbip-win2 installer into <paramref name="folder"/> (the install run's, #413) and returns its
    /// path. The SHA-256 from GitHub proves the download is what GitHub lists; Pan, handed it after the prompt, checks the
    /// signature (who built it, also if the release account was taken over) and runs it (#243). The caller deletes the file.
    /// </summary>
    public static async Task<string> DownloadInstallerAsync(HttpClient http, string folder, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        var release = await new GitHubReleaseClient(http).GetLatestAsync("vadimgrn", "usbip-win2", cancellationToken)
            ?? throw new InvalidOperationException(ControllerSupportTexts.UsbipVersionUnknown);
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var asset = release.FindAsset(n => n.StartsWith("USBip-", StringComparison.OrdinalIgnoreCase)
                                           && n.EndsWith($"-{arch}.exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(ControllerSupportTexts.UsbipNoInstaller(arch));

        // The name is the release's: only a plain file name lands in the run's folder.
        var installer = Path.Combine(folder, Path.GetFileName(asset.Name));
        await VerifiedDownload.DownloadAsync(http, asset, installer, cancellationToken, progress);
        return installer;
    }
}
