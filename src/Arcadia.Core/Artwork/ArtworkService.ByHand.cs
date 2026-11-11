using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork;

/// <summary>An image "Artwork ändern" offers (#490): the launcher's, or one of the source of more images.</summary>
/// <param name="File">The image to show on its tile.</param>
/// <param name="Source">Where it comes from, kept in the cache once it is taken.</param>
/// <param name="Score">Its rating at the source, if it has one.</param>
public sealed record ArtworkOffer(ArtworkFile File, ArtworkSource Source, string Origin, string Stamp, int? Score)
{
    /// <summary>The offer is <paramref name="image"/>: the tile of the image in use gets the tick.</summary>
    public bool Is(CachedImage? image) => image is not null && image.Source == Source && image.Origin == Origin && image.Stamp == Stamp;
}

/// <summary>What "Artwork ändern" shows for one game and kind (#490).</summary>
/// <param name="Current">The image in use; null while the placeholder shows.</param>
/// <param name="CurrentImage">Where the image in use came from and whether it was set by hand.</param>
/// <param name="Own">The player's own file, also while another image is in use; null if none was chosen.</param>
/// <param name="OwnName">The own file's name as it was chosen.</param>
/// <param name="Offers">The launcher's image, then the source's, best first.</param>
public sealed record ArtworkChoices(
    ArtworkFile? Current, CachedImage? CurrentImage, ArtworkFile? Own, string? OwnName, IReadOnlyList<ArtworkOffer> Offers, SuggestionKeyState KeyState)
{
    public bool OwnInUse => CurrentImage?.Source == ArtworkSource.File;
}

/// <summary>Why an own file was or wasn't taken (#490).</summary>
public enum OwnFileResult
{
    Taken,

    /// <summary>Over 20 MB: "Das Bild ist größer als 20 MB und wurde nicht übernommen."</summary>
    TooLarge,

    /// <summary>No PNG, JPEG or WebP Arcadia can show, or cut off.</summary>
    NotAnImage,

    /// <summary>It can't be read (gone, locked, no right), or the cache can't be written.</summary>
    Unreadable,
}

/// <summary>
/// "Artwork ändern" (#490): the player's own choice for one game. Everything chosen here is set by hand, so no run replaces
/// it unless "Von Hand gesetzte Artworks überschreiben" is on for "Alle Artworks neu laden"; "Standard wiederherstellen"
/// gives the image back to Arcadia's choice. Each call writes under the same lock as the run's games, so a choice never
/// crosses a run writing the same game; downloads for the offers run outside it.
/// </summary>
public sealed partial class ArtworkService
{
    /// <summary>The most images the source of more images offers per game and kind (#13).</summary>
    public const int MaxSuggestions = 20;

    // Offers are downloaded a few at a time: the modal fills quickly without twenty requests at once.
    private const int ParallelOfferDownloads = 4;

    private IArtworkSuggestions? _suggestions;

    /// <summary>
    /// The source of more images: SteamGridDB with the player's key (<see cref="SteamGridDb"/>, #489) unless another is
    /// given; null without one: the modal then shows the notice.
    /// </summary>
    public IArtworkSuggestions? Suggestions
    {
        get => _suggestions ??= SteamGridDb is { } key ? new SteamGridDbSuggestions(key, StoreIdOf) : null;
        init => _suggestions = value;
    }

