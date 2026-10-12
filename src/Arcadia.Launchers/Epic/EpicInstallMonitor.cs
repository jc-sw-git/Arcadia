using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Epic;

/// <summary>
/// Follows Epic installations through three files the launcher keeps up to date
/// (verified on the owner's PC, 2026-10-10):
/// <list type="bullet">
/// <item><c>Data\DownloadManager\DownloadState_&lt;account&gt;.json</c> — the download queue in order,
/// including paused entries; empty once nothing is pending.</item>
/// <item><c>EpicGamesLauncher.log</c> — queue, start, resume, pause, cancel, completion and uninstall per app.</item>
/// <item><c>Data\Manifests\Pending\*.item</c> — install size and location of a started installation.
/// It stays behind after a cancel, so it only supplies sizes, never the state.</item>
/// </list>
/// Progress is the size of the install directory against the manifest's install size: Epic
/// downloads into <c>.egstore</c> inside that directory and moves the files out at the end.
/// </summary>
public sealed partial class EpicInstallMonitor(string? dataDirectory = null, string? logPath = null) : IInstallMonitor
{
    private readonly string _dataDirectory = dataDirectory ?? DefaultDataDirectory();
    private readonly string _logPath = logPath ?? DefaultLogPath();
    private readonly Dictionary<string, EpicInstallEvent> _lastEvent = new(StringComparer.Ordinal);
    private readonly HashSet<string> _uninstalling = new(StringComparer.Ordinal);

    // Epic downloads one app at a time. Resuming a paused app while another downloads cancels the
    // running one without logging a pause (owner's log, 2026-10-12), so only the latest start runs.
    private string? _latestStart;

    // Apps the log reported on while Arcadia was watching. For them the log decides whether an
    // installation is open; the queue file only orders them. Epic doesn't always rewrite the queue
    // file right away (seen when a paused install was cancelled while another one ran).
    private readonly HashSet<string> _seenLive = new(StringComparer.Ordinal);
    private long _logOffset;
    private string _partialLine = string.Empty;
    private bool _primed;

    public string LauncherId => KnownLaunchers.Epic.Id;

    public InstallSnapshot Poll()
    {
        // The first read only learns the current state; past completions are not news.
        var newEvents = ReadNewLogLines().Select(ParseLogLine).OfType<(string App, EpicInstallEvent Event)>().ToList();
        var finished = new Dictionary<string, InstallOutcome>(StringComparer.Ordinal);
        foreach (var (app, installEvent) in newEvents)
        {
            _lastEvent[app] = installEvent;

            // An uninstall runs as a task too, but whether it stops a download is unknown; assume not.
            if (installEvent == EpicInstallEvent.Started && !_uninstalling.Contains(app))
            {
                _latestStart = app;
            }

            if (_primed)
            {
                _seenLive.Add(app);
            }

            // An uninstall runs through the download queue like an install: "Uninstall", then
            // "Queued install", "StartTask" and "HandleTaskComplete". Only the first line tells them apart.
            if (installEvent == EpicInstallEvent.Uninstalled)
            {
                _uninstalling.Add(app);
                continue;
            }

            var outcome = OutcomeOf(installEvent);
            if (outcome is not null && _uninstalling.Remove(app))
            {
                outcome = outcome == InstallOutcome.Completed ? InstallOutcome.Uninstalled : InstallOutcome.Failed;
            }

            if (_primed && outcome is { } finishedOutcome)
            {
                finished[app] = finishedOutcome;
            }
        }

        _primed = true;

        var queue = ReadQueue();
        var open = queue.Where(app => !_seenLive.Contains(app) || IsOpen(app))
            .Concat(_seenLive.Where(app => !queue.Contains(app) && IsOpen(app)).Order(StringComparer.Ordinal))
            .ToList();

        var pending = ReadPendingManifests();
        var active = new Dictionary<string, InstallProgress>(StringComparer.Ordinal);
        var waiting = 0;
        foreach (var app in open)
        {
            _lastEvent.TryGetValue(app, out var last);
            if (_uninstalling.Contains(app))
            {
                active[app] = new InstallProgress(InstallPhase.Uninstalling);
            }
            else if (last is EpicInstallEvent.Started or EpicInstallEvent.Paused)
            {
                var fraction = pending.TryGetValue(app, out var manifest) ? FractionOf(manifest) : null;
                // A complete download moving its files may overlap the next app's start; it isn't paused.
                var phase = fraction >= 0.999 && last == EpicInstallEvent.Started ? InstallPhase.Finishing
                    : last == EpicInstallEvent.Paused || app != _latestStart ? InstallPhase.Paused
                    : InstallPhase.Downloading;
                active[app] = new InstallProgress(phase, fraction);
            }
            else
            {
                active[app] = new InstallProgress(InstallPhase.Queued, QueuePosition: ++waiting);
            }
        }

        return new InstallSnapshot(active, finished);
    }

