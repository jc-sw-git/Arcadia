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
    /// Downloads the latest usbip-win2 installer into Arcadia's temp folder and returns its path. The SHA-256 from
    /// GitHub proves the download is what GitHub lists; Pan's <c>--install --usbip-installer</c> checks the signature
    /// (who built it, also if the release account was taken over) and runs it in the one elevated setup step (#243).
    /// The caller deletes the file.
    /// </summary>
    public static async Task<string> DownloadInstallerAsync(HttpClient http, CancellationToken cancellationToken = default)
    {
        var release = await new GitHubReleaseClient(http).GetLatestAsync("vadimgrn", "usbip-win2", cancellationToken)
            ?? throw new InvalidOperationException("Die aktuelle usbip-win2-Version konnte nicht ermittelt werden.");
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var asset = release.FindAsset(n => n.StartsWith("USBip-", StringComparison.OrdinalIgnoreCase)
                                           && n.EndsWith($"-{arch}.exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Kein usbip-win2-Installer für {arch} gefunden.");

        var installer = Path.Combine(Path.GetTempPath(), "Arcadia", asset.Name);
        await VerifiedDownload.DownloadAsync(http, asset, installer, cancellationToken);
        return installer;
    }
}
