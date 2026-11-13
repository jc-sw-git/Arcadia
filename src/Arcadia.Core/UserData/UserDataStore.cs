using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.UserData;

/// <summary>What the player set for one game by hand (#43): its own title (#496). Its own tags are kept per tag (<see cref="OwnTag"/>).</summary>
public sealed record GameUserData
{
    public static GameUserData None { get; } = new();

    /// <summary>The player's title, shown above every provider's; null: the automatic title shows.</summary>
    public string? Title { get; init; }

    [JsonIgnore]
    public bool IsEmpty => Title is null;
}

/// <summary>
/// One of the player's own tags (#495) and the keys of its games, also of games absent from the library; a tag without
/// games stays until it is deleted.
/// </summary>
public sealed record OwnTag(string Name, ImmutableHashSet<string> Games)
{
    public OwnTag(string name)
        : this(name, ImmutableHashSet.Create<string>(StringComparer.Ordinal))
    {
    }
}

/// <summary>
/// What the player set by hand for games (#43), kept apart from the caches, which Arcadia may delete and fetch again:
/// <c>%APPDATA%\Arcadia\user-data.json</c>, own titles per game key and own tags (#495) with their games' keys, so a game that leaves the library (uninstalled, account
/// switched, launcher off) finds its data again when it comes back. A change holds in memory at once; the file is
/// written in the background through a temporary file and a rename. A file that can't be read at the start is never
/// overwritten: a copy is kept beside it and nothing can be changed until the player repaired or deleted it and
/// Arcadia started again. A write that fails puts back what the file holds, so what shows is what is stored.
/// </summary>
public sealed class UserDataStore
{
    public const string FileName = "user-data.json";

    /// <summary>The ending of the copy of a file that couldn't be read, as <c>settings.json.broken</c>.</summary>
    public const string CopySuffix = ".broken";

    /// <summary>The longest own title; longer ones are refused while typing.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>The longest own tag name (#495); the keyboard types no more.</summary>
    public const int MaxTagLength = 40;

    /// <summary>Tag names are equal ignoring case (#495): "couch-koop" is "Couch-Koop", the first spelling stays.</summary>
    public static StringComparer TagNames { get; } = StringComparer.OrdinalIgnoreCase;

    private const int FileVersion = 1;

