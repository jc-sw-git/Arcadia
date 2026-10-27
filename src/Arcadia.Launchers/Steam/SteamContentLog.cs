using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Arcadia.Launchers.Steam;

/// <summary>Where one app's download stands, as Steam's content log last told it.</summary>
public enum SteamDownloadPhase
{
    /// <summary>In the queue: never started, or suspended because another download took over (<c>Priority</c>).</summary>
    Queued,

    /// <summary>Steam works on the app but doesn't download yet: it verifies, reconfigures or preallocates.</summary>
    Preparing,

    Downloading,

    /// <summary>Suspended by the user (<c>Disabled</c>), on a logoff, or for any other reason than another download.</summary>
    Paused,

    /// <summary>The data is complete; Steam verifies or moves the files (<c>Verifying Staged</c>, <c>Committing</c>).</summary>
    Finishing,

    /// <summary>Nothing left to do: the update finished or the app no longer needs one.</summary>
    Done,
}

/// <summary>What Steam's content log last said about one app's download.</summary>
/// <param name="Started">Steam has begun this download (now or before a pause), so it is no mere scheduled update.</param>
/// <param name="DownloadedAtStart">Download bytes at the last start or resume (<c>update started : download a/b</c>).</param>
/// <param name="DownloadTotal">Download bytes of the whole update, from the same line; 0 before it was seen.</param>
/// <param name="FromEarlierSession">
/// Steam has restarted or switched accounts since; whatever this app did then has stopped unless Steam mentions it again.
/// </param>
public sealed record SteamContentState(
    SteamDownloadPhase Phase,
    bool Started = false,
    long DownloadedAtStart = 0,
    long DownloadTotal = 0,
    bool FromEarlierSession = false)
{
    /// <summary>True while Steam works on the app now rather than letting it wait or rest.</summary>
    public bool IsRunning => !FromEarlierSession && Phase is SteamDownloadPhase.Preparing or SteamDownloadPhase.Downloading or SteamDownloadPhase.Finishing;
}

/// <summary>
/// Follows <c>logs/content_log.txt</c>, where Steam writes every state change of its downloads the moment it happens
/// (owner's PC, 2026-10-12 and 2026-10-27; lines in <c>docs/launchers/steam/library-and-installs.md</c>). The app manifests flag a waiting
/// and a running download alike and get their byte counters only now and then; the log tells them apart, names pauses
/// and their reason, and gives the exact download bytes at every start and resume. It has no per-app progress while a
/// download runs. Steam moves the file to <c>content_log.previous.txt</c> at about 4 MB and starts a new one.
/// </summary>
public sealed partial class SteamContentLog(string path)
{
    private readonly Dictionary<uint, SteamContentState> _states = [];
    private long _offset;
    private string _partialLine = string.Empty;

