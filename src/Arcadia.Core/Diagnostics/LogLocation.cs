namespace Arcadia.Core.Diagnostics;

/// <summary>
/// Where <c>arcadia.log</c> and <c>sisr.log</c> live: <c>%LOCALAPPDATA%\Arcadia-Logs</c>. Not inside
/// <c>%LOCALAPPDATA%\Arcadia</c>, which is Velopack's install folder: its setup takes any file there for an
/// installation ("bereits installiert") and deletes the folder when the user overwrites or repairs it, so a dev
/// build or the portable version that logged there made the setup misreport and lost the logs (owner, 2026-10-24).
/// </summary>
public static class LogLocation
{
    public const string FolderName = "Arcadia-Logs";

    public static string Directory { get; } = For(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static string For(string localAppData) => Path.Combine(localAppData, FolderName);

    /// <summary>
    /// Moves the logs of versions up to 0.1.0-rc.1 out of the install folder, and removes that folder if nothing
    /// else is in it, so the setup no longer sees an installation. Best effort: a file still open (a SISR that
    /// kept running across an update) stays and is tried again at the next start.
    /// </summary>
    public static void MoveOldLogs(string localAppData)
    {
        var installFolder = Path.Combine(localAppData, "Arcadia");
        var oldLogs = Path.Combine(installFolder, "logs");
        try
        {
            if (System.IO.Directory.Exists(oldLogs))
            {
                var logs = For(localAppData);
                if (System.IO.Directory.Exists(logs))
                {
                    System.IO.Directory.Delete(oldLogs, recursive: true);
                }
                else
                {
                    System.IO.Directory.Move(oldLogs, logs);
                }
            }

            if (System.IO.Directory.Exists(installFolder) && !System.IO.Directory.EnumerateFileSystemEntries(installFolder).Any())
            {
                System.IO.Directory.Delete(installFolder);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Nothing is lost: the old logs stay where they were until the next start.
        }
    }
}
