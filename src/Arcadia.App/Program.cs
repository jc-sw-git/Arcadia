using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Diagnostics;
using Arcadia.Core.Launchers;
using Arcadia.Launchers;
using Arcadia.Pan;
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
            .WithArcadiaFonts()
            .LogToTrace();

    /// <summary>
    /// Velopack's Update.exe (1.2.161) ends the uninstall hook after 60 s; its API docs still say 30 s, so check
    /// <c>commands/uninstall.rs</c> when Velopack is updated. Removing SISR usually takes a few seconds and at most
    /// about 20 s (closing Steam for the marker, VIIPER's uninstaller), so the questions share 40 s and each says how
    /// long it waits. If the removal runs long, Velopack ends it after the important steps
    /// (<see cref="ControllerSupportRemoval.Remove"/>).
    /// </summary>
    private const uint QuestionsTimeoutMs = 40_000;

    /// <summary>When Velopack's Update.exe ends the uninstall hook (see <see cref="QuestionsTimeoutMs"/>).</summary>
    private static readonly TimeSpan UninstallHookLimit = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Asks whether the controller emulation's drivers and SISR (#243 part B2), and Arcadia's own files (settings, logos,
    /// logs) should go too. Both stay when the user says no or doesn't answer in time: other programs may use the
    /// drivers, and the files bring the settings back with a new installation.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void OnBeforeUninstall(SemanticVersion version)
    {
        var elapsed = Stopwatch.StartNew();
        ControllerSupportRemoval.RemoveOwnViiper(AppLog.Write);
        var parts = ControllerSupportRemoval.FindInstalledParts();
        var hasData = Directory.Exists(LogLocation.DataFolder);
        var questions = (parts.Any ? 1u : 0u) + (hasData ? 1u : 0u);
        var timeoutMs = questions == 0 ? 0 : QuestionsTimeoutMs / questions;

        // Steam the program, which SISR left its marker in: the launcher that owns Steam Input.
        var steam = LauncherRegistry.SteamInputOwnerOf(BuiltInLaunchers.Create(TimeProvider.System, demo: false))?.Program;
        var removeParts = parts.Any && Ask(ControllerSupportRemoval.Question(parts, steam?.IsRunning == true, timeoutMs / 1000), timeoutMs);
        var removeData = hasData && AskToRemoveData(timeoutMs);

        if (removeParts)
        {
            ControllerSupportRemoval.Remove(steam, AppLog.Write);
        }

        if (removeData)
        {
            RemoveData();
        }

        var removeDrivers = removeParts && parts.Drivers;
        var pan = RemovePan(removeDrivers);

        // Windows keeps a loaded driver until the next restart (#407); a new install before then can't set the drivers up.
        if (ControllerSupportRemoval.RestartNoticeDue(removeDrivers, pan))
        {
            var noticeTime = ControllerSupportRemoval.RestartNoticeTime(elapsed.Elapsed, UninstallHookLimit);
            // Not into a log folder the player just chose to delete: writing would create it again.
            if (!removeData)
            {
                AppLog.Write(noticeTime is null
                    ? "Deinstallation: Windows entfernt die Treiber erst beim Neustart; für den Hinweis blieb keine Zeit"
                    : "Deinstallation: Windows entfernt die Treiber erst beim nächsten Neustart");
            }

            if (noticeTime is { } time)
            {
                Inform(ControllerSupportRemoval.RestartNotice, (uint)time.TotalMilliseconds);
            }
        }
    }

    /// <summary>
    /// Pan goes with Arcadia, always (#243): it exists for Arcadia alone. One UAC prompt, and only when Pan is there or
    /// the drivers are to go (admin rights only where unavoidable): usbip-win2 and HidHide go in the same prompt, after
    /// Pan undid its own HidHide entries (#243 part B2). Declined, the service stays and removes itself at the next
    /// Windows start, when it finds no Arcadia (#422). Capped so Velopack's hook limit holds; Pan, started elevated on
    /// its own, finishes the drivers' uninstallers even if the hook ends first, and what it can't finish at the next starts.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static PanUninstallResult RemovePan(bool removeDrivers)
    {
        if (!PanSetup.IsBundled || (!removeDrivers && !PanSetup.IsInstalled && !Directory.Exists(PanPaths.InstallDirectory)))
        {
            return PanUninstallResult.NotRun;
        }

        try
        {
            using var limit = new CancellationTokenSource(TimeSpan.FromMilliseconds(QuestionsTimeoutMs));
            if (!PanSetup.UninstallAsync(removeDrivers, limit.Token).GetAwaiter().GetResult())
            {
                AppLog.Write("Pan bleibt installiert: die Windows-Abfrage wurde abgelehnt");
                return PanUninstallResult.Declined;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or OperationCanceledException)
        {
            // Past the UAC prompt Pan runs on elevated or failed partway; unknown counts as started, so the notice shows.
            AppLog.Write($"Pan konnte nicht entfernt werden: {ex.Message}");
        }
        catch (Win32Exception ex)
        {
            // Pan never started (e.g. elevation blocked by policy): nothing was removed, and the hook must go on.
            AppLog.Write($"Pan konnte nicht gestartet werden: {ex.Message}");
            return PanUninstallResult.NotRun;
        }

        return PanUninstallResult.Started;
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

    private static void Inform(string text, uint timeoutMs)
    {
        const uint Ok = 0x0, IconInformation = 0x40, SetForeground = 0x10000, TopMost = 0x40000;
        MessageBoxTimeout(IntPtr.Zero, text, "Arcadia deinstallieren", Ok | IconInformation | SetForeground | TopMost, 0, timeoutMs);
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