    /// <summary>Reads what Steam wrote since the last call; false if the log can't be read.</summary>
    public bool Update()
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < _offset)
            {
                // A new file after the rotation; the states learned so far stay true until Steam changes them.
                _offset = 0;
                _partialLine = string.Empty;
            }

            stream.Seek(_offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = _partialLine + reader.ReadToEnd();
            _offset = stream.Position; // not Length: Steam may have appended after ReadToEnd

            var end = text.LastIndexOf('\n');
            _partialLine = end < 0 ? text : text[(end + 1)..];
            if (end >= 0)
            {
                Apply(text[..end].Split('\n'));
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The last known state of an app, or null if the log hasn't mentioned it.</summary>
    public SteamContentState? StateOf(uint appId) => _states.GetValueOrDefault(appId);

    public void Apply(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (line.Contains("] Client version:", StringComparison.Ordinal))
            {
                // Steam started anew, maybe with another account: nothing runs until Steam says so again.
                foreach (var (earlier, state) in _states.ToList())
                {
                    _states[earlier] = state with { FromEarlierSession = true };
                }

                continue;
            }

            if (!line.Contains("] AppID ", StringComparison.Ordinal) || AppLine().Match(line) is not { Success: true } match
                || !uint.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
            {
                continue;
            }

            var current = _states.GetValueOrDefault(appId) ?? new SteamContentState(SteamDownloadPhase.Done);
            if (Next(current, match.Groups["event"].Value, match.Groups["rest"].Value) is { } next)
            {
                _states[appId] = next;
            }
        }
    }

    // A download that ended forgets its bytes, so the next update of the app doesn't show them.
    private static readonly SteamContentState Over = new(SteamDownloadPhase.Done);

    // Null when the line says nothing about the download (prefetch, scheduler, preallocated files …).
    private static SteamContentState? Next(SteamContentState current, string kind, string rest)
    {
        var live = current with { FromEarlierSession = false };
        switch (kind)
        {
            case "update started":
                var bytes = StartedBytes().Match(rest);
                return bytes.Success
                    && long.TryParse(bytes.Groups["done"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var done)
                    && long.TryParse(bytes.Groups["total"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var total)
                    ? live with { Phase = SteamDownloadPhase.Downloading, Started = true, DownloadedAtStart = done, DownloadTotal = total }
                    : live with { Phase = SteamDownloadPhase.Downloading, Started = true };

            case "update canceled":
                // "Priority (Suspended)": another download took over. "Disabled (Suspended)": paused by the user, also on a
                // logoff. "Uninstall (Canceled)": the download is dropped for good.
                return rest.Contains("(Canceled)", StringComparison.Ordinal) ? Over
                    : rest.StartsWith("Priority", StringComparison.Ordinal) ? live with { Phase = SteamDownloadPhase.Queued }
                    : live with { Phase = SteamDownloadPhase.Paused };

            case "finished update":
                return Over;

            case "App update changed":
                // "…,Stopping," follows every "update canceled", whose reason already said what comes next.
                var steps = Flags(rest);
                return steps.Contains("Stopping") ? null
                    : steps.Contains("Committing") || steps.Contains("Verifying Staged") ? live with { Phase = SteamDownloadPhase.Finishing }
                    : steps.Contains("Downloading") ? live with { Phase = SteamDownloadPhase.Downloading }
                    : steps.Contains("Preallocating") || steps.Contains("Verifying Installed") || steps.Contains("Reconfiguring")
                        ? live with { Phase = SteamDownloadPhase.Preparing }
                    : null;

            case "state changed":
                return StateChanged(live, Flags(rest), afterRestart: current.FromEarlierSession);

            default:
                return null;
        }
    }

    private static SteamContentState StateChanged(SteamContentState state, string[] flags, bool afterRestart)
    {
        var started = state.Started || flags.Contains("Update Started");
        if (!flags.Contains("Update Required"))
        {
            // Committed (or never needed an update): whatever follows is bookkeeping.
            return Over;
        }

        if (flags.Contains("Update Running"))
        {
            return state.IsRunning ? state with { Started = started } : state with { Phase = SteamDownloadPhase.Preparing, Started = started };
        }

        // Out of the queue (the user removed it, or it is being uninstalled): nothing to show.
        if (!flags.Contains("Update Queued"))
        {
            return flags.Contains("Update Paused") ? state with { Phase = SteamDownloadPhase.Paused, Started = started } : Over;
        }

        // Not running: after an "update canceled" the reason has already set Paused or Queued; otherwise it waits.
        // A new session lists every download it will resume as queued, the ones paused before included.
        return !afterRestart && state.Phase is SteamDownloadPhase.Paused or SteamDownloadPhase.Queued
            ? state with { Started = started }
            : state with { Phase = SteamDownloadPhase.Queued, Started = started };
    }

    // "Update Required,Update Queued,Update Started, (Suspended)": the part in brackets is a note, no flag.
    private static string[] Flags(string rest)
    {
        var note = rest.IndexOf('(');
        return (note < 0 ? rest : rest[..note]).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    [GeneratedRegex(@"\] AppID (?<id>\d+) (?<event>state changed|App update changed|update started|update canceled|finished update)\b(?: : )?(?<rest>[^\r]*)")]
    private static partial Regex AppLine();

    [GeneratedRegex(@"^download (?<done>\d+)/(?<total>\d+)")]
    private static partial Regex StartedBytes();
}