    private bool IsOpen(string app) => _uninstalling.Contains(app)
        || (_lastEvent.TryGetValue(app, out var last) && last is EpicInstallEvent.Queued or EpicInstallEvent.Started or EpicInstallEvent.Paused);

    /// <summary>The app (Epic AppName) and event a log line reports, or null for unrelated lines.</summary>
    public static (string App, EpicInstallEvent Event)? ParseLogLine(string line)
    {
        if (!line.Contains("LogDownloadManager", StringComparison.Ordinal)
            && !line.Contains("LogEoshPatcher", StringComparison.Ordinal)
            && !line.Contains("LogCommunityPortalAppManager", StringComparison.Ordinal))
        {
            return null;
        }

        if (CompleteLine().Match(line) is { Success: true } complete)
        {
            var ok = complete.Groups["code"].Value == "ok";
            return AppOf(complete.Groups["id"].Value) is { } app ? (app, ok ? EpicInstallEvent.Completed : EpicInstallEvent.Failed) : null;
        }

        foreach (var (pattern, installEvent) in Patterns)
        {
            if (pattern.Match(line) is { Success: true } match && AppOf(match.Groups["id"].Value) is { } app)
            {
                return (app, installEvent);
            }
        }

        return null;
    }

    private static readonly (Regex Pattern, EpicInstallEvent Event)[] Patterns =
    [
        (QueuedLine(), EpicInstallEvent.Queued),
        (StartLine(), EpicInstallEvent.Started),
        (ResumeLine(), EpicInstallEvent.Started),
        (PauseLine(), EpicInstallEvent.Paused),
        (CancelLine(), EpicInstallEvent.Cancelled),
        (UninstallLine(), EpicInstallEvent.Uninstalled),
    ];

    /// <summary>"namespace:item:app[:version]" → app.</summary>
    private static string? AppOf(string id)
    {
        var parts = id.Split(':');
        return parts.Length >= 3 && parts[2].Length > 0 ? parts[2] : null;
    }

    private static InstallOutcome? OutcomeOf(EpicInstallEvent installEvent) => installEvent switch
    {
        EpicInstallEvent.Completed => InstallOutcome.Completed,
        EpicInstallEvent.Cancelled => InstallOutcome.Cancelled,
        EpicInstallEvent.Failed => InstallOutcome.Failed,
        EpicInstallEvent.Uninstalled => InstallOutcome.Uninstalled,
        _ => null,
    };

    [GeneratedRegex(@"HandleTaskComplete: AppId \[(?<id>[^\]]+)\] AlertCode=\[(?<code>[^\]]*)\]")]
    private static partial Regex CompleteLine();

    [GeneratedRegex(@"Queued install for the (?<id>[^\s,]+)")]
    private static partial Regex QueuedLine();

    [GeneratedRegex(@"StartTask: BinaryId \[(?<id>[^\]]+)\]")]
    private static partial Regex StartLine();

    [GeneratedRegex(@"Resume of active installation is requested for the (?<id>\S+)")]
    private static partial Regex ResumeLine();

    [GeneratedRegex(@"Pause is requested for the (?<id>\S+)")]
    private static partial Regex PauseLine();

