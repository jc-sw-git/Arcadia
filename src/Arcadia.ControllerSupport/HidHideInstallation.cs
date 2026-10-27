using System.Diagnostics;
using System.Runtime.Versioning;
using Arcadia.Pan;
using Microsoft.Win32;

namespace Arcadia.ControllerSupport;

/// <summary>
/// HidHide as Arcadia sees it without administrator rights (#243 part B2): installed or not, which version, and the
/// pinned installer "Treiber installieren" hands to Pan. Whether its driver runs and how it is set (inverse mode,
/// cloaking) only Pan can read; Arcadia asks it (<see cref="Pan.IHidingCheck"/>).
/// </summary>
[SupportedOSPlatform("windows")]
public static class HidHideInstallation
{
    private const string ServiceKey = @"SYSTEM\CurrentControlSet\Services\HidHide";
    private const string SessionManagerKey = @"SYSTEM\CurrentControlSet\Control\Session Manager";

    /// <summary>The version of HidHide's driver file, or null if HidHide isn't installed.</summary>
    public static Version? InstalledVersion()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ServiceKey);
            if (key?.GetValue("ImagePath") is not string imagePath
                || HidHideWire.DriverFile(imagePath, Environment.GetFolderPath(Environment.SpecialFolder.Windows)) is not { } file
                || !File.Exists(file))
            {
                return null;
            }

            var info = FileVersionInfo.GetVersionInfo(file);
            return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>HidHide was uninstalled and goes at the next restart: its version still reads as installed until then.</summary>
    public static bool IsRemovalPending()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ServiceKey);
            if (key is null)
            {
                return false;
            }

            using var sessionManager = Registry.LocalMachine.OpenSubKey(SessionManagerKey);
            var driverFile = key.GetValue("ImagePath") is string imagePath
                ? HidHideWire.DriverFile(imagePath, Environment.GetFolderPath(Environment.SpecialFolder.Windows))
                : null;
            return HidHideWire.RemovalPending(key.GetValue("DeleteFlag") as int?, sessionManager?.GetValue("PendingFileRenameOperations") as string[], driverFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>
    /// Downloads the pinned HidHide installer into Arcadia's temp folder and checks its SHA-256; Pan checks the
    /// signature of its own copy before it runs it.
    /// </summary>
    public static async Task<string> DownloadInstallerAsync(HttpClient http, CancellationToken cancellationToken)
    {
        var installer = Path.Combine(Path.GetTempPath(), "Arcadia", HidHideRelease.InstallerName);
        await VerifiedDownload.DownloadAsync(
            http,
            new ReleaseAsset(HidHideRelease.InstallerName, HidHideRelease.InstallerUrl, HidHideRelease.InstallerSize, HidHideRelease.Sha256),
            installer,
            cancellationToken);
        return installer;
    }
}
