using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork.SteamGridDb;

/// <summary>Where a game found only by its name stands (#491).</summary>
public enum NameMatchState
{
    /// <summary>On the waiting list ("Warten auf deine Auswahl") with its hits; it keeps its launcher image or placeholder.</summary>
    Waiting,

    /// <summary>Its SteamGridDB game is known (chosen, taken as the only or the first hit): asked by that, never by name again.</summary>
    Chosen,

    /// <summary>Skipped with Y: never searched by name again.</summary>
    Skipped,
}

/// <summary>One game's match: its SteamGridDB game when chosen, its hits while it waits.</summary>
/// <param name="Position">While it waits: its place on the waiting list, later games behind earlier ones.</param>
public sealed record NameMatch(NameMatchState State, int? GameId, IReadOnlyList<SteamGridDbHit> Hits, int Position = 0)
{
    /// <summary>Both kinds: what a game waiting from before #506 counts as seeking.</summary>
    public static readonly IReadOnlyList<ArtworkKind> BothKinds = [ArtworkKind.Cover, ArtworkKind.Hero];

    /// <summary>While it waits: the kinds SteamGridDB is to give it after the choice ("Gesucht: …", #506).</summary>
    public IReadOnlyList<ArtworkKind> Sought { get; init; } = BothKinds;

    /// <summary>
    /// While it waits: the hits known to have an image of <see cref="TileKind"/>. The others weren't checked yet (waiting
    /// from before #506, or the check failed) and are checked when the game is opened on the waiting list.
    /// </summary>
    public IReadOnlySet<int> Checked { get; init; } = ImmutableHashSet<int>.Empty;

    /// <summary>The kind a hit's tile shows and is checked for: the cover when both are sought.</summary>
    public ArtworkKind TileKind => TileKindOf(Sought);

    public static ArtworkKind TileKindOf(IReadOnlyList<ArtworkKind> sought) => sought.Contains(ArtworkKind.Cover) ? ArtworkKind.Cover : ArtworkKind.Hero;

    public static NameMatch Chosen(int gameId) => new(NameMatchState.Chosen, gameId, []);

    public static NameMatch Skipped { get; } = new(NameMatchState.Skipped, null, []);

    public static NameMatch Waiting(IReadOnlyList<SteamGridDbHit> hits, int position) => new(NameMatchState.Waiting, null, hits, position);

    /// <param name="hits">The hits with an image of the sought kind, every one checked.</param>
    public static NameMatch Waiting(IReadOnlyList<SteamGridDbHit> hits, int position, IReadOnlyList<ArtworkKind> sought) =>
        new(NameMatchState.Waiting, null, hits, position) { Sought = sought, Checked = hits.Select(h => h.Id).ToImmutableHashSet() };
}

/// <summary>
/// The matches by name of every game, by game key (#491), kept across restarts in one file of the artwork cache
/// (<c>name-matches.json</c>), also for games that left the library (D7). Written through a temporary file; a file that
/// can't be read counts as no matches (the games are searched again), which loses no image. Values read are checked: an
/// id must be a positive number, a hit needs an id and a name. Thread-safe.
/// </summary>
public sealed class NameMatchStore(string path)
{
    public const string FileName = "name-matches.json";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Lock _write = new();
    private ImmutableDictionary<string, NameMatch>? _matches;

    public Action<string> Log { get; init; } = _ => { };

    /// <summary>Every game's match, read from the file at the first use.</summary>
    public ImmutableDictionary<string, NameMatch> All => Volatile.Read(ref _matches) ?? Load();

    public NameMatch? Of(string gameKey) => All.GetValueOrDefault(gameKey);

    /// <summary>The place behind every game waiting now.</summary>
    public int NextPosition => All.Values.Where(m => m.State == NameMatchState.Waiting).Select(m => m.Position + 1).DefaultIfEmpty(1).Max();

    /// <summary>Forgets the game's match (it is searched by name again) and writes the file; false if it couldn't be written.</summary>
    public bool Remove(string gameKey)
    {
        lock (_write)
        {
            var all = All.Remove(gameKey);
            Volatile.Write(ref _matches, all);
            return TryWrite(all);
        }
    }

    /// <summary>Keeps the game's match and writes the file; false if it couldn't be written (it lasts until Arcadia closes).</summary>
    public bool Set(string gameKey, NameMatch match) => Set([KeyValuePair.Create(gameKey, match)]);

    public bool Set(IEnumerable<KeyValuePair<string, NameMatch>> matches)
    {
        lock (_write)
        {
            var all = All.SetItems(matches);
            Volatile.Write(ref _matches, all);
            return TryWrite(all);
        }
    }

