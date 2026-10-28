namespace Arcadia.Pan;

/// <summary>Where Pan lives and writes (#243, review point 10): never a user-writable folder.</summary>
public static class PanPaths
{
    public const string ServiceName = "ArcadiaPan";
    public const string DisplayName = "Pan - Arcadias Guardian";
    public const string PipeName = "ArcadiaPan";
    public const string ExecutableName = "Pan.exe";

    /// <summary><c>%ProgramFiles%\Arcadia</c>: writable only by administrators; Pan's own folders are inside.</summary>
    public static string ArcadiaProgramFiles => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Arcadia");

    /// <summary><c>%ProgramFiles%\Arcadia\Pan</c>: writable only by administrators.</summary>
    public static string InstallDirectory => Path.Combine(ArcadiaProgramFiles, "Pan");

    /// <summary><c>%ProgramFiles%\Arcadia\Pan-Uninstall</c>: the copy <c>--uninstall</c> hands off to (#270).</summary>
    public static string HandOffDirectory => Path.Combine(ArcadiaProgramFiles, UninstallHandOff.DirectoryName);

    public static string InstalledExecutable => Path.Combine(InstallDirectory, ExecutableName);

    /// <summary><c>%ProgramData%\Arcadia\Pan</c>: journal and log, administrators and SYSTEM only.</summary>
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Arcadia", "Pan");

    public static string JournalPath => Path.Combine(DataDirectory, "journal.json");

    public static string LogPath => Path.Combine(DataDirectory, "pan.log");
}
