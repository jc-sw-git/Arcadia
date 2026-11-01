namespace Arcadia.Core.Diagnostics;

/// <summary>
/// Where <c>arcadia.log</c> lives (and <c>sisr.log</c> of the SISR older versions started): <c>%APPDATA%\Arcadia\logs</c>, beside the settings, so all of
/// Arcadia's own files are in one folder that uninstalling offers to delete (owner, 2026-10-24). Never inside
/// <c>%LOCALAPPDATA%\Arcadia</c>, Velopack's install folder: its setup takes any file there for an installation
/// ("bereits installiert") and deletes the folder when the user overwrites or repairs it.
/// </summary>
public static class LogLocation
{
    /// <summary>Arcadia's own files: settings, chosen logos, Epic play times and the logs.</summary>
    public static string DataFolder { get; } = DataFolderIn(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public static string Directory { get; } = For(DataFolder);

    public static string DataFolderIn(string appData) => Path.Combine(appData, "Arcadia");

    public static string For(string dataFolder) => Path.Combine(dataFolder, "logs");

    /// <summary>
    /// Moves the logs of earlier versions here: up to 0.1.0-rc.1 from the install folder, for a short while on main
    /// from <c>%LOCALAPPDATA%\Arcadia-Logs</c>. An install folder that is empty afterwards goes too, so the setup no
    /// longer sees an installation. Best effort: a file still open (an SISR an older version kept running across an update)
    /// stays and is tried again at the next start.
    /// </summary>
    public static void MoveOldLogs(string localAppData, string dataFolder)
    {
        var installFolder = Path.Combine(localAppData, "Arcadia");
        try
        {
            MoveLogs(Path.Combine(installFolder, "logs"), For(dataFolder));
            MoveLogs(Path.Combine(localAppData, "Arcadia-Logs"), For(dataFolder));
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

    private static void MoveLogs(string from, string to)
    {
        if (!System.IO.Directory.Exists(from))
        {
            return;
        }

        if (System.IO.Directory.Exists(to))
        {
            // The current logs win; the old ones are from a version that ran before them.
            System.IO.Directory.Delete(from, recursive: true);
            return;
        }

        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        System.IO.Directory.Move(from, to);
    }
}