    private bool TryWrite(ImmutableDictionary<string, NameMatch> all)
    {
        try
        {
            Write(all);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log($"Artwork: Auswahl nach Namen nicht gespeichert: {e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    private ImmutableDictionary<string, NameMatch> Load()
    {
        var matches = Read();
        return Interlocked.CompareExchange(ref _matches, matches, null) ?? matches;
    }

    private ImmutableDictionary<string, NameMatch> Read()
    {
        var matches = ImmutableDictionary.CreateBuilder<string, NameMatch>(StringComparer.Ordinal);
        try
        {
            RemoveTemporaryFiles();
            if (!File.Exists(path))
            {
                return matches.ToImmutable();
            }

            using var json = JsonDocument.Parse(File.ReadAllBytes(path));
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("not an object");
            }

            foreach (var entry in json.RootElement.EnumerateObject())
            {
                if (entry.Name.Length > 0 && Valid(entry.Value) is { } match)
                {
                    matches[entry.Name] = match;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log($"Artwork: Auswahl nach Namen nicht lesbar ({e.GetType().Name}); die Spiele werden neu gesucht");
            matches.Clear();
        }

        return matches.ToImmutable();
    }

    /// <summary>
    /// A stored match made usable, each value checked on its own; null for one that can't be (an unknown state, a chosen
    /// game without a plain positive id, a waiting one without a list of hits). A waiting one from before #506 has no sought kinds
    /// and no checked hits: it seeks both, and its hits are checked when it is opened.
    /// </summary>
    private static NameMatch? Valid(JsonElement stored)
    {
        if (stored.ValueKind != JsonValueKind.Object
            || !stored.TryGetProperty("state", out var stateValue) || stateValue.ValueKind != JsonValueKind.String
            || !Enum.TryParse<NameMatchState>(stateValue.GetString(), ignoreCase: false, out var state) || !Enum.IsDefined(state))
        {
            return null;
        }

        switch (state)
        {
            case NameMatchState.Chosen:
                return PositiveNumber(stored, "gameId") is { } id ? NameMatch.Chosen(id) : null;
            case NameMatchState.Skipped:
                return NameMatch.Skipped;
        }

        var hits = new List<SteamGridDbHit>();
        var checkedHits = ImmutableHashSet.CreateBuilder<int>();
        if (stored.TryGetProperty("hits", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var hit in list.EnumerateArray())
            {
                if (hit.ValueKind == JsonValueKind.Object
                    && PositiveNumber(hit, "id") is { } hitId && hits.All(h => h.Id != hitId)
                    && hit.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                    && name.GetString() is { Length: > 0 and <= SteamGridDbClient.MaxNameLength } title
                    && hits.Count < SteamGridDbClient.MaxHits)
                {
                    hits.Add(new SteamGridDbHit(hitId, title, PositiveNumber(hit, "year") is { } year and < 10000 ? year : null));
                    if (hit.TryGetProperty("checked", out var isChecked) && isChecked.ValueKind == JsonValueKind.True)
                    {
                        checkedHits.Add(hitId);
                    }
                }
            }
        }

        // A waiting game may have no hits left: all dropped while the player looked (#506); it waits until skipped.
        return list.ValueKind == JsonValueKind.Array
            ? NameMatch.Waiting(hits, PositiveNumber(stored, "position") ?? 0) with { Sought = SoughtOf(stored), Checked = checkedHits.ToImmutable() }
            : null;
    }

    /// <summary>The stored sought kinds, each a known kind once; both when none is stored.</summary>
    private static IReadOnlyList<ArtworkKind> SoughtOf(JsonElement stored)
    {
        var sought = new List<ArtworkKind>();
        if (stored.TryGetProperty("sought", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var kind in list.EnumerateArray())
            {
                if (kind.ValueKind == JsonValueKind.String
                    && Enum.TryParse<ArtworkKind>(kind.GetString(), ignoreCase: false, out var known) && Enum.IsDefined(known) && !sought.Contains(known))
                {
                    sought.Add(known);
                }
            }
        }

        return sought.Count == 0 ? NameMatch.BothKinds : sought;
    }

    private static int? PositiveNumber(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0
            ? number
            : null;

    private void RemoveTemporaryFiles()
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        if (Directory.Exists(folder))
        {
            foreach (var leftover in Directory.EnumerateFiles(folder, Path.GetFileName(path) + ".*.tmp"))
            {
                File.Delete(leftover);
            }
        }
    }

    private void Write(ImmutableDictionary<string, NameMatch> all)
    {
        var stored = all.ToDictionary(
            e => e.Key,
            e => new StoredMatch(
                e.Value.State.ToString(),
                e.Value.GameId,
                e.Value.Hits.Count == 0 && e.Value.State != NameMatchState.Waiting ? null : [.. e.Value.Hits.Select(h => new StoredHit(h.Id, h.Name, h.Year, e.Value.Checked.Contains(h.Id) ? true : null))],
                e.Value.State == NameMatchState.Waiting ? e.Value.Position : null,
                e.Value.State == NameMatchState.Waiting ? [.. e.Value.Sought.Select(k => k.ToString())] : null),
            StringComparer.Ordinal);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(stored, Json));
        File.Move(temporary, path, overwrite: true);
    }

    private sealed record StoredMatch(string State, int? GameId, List<StoredHit>? Hits, int? Position, List<string>? Sought);

    private sealed record StoredHit(int Id, string Name, int? Year, bool? Checked);
}
