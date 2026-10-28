using System.Globalization;
using Arcadia.Pan.Windows;

namespace Arcadia.Pan;

/// <summary>
/// <c>Pan.exe</c> (#243). Without arguments it runs as the service (or, started from a console, says how to use
/// it). Commands, all started elevated by Arcadia or by hand:
/// <c>--install [--expect-hash sha256] [--usbip-installer path --usbip-sha256 sha256] [--hidhide-installer path [--hidhide-repair]]</c>,
/// <c>--uninstall [--remove-drivers]</c>, <c>--restore</c>, <c>--console</c> (the service in a console, for development),
/// <c>--version</c>.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Pan runs on Windows only.");
            return 2;
        }

        return MainOnWindows(args);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static int MainOnWindows(string[] args)
    {
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;
        if (command == "--version")
        {
            Console.WriteLine($"{Version()} protocol {PanMessage.ProtocolVersion}");
            return 0;
        }

        if (PanCommands.NeedsAdministrator(command) && !IsElevated())
        {
            // Unelevated, --install got as far as setting the data folder aside and then failed to make it anew
            // (owner's B2 test, 2026-10-27): nothing is touched without the rights to finish.
            Console.Error.WriteLine($"Pan.exe {command} needs administrator rights: run it from an elevated prompt.");
            return NotElevatedExitCode;
        }

        var log = new PanLog(PanPaths.LogPath, console: command.Length > 0);
        try
        {
            switch (command)
            {
                case "":
                    if (ServiceHost.TryRun(log))
                    {
                        return 0;
                    }

                    Console.Error.WriteLine($"Pan {Version()} is Arcadia's guardian service and is started by Windows. Commands: --install, --uninstall, --restore, --console, --version.");
                    return 1;
                case "--console":
                    using (var stop = new CancellationTokenSource())
                    {
                        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
                        ServiceHost.RunAsync(log, stop.Token).GetAwaiter().GetResult();
                    }

                    return 0;
                case "--install":
                    // The log lives in the data folder: secured before anything is written there (review, bug 1). A
                    // failure here can't be logged, so its own exit code tells Arcadia what to say.
                    IReadOnlyList<string> setAside;
                    try
                    {
                        setAside = ServiceSetup.SecureDataDirectory();
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                    {
                        Console.Error.WriteLine($"The data folder could not be secured: {ex.Message}");
                        return DataFolderExitCode;
                    }

                    log.TrustFolder();
                    foreach (var folder in setAside)
                    {
                        log.Write($"Data folder not trusted: {folder}; made anew, its journal not used");
                    }

                    UninstallHandOff.RemoveLeftover(OwnPath(), PanPaths.HandOffDirectory, log.Write);

                    if (Option(args, "--usbip-installer") is { } usbipInstaller)
                    {
                        var usbipHash = Option(args, "--usbip-sha256")
                            ?? throw new InvalidOperationException("--usbip-installer needs --usbip-sha256 (the hash Arcadia checked at the download)");
                        UsbipInstall.RunAsync(usbipInstaller, usbipHash, log.Write).GetAwaiter().GetResult();
                    }

                    // HidHide matters only for showing Steam a copy of the puck (#242); a failed HidHide must not cost
                    // the player Pan and with it the Xbox pad, so Pan is set up anyway and the exit code says so.
                    var hidHideFailed = false;
                    if (Option(args, "--hidhide-installer") is { } hidHideInstaller)
                    {
                        try
                        {
                            HidHideInstall.RunAsync(hidHideInstaller, args.Contains("--hidhide-repair", StringComparer.OrdinalIgnoreCase), log.Write).GetAwaiter().GetResult();
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
                        {
                            log.Write($"HidHide not installed: {ex.Message}");
                            Console.Error.WriteLine(ex.Message);
                            hidHideFailed = true;
                        }
                    }

                    ServiceSetup.Install(OwnPath(), Option(args, "--expect-hash"), log.Write);
                    return hidHideFailed ? HidHideFailedExitCode : 0;
                case "--uninstall":
                    // Runs at every Arcadia uninstall, also where Pan never was: a data folder that isn't ours is
                    // neither written nor deleted elevated (review, bug 1); the service and Program Files go anyway.
                    var dataProblem = ServiceSetup.CheckDataDirectory();
                    if (dataProblem is null)
                    {
                        log.TrustFolder();
                    }
                    else
                    {
                        Console.Error.WriteLine($"Leaving the data folder alone: {dataProblem}.");
                    }

                    // From Arcadia's folder the uninstall moves to a copy in Program Files and this process exits (#270).
                    var ownPath = OwnPath();
                    // The prompt returns at once, so a second --uninstall may start while the copy still works: it must not
                    // run teardown, undo and driver removal a second time beside it (QE review of #270).
                    if (RunningHandOffCopy() is { } running)
                    {
                        log.Write($"Not uninstalling: the uninstall in {PanPaths.HandOffDirectory} still runs (process {running})");
                        Console.WriteLine($"An uninstall is already running (process {running}); {PanPaths.LogPath} says how it went.");
                        return 0;
                    }

                    UninstallHandOff.RemoveLeftover(ownPath, PanPaths.HandOffDirectory, log.Write);
                    if (UninstallHandOff.TryHandOff(ownPath, PanPaths.ArcadiaProgramFiles, PanPaths.HandOffDirectory, copy => StartHandOff(copy, args), log.Write) == HandOffResult.HandedOff)
                    {
                        Console.WriteLine($"The uninstall goes on in {PanPaths.HandOffDirectory}; {PanPaths.LogPath} says how it went.");
                        return 0;
                    }

                    var data = dataProblem is null ? DataFolder.Ours
                        : Directory.Exists(PanPaths.DataDirectory) || File.Exists(PanPaths.DataDirectory) ? DataFolder.NotOurs
                        : DataFolder.Missing;
                    // The drivers only when the user said yes at Arcadia's uninstall (#243 part B2).
                    Func<Task<bool>>? removeDrivers = Flag(args, "--remove-drivers") ? () => DriverRemoval.RunAsync(log.Write) : null;
                    try
                    {
                        return ServiceSetup.UninstallAsync(data, removeDrivers, log.StopFileLogging, log.Write).GetAwaiter().GetResult() switch
                        {
                            UninstallOutcome.RestoreIncomplete => RestoreIncompleteExitCode,
                            UninstallOutcome.DriversLeft => DriversNotRemovedExitCode,
                            _ => 0,
                        };
                    }
                    finally
                    {
                        if (UninstallHandOff.IsInside(ownPath, PanPaths.HandOffDirectory))
                        {
                            ServiceSetup.ScheduleHandOffRemoval(ownPath, log.Write);
                        }
                    }
                case "--restore":
                    if (ServiceSetup.CheckDataDirectory() is { } problem)
                    {
                        Console.Error.WriteLine($"Not restoring: {problem}; run Pan.exe --install again.");
                        return 3;
                    }

                    log.TrustFolder();
                    return Restore.RunAsync(log.Write).GetAwaiter().GetResult() ? 0 : RestoreIncompleteExitCode;
                default:
                    Console.Error.WriteLine($"Unknown command {args[0]}. Commands: --install, --uninstall, --restore, --console, --version.");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            log.Write($"{command} failed: {ex.Message}");
            Console.Error.WriteLine($"{command} failed: {ex.Message}");
            return 3;
        }
    }

    /// <summary>Exit code of <c>--install</c> when the data folder can't be secured; Arcadia names the folder then.</summary>
    public const int DataFolderExitCode = 4;

    /// <summary>
    /// Exit code of <c>--restore</c> and <c>--uninstall</c> when something listed in the journal could not be undone;
    /// the journal and Pan.exe stay for another <c>--restore</c>, and Arcadia says so.
    /// </summary>
    public const int RestoreIncompleteExitCode = 5;

    /// <summary>
    /// Exit code of <c>--install</c> when Pan was set up but HidHide was not (signature, setup error): Arcadia says so
    /// and keeps the HidHide row; <c>pan.log</c> says why.
    /// </summary>
    public const int HidHideFailedExitCode = 6;

    /// <summary>Exit code of <c>--uninstall --remove-drivers</c> when Pan went but a driver stayed; <c>pan.log</c> says which.</summary>
    public const int DriversNotRemovedExitCode = 7;

    /// <summary>Exit code of a command that changes the machine, run without administrator rights: nothing was done.</summary>
    public const int NotElevatedExitCode = 8;

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// The id of another process running the hand-off copy, or null. Checked by its image path: only administrators can
    /// start a program from Program Files, so nobody else can fake one (a named mutex could be squatted by any user).
    /// </summary>
    private static int? RunningHandOffCopy()
    {
        var copy = Path.Combine(PanPaths.HandOffDirectory, PanPaths.ExecutableName);
        var self = Environment.ProcessId;
        foreach (var process in System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(PanPaths.ExecutableName)))
        {
            using (process)
            {
                try
                {
                    if (process.Id != self && string.Equals(process.MainModule?.FileName, copy, StringComparison.OrdinalIgnoreCase))
                    {
                        return process.Id;
                    }
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    // Exited meanwhile, or a process this one may not inspect: not the copy, which runs elevated like this one.
                }
            }
        }

        return null;
    }

    private static string OwnPath() => Environment.ProcessPath ?? throw new InvalidOperationException("own path unknown");

    /// <summary>
    /// Starts the hand-off copy with this process's arguments, elevated like this one (a plain start inherits the
    /// token), and doesn't wait: the bundled exe must be closed before Velopack deletes Arcadia's folder.
    /// </summary>
    private static void StartHandOff(string copy, string[] args)
    {
        var info = new System.Diagnostics.ProcessStartInfo(copy) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(copy)! };
        foreach (var argument in args)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(info) ?? throw new InvalidOperationException($"{copy} did not start");
    }

    private static bool Flag(string[] args, string name) => args.Skip(1).Contains(name, StringComparer.OrdinalIgnoreCase);

    private static string? Option(string[] args, string name)
    {
        for (var i = 1; i + 1 < args.Length; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string Version() => (typeof(Program).Assembly.GetName().Version ?? new Version(0, 0)).ToString(3);
}