    /// <summary>
    /// What the modal shows for <paramref name="game"/>'s <paramref name="kind"/>: the image in use, the own file and the
    /// offers. The launcher's image is the one Arcadia would choose among the launcher's; offered images it has to
    /// download wait in the cache until one is taken. Off the UI thread; at most <see cref="DownloadTimeout"/> per download.
    /// </summary>
    public async Task<ArtworkChoices> ChoicesAsync(Game game, ArtworkKind kind, CancellationToken cancel = default)
    {
        var key = game.Key;
        CachedImage? current;
        ArtworkFile? currentFile;
        ArtworkFile? own;
        string? ownName;
        ImageSize? currentSize;
        await _write.WaitAsync(cancel);
        try
        {
            var record = RecordOf(key);
            current = record.Of(kind);
            currentSize = current is null ? null : CheckedSize(key, kind, current);
            currentFile = current is null || currentSize is null ? null : FileOf(key, kind, current, currentSize.Value);
            var ownImage = record.OwnOf(kind);
            own = ownImage is null ? null : OwnFileOf(key, kind, ownImage);
            ownName = own is null ? null : ownImage!.Stamp;
            _cache.ClearSuggestions(key, kind);
        }
        finally
        {
            _write.Release();
        }

        var run = NewRunState();
        var offers = new List<ArtworkOffer>();
        if (await LauncherOfferAsync(game, kind, current, currentFile, currentSize, run, cancel) is { } launcher)
        {
            offers.Add(launcher);
        }

        var keyState = Suggestions?.KeyState ?? SuggestionKeyState.Missing;
        if (keyState == SuggestionKeyState.Valid)
        {
            offers.AddRange(await SuggestedOffersAsync(game, kind, run, cancel));
        }

        return new ArtworkChoices(currentFile, current, own, ownName, offers, keyState);
    }

    /// <summary>Where the image in use came from and whether it was set by hand; null while the placeholder shows.</summary>
    public async Task<CachedImage?> InUseAsync(Game game, ArtworkKind kind, CancellationToken cancel = default)
    {
        await _write.WaitAsync(cancel);
        try
        {
            return RecordOf(game.Key).Of(kind);
        }
        finally
        {
            _write.Release();
        }
    }

    /// <summary>A tile of the modal was taken: the game shows that image from now on, set by hand.</summary>
    /// <returns>False if the offer's image is gone or damaged meanwhile: nothing changed.</returns>
    public async Task<bool> UseAsync(Game game, ArtworkKind kind, ArtworkOffer offer, CancellationToken cancel = default)
    {
        await _write.WaitAsync(cancel);
        try
        {
            var record = RecordOf(game.Key);
            if (record.Of(kind) is { } current && offer.Is(current))
            {
                // The image in use already: it only becomes set by hand.
                Save(record.With(kind, current with { SetByHand = true }));
                return true;
            }

            if (!_cache.IsSuggestion(offer.File.Path) || ReadLocal(offer.File.Path) is not { } content
                || ImageCheck.Of(new MemoryStream(content)) is not { } size || ArtworkCache.ExtensionOf(content) is not { } extension)
            {
                Log($"Artwork: angebotenes {Name(kind)} von {game.Key} ist nicht mehr lesbar");
                return false;
            }

            var file = _cache.WriteImage(game.Key, kind, content, extension);
            Written(game.Key, kind, size);
            Save(record.With(kind, new CachedImage(file, offer.Source, offer.Origin, offer.Stamp, SetByHand: true)));
            Log($"Artwork: {Name(kind)} von {game.Key} von Hand gesetzt ({offer.Origin})");
            return true;
        }
        finally
        {
            _write.Release();
        }
    }

    /// <summary>
    /// The player chose a file of their own: a PNG, JPEG or WebP of at most 20 MB is copied into the cache and shown, set by
    /// hand; it stays offered as "Eigene Datei" when another image is chosen later. The path is only read, never kept.
    /// </summary>
    public async Task<OwnFileResult> UseFileAsync(Game game, ArtworkKind kind, string path, CancellationToken cancel = default)
    {
        byte[]? content;
        try
        {
            content = await ReadAtMostAsync(path, ImageCheck.MaxBytes, cancel);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Neither the path nor the file's name reach the log: they may say who the player is.
            Log($"Artwork: eigene Datei für {Name(kind)} von {game.Key} nicht lesbar: {e.GetType().Name}");
            return OwnFileResult.Unreadable;
        }

        if (content is null)
        {
            Log($"Artwork: eigene Datei für {Name(kind)} von {game.Key} ist größer als 20 MB");
            return OwnFileResult.TooLarge;
        }

        if (ImageCheck.Of(new MemoryStream(content)) is not { } size || ArtworkCache.ExtensionOf(content) is not { } extension)
        {
            Log($"Artwork: eigene Datei für {Name(kind)} von {game.Key} ist kein lesbares Bild");
            return OwnFileResult.NotAnImage;
        }

        await _write.WaitAsync(cancel);
        try
        {
            var file = _cache.WriteImage(game.Key, kind, content, extension, own: true);
            Written(game.Key, kind, size);
            var image = new CachedImage(file, ArtworkSource.File, "file", Path.GetFileName(path), SetByHand: true);
            Save(RecordOf(game.Key).WithOwn(kind, image).With(kind, image));
            Log($"Artwork: {Name(kind)} von {game.Key} aus eigener Datei gesetzt");
            return OwnFileResult.Taken;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log($"Artwork: eigene Datei für {Name(kind)} von {game.Key} nicht gespeichert: {e.GetType().Name}: {e.Message}");
            return OwnFileResult.Unreadable;
        }
        finally
        {
            _write.Release();
        }
    }

