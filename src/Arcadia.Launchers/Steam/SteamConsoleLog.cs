using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Follows <c>logs/console_log.txt</c>, where Steam logs every step of a game start (owner's PC, 2026-10-27, #277). A start
/// Steam aborts ends with <c>[2026-10-27 17:27:35] GameAction [AppID 214490, ActionID 1] : LaunchApp failed with
/// AppError_5 with ""</c> within a second of <c>steam://rungameid</c>; Steam waiting for the player (launch option, EULA,
/// cloud conflict) is logged as <c>waiting for user response</c> and is no failure. Only what Steam writes after the first
/// read counts: the history before Arcadia looked holds no start of Arcadia's.
/// </summary>
/// <param name="path">Where the log is, or null while Steam isn't found; asked on every read, Steam's folder may change.</param>
/// <param name="zone">Steam's time stamps are local time; the tests set the zone.</param>
public sealed partial class SteamConsoleLog(Func<string?> path, TimeZoneInfo? zone = null)
{
    /// <summary>Steam's codes whose meaning is confirmed, with the sentence the player reads (owner's PC, 2026-10-27).</summary>
    private static readonly Dictionary<string, string> KnownReasons = new(StringComparer.Ordinal)
    {
        // Steam's own dialog said "Keine Lizenzen" (3000th Duel and another game the account doesn't own).
        ["AppError_5"] = "Das angemeldete Steam-Konto besitzt dieses Spiel nicht.",
    };

    private readonly TimeZoneInfo _zone = zone ?? TimeZoneInfo.Local;
    private string? _readPath;
    private long _offset;
    private string _partialLine = string.Empty;

    /// <summary>The starts Steam aborted since the last call; none if the log can't be read.</summary>
    public IReadOnlyList<RefusedStart> ReadRefusals()
    {
        if (path() is not { } current)
        {
            return [];
        }

        try
        {
            using var stream = new FileStream(current, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (!string.Equals(_readPath, current, StringComparison.OrdinalIgnoreCase))
            {
                // The first read, or another Steam: what was logged before belongs to no start of Arcadia's.
                _readPath = current;
                _offset = stream.Length;
                _partialLine = string.Empty;
                return [];
            }

            if (stream.Length < _offset)
            {
                // Steam started a new file.
                _offset = 0;
                _partialLine = string.Empty;
            }

            stream.Seek(_offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = _partialLine + reader.ReadToEnd();
            _offset = stream.Length;

            var end = text.LastIndexOf('\n');
            _partialLine = end < 0 ? text : text[(end + 1)..];
            return end < 0 ? [] : RefusalsIn(text[..end].Split('\n'));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>The aborted starts in these lines of the log.</summary>
    public IReadOnlyList<RefusedStart> RefusalsIn(IEnumerable<string> lines)
    {
        var refusals = new List<RefusedStart>();
        foreach (var line in lines)
        {
            if (!line.Contains("LaunchApp failed", StringComparison.Ordinal) || FailureLine().Match(line) is not { Success: true } match
                || !DateTime.TryParseExact(match.Groups["at"].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                continue;
            }

            var code = match.Groups["code"].Value;
            var at = new DateTimeOffset(local, _zone.GetUtcOffset(local));
            refusals.Add(new RefusedStart(match.Groups["id"].Value, at, KnownReasons.GetValueOrDefault(code), code));
        }

        return refusals;
    }

    // The code only: what follows it ("with …") may hold a path on the player's PC, which stays out of Arcadia's log.
    [GeneratedRegex(@"^\[(?<at>\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)\] GameAction \[AppID (?<id>\d+), ActionID \d+\] : LaunchApp failed with (?<code>[A-Za-z0-9_]+)")]
    private static partial Regex FailureLine();
}
