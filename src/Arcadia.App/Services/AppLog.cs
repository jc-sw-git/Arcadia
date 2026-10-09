using System.Globalization;

namespace Arcadia.App.Services;

/// <summary>A small append-only log in <c>%LOCALAPPDATA%\Arcadia\logs\arcadia.log</c>.</summary>
public static class AppLog
{
    private static readonly object Gate = new();

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Arcadia", "logs");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(Directory);
                var line = $"{DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} [{Environment.ProcessId}] {message}{Environment.NewLine}";
                File.AppendAllText(Path.Combine(Directory, "arcadia.log"), line);
            }
        }
        catch (IOException)
        {
            // Logging must never break the app.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
