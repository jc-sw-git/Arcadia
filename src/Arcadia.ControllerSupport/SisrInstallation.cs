using Arcadia.Core.Diagnostics;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.ControllerSupport;

/// <summary>What of SISR is on the PC, for the group "SISR" in the controller settings (#214).</summary>
/// <param name="Program">SISR's folder <c>%LOCALAPPDATA%\SISR</c> exists.</param>
/// <param name="MarkerAccounts">Steam accounts with a shortcut to exactly that folder's <c>SISR.exe</c>.</param>
/// <param name="CefFlag">Steam's <c>.cef-enable-remote-debugging</c>, which SISR needs to read the layout.</param>
public sealed record SisrFound(bool Program, int MarkerAccounts, bool CefFlag)
{
    public bool HasMarker => MarkerAccounts > 0;

    /// <summary>
    /// Arcadia can't tell an SISR it installed from one the user installed, so the group shows for either; the CEF flag
    /// alone doesn't count: other programs set it too.
    /// </summary>
    public bool ShowsGroup => Program || HasMarker;
}

/// <summary>A running SISR or VIIPER; path and command line are null when Windows doesn't tell (elevated, just gone).</summary>
public sealed record SisrProcess(int Id, string? ExecutablePath, string? CommandLine);

/// <summary>What <see cref="SisrInstallation.Remove"/> left and why; empty when everything went.</summary>
public sealed record SisrRemoval(IReadOnlyList<string> NotRemoved, string? Cause)
{
    public bool Complete => NotRemoved.Count == 0;
}

/// <summary>The Windows parts <see cref="SisrInstallation"/> uses; replaced in tests.</summary>
public interface ISisrSystem
{
    /// <summary>Running processes with this name (<c>SISR</c>, <c>viiper</c>).</summary>
    IReadOnlyList<SisrProcess> Processes(string name);

    /// <summary>Ends the process and its children; true once it's gone (within a couple of seconds).</summary>
    bool Stop(int processId);

    bool HasUninstallEntry { get; }

    void DeleteUninstallEntry();

    /// <summary><c>viiper.exe uninstall --quiet</c>: VIIPER's own clean-up of what its <c>install</c> set up.</summary>
    void UninstallViiper(string viiperExecutable);

    /// <summary><c>steam://forceinputappid/0</c>: Steam drops the layout SISR forced on it; starts Steam if it is closed.</summary>
    void ResetSteamInputLayout();

    /// <summary>Detaches the pads usbip-win2 still holds for VIIPER's address (<see cref="UsbipDriver.DetachViiperDevicesAsync"/>).</summary>
    void DetachViiperDevices();
}

/// <summary>The folders <see cref="SisrInstallation"/> looks in; temp folders in tests.</summary>
/// <param name="ArcadiaLogDirectories">Where Arcadia ever put <c>sisr.log</c>: the current log folder and older ones.</param>
/// <param name="ArcadiaViiper">Arcadia's own <c>viiper.exe</c>, which still uses VIIPER's key folder (#304); null at uninstall.</param>
public sealed record SisrPaths(
    string LocalAppData,
    string RoamingAppData,
    string StartMenuPrograms,
    string Desktop,
    IReadOnlyList<string> ArcadiaLogDirectories,
    string? ArcadiaViiper);

/// <summary>
/// SISR as the old Arcadia (or the user) left it in <c>%LOCALAPPDATA%\SISR</c>, now that Pan replaces it (#214): finds it,
/// removes it on the player's request and stops only an SISR the old Arcadia started. Arcadia never starts SISR.
/// </summary>
public sealed class SisrInstallation(SisrPaths paths, ILauncherProgram? steam, ISisrSystem system)
{
    /// <summary>How the old Arcadia started SISR (<c>SisrHost.Start</c> up to #214); a user's SISR never listens there.</summary>
    public const string ArcadiaApiArgument = "--api.listen-address localhost:47640";

    public const string StopFailedCause =
        "SISR.exe lässt sich nicht beenden, deshalb bleiben seine Dateien. Starte den PC neu und versuch es dann noch einmal.";

    public const string SteamRunningCause =
        "Steam lief, deshalb bleibt der Eintrag „SISR Marker“. Versuch es noch einmal.";

