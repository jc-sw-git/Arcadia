using System.Text.Json;
using System.Text.Json.Serialization;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Metadata;

/// <summary>
/// One provider's data of one game as the cache keeps it (#493): what the launcher's local files said, and the store's
/// answer once there is one. Nothing merged, so a change of the order needs no new request.
/// </summary>
/// <param name="GameKey">The game's <see cref="Game.Key"/>.</param>
/// <param name="Source">The provider's name as shown, e.g. "Steam".</param>
/// <param name="Store">The store's answer; null while the game wasn't asked yet (or its answer was unusable).</param>
/// <param name="StoreAt">When the store answered.</param>
public sealed record CachedMetadata(string GameKey, string Source, GameMetadata? Local, GameMetadata? Store, DateTimeOffset? StoreAt)
{
    /// <summary>The provider's data: the local files' fields first, the store's for what they lack.</summary>
    public GameMetadata Data => (Local ?? GameMetadata.Empty).Or(Store);
}

/// <summary>
/// The raw metadata per provider (#493): <c>%LOCALAPPDATA%\Arcadia\metadata\&lt;launcher&gt;\&lt;game&gt;.json</c>, one
/// file per game, each written through a temporary file and a rename. A damaged file is deleted and fetched again; values
/// read back are checked like a store's answer. File names come from the key and never leave the cache.
/// </summary>
public sealed class MetadataCache(string root)
{
    private const string Extension = ".json";

    // A cache file is a few KB; a far larger one is no file Arcadia wrote.
    private const long MaxFile = 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        MaxDepth = 16,
    };

    public string Root { get; } = Path.GetFullPath(root);

    /// <summary>The file of <paramref name="gameKey"/>; its folder is the game's launcher, which a key always names.</summary>
    public string PathOf(string gameKey) =>
        Path.Combine(Root, CacheFiles.NameOf(Game.LauncherIdOf(gameKey)), CacheFiles.NameOf(gameKey) + Extension);

    /// <summary>
    /// Every game in the cache. A file that can't be read as Arcadia wrote it is deleted (and named in <paramref name="log"/>
    /// once, by count); a folder that can't be listed right now is left for the next start.
    /// </summary>
    public IReadOnlyList<CachedMetadata> ReadAll(Action<string> log)
    {
        var found = new List<CachedMetadata>();
        if (!Directory.Exists(Root))
        {
            return found;
        }

        var broken = 0;
        try
        {
            foreach (var folder in Directory.EnumerateDirectories(Root))
            {
                CacheFiles.RemoveTemporaryFiles(folder);
                foreach (var file in Directory.EnumerateFiles(folder, "*" + Extension))
                {
                    if (ReadFile(file) is { } cached)
                    {
                        found.Add(cached);
                    }
                    else
                    {
                        broken++;
                        TryDelete(file);
                    }
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log($"Metadaten: Cache nicht vollständig lesbar: {e.GetType().Name}: {e.Message}");
        }

        if (broken > 0)
        {
            log($"Metadaten: {broken} beschädigte Cache-Datei(en) verworfen, sie werden neu geladen");
        }

        return found;
    }

    /// <summary>Writes the game's file; throws <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> if the disk refuses.</summary>
    public void Write(CachedMetadata cached)
    {
        var path = PathOf(cached.GameKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        CacheFiles.WriteThroughTemporary(path, JsonSerializer.SerializeToUtf8Bytes(Stored.Of(cached), Json));
    }

    /// <summary>The file's content, checked; null if it isn't a file Arcadia wrote for the key its name stands for.</summary>
    private CachedMetadata? ReadFile(string file)
    {
        try
        {
            if (new FileInfo(file).Length > MaxFile)
            {
                return null;
            }

            var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllBytes(file), Json);
            if (stored?.Game is not { Length: > 0 } key || !key.Contains(':') || PathOf(key) != Path.GetFullPath(file)
                || stored.Source is not { Length: > 0 and <= MetadataCheck.MaxName } source)
            {
                return null;
            }

            return new CachedMetadata(
                key,
                source,
                stored.Local is null ? null : MetadataCheck.Checked(stored.Local),
                stored.Store is null ? null : MetadataCheck.Checked(stored.Store),
                stored.StoreAt);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left for the next start; its game is fetched again meanwhile.
        }
    }

    private sealed class Stored
    {
        public string? Game { get; set; }

        public string? Source { get; set; }

        public GameMetadata? Local { get; set; }

        public GameMetadata? Store { get; set; }

        public DateTimeOffset? StoreAt { get; set; }

        public static Stored Of(CachedMetadata cached) => new()
        {
            Game = cached.GameKey,
            Source = cached.Source,
            Local = Escaped(cached.Local),
            Store = Escaped(cached.Store),
            StoreAt = cached.StoreAt,
        };

        /// <summary>
        /// The checked texts written as HTML text, the way a store would: reading checks them as markup again, and a
        /// "&lt;" or "&amp;amp;" in a description must come back as it was shown, not decoded a second time.
        /// </summary>
        private static GameMetadata? Escaped(GameMetadata? metadata) => metadata is null ? null : metadata with
        {
            Description = Escape(metadata.Description),
            ShortDescription = Escape(metadata.ShortDescription),
            Developers = [.. metadata.Developers.Select(name => Escape(name)!)],
            Publishers = [.. metadata.Publishers.Select(name => Escape(name)!)],
            Tags = [.. metadata.Tags.Select(tag => Escape(tag)!)],
        };

        private static string? Escape(string? text) => text is null ? null : System.Net.WebUtility.HtmlEncode(text);
    }
}
