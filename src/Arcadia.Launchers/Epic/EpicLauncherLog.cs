using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Arcadia.Launchers.Epic;

/// <summary>One play session the Epic launcher tracked. <see cref="End"/> is null while the game runs or if the launcher died.</summary>
public sealed record EpicPlaySession(string AppName, DateTimeOffset Start, DateTimeOffset? End);

/// <summary>What the launcher last reported about an installed app (<c>get-installed-apps-response</c>).</summary>
public sealed record EpicInstalledAppState(string InstalledVersion, bool HasUpdate);

/// <summary>Everything Arcadia takes from one Epic log file.</summary>
/// <param name="InstalledApps">The last installed-apps report in the file, by app name, or null if the file has none.</param>
public sealed record EpicLogFacts(IReadOnlyList<EpicPlaySession> Sessions, IReadOnlyDictionary<string, EpicInstalledAppState>? InstalledApps)
{
    public static EpicLogFacts Empty { get; } = new([], null);
}

/// <summary>
/// Reads play sessions and update state from the Epic launcher's logs (verified on the owner's PC,
/// 2026-10-12). Epic keeps the total playtime only on its servers; locally the log has
/// <c>Started/Stopped tracking playtime for &lt;ns&gt;:&lt;item&gt;:&lt;app&gt;</c> per session, and on
/// every launcher start a JSON report of the installed apps with <c>hasUpdate</c>. Log times are UTC.
/// </summary>
public static partial class EpicLauncherLog
{
    private const string InstalledAppsMarker = "\"name\":\"get-installed-apps-response\"";
    private const string ResponsePrefix = "Sending response: ";

    /// <summary>
    /// The launcher's logs, oldest first: the backups Epic rotates at each start
    /// (<c>EpicGamesLauncher-backup-&lt;date&gt;.log</c>) and the current one.
    /// </summary>
    public static IReadOnlyList<string> Files(string logDirectory)
    {
        try
        {
            if (!Directory.Exists(logDirectory))
            {
                return [];
            }

            return Directory.GetFiles(logDirectory, "EpicGamesLauncher*.log")
                .Select(path => new FileInfo(path))
                .OrderBy(file => file.LastWriteTimeUtc)
                .Select(file => file.FullName)
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static EpicLogFacts Parse(IEnumerable<string> lines)
    {
        var sessions = new List<EpicPlaySession>();
        var open = new Dictionary<string, int>(StringComparer.Ordinal);
        IReadOnlyDictionary<string, EpicInstalledAppState>? installedApps = null;
        foreach (var line in lines)
        {
            if (line.Contains("tracking playtime for ", StringComparison.Ordinal) && PlaytimeLine().Match(line) is { Success: true } match
                && ParseTime(match.Groups["time"].Value) is { } time)
            {
                var app = match.Groups["app"].Value;
                if (match.Groups["what"].Value == "Started")
                {
                    open[app] = sessions.Count;
                    sessions.Add(new EpicPlaySession(app, time, null));
                }
                else if (open.Remove(app, out var index) && time >= sessions[index].Start)
                {
                    sessions[index] = sessions[index] with { End = time };
                }
            }
            else if (line.Contains(InstalledAppsMarker, StringComparison.Ordinal) && ParseInstalledApps(line) is { } report)
            {
                installedApps = report;
            }
        }

        return new EpicLogFacts(sessions, installedApps);
    }

    public static EpicLogFacts ParseFile(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return Parse(ReadLines(reader));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return EpicLogFacts.Empty;
        }
    }

    private static IEnumerable<string> ReadLines(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static Dictionary<string, EpicInstalledAppState>? ParseInstalledApps(string line)
    {
        var start = line.IndexOf(ResponsePrefix, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line.AsMemory(start + ResponsePrefix.Length));
            if (!document.RootElement.TryGetProperty("payload", out var payload)
                || !payload.TryGetProperty("products", out var products)
                || products.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var apps = new Dictionary<string, EpicInstalledAppState>(StringComparer.Ordinal);
            foreach (var product in products.EnumerateArray())
            {
                if (product.ValueKind == JsonValueKind.Object
                    && product.TryGetProperty("appName", out var app) && app.ValueKind == JsonValueKind.String
                    && product.TryGetProperty("installedVersion", out var version) && version.ValueKind == JsonValueKind.String)
                {
                    var hasUpdate = product.TryGetProperty("hasUpdate", out var update) && update.ValueKind == JsonValueKind.True;
                    apps[app.GetString()!] = new EpicInstalledAppState(version.GetString()!, hasUpdate);
                }
            }

            return apps;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateTimeOffset? ParseTime(string value) =>
        DateTime.TryParseExact(value, "yyyy.MM.dd-HH.mm.ss", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var time)
            ? new DateTimeOffset(time, TimeSpan.Zero)
            : null;

    [GeneratedRegex(@"^\[(?<time>\d{4}\.\d{2}\.\d{2}-\d{2}\.\d{2}\.\d{2}):\d+\]\[[ \d]*\]LogPortalPublishTime: (?<what>Started|Stopped) tracking playtime for [^:\s]+:[^:\s]+:(?<app>[^:\s]+)\s*$")]
    private static partial Regex PlaytimeLine();
}
