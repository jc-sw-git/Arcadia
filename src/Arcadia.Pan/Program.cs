using System.Globalization;
using Arcadia.Pan.Windows;

namespace Arcadia.Pan;

/// <summary>
/// <c>Pan.exe</c> (#243). Without arguments it runs as the service (or, started from a console, says how to use
/// it). Commands, all started elevated by Arcadia or by hand:
/// <c>--install [--expect-hash sha256]</c>, <c>--uninstall</c>, <c>--restore</c>, <c>--console</c> (the service in a
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
                    var expected = args.Length >= 3 && args[1].Equals("--expect-hash", StringComparison.OrdinalIgnoreCase) ? args[2] : null;
                    ServiceSetup.Install(Environment.ProcessPath ?? throw new InvalidOperationException("own path unknown"), expected, log.Write);
                    return 0;
                case "--uninstall":
                    ServiceSetup.UninstallAsync(log.Write).GetAwaiter().GetResult();
                    return 0;
                case "--restore":
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
            return 3;
        }
    }

    private static string Version() => (typeof(Program).Assembly.GetName().Version ?? new Version(0, 0)).ToString(3);
}
