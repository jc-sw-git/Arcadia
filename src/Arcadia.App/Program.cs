using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Diagnostics;
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

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
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
    /// Velopack ends the uninstall hook after 30 s. Removing SISR takes up to about 10 s (VIIPER's uninstaller and
    /// stopping processes wait), so the questions share the other 20 s and each says how long it waits.
    /// </summary>
    private const uint QuestionsTimeoutMs = 20_000;

    /// <summary>
    /// Asks whether SISR and what belongs to it, and Arcadia's own files (settings, logos, logs) should go too.
    /// Both stay when the user says no or doesn't answer in time: SISR may be used without Arcadia, and the files
    /// bring the settings back with a new installation.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void OnBeforeUninstall(SemanticVersion version)
    {
        var parts = ControllerSupportRemoval.FindInstalledParts();
        var hasData = Directory.Exists(LogLocation.DataFolder);
        var questions = (parts.Count > 0 ? 1u : 0u) + (hasData ? 1u : 0u);
        var timeoutMs = questions == 0 ? 0 : QuestionsTimeoutMs / questions;

        var removeSisr = parts.Count > 0 && AskToRemoveSisr(parts, timeoutMs);
        var removeData = hasData && AskToRemoveData(timeoutMs);

        if (removeSisr)
        {
            ControllerSupportRemoval.Remove(AppLog.Write);
        }
        else
        {
            QuitSisr();
        }

        if (removeData)
        {
            RemoveData();
        }
    }

    private static bool AskToRemoveSisr(IReadOnlyList<string> parts, uint timeoutMs)
    {
        var text = "Arcadia wird deinstalliert.\n\n"
            + "Soll auch SISR für den Steam Controller mit allem, was dazugehört, entfernt werden?\n\n"
            + string.Concat(parts.Select(p => $"•  {p}\n"))
            + "\nAndere Programme, die SISR nutzen, funktionieren danach nicht mehr. "
            + $"Ohne Antwort innerhalb von {timeoutMs / 1000} Sekunden bleibt SISR installiert; Arcadia wird trotzdem deinstalliert.";
        return Ask(text, timeoutMs);
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
            // A SISR left running keeps sisr.log open; the rest is gone, and nothing else depends on it.
        }
    }

    // Undocumented but present in user32 since Windows XP; returns 32000 on timeout.
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxTimeoutW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxTimeout(IntPtr owner, string text, string caption, uint type, ushort language, uint milliseconds);

    /// <summary>
    /// SISR lives outside Arcadia's install directory; if Arcadia didn't get to stop it (e.g. it
    /// was killed), stop it before uninstalling so no orphaned SISR keeps holding the controller.
    /// </summary>
    private static void QuitSisr()
    {
        // QuitAsync times out after 3 s and reports failure instead of throwing.
        using var http = new HttpClient();
        new SisrApiClient(http, SisrHost.ApiPort).QuitAsync().GetAwaiter().GetResult();
    }
}