    /// <summary>The real folders; the log folders are today's and those of versions up to 0.1.0-rc.1 (<see cref="LogLocation.MoveOldLogs"/>).</summary>
    public static SisrPaths DefaultPaths(string? arcadiaViiper)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new(
            localAppData,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            [LogLocation.Directory, Path.Combine(localAppData, "Arcadia", "logs"), Path.Combine(localAppData, "Arcadia-Logs")],
            arcadiaViiper);
    }

    public string InstallDirectory => Path.Combine(paths.LocalAppData, "SISR");

    public string Executable => Path.Combine(InstallDirectory, "SISR.exe");

    private string StandaloneViiperDirectory => Path.Combine(paths.LocalAppData, "VIIPER");

    private string ViiperKeyDirectory => Path.Combine(paths.RoamingAppData, "VIIPER");

    /// <summary>
    /// SISR's settings, and its WebView2 data in a folder named after its exe (seen on the owner's PC, 2026-10-16).
    /// </summary>
    private IEnumerable<string> DataDirectories =>
    [
        Path.Combine(paths.RoamingAppData, "SISR"),
        Path.Combine(paths.RoamingAppData, "SISR.exe"),
    ];

    private IEnumerable<string> Links =>
    [
        Path.Combine(paths.Desktop, "SISR.lnk"),
        Path.Combine(paths.StartMenuPrograms, "SISR.lnk"),
        Path.Combine(paths.StartMenuPrograms, "SISR (No Steam).lnk"),
    ];

    public SisrFound Find() => new(
        Directory.Exists(InstallDirectory),
        steam?.ShortcutsTo(Executable) ?? 0,
        CefDebuggingFlag is { } flag && File.Exists(flag));

    /// <summary>Any SISR runs, from wherever: the warning "SISR läuft" under Pan (decision 5).</summary>
    public bool IsAnyRunning => system.Processes("SISR").Count > 0;

    /// <summary>
    /// The old Arcadia kept SISR running across its update; the new one stops it at start, but only an SISR whose
    /// command line shows Arcadia's start. One the user started stays (decision 6). A VIIPER from SISR's folder that no
    /// SISR from there runs any more goes too: Arcadia's own VIIPER would otherwise use it. Returns how many SISR were
    /// stopped.
    /// </summary>
    public int StopStartedByOldArcadia(bool steamRunning)
    {
        var stopped = system.Processes("SISR").Count(p => IsStartedByArcadia(p.CommandLine, paths.ArcadiaLogDirectories) && system.Stop(p.Id));
        var leftovers = system.Processes("SISR").Any(IsFromInstallDirectory) ? 0 : StopViiperFromInstallDirectory();
        CleanUpAfterStop(stopped > 0 && steamRunning, leftovers);
        return stopped;
    }

    /// <summary>Arcadia's API port and its <c>--log.file</c> in one of Arcadia's log folders, as <c>SisrHost.Start</c> wrote them.</summary>
    public static bool IsStartedByArcadia(string? commandLine, IEnumerable<string> arcadiaLogDirectories)
    {
        if (commandLine is null || !commandLine.Contains(ArcadiaApiArgument, StringComparison.Ordinal))
        {
            return false;
        }

        const string logArgument = "--log.file ";
        var at = commandLine.IndexOf(logArgument, StringComparison.Ordinal);
        if (at < 0)
        {
            return false;
        }

        var rest = commandLine[(at + logArgument.Length)..].TrimStart();
        var logFile = rest.StartsWith('"') ? rest[1..Math.Max(1, rest.IndexOf('"', 1))] : rest.Split(' ')[0];
        return arcadiaLogDirectories.Any(dir =>
            string.Equals(logFile, Path.Combine(dir, "sisr.log"), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// First step of "SISR entfernen": stops SISR from its folder and the VIIPER it brought. False when SISR.exe won't
    /// stop; then nothing else is touched (<see cref="StopFailedCause"/>).
    /// </summary>
    private bool StopForRemoval(bool steamRunning)
    {
        var stopped = system.Processes("SISR").Where(IsFromInstallDirectory).Count(p => system.Stop(p.Id));
        CleanUpAfterStop(stopped > 0 && steamRunning, StopViiperFromInstallDirectory());
        return !system.Processes("SISR").Any(IsFromInstallDirectory);
    }

    private int StopViiperFromInstallDirectory() =>
        system.Processes("viiper").Where(IsFromInstallDirectory).Count(p => system.Stop(p.Id));

    /// <summary>
    /// What SISR's own quit does and a kill skips (as the old Arcadia did after it, until #214): Steam drops the layout
    /// SISR forced, and pads of a killed VIIPER that usbip-win2 still holds go, but only while no other VIIPER runs,
    /// since the pads on VIIPER's address may be that one's.
    /// </summary>
    private void CleanUpAfterStop(bool resetSteamLayout, int viipersStopped)
    {
        if (resetSteamLayout)
        {
            system.ResetSteamInputLayout();
        }

        if (viipersStopped > 0 && system.Processes("viiper").Count == 0)
        {
            system.DetachViiperDevices();
        }
    }

    /// <summary>
    /// Removes everything listed in #214 decision 2 that is there; idempotent, so a second run finishes an interrupted one.
    /// The marker needs Steam closed (it writes <c>shortcuts.vdf</c> back from memory when it exits); the caller closes it.
    /// usbip-win2 stays (Pan needs it), and so do the per-game modes.
    /// </summary>
    public SisrRemoval Remove(bool steamRunning)
    {
        if (!StopForRemoval(steamRunning))
        {
            return new(["SISR"], StopFailedCause);
        }

        var notRemoved = new List<string>();
        string? cause = null;

        void Step(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                notRemoved.Add(what);
                cause ??= $"{what} ließ sich nicht entfernen: {ex.Message}";
            }
        }

        Step("SISR-Ordner", () => DeleteDirectory(InstallDirectory));
        foreach (var data in DataDirectories)
        {
            Step($"SISR-Daten ({Path.GetFileName(data)})", () => DeleteDirectory(data));
        }

        Step("SISR-Verknüpfungen", () =>
        {
            foreach (var link in Links)
            {
                DeleteFile(link);
            }
        });
        Step("SISR-Deinstallationseintrag", () =>
        {
            if (system.HasUninstallEntry)
            {
                system.DeleteUninstallEntry();
            }
        });

        if (steam is not null)
        {
            if (steam.ShortcutsTo(Executable) > 0)
            {
                if (steamRunning)
                {
                    notRemoved.Add("Steam-Eintrag „SISR Marker“");
                    cause ??= SteamRunningCause;
                }
                else
                {
                    Step("Steam-Eintrag „SISR Marker“", () => steam.RemoveShortcutsTo(Executable));
                }
            }

            if (CefDebuggingFlag is { } flag)
            {
                Step("Steams CEF-Debugging", () => DeleteFile(flag));
            }
        }

        RemoveViiper(Step);
        return new(notRemoved, cause);
    }

    /// <summary>
    /// SISR's VIIPER: a standalone install in <c>%LOCALAPPDATA%\VIIPER</c> unless a VIIPER runs from there, and the key
    /// folder only while no other VIIPER uses it (Arcadia's own still does until #304).
    /// </summary>
    private void RemoveViiper(Action<string, Action> step)
    {
        var standalone = Path.Combine(StandaloneViiperDirectory, "viiper.exe");
        var runningViipers = system.Processes("viiper");
        if (Directory.Exists(StandaloneViiperDirectory) && !runningViipers.Any(p => IsInside(p.ExecutablePath, StandaloneViiperDirectory)))
        {
            step("VIIPER", () =>
            {
                if (File.Exists(standalone))
                {
                    system.UninstallViiper(standalone);
                }

                DeleteDirectory(StandaloneViiperDirectory);
            });
        }

        var otherViiper = (paths.ArcadiaViiper is { } own && File.Exists(own))
            || Directory.Exists(StandaloneViiperDirectory)
            || runningViipers.Any(p => !IsFromInstallDirectory(p));
        if (!otherViiper)
        {
            step("VIIPER-Schlüssel", () => DeleteDirectory(ViiperKeyDirectory));
        }
    }

    /// <summary>The flag SISR set in Steam's folder to reach Steam's browser (CEF remote debugging); null without Steam.</summary>
    private string? CefDebuggingFlag => steam?.Folder is { } folder ? Path.Combine(folder, ".cef-enable-remote-debugging") : null;

    private bool IsFromInstallDirectory(SisrProcess process) => IsInside(process.ExecutablePath, InstallDirectory);

    private static bool IsInside(string? path, string directory) =>
        path is not null
        && path.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    /// <summary>File.Delete throws when the folder is missing, e.g. no Desktop link folder.</summary>
    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
