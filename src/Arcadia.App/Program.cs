using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
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

    /// <summary>Velopack ends the uninstall hook after 30 s; the question leaves time for the removal.</summary>
    private const uint QuestionTimeoutMs = 20_000;

    /// <summary>
    /// Asks whether SISR and what belongs to it should go too. SISR may be used without Arcadia,
    /// so it stays when the user says no or doesn't answer in time.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void OnBeforeUninstall(SemanticVersion version)
    {
        var parts = ControllerSupportRemoval.FindInstalledParts();
        if (parts.Count > 0 && AskToRemove(parts))
        {
            ControllerSupportRemoval.Remove(AppLog.Write);
        }
        else
        {
            QuitSisr();
        }
    }

    private static bool AskToRemove(IReadOnlyList<string> parts)
    {
        const uint YesNo = 0x4, IconQuestion = 0x20, DefaultNo = 0x100, SetForeground = 0x10000, TopMost = 0x40000;
        const int Yes = 6;
        var text = "Arcadia wird deinstalliert.\n\n"
            + "Soll auch der Steam Controller Support mit allem, was dazugehört, entfernt werden?\n\n"
            + string.Concat(parts.Select(p => $"•  {p}\n"))
            + "\nAndere Programme, die SISR nutzen, funktionieren danach nicht mehr. "
            + $"Ohne Antwort innerhalb von {QuestionTimeoutMs / 1000} Sekunden bleibt alles installiert.";
        return MessageBoxTimeout(IntPtr.Zero, text, "Arcadia deinstallieren", YesNo | IconQuestion | DefaultNo | SetForeground | TopMost, 0, QuestionTimeoutMs) == Yes;
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
