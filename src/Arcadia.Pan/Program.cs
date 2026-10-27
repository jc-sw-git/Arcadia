using System.Globalization;
using Arcadia.Pan.Windows;

namespace Arcadia.Pan;

/// <summary>
/// <c>Pan.exe</c> (#243). Without arguments it runs as the service (or, started from a console, says how to use
/// it). Commands, all started elevated by Arcadia or by hand:
/// <c>--install [--expect-hash sha256] [--usbip-installer path --usbip-sha256 sha256] [--hidhide-installer path]</c>,
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
                    try
                    {
                        ServiceSetup.SecureDataDirectory();
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                    {
                        Console.Error.WriteLine($"The data folder could not be secured: {ex.Message}");
                        return DataFolderExitCode;
                    }

                    log.TrustFolder();
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
                            HidHideInstall.RunAsync(hidHideInstaller, log.Write).GetAwaiter().GetResult();
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
                        {
                            log.Write($"HidHide not installed: {ex.Message}");
                            Console.Error.WriteLine(ex.Message);
                            hidHideFailed = true;
                        }
                    }

                    ServiceSetup.Install(Environment.ProcessPath ?? throw new InvalidOperationException("own path unknown"), Option(args, "--expect-hash"), log.Write);
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

                    var data = dataProblem is null ? DataFolder.Ours
                        : Directory.Exists(PanPaths.DataDirectory) || File.Exists(PanPaths.DataDirectory) ? DataFolder.NotOurs
                        : DataFolder.Missing;
                    // The drivers only when the user said yes at Arcadia's uninstall (#243 part B2).
                    Func<Task<bool>>? removeDrivers = Flag(args, "--remove-drivers") ? () => DriverRemoval.RunAsync(log.Write) : null;
                    return ServiceSetup.UninstallAsync(data, removeDrivers, log.StopFileLogging, log.Write).GetAwaiter().GetResult() switch
                    {
                        UninstallOutcome.RestoreIncomplete => RestoreIncompleteExitCode,
                        UninstallOutcome.DriversLeft => DriversNotRemovedExitCode,
                        _ => 0,
                    };
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