    /// <summary>"Eigene Datei" taken again after another image: the own file the cache keeps is in use again.</summary>
    /// <returns>False if the game has no own file of <paramref name="kind"/>.</returns>
    public async Task<bool> UseOwnFileAsync(Game game, ArtworkKind kind, CancellationToken cancel = default)
    {
        await _write.WaitAsync(cancel);
        try
        {
            var record = RecordOf(game.Key);
            if (record.OwnOf(kind) is not { } own || ImageCheck.OfFile(_cache.PathOf(game.Key, own)) is not { } size)
            {
                return false;
            }

            Written(game.Key, kind, size);
            Save(record.With(kind, own));
            return true;
        }
        finally
        {
            _write.Release();
        }
    }

    /// <summary>
    /// "Standard wiederherstellen" (Y): the image is Arcadia's choice again, as a scan would make it; the own file stays
    /// offered. Without any image the placeholder shows.
    /// </summary>
    public async Task RestoreAsync(Game game, ArtworkKind kind, CancellationToken cancel = default)
    {
        await _write.WaitAsync(cancel);
        try
        {
            var record = RecordOf(game.Key);
            if (record.Of(kind) is not { SetByHand: true } image)
            {
                return;
            }

            // Only this kind gives way: the other keeps what it is, set by hand or not.
            record = record.With(kind, image.Source == ArtworkSource.File ? null : image with { SetByHand = false });
            _records[game.Key] = record;
            var lookup = _sources.TryGetValue(game.LauncherId, out var source) ? source.Look() : null;
            var changes = new ChangeBatcher(this);
            await RestoreKindAsync(game, kind, record, lookup, changes, cancel);
            changes.Flush();
            Log($"Artwork: {Name(kind)} von {game.Key} wieder automatisch");
        }
        finally
        {
            _write.Release();
        }
    }

    private async Task RestoreKindAsync(Game game, ArtworkKind kind, CachedArtwork record, IArtworkLookup? lookup, ChangeBatcher changes, CancellationToken cancel)
    {
        var image = record.Of(kind);
        var size = image is null ? null : CheckedSize(game.Key, kind, image);
        var run = NewRunState();
        var pick = lookup is null ? null : await PickAsync(game, kind, lookup.For(game, kind), image, size, run, cancel);
        var restored = Apply(record with { Unreadable = false }, kind, pick ?? (size is null ? null : new Pick.Keep()), run);
        if (restored.IsEmpty)
        {
            _records.Remove(game.Key);
        }
        else
        {
            _records[game.Key] = restored;
        }

        _cache.WriteMetadata(restored);
        Show(restored, changes);
    }

    /// <summary>What the cache holds for the game; the caller holds <see cref="_write"/>.</summary>
    private CachedArtwork RecordOf(string key) => _records.GetValueOrDefault(key) ?? _cache.Read(key) ?? new CachedArtwork(key, null, null);

    /// <summary>Keeps the game's new state, writes its metadata and tells the views; the caller holds <see cref="_write"/>.</summary>
    private void Save(CachedArtwork artwork)
    {
        // Written now, the metadata is readable again; what was protected as set by hand stays so (T490-26).
        artwork = artwork with { Unreadable = false };
        if (artwork.IsEmpty)
        {
            _records.Remove(artwork.GameKey);
        }
        else
        {
            _records[artwork.GameKey] = artwork;
        }

        _cache.WriteMetadata(artwork);
        var changes = new ChangeBatcher(this);
        Show(artwork, changes);
        changes.Flush();
    }

