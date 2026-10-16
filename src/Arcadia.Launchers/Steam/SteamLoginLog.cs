using System.Text;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Reads <c>logs/steamui_login.txt</c>, where Steam logs each login of its client (verified on the
/// owner's PC, 2026-10-16): <c>[ None ] Starting login</c>, then the states up to
/// <c>[ WaitingForLibraryReady ] SetLoginState: Success - OK</c>. Steam sets the active user in the
/// registry as soon as the server accepts the login, but can still hang before <c>Success</c> (seen
/// when the Steam Controller stopped answering, 2026-10-16); its window then stays at "loading user data".
/// </summary>
public static class SteamLoginLog
{
    // One login takes about 1 KB; the file only grows, so its end holds the latest one.
    private const int TailBytes = 16 * 1024;

    /// <summary>
    /// Whether Steam's latest login reached <c>Success</c>; null if the log can't be read or names
    /// no login (then callers must not block on it).
    /// </summary>
    public static bool? IsLatestLoginComplete(string path)
    {
        string tail;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(Math.Max(0, stream.Length - TailBytes), SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            tail = reader.ReadToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return IsLatestLoginCompleteIn(tail);
    }

    public static bool? IsLatestLoginCompleteIn(string log)
    {
        var start = log.LastIndexOf("] Starting login", StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        return log.IndexOf("SetLoginState: Success", start, StringComparison.Ordinal) >= 0;
    }
}
