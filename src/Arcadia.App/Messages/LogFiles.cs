using System.ComponentModel;
using System.Diagnostics;
using Arcadia.App.Services;
using Arcadia.Core.Diagnostics;
using Arcadia.Pan;

namespace Arcadia.App.Messages;

/// <summary>The three logs "Über Arcadia" opens (#433).</summary>
public enum LogKind
{
    Arcadia,
    Viiper,
    Pan,
}

/// <summary>Whether a log row can open its file now; checked whenever the page shows, not only at the start.</summary>
public enum LogFileState
{
    Present,

    /// <summary>Not written yet, e.g. viiper.log before VIIPER ran once.</summary>
    Missing,

    /// <summary>pan.log without Pan: there is none until Pan is installed.</summary>
    PanNotInstalled,
}

/// <summary>
/// Arcadia's logs on disk. It takes a <see cref="LogKind"/>, never a path, so only the three fixed paths ever reach the
/// process start: nothing from settings.json, a file or the UI (#433).
/// </summary>
public interface ILogFiles
{
    /// <summary>The path as shown in the info panel, without the user's name.</summary>
    string ShownPathOf(LogKind kind);

    LogFileState StateOf(LogKind kind);

    /// <summary>Opens the log in the system's default app for <c>.log</c>; throws when the system can't.</summary>
    void Open(LogKind kind);
}

/// <summary>The real logs: arcadia.log and viiper.log in <see cref="LogLocation.Directory"/>, pan.log in Pan's data folder.</summary>
public sealed class LogFiles : ILogFiles
{
    private static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public string ShownPathOf(LogKind kind)
    {
        var path = PathOf(kind);
        // %APPDATA% names the user's profile; the info panel never shows the user's name.
        return AppData.Length > 0 && path.StartsWith(AppData, StringComparison.OrdinalIgnoreCase)
            ? "%APPDATA%" + path[AppData.Length..]
            : path;
    }

    public LogFileState StateOf(LogKind kind) =>
        File.Exists(PathOf(kind)) ? LogFileState.Present
        : kind == LogKind.Pan && !File.Exists(PanPaths.InstalledExecutable) ? LogFileState.PanNotInstalled
        : LogFileState.Missing;

    public void Open(LogKind kind)
    {
        // The file itself, handed to the shell: Windows picks the app registered for .log, no program of ours runs.
        using var opened = Process.Start(new ProcessStartInfo(PathOf(kind)) { UseShellExecute = true });
        AppLog.Write($"Log geöffnet: {kind}");
    }

    /// <summary>The fixed paths; a kind outside the enum is a programming error, not a path to guess.</summary>
    private static string PathOf(LogKind kind) => kind switch
    {
        LogKind.Arcadia => Path.Combine(AppLog.Directory, "arcadia.log"),
        LogKind.Viiper => Path.Combine(LogLocation.Directory, "viiper.log"),
        LogKind.Pan => PanPaths.LogPath,
        _ => throw new InvalidEnumArgumentException(nameof(kind), (int)kind, typeof(LogKind)),
    };
}

/// <summary>A log's file name and why it didn't open, as the player reads them in a toast.</summary>
public static class LogNames
{
    // Windows' error codes from ShellExecute.
    private const int FileNotFound = 2;
    private const int AccessDenied = 5;
    private const int NoAssociation = 1155;

    /// <summary>In German and without a path, unlike the system's message, which may name the user's profile.</summary>
    public static string WhyNotOpened(Exception error) => error switch
    {
        Win32Exception { NativeErrorCode: NoAssociation } => AppTexts.LogFileNoProgram,
        Win32Exception { NativeErrorCode: AccessDenied } or UnauthorizedAccessException => AppTexts.LogFileAccessDenied,
        Win32Exception { NativeErrorCode: FileNotFound } or FileNotFoundException => AppTexts.FileGone,
        TimeoutException => AppTexts.LogFileNoAnswer,
        _ => AppTexts.DetailsInArcadiaLog,
    };

    public static string Of(LogKind kind) => kind switch
    {
        LogKind.Arcadia => "arcadia.log",
        LogKind.Viiper => "viiper.log",
        _ => "pan.log",
    };
}
