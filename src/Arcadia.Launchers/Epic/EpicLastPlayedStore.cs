using System.Text.Json;

namespace Arcadia.Launchers.Epic;

/// <summary>
/// Remembers when each Epic game was last played. Epic's logs hold only the last few launcher
/// starts, so without this a game's "last played" would vanish once its session rotates out.
/// </summary>
public sealed class EpicLastPlayedStore(string path)
{
    private readonly Lock _lock = new();
    private Dictionary<string, DateTimeOffset>? _lastPlayed;

    public static EpicLastPlayedStore CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Arcadia", "epic-last-played.json"));

    /// <summary>Adds the sessions' start times, saves if anything got newer, and returns the latest start per app.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Merge(IEnumerable<EpicPlaySession> sessions)
    {
        lock (_lock)
        {
            _lastPlayed ??= Load();
            var changed = false;
            foreach (var session in sessions)
            {
                if (!_lastPlayed.TryGetValue(session.AppName, out var known) || session.Start > known)
                {
                    _lastPlayed[session.AppName] = session.Start;
                    changed = true;
                }
            }

            if (changed)
            {
                Save(_lastPlayed);
            }

            return new Dictionary<string, DateTimeOffset>(_lastPlayed, StringComparer.Ordinal);
        }
    }

    // A missing or broken file only loses dates the logs may still have; start over rather than fail the scan.
    private Dictionary<string, DateTimeOffset> Load()
    {
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<Dictionary<string, DateTimeOffset>>(File.ReadAllText(path)) is { } stored)
            {
                return new Dictionary<string, DateTimeOffset>(stored, StringComparer.Ordinal);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        return new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
    }

    private void Save(Dictionary<string, DateTimeOffset> lastPlayed)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(lastPlayed));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not saved: the dates still come from the logs next time, as long as Epic keeps them.
        }
    }
}
