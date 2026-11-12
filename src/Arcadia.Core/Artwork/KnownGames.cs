using System.Text.Json;

namespace Arcadia.Core.Artwork;

/// <summary>
/// The games Arcadia has seen in a library (#506), kept across restarts: SteamGridDB is asked on its own only for a game
/// not in it, once. A game leaving the library stays in it, so coming back it isn't new. With no list but images in the
/// cache (the first start with this version) or a damaged list, every game of the first libraries read counts as known:
/// nothing is searched then. At the very first start of Arcadia (no list, no images) every game is new.
/// </summary>
/// <remarks>Used by the artwork run only, one thread at a time. Written through a temporary file, then renamed.</remarks>
internal sealed class KnownGames(string path)
{
    public const string FileName = "known-games.json";

    private HashSet<string>? _keys;

    // No usable list was read: the first libraries are taken as known as a whole.
    private bool _fresh;
    private bool _dirty;

    public Action<string> Log { get; init; } = _ => { };

    /// <summary>
    /// The games of <paramref name="keys"/> Arcadia hasn't seen; empty the first time without a stored list, which then
    /// holds them all.
    /// </summary>
    public IReadOnlySet<string> NewOf(IEnumerable<string> keys)
    {
        var known = _keys ??= Read();
        if (_fresh)
        {
            _fresh = false;
            _dirty = true;
            known.UnionWith(keys);
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return keys.Where(key => !known.Contains(key)).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The game was searched (or the player stopped the search before it): never new again.</summary>
    public void Add(string key)
    {
        if ((_keys ??= Read()).Add(key))
        {
            _dirty = true;
        }
    }

    /// <summary>Writes what changed; a failure is logged, the list then lasts until Arcadia closes.</summary>
    public void Save()
    {
        if (!_dirty || _keys is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temporary = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(_keys.Order(StringComparer.Ordinal).ToList()));
            File.Move(temporary, path, overwrite: true);
            _dirty = false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log($"Artwork: Liste bekannter Spiele nicht gespeichert: {e.GetType().Name}: {e.Message}");
        }
    }

    private HashSet<string> Read()
    {
        try
        {
            RemoveTemporaryFiles();
            if (!File.Exists(path) && !HoldsArtwork())
            {
                // A first start of Arcadia at all: every game it reads is new.
                return new HashSet<string>(StringComparer.Ordinal);
            }

            if (File.Exists(path))
            {
                var stored = JsonSerializer.Deserialize<List<string?>>(File.ReadAllBytes(path));
                if (stored is not null)
                {
                    return stored.OfType<string>().Where(key => key.Length > 0).ToHashSet(StringComparer.Ordinal);
                }

                Log("Artwork: Liste bekannter Spiele leer; die Spiele der Bibliothek gelten als bekannt");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log($"Artwork: Liste bekannter Spiele nicht lesbar ({e.GetType().Name}); die Spiele der Bibliothek gelten als bekannt");
        }

        _fresh = true;
        return new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>The cache holds images already: Arcadia ran before this list existed.</summary>
    private bool HoldsArtwork()
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        return Directory.Exists(folder) && Directory.EnumerateDirectories(folder).Any(d => !Path.GetFileName(d).StartsWith('.'));
    }

    private void RemoveTemporaryFiles()
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        if (Directory.Exists(folder))
        {
            foreach (var leftover in Directory.EnumerateFiles(folder, Path.GetFileName(path) + ".*.tmp"))
            {
                try
                {
                    File.Delete(leftover);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // A leftover that can't go now goes at a later start; the stored list is still read.
                    Log($"Artwork: übrig gebliebene Datei der Liste bekannter Spiele nicht gelöscht ({e.GetType().Name})");
                }
            }
        }
    }
}