    // Titles and tags of a few thousand games are well below this; a far larger file is none Arcadia wrote.
    private const long MaxFile = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        MaxDepth = 16,
    };

    private readonly object _gate = new();
    private readonly string _folder;
    private ImmutableDictionary<string, GameUserData> _games = ImmutableDictionary.Create<string, GameUserData>(StringComparer.Ordinal);

    // What the file holds: a failed write goes back to it.
    private ImmutableDictionary<string, GameUserData> _stored = ImmutableDictionary.Create<string, GameUserData>(StringComparer.Ordinal);
    private ImmutableList<OwnTag> _tags = [];
    private ImmutableList<OwnTag> _storedTags = [];
    private Task _writes = Task.CompletedTask;

    /// <param name="folder">Arcadia's data folder, beside <c>settings.json</c>.</param>
    public UserDataStore(string folder)
    {
        _folder = Path.GetFullPath(folder);
        FilePath = Path.Combine(_folder, FileName);
    }

    public string FilePath { get; }

    public Action<string>? Log { get; set; }

    /// <summary>The file was there but couldn't be read at the start: every game shows its automatic data, nothing can be changed.</summary>
    public bool IsUnreadable { get; private set; }

    /// <summary>The data of these games changed: by <see cref="Change"/> (on its caller's thread) or put back after a failed write (on a background thread).</summary>
    public event EventHandler<IReadOnlyCollection<string>>? Changed;

    /// <summary>The own tags changed (#495): by <see cref="ChangeTags"/> (on its caller's thread) or put back after a failed write (on a background thread).</summary>
    public event EventHandler? TagsChanged;

    /// <summary>Writing the file failed (disk full, file locked); raised on a background thread after <see cref="Changed"/> put the stored data back.</summary>
    public event EventHandler<Exception>? WriteFailed;

    /// <summary>
    /// Reads the file, once at the start; a missing one is no data yet. Removes what a write that broke off left. A file
    /// that can't be read (damaged, from a later version, too large, locked) makes <see cref="IsUnreadable"/> true.
    /// </summary>
    public void Open()
    {
        try
        {
            RemoveBrokenOffWrites();
            if (!File.Exists(FilePath))
            {
                return;
            }

            if (new FileInfo(FilePath).Length > MaxFile)
            {
                throw new InvalidDataException("too large");
            }

            var file = JsonSerializer.Deserialize<UserDataFile>(File.ReadAllBytes(FilePath), Json);
            if (file is not { Version: FileVersion, Games: { } games })
            {
                throw new InvalidDataException($"version {file?.Version}");
            }

            _tags = _storedTags = CheckedTags(file.Tags?.OfType<TagEntry>().Select(tag => (tag.Name, (IEnumerable<string>?)tag.Games)) ?? []);

            var kept = games
                .Where(entry => entry.Key.Length > 0 && entry.Value is not null)
                .Select(entry => KeyValuePair.Create(entry.Key, Checked(entry.Value)))
                .Where(entry => !entry.Value.IsEmpty);
            _games = _stored = ImmutableDictionary.CreateRange(StringComparer.Ordinal, kept);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            IsUnreadable = true;
            // Only the file's name and the kind of failure: a message may name the user's folders or titles.
            var copyFailure = KeepCopy();
            Log?.Invoke($"{FileName} nicht lesbar ({ex.GetType().Name}), wird nicht überschrieben; Kopie {FileName}{CopySuffix}: {copyFailure ?? "angelegt"}");
        }
    }

    /// <summary>The data the player set for the game of <paramref name="gameKey"/>; <see cref="GameUserData.None"/> without any.</summary>
    public GameUserData Of(string gameKey)
    {
        lock (_gate)
        {
            return _games.GetValueOrDefault(gameKey) ?? GameUserData.None;
        }
    }

    /// <summary>
    /// Changes the game's data at once and writes the file in the background; the caller never waits for the disk. A title
    /// too long or holding control characters is refused and the one before stays. False while the file is unreadable:
    /// then nothing changes.
    /// </summary>
    public bool Change(string gameKey, Func<GameUserData, GameUserData> change)
    {
        lock (_gate)
        {
            if (IsUnreadable)
            {
                return false;
            }

            var before = _games.GetValueOrDefault(gameKey) ?? GameUserData.None;
            var after = Refused(change(before), before);
            if (after == before)
            {
                return true;
            }

            _games = after.IsEmpty ? _games.Remove(gameKey) : _games.SetItem(gameKey, after);
            QueueWrite();
        }

        Changed?.Invoke(this, [gameKey]);
        return true;
    }

    /// <summary>The own tags in the order they were made, with their games' keys (#495); none while the file is unreadable.</summary>
    public ImmutableList<OwnTag> Tags
    {
        get
        {
            lock (_gate)
            {
                return _tags;
            }
        }
    }

    /// <summary>
    /// Changes the own tags at once (#495) and writes the file in the background, as <see cref="Change"/>. What comes back is
    /// kept as <see cref="CheckedTags"/> keeps a file's tags: names that can't be kept drop out, names equal ignoring case
    /// merge. False while the file is unreadable: then nothing changes.
    /// </summary>
    public bool ChangeTags(Func<ImmutableList<OwnTag>, IEnumerable<OwnTag>> change)
    {
        lock (_gate)
        {
            if (IsUnreadable)
            {
                return false;
            }

            var after = CheckedTags(change(_tags).Select(tag => ((string?)tag.Name, (IEnumerable<string>?)tag.Games)));
            if (SameTags(after, _tags))
            {
                return true;
            }

            _tags = after;
            QueueWrite();
        }

        TagsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>A tag name as it is kept (#495): without spaces around it; empty, too long or holding control characters is none.</summary>
    public static string? CheckedTagName(string? name) =>
        name?.Trim() is { Length: > 0 and <= MaxTagLength } trimmed && !trimmed.Any(char.IsControl) ? trimmed : null;

    /// <summary>Completes once every change so far is written or put back; for the tests and a clean shutdown.</summary>
    public Task WhenWrittenAsync()
    {
        lock (_gate)
        {
            return _writes;
        }
    }

    // Called under the gate.
    private void QueueWrite() =>
        _writes = _writes.ContinueWith(_ => WriteLatest(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

    // Writes the newest state; queued once per change, so a later one finds it written already.
    private void WriteLatest()
    {
        ImmutableDictionary<string, GameUserData> latest;
        ImmutableList<OwnTag> latestTags;
        lock (_gate)
        {
            latest = _games;
            latestTags = _tags;
            if (ReferenceEquals(latest, _stored) && ReferenceEquals(latestTags, _storedTags))
            {
                return;
            }
        }

        try
        {
            Directory.CreateDirectory(_folder);
            var tags = latestTags.Select(tag => new TagEntry(tag.Name, [.. tag.Games.Order(StringComparer.Ordinal)])).ToList();
            CacheFiles.WriteThroughTemporary(FilePath, JsonSerializer.SerializeToUtf8Bytes(new UserDataFile(FileVersion, latest, tags), Json));
            lock (_gate)
            {
                _stored = latest;
                _storedTags = latestTags;
            }
        }
        catch (Exception ex)
        {
            // Every change not on disk goes back, including one made meanwhile: what shows is what is stored.
            List<string> reverted;
            bool tagsReverted;
            lock (_gate)
            {
                reverted = [.. _games.Keys.Union(_stored.Keys).Where(key => _games.GetValueOrDefault(key) != _stored.GetValueOrDefault(key))];
                tagsReverted = !ReferenceEquals(_tags, _storedTags);
                _games = _stored;
                _tags = _storedTags;
            }

            if (reverted.Count > 0)
            {
                Changed?.Invoke(this, reverted);
            }

            if (tagsReverted)
            {
                TagsChanged?.Invoke(this, EventArgs.Empty);
            }

            WriteFailed?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Tags as they are kept, from the file or a change (#495): a name that can't be kept drops out with its games, the
    /// others stay; names equal ignoring case are one tag under the first spelling, with the games of both once. Game keys
    /// absent from the library are kept; empty ones drop out.
    /// </summary>
    private static ImmutableList<OwnTag> CheckedTags(IEnumerable<(string? Name, IEnumerable<string>? Games)> tags)
    {
        var kept = new List<OwnTag>();
        var byName = new Dictionary<string, int>(TagNames);
        foreach (var (name, games) in tags)
        {
            if (CheckedTagName(name) is not { } checkedName)
            {
                continue;
            }

            var keys = (games ?? []).Where(key => !string.IsNullOrEmpty(key));
            if (byName.TryGetValue(checkedName, out var index))
            {
                kept[index] = kept[index] with { Games = kept[index].Games.Union(keys) };
            }
            else
            {
                byName[checkedName] = kept.Count;
                kept.Add(new OwnTag(checkedName, ImmutableHashSet.CreateRange(StringComparer.Ordinal, keys)));
            }
        }

        return [.. kept];
    }

    private static bool SameTags(ImmutableList<OwnTag> a, ImmutableList<OwnTag> b) =>
        a.Count == b.Count && a.Zip(b).All(pair => pair.First.Name == pair.Second.Name && pair.First.Games.SetEquals(pair.Second.Games));

    private static GameUserData Checked(GameUserData data) => data with { Title = CheckedTitle(data.Title) };

    /// <summary>A change as it is kept: an empty title clears the own one, a title that can't be kept leaves it as it was.</summary>
    private static GameUserData Refused(GameUserData changed, GameUserData before) =>
        changed with { Title = string.IsNullOrWhiteSpace(changed.Title) ? null : CheckedTitle(changed.Title) ?? before.Title };

    /// <summary>A title as it is kept: without spaces around it; empty, too long or holding control characters is none.</summary>
    private static string? CheckedTitle(string? title) =>
        title?.Trim() is { Length: > 0 and <= MaxTitleLength } trimmed && !trimmed.Any(char.IsControl) ? trimmed : null;

    /// <summary>Copies the unreadable file beside it; the kind of failure when that failed too, else null.</summary>
    private string? KeepCopy()
    {
        try
        {
            File.Copy(FilePath, FilePath + CopySuffix, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.GetType().Name;
        }
    }

    private void RemoveBrokenOffWrites()
    {
        if (!Directory.Exists(_folder))
        {
            return;
        }

        try
        {
            foreach (var leftover in Directory.EnumerateFiles(_folder, FileName + ".*" + CacheFiles.TemporarySuffix))
            {
                File.Delete(leftover);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left for the next start; the file itself is still read.
        }
    }

    /// <summary>
    /// The file as written: a version, so a later Arcadia's file isn't read wrong, the data per game key
    /// (<see cref="Game.Key"/>) and the own tags (#495), which a file of #496 doesn't have yet.
    /// </summary>
    private sealed record UserDataFile(int Version, IReadOnlyDictionary<string, GameUserData>? Games, IReadOnlyList<TagEntry?>? Tags = null);

    private sealed record TagEntry(string? Name, IReadOnlyList<string>? Games);
}
