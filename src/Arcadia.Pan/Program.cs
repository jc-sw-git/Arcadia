using System.Globalization;
using Arcadia.Pan.Windows;

namespace Arcadia.Pan;

/// <summary>
/// <c>Pan.exe</c> (#243). Without arguments it runs as the service (or, started from a console, says how to use
/// it). Commands, all started elevated by Arcadia or by hand:
/// <c>--install [--expect-hash sha256] [--usbip-installer path]</c>, <c>--uninstall</c>, <c>--restore</c>, <c>--console</c> (the service in a
/// console, for development), <c>--version</c>.
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
                        UsbipInstall.RunAsync(usbipInstaller, log.Write).GetAwaiter().GetResult();
                    }

                    ServiceSetup.Install(Environment.ProcessPath ?? throw new InvalidOperationException("own path unknown"), Option(args, "--expect-hash"), log.Write);
                    return 0;
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

                    ServiceSetup.UninstallAsync(removeData: dataProblem is null, log.Write).GetAwaiter().GetResult();
                    return 0;
                case "--restore":
                    if (ServiceSetup.CheckDataDirectory() is { } problem)
                    {
                        Console.Error.WriteLine($"Not restoring: {problem}; run Pan.exe --install again.");
                        return 3;
                    }

                    log.TrustFolder();
                    Restore.RunAsync(log.Write).GetAwaiter().GetResult();
                    return 0;
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
