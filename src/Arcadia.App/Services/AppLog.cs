using System.Globalization;

namespace Arcadia.App.Services;

/// <summary>
/// A small append-only log in <c>%LOCALAPPDATA%\Arcadia\logs\arcadia.log</c>. Every gamepad
/// button lands here, so once the file passes <see cref="MaxSize"/> it moves to
/// <c>arcadia.log.1</c> (replacing the previous one) and a new file starts.
/// </summary>
public static class AppLog
{
    private const long MaxSize = 5 * 1024 * 1024;

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
                var path = Path.Combine(Directory, "arcadia.log");
                if (File.Exists(path) && new FileInfo(path).Length > MaxSize)
                {
                    File.Move(path, path + ".1", overwrite: true);
                }

                var line = $"{DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} [{Environment.ProcessId}] {message}{Environment.NewLine}";
                File.AppendAllText(path, line);
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
