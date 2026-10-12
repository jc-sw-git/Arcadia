using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Arcadia.Launchers.Steam;

/// <summary>What Steam's content log last said about one app's download.</summary>
/// <param name="IsRunning">Steam is working on this app right now (<c>Update Running</c>), not just queueing it.</param>
/// <param name="IsFinishing">The data is complete; Steam verifies or moves the files (<c>Verifying Staged</c>, <c>Committing</c>).</param>
public sealed record SteamContentState(bool IsRunning, bool IsFinishing);

/// <summary>
/// Follows <c>logs/content_log.txt</c>, where Steam writes every state change of its downloads
/// (verified on the owner's PC, 2026-10-12): <c>AppID &lt;id&gt; state changed : Update Required,Fully
/// Installed,Update Queued,Update Running,…</c> and <c>AppID &lt;id&gt; App update changed : Running
/// Update,Downloading,Staging,</c>. Unlike the app manifests, which flag a waiting and a running
/// download alike, only the app Steam works on carries <c>Update Running</c>.
/// Steam moves the file to <c>content_log.previous.txt</c> at about 4 MB and starts a new one.
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
            _offset = stream.Length;

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
            if (!line.Contains("AppID ", StringComparison.Ordinal) || ChangeLine().Match(line) is not { Success: true } match
                || !uint.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var appId))
            {
                continue;
            }

            var flags = match.Groups["flags"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var current = _states.GetValueOrDefault(appId) ?? new SteamContentState(false, false);
            _states[appId] = match.Groups["kind"].Value == "state"
                ? current with { IsRunning = flags.Contains("Update Running"), IsFinishing = current.IsFinishing && flags.Contains("Update Running") }
                : current with { IsFinishing = flags.Contains("Committing") || flags.Contains("Verifying Staged") };
        }
    }

    [GeneratedRegex(@"\] AppID (?<id>\d+) (?<kind>state|App update) changed : (?<flags>[^(\r]*)")]
    private static partial Regex ChangeLine();
}