    [GeneratedRegex(@"Cancel of active installation is requested for the (?<id>\S+)")]
    private static partial Regex CancelLine();

    [GeneratedRegex(@"FCommunityPortalAppManagerImpl: Uninstall (?<id>\S+)")]
    private static partial Regex UninstallLine();

    private List<string> ReadNewLogLines()
    {
        var lines = new List<string>();
        try
        {
            using var stream = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            // Epic starts a new log (and backs up the old one) on every launcher start.
            if (stream.Length < _logOffset)
            {
                _logOffset = 0;
                _partialLine = string.Empty;
                _lastEvent.Clear();
                _uninstalling.Clear();
                _seenLive.Clear();
                _latestStart = null;
            }

            stream.Seek(_logOffset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = _partialLine + reader.ReadToEnd();
            _logOffset = stream.Length;

            var end = text.LastIndexOf('\n');
            _partialLine = end < 0 ? text : text[(end + 1)..];
            if (end >= 0)
            {
                lines.AddRange(text[..end].Split('\n'));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // No log yet (Epic never started) or briefly locked: nothing new this round.
        }

        return lines;
    }

    /// <summary>App names in Epic's download queue, in order.</summary>
    private List<string> ReadQueue()
    {
        var directory = Path.Combine(_dataDirectory, "DownloadManager");
        var queue = new List<string>();
        try
        {
            if (!Directory.Exists(directory))
            {
                return queue;
            }

            var file = new DirectoryInfo(directory).GetFiles("DownloadState_*.json")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (file is null)
            {
                return queue;
            }

            using var document = JsonDocument.Parse(ReadShared(file.FullName));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return queue;
            }

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object
                    && entry.TryGetProperty("AppId", out var appId)
                    && appId.ValueKind == JsonValueKind.Object
                    && appId.TryGetProperty("AppName", out var name)
                    && name.GetString() is { Length: > 0 } app
                    && !queue.Contains(app))
                {
                    queue.Add(app);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Epic rewrites the file while we read it; the next poll gets it.
        }

        return queue;
    }

    private sealed record PendingManifest(string InstallLocation, long InstallSize);

    private Dictionary<string, PendingManifest> ReadPendingManifests()
    {
        var result = new Dictionary<string, PendingManifest>(StringComparer.Ordinal);
        var directory = Path.Combine(_dataDirectory, "Manifests", "Pending");
        try
        {
            if (!Directory.Exists(directory))
            {
                return result;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.item"))
            {
                try
                {
                    using var document = JsonDocument.Parse(ReadShared(file));
                    var root = document.RootElement;
                    if (root.ValueKind == JsonValueKind.Object
                        && root.TryGetProperty("AppName", out var app) && app.GetString() is { Length: > 0 } appName
                        && root.TryGetProperty("InstallLocation", out var location) && location.GetString() is { Length: > 0 } installLocation
                        && root.TryGetProperty("InstallSize", out var size) && size.TryGetInt64(out var installSize) && installSize > 0)
                    {
                        result[appName] = new PendingManifest(installLocation, installSize);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    // Being written; skip this round.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Directory vanished between the check and the enumeration.
        }

        return result;
    }

    private static double? FractionOf(PendingManifest manifest)
    {
        var size = DirectorySize(manifest.InstallLocation);
        return size is null ? null : Math.Clamp((double)size.Value / manifest.InstallSize, 0, 1);
    }

    private static long? DirectorySize(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return 0;
            }

            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            return new DirectoryInfo(path).EnumerateFiles("*", options).Sum(f => f.Length);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string DefaultDataDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data");

    private static string DefaultLogPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EpicGamesLauncher", "Saved", "Logs", "EpicGamesLauncher.log");
}

/// <summary>What the Epic log says happened to an installation.</summary>
public enum EpicInstallEvent
{
    Queued,

    /// <summary>Downloading started or resumed.</summary>
    Started,

    Paused,
    Cancelled,
    Completed,
    Failed,
    Uninstalled,
}
