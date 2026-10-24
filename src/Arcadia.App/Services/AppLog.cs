using System.Globalization;
using Arcadia.Core.Diagnostics;

namespace Arcadia.App.Services;

/// <summary>
/// A small append-only log in <c>arcadia.log</c> in <see cref="LogLocation"/>. Every gamepad
/// button lands here, so once the file passes <see cref="MaxSize"/> it moves to
/// <c>arcadia.log.1</c> (replacing the previous one) and a new file starts. Paths in the user's
/// profile are written without the user's name (<see cref="LogPrivacy"/>).
/// </summary>
public static class AppLog
{
    private const long MaxSize = 5 * 1024 * 1024;

    private static readonly object Gate = new();

    private static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Set only by the App's tests, so they never write into the user's own log.</summary>
    public static string Directory { get; internal set; } = LogLocation.Directory;

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

                var line = $"{DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} [{Environment.ProcessId}] {LogPrivacy.WithoutUserProfile(message, UserProfile)}{Environment.NewLine}";
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
