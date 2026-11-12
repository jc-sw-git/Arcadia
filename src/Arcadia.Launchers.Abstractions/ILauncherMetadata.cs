namespace Arcadia.Launchers.Abstractions;

/// <summary>
/// The launcher as a metadata provider (#43): what its own files on this PC say about its games, and optionally a store
/// page Arcadia asks per game. Arcadia does the asking itself (HTTPS only, at most <see cref="IMetadataStore.Limit"/>, 10 s
/// per answer, waits on HTTP 429), caches every answer and checks each value before it shows it.
/// </summary>
public interface ILauncherMetadata
{
    /// <summary>The fields this launcher can deliver at all, its local files and its store together.</summary>
    IReadOnlySet<MetadataField> Fields { get; }

    /// <summary>One line in "Datenquellen" on what the launcher delivers, e.g. "Nur der Entwickler".</summary>
    string Summary { get; }

    /// <summary>The info text in "Datenquellen": where the data comes from, the store's limit and what the store learns.</summary>
    string About { get; }

    /// <summary>
    /// Reads the launcher's local files once for a run over <paramref name="games"/> (the launcher's own). May read the disk;
    /// call it off the UI thread. Never throws for a file it can't read: it knows nothing then.
    /// </summary>
    IMetadataLookup Look(IReadOnlyCollection<Game> games);

    /// <summary>The launcher's store page per game; null if Arcadia asks no store for this launcher.</summary>
    IMetadataStore? Store => null;

    /// <summary>
    /// The game's id in a store that metadata collections know it by (#494), e.g. its Steam AppID: IGDB finds the game by
    /// it, without searching by name. Null if the launcher can't name one. Never throws.
    /// </summary>
    ArtworkStoreId? StoreIdOf(Game game) => null;
}

/// <summary>What <see cref="ILauncherMetadata.Look"/> found in the launcher's local files.</summary>
public interface IMetadataLookup
{
    /// <summary>The game's data from the local files; null if they know nothing of it.</summary>
    GameMetadata? Of(Game game);
}

/// <summary>A store Arcadia asks once per game for what the local files lack (Steam's <c>appdetails</c>).</summary>
public interface IMetadataStore
{
    /// <summary>The most requests the store takes in a span of time; Arcadia spreads its requests evenly within it.</summary>
    RequestLimit Limit { get; }

    /// <summary>
    /// The <c>https</c> address of the game's page, holding nothing but the game's store id and the language; null if the
    /// game has none (an id that isn't the store's). Never throws.
    /// </summary>
    Uri? AddressOf(Game game);

    /// <summary>
    /// The store's answer for <paramref name="game"/>: its data, <see cref="GameMetadata.Empty"/> when the store says it has
    /// none, or null when the answer is unusable (broken, for another game) and the game is asked again next time.
    /// Never throws.
    /// </summary>
    GameMetadata? Parse(Game game, ReadOnlySpan<byte> answer);
}

/// <summary>At most <paramref name="Requests"/> requests per <paramref name="Per"/>.</summary>
public sealed record RequestLimit(int Requests, TimeSpan Per)
{
    /// <summary>The gap between two requests that keeps to the limit.</summary>
    public TimeSpan Gap => Requests > 0 ? Per / Requests : Per;
}