    /// <summary>A new file of the game's kind is in the cache: its size is known, and views load it anew.</summary>
    private void Written(string key, ArtworkKind kind, ImageSize size)
    {
        _checked[(key, kind)] = size;
        _versions[(key, kind)] = _versions.GetValueOrDefault((key, kind)) + 1;
    }

    private ArtworkFile FileOf(string key, ArtworkKind kind, CachedImage image, ImageSize size) =>
        new(key, kind, _cache.PathOf(key, image), size, _versions.GetValueOrDefault((key, kind)));

    private ArtworkFile? OwnFileOf(string key, ArtworkKind kind, CachedImage own)
    {
        var path = _cache.PathOf(key, own);
        return ImageCheck.OfFile(path) is { } size ? new ArtworkFile(key, kind, path, size, _versions.GetValueOrDefault((key, kind))) : null;
    }

    /// <summary>
    /// The launcher's image as a scan would choose it among the launcher's: the image in use where it is that one, else a
    /// copy waiting in the cache.
    /// </summary>
    private async Task<ArtworkOffer?> LauncherOfferAsync(
        Game game, ArtworkKind kind, CachedImage? current, ArtworkFile? currentFile, ImageSize? currentSize, RunState run, CancellationToken cancel)
    {
        if (!_sources.TryGetValue(game.LauncherId, out var source))
        {
            return null;
        }

        var fromLauncher = current?.Source == ArtworkSource.Launcher ? current : null;
        var pick = await PickAsync(game, kind, source.Look().For(game, kind), fromLauncher, fromLauncher is null ? null : currentSize, run, cancel);
        switch (pick)
        {
            case Pick.Keep when fromLauncher is not null && currentFile is not null:
                return new ArtworkOffer(currentFile, ArtworkSource.Launcher, fromLauncher.Origin, fromLauncher.Stamp, null);
            case Pick.Take take:
                var path = _cache.WriteSuggestion(game.Key, kind, take.Content, ArtworkCache.ExtensionOf(take.Content)!);
                return new ArtworkOffer(new ArtworkFile(game.Key, kind, path, take.Size, 0), ArtworkSource.Launcher, take.Candidate.Origin, take.Stamp, null);
            default:
                return null;
        }
    }

    /// <summary>The source's images, best first, at most <see cref="MaxSuggestions"/>; those it can't load are left out.</summary>
    private async Task<IReadOnlyList<ArtworkOffer>> SuggestedOffersAsync(Game game, ArtworkKind kind, RunState run, CancellationToken cancel)
    {
        var found = await Suggestions!.FindAsync(game, kind, cancel);
        var images = found.Take(MaxSuggestions).ToList();
        var offers = new ArtworkOffer?[images.Count];
        using var slots = new SemaphoreSlim(ParallelOfferDownloads);
        await Task.WhenAll(images.Select(async (image, index) =>
        {
            await slots.WaitAsync(cancel);
            try
            {
                if (await DownloadAsync(game, kind, image.Download, run, cancel) is { } content
                    && ImageCheck.Of(new MemoryStream(content)) is { } size && ArtworkCache.ExtensionOf(content) is { } extension)
                {
                    var path = _cache.WriteSuggestion(game.Key, kind, content, extension);
                    offers[index] = new ArtworkOffer(new ArtworkFile(game.Key, kind, path, size, 0), ArtworkSource.SteamGridDb, image.Origin, image.Download.AbsoluteUri, image.Score);
                }
            }
            finally
            {
                slots.Release();
            }
        }));

        return offers.OfType<ArtworkOffer>().ToList();
    }

    /// <summary>
    /// The file's bytes, or null past <paramref name="limit"/>. Read to the limit, not by the length the file claims:
    /// a pipe or device says 0 and would otherwise be read until memory runs out.
    /// </summary>
    private static async Task<byte[]?> ReadAtMostAsync(string path, long limit, CancellationToken cancel)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var content = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancel)) > 0)
        {
            if (content.Length + read > limit)
            {
                return null;
            }

            content.Write(buffer, 0, read);
        }

        return content.ToArray();
    }
}
