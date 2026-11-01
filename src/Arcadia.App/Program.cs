using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Diagnostics;
using Arcadia.Pan;
using Arcadia.Launchers.Steam;
using Avalonia;
using Velopack;

namespace Arcadia.App;

sealed partial class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first: the installer and updater start Arcadia with hook arguments and expect it
        // to exit right after. Does nothing when Arcadia runs from bin/.
        var velopack = VelopackApp.Build();
        if (OperatingSystem.IsWindows())
        {
            velopack.OnBeforeUninstallFastCallback(OnBeforeUninstall);
        }

        velopack.Run();
        LogLocation.MoveOldLogs(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LogLocation.DataFolder);

        // Crashes used to show only in the Windows event log; now every one lands in arcadia.log.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLog.Write($"Absturz durch unbehandelten Fehler: {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Write($"Unbeobachteter Fehler in einer Hintergrundaufgabe: {e.Exception}");
            e.SetObserved();
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(WaitForEarlierArcadia(args));
    }

    /// <summary>
    /// Passed with the process id of the Arcadia that restarts itself (#396): this one starts once that one has exited,
    /// so the two never talk to Pan at the same time (Pan refuses a second client).
    /// </summary>
    public const string WaitForExitArgument = "--wait-for-exit";

    /// <summary>Above the ~7 s an exit takes at most (the emulation's cleanup, Pan's client); then it starts anyway.</summary>
    private static readonly TimeSpan EarlierArcadiaWait = TimeSpan.FromSeconds(20);

    /// <summary>Waits for the Arcadia named after <see cref="WaitForExitArgument"/>, if any; returns the arguments without it.</summary>
    private static string[] WaitForEarlierArcadia(string[] args)
    {
        var at = Array.IndexOf(args, WaitForExitArgument);
        if (at < 0)
        {
            return args;
        }

        if (at + 1 < args.Length && int.TryParse(args[at + 1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id))
        {
            try
            {
                using var earlier = System.Diagnostics.Process.GetProcessById(id);
                using var self = System.Diagnostics.Process.GetCurrentProcess();
                // A reused id that isn't an Arcadia's is no reason to wait.
                if (earlier.ProcessName == self.ProcessName && !earlier.WaitForExit(EarlierArcadiaWait))
                {
                    AppLog.Write($"Die vorige Arcadia (Prozess {id}) läuft nach {EarlierArcadiaWait.TotalSeconds:0} s noch; Arcadia startet trotzdem");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Already gone, or not ours to look at: nothing to wait for.
            }
        }

        return [.. args.Take(at), .. args.Skip(at + 2)];
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// Velopack's Update.exe (1.2.161) ends the uninstall hook after 60 s; its API docs still say 30 s, so check
    /// <c>commands/uninstall.rs</c> when Velopack is updated. Removing SISR usually takes a few seconds and at most
    /// about 20 s (closing Steam for the marker, VIIPER's uninstaller), so the questions share 40 s and each says how
    /// long it waits. If the removal runs long, Velopack ends it after the important steps
    /// (<see cref="ControllerSupportRemoval.Remove"/>).
    /// </summary>
    private const uint QuestionsTimeoutMs = 40_000;

    /// <summary>
    /// Asks whether the controller emulation's drivers and SISR (#243 part B2), and Arcadia's own files (settings, logos,
    /// logs) should go too. Both stay when the user says no or doesn't answer in time: other programs may use the
    /// drivers, and the files bring the settings back with a new installation.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void OnBeforeUninstall(SemanticVersion version)
    {
        var parts = ControllerSupportRemoval.FindInstalledParts();
        var hasData = Directory.Exists(LogLocation.DataFolder);
        var questions = (parts.Any ? 1u : 0u) + (hasData ? 1u : 0u);
        var timeoutMs = questions == 0 ? 0 : QuestionsTimeoutMs / questions;

        var removeParts = parts.Any && Ask(ControllerSupportRemoval.Question(parts, SteamInstallation.IsRunning, timeoutMs / 1000), timeoutMs);
        var removeData = hasData && AskToRemoveData(timeoutMs);

        if (removeParts)
        {
            ControllerSupportRemoval.Remove(AppLog.Write);
        }

        if (removeData)
        {
            RemoveData();
        }

        RemovePan(removeDrivers: removeParts && parts.Drivers);
    }

    /// <summary>
    /// Pan goes with Arcadia, always (#243): it exists for Arcadia alone. One UAC prompt, and only when Pan is there or
    /// the drivers are to go (admin rights only where unavoidable): usbip-win2 and HidHide go in the same prompt, after
    /// Pan undid its own HidHide entries (#243 part B2). Declined, the service stays (harmless: it idles and stops
    /// itself) and <c>Pan.exe --uninstall</c> removes it by hand. Capped so Velopack's hook limit holds; Pan, started
    /// elevated on its own, finishes the drivers' uninstallers even if the hook ends first.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void RemovePan(bool removeDrivers)
    {
        if (!PanSetup.IsBundled || (!removeDrivers && !PanSetup.IsInstalled && !Directory.Exists(PanPaths.InstallDirectory)))
        {
            return;
        }

        try
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromMilliseconds(QuestionsTimeoutMs));
            if (!PanSetup.UninstallAsync(removeDrivers, limit.Token).GetAwaiter().GetResult())
            {
                AppLog.Write("Pan bleibt installiert: die Windows-Abfrage wurde abgelehnt");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or OperationCanceledException)
        {
            AppLog.Write($"Pan konnte nicht entfernt werden: {ex.Message}");
        }
    }

    private static bool AskToRemoveData(uint timeoutMs)
    {
        var text = "Arcadia wird deinstalliert.\n\n"
            + "Sollen auch Arcadias Einstellungen, eigene Logos und Logs gelöscht werden?\n\n"
            + $"{LogLocation.DataFolder}\n\n"
            + "Bleiben sie, sind die Einstellungen bei einer neuen Installation wieder da. "
            + $"Ohne Antwort innerhalb von {timeoutMs / 1000} Sekunden bleiben sie erhalten.";
        return Ask(text, timeoutMs);
    }

    private static bool Ask(string text, uint timeoutMs)
    {
        const uint YesNo = 0x4, IconQuestion = 0x20, DefaultNo = 0x100, SetForeground = 0x10000, TopMost = 0x40000;
        const int Yes = 6;
        return MessageBoxTimeout(IntPtr.Zero, text, "Arcadia deinstallieren", YesNo | IconQuestion | DefaultNo | SetForeground | TopMost, 0, timeoutMs) == Yes;
    }

    private static void RemoveData()
    {
        try
        {
            Directory.Delete(LogLocation.DataFolder, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A program left running may keep a log open; the rest is gone, and nothing else depends on it.
        }
    }

    // Undocumented but present in user32 since Windows XP; returns 32000 on timeout.
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxTimeoutW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxTimeout(IntPtr owner, string text, string caption, uint type, ushort language, uint milliseconds);
}
