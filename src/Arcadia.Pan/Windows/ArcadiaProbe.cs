using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Arcadia.Pan.Windows;

/// <summary>
/// Looks for Arcadia on this PC for <see cref="SelfRemoval"/> (#422): every profile Windows lists under
/// <c>ProfileList</c> (other users' too; the service runs as SYSTEM), and the installs <c>--install</c> recorded.
/// Only reads attributes; in doubt the answer is <see cref="ArcadiaPresence.Unknown"/>, and Pan stays.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ArcadiaProbe
{
    private const string ProfileList = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList";

    public static ArcadiaPresence Look() =>
        ArcadiaInstallations.Look(Profiles(), new RecordedInstallsFile(Path.Combine(PanPaths.DataDirectory, RecordedInstallsFile.FileName)).Load(), Probe);

    /// <summary>The profiles' folders; null when the list can't be read.</summary>
    private static List<string>? Profiles()
    {
        try
        {
            using var list = Registry.LocalMachine.OpenSubKey(ProfileList);
            if (list is null)
            {
                return null;
            }

            var profiles = new List<string>();
            foreach (var sid in list.GetSubKeyNames())
            {
                using var profile = list.OpenSubKey(sid);
                if (profile?.GetValue("ProfileImagePath") is string folder && folder.Length > 0)
                {
                    profiles.Add(Environment.ExpandEnvironmentVariables(folder));
                }
            }

            return profiles;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// <see cref="File.Exists"/> says false also when it may not look; reading the attributes tells "not there" from
    /// "can't tell". A missing folder on a drive that isn't there (a profile on a detached disk) can't be told either.
    /// </summary>
    private static ArcadiaPresence Probe(string file)
    {
        try
        {
            if (!Path.IsPathFullyQualified(file))
            {
                return ArcadiaPresence.Absent;
            }

            File.GetAttributes(file);
            return ArcadiaPresence.Found;
        }
        catch (FileNotFoundException)
        {
            return ArcadiaPresence.Absent;
        }
        catch (DirectoryNotFoundException)
        {
            return Path.GetPathRoot(file) is { Length: > 0 } root && Directory.Exists(root) ? ArcadiaPresence.Absent : ArcadiaPresence.Unknown;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return ArcadiaPresence.Unknown;
        }
    }
}
