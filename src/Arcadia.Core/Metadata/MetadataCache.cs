using System.Text.Json;
using System.Text.Json.Serialization;
using Arcadia.Core.Library;
using Arcadia.Core.Metadata.Igdb;
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
/// <param name="Igdb">IGDB's answer (#494); null while IGDB wasn't asked or found nothing by the game's name.</param>
/// <param name="IgdbByName">IGDB's search by name while the game waits for the player's choice or after a skip; null otherwise.</param>
/// <param name="StoreLanguage">
/// The UI language the store was asked in, by two-letter code (#557); an answer from before languages were asked is
/// German. Its texts stay in it until "Alle Metadaten neu laden".
/// </param>
public sealed record CachedMetadata(
    string GameKey,
    string Source,
    GameMetadata? Local,
    GameMetadata? Store,
    DateTimeOffset? StoreAt,
    IgdbCached? Igdb = null,
    IgdbNameMatch? IgdbByName = null,
    string? StoreLanguage = null)
{
    /// <summary>The provider's data: the local files' fields first, the store's for what they lack.</summary>
    public GameMetadata Data => (Local ?? GameMetadata.Empty).Or(Store);
}

/// <summary>
/// IGDB's answer for one game (#494): the IGDB game it is (null: IGDB doesn't know the game's Steam AppID, so it has no
/// data and is not searched by name, D3), that game's title and its data, unmerged.
/// </summary>
/// <param name="Reading">How the answer was read (<see cref="Igdb.IgdbAnswer.Reading"/>); an older reading is asked again.</param>
public sealed record IgdbCached(long? GameId, string? Title, GameMetadata Data, DateTimeOffset At, int Reading = Igdb.IgdbAnswer.Reading);

/// <summary>
/// A search by name with several hits and "Selbst auswählen" (#494): the game waits on "Warten auf deine Auswahl" marked
/// "Metadaten" with its hits (names and data checked, IGDB's order), or was skipped with Y and is never searched by name
/// on IGDB again (D8).
/// </summary>
/// <param name="Position">While it waits: its place on the list, later games behind earlier ones.</param>
public sealed record IgdbNameMatch(bool Skipped, IReadOnlyList<IgdbGame> Hits, long Position)
{
    public static IgdbNameMatch Skip { get; } = new(true, [], 0);

    public static IgdbNameMatch Waiting(IReadOnlyList<IgdbGame> hits, long position) => new(false, hits, position);
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
                stored.StoreAt,
                stored.Igdb is { At: { } at } igdb ? new IgdbCached(
                    igdb.GameId is > 0 ? igdb.GameId : null,
                    MetadataCheck.CheckedTitle(igdb.Title),
                    MetadataCheck.Checked(igdb.Data),
                    at,
                    igdb.Reading ?? 0) : null,
                StoredByName.Valid(stored.IgdbByName),
                stored.Store is null ? null : LanguageOf(stored.StoreLanguage));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return null;
        }
    }

    // Every answer before #557 was asked in German; anything but a two-letter code isn't a language Arcadia wrote.
    private static string LanguageOf(string? stored) =>
        stored is null ? "de" : stored.Length == 2 && stored.All(char.IsAsciiLetterLower) ? stored : "de";

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

        public string? StoreLanguage { get; set; }

        public StoredIgdb? Igdb { get; set; }

        public StoredByName? IgdbByName { get; set; }

        public static Stored Of(CachedMetadata cached) => new()
        {
            Game = cached.GameKey,
            Source = cached.Source,
            Local = Escaped(cached.Local),
            Store = Escaped(cached.Store),
            StoreAt = cached.StoreAt,
            StoreLanguage = cached.Store is null ? null : cached.StoreLanguage,
            Igdb = cached.Igdb is { } igdb ? new StoredIgdb
            {
                GameId = igdb.GameId,
                Title = igdb.Title is null ? null : System.Net.WebUtility.HtmlEncode(igdb.Title),
                Data = Escaped(igdb.Data),
                At = igdb.At,
                Reading = igdb.Reading,
            } : null,
            IgdbByName = StoredByName.Of(cached.IgdbByName),
        };

        /// <summary>
        /// The checked texts written as HTML text, the way a store would: reading checks them as markup again, and a
        /// "&lt;" or "&amp;amp;" in a description must come back as it was shown, not decoded a second time.
        /// </summary>
        internal static GameMetadata? Escaped(GameMetadata? metadata) => metadata is null ? null : metadata with
        {
            Description = Escape(metadata.Description),
            ShortDescription = Escape(metadata.ShortDescription),
            Developers = [.. metadata.Developers.Select(name => Escape(name)!)],
            Publishers = [.. metadata.Publishers.Select(name => Escape(name)!)],
            Tags = [.. metadata.Tags.Select(tag => Escape(tag)!)],
        };

        internal static string? Escape(string? text) => text is null ? null : System.Net.WebUtility.HtmlEncode(text);
    }

    private sealed class StoredIgdb
    {
        public long? GameId { get; set; }

        public string? Title { get; set; }

        public GameMetadata? Data { get; set; }

        public DateTimeOffset? At { get; set; }

        /// <summary>Missing in files written before #541: those answers count as read the old way (0).</summary>
        public int? Reading { get; set; }
    }

    /// <summary>
    /// A waiting or skipped search by name as written (#494). Read back, each hit is checked on its own like an answer: an
    /// id that isn't a plain positive number, a name that isn't one or a second hit of the same id is left out; a waiting
    /// game with no hit left counts as not yet searched.
    /// </summary>
    private sealed class StoredByName
    {
        public bool? Skipped { get; set; }

        public long? Position { get; set; }

        public List<StoredHit>? Hits { get; set; }

        public static StoredByName? Of(IgdbNameMatch? match) => match switch
        {
            null => null,
            { Skipped: true } => new StoredByName { Skipped = true },
            _ => new StoredByName
            {
                Position = match.Position,
                Hits = [.. match.Hits.Select(hit => new StoredHit
                {
                    Id = JsonSerializer.SerializeToElement(hit.Id),
                    Name = Stored.Escape(hit.Name),
                    Year = hit.Year,
                    Cover = hit.CoverId,
                    Data = Stored.Escaped(hit.Data),
                })],
            },
        };

        public static IgdbNameMatch? Valid(StoredByName? stored)
        {
            if (stored is null)
            {
                return null;
            }

            if (stored.Skipped == true)
            {
                return IgdbNameMatch.Skip;
            }

            var hits = new List<IgdbGame>();
            foreach (var hit in stored.Hits ?? [])
            {
                if (hits.Count < IgdbMatching.MaxHits
                    && hit.Id is { ValueKind: JsonValueKind.Number } id && id.TryGetInt64(out var gameId) && gameId > 0 && hits.All(h => h.Id != gameId)
                    && MetadataCheck.CheckedTitle(hit.Name) is { Length: > 0 } name)
                {
                    hits.Add(new IgdbGame(
                        gameId,
                        name,
                        null,
                        MetadataCheck.Checked(hit.Data),
                        IgdbImages.IsImageId(hit.Cover) ? hit.Cover : null,
                        hit.Year is > 0 and < 10000 ? hit.Year : null));
                }
            }

            return hits.Count > 0 ? IgdbNameMatch.Waiting(hits, stored.Position is > 0 ? stored.Position.Value : 0) : null;
        }
    }

    private sealed class StoredHit
    {
        public JsonElement? Id { get; set; }

        public string? Name { get; set; }

        public int? Year { get; set; }

        public string? Cover { get; set; }

        public GameMetadata? Data { get; set; }
    }
}
