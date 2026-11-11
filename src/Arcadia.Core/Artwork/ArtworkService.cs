using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork;

/// <summary>
/// Brings every game's cover and background into the cache and says which games' images changed (#13, #487). After each
/// scan it runs once over the libraries in the background: for every image not set by hand it takes the launcher's images in
/// their order and keeps the first that fits the format within the tolerance, else the first image at all; an image already
/// cached stays unless an earlier or changed file of the launcher replaces it, so a download happens only for what is missing.
/// </summary>
/// <remarks>
/// One run at a time, off the UI thread; a scan during a run queues the newest libraries for the next run, so a run cut off
/// (Arcadia closed) is simply taken up by the next. What the player does in "Artwork ändern" (#490, ArtworkService.ByHand.cs)
/// waits for the game the run is at, never for the whole run: each game is written under one lock. <see cref="Of"/> may be called from any thread; <see cref="Changed"/> and
/// <see cref="Failed"/> are raised on the run's thread. Failures of single images are logged and leave the placeholder; no
/// download outlives <see cref="DownloadTimeout"/>, and after a timeout or with no network the run downloads nothing more.
/// </remarks>
public sealed partial class ArtworkService : IDisposable
{
    /// <summary>The longest a download may take (#13); then the image waits for the next scan.</summary>
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan ChangeBatch = TimeSpan.FromMilliseconds(250);

    private readonly ArtworkCache _cache;
    private readonly HttpClient _http;
    private readonly TimeProvider _time;
    private readonly double _tolerance;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _gate = new();

    // One writer of the cache at a time: the run per game, or one of the player's choices (#490).
    private readonly SemaphoreSlim _write = new(1, 1);

    // The launchers' images by launcher id and the libraries last read, for a single game's choice and "Alle neu laden".
    private readonly ConcurrentDictionary<string, ILauncherArtwork> _sources = new(StringComparer.Ordinal);
    private IReadOnlyList<LibraryArtwork>? _lastLibraries;

    // What the views show, readable from any thread.
    private readonly ConcurrentDictionary<string, GameArtwork> _shown = new(StringComparer.Ordinal);

    // Images the view couldn't decode: missing from the next run on.
    private readonly ConcurrentDictionary<(string Key, ArtworkKind Kind), bool> _broken = new();

    // The run's own state: only the run touches it.
    private readonly Dictionary<string, CachedArtwork> _records = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Key, ArtworkKind Kind), ImageSize> _checked = [];
    private readonly Dictionary<(string Key, ArtworkKind Kind), long> _versions = [];
    private bool _indexed;

    private PendingRun? _pending;
    private Task _runner = Task.CompletedTask;

    /// <param name="tolerance">How much of an image the crop may cut away for it to fit (<see cref="ArtworkFormat"/>).</param>
    public ArtworkService(ArtworkCache cache, HttpClient http, TimeProvider time, double tolerance = ArtworkFormat.NormalTolerance)
    {
        _cache = cache;
        _http = http;
        _time = time;
        _tolerance = tolerance;
    }

    public Action<string> Log { get; init; } = _ => { };

    /// <summary>The keys of games whose shown images changed; raised on the run's thread, in batches.</summary>
    public event EventHandler<IReadOnlyCollection<string>>? Changed;

    /// <summary>A run ended with an unexpected error, with its stack trace; the next scan starts a new run.</summary>
    public event EventHandler<Exception>? Failed;

    /// <summary>The game's cached images; null while it has none (or the cache isn't read yet).</summary>
    public GameArtwork? Of(string gameKey) => _shown.GetValueOrDefault(gameKey);

    /// <summary>Reads what the cache holds, in the background, so the covers show before the first scan ends.</summary>
    public void Open() => Start(null);

    /// <summary>
    /// The libraries were read (again): brings their images into the cache in the background, and removes the automatic
    /// images of games no longer in a library that was read whole.
    /// </summary>
    public void Update(IReadOnlyList<LibraryArtwork> libraries) => Start(new PendingRun(libraries, Reload: false, Overwrite: false));

    /// <summary>
    /// "Alle Artworks neu laden" (#13, #490): takes every game's images from their sources again, also those already cached.
    /// Images set by hand stay unless <paramref name="overwriteSetByHand"/> ("Von Hand gesetzte Artworks überschreiben");
    /// then they give way to what Arcadia would choose, and own files stay offered. An image that can't be loaded now stays.
    /// </summary>
    /// <returns>False before the first scan: there is nothing to load yet.</returns>
    public bool ReloadAll(bool overwriteSetByHand)
    {
        IReadOnlyList<LibraryArtwork>? libraries;
        lock (_gate)
        {
            libraries = _lastLibraries;
        }

        if (libraries is null)
        {
            return false;
        }

        Log($"Artwork: alle neu laden{(overwriteSetByHand ? ", auch von Hand gesetzte" : string.Empty)}");
        Start(new PendingRun(libraries, Reload: true, Overwrite: overwriteSetByHand));
        return true;
    }

    /// <summary>The run going on now and those queued behind it; for the tests.</summary>
    public Task WhenIdleAsync()
    {
        lock (_gate)
        {
            return _runner;
        }
    }

    /// <summary>
    /// The view couldn't decode the image (damaged inside): it counts as missing, the next scan loads it again. An image
    /// "Artwork ändern" only offers is no game's image: the offer just shows nothing.
    /// </summary>
    public void ReportBroken(ArtworkFile file)
    {
        if (Of(file.GameKey)?.Of(file.Kind)?.Path != file.Path)
        {
            return;
        }

        _broken[(file.GameKey, file.Kind)] = true;
        Log($"Artwork: {Name(file.Kind)} von {file.GameKey} ist beschädigt, wird beim nächsten Einlesen neu geladen");
    }

    public void Dispose() => _shutdown.Cancel();

    private void Start(PendingRun? run)
    {
        lock (_gate)
        {
            if (run is not null)
            {
                // A reload queued stays one when a scan comes after it; the newest libraries go.
                _pending = _pending is { } queued
                    ? run with { Reload = run.Reload || queued.Reload, Overwrite = run.Overwrite || queued.Overwrite }
                    : run;
                _lastLibraries = run.Libraries;
                foreach (var (library, source) in run.Libraries)
                {
                    if (source is not null)
                    {
                        _sources[library.Launcher.Id] = source;
                    }
                }
            }

            if (_runner.IsCompleted)
            {
                _runner = Task.Run(RunAsync);
            }
        }
    }

    private async Task RunAsync()
    {
        try
        {
            while (true)
            {
                var changes = new ChangeBatcher(this);
                if (!_indexed)
                {
                    await _write.WaitAsync(_shutdown.Token);
                    try
                    {
                        ReadIndex(changes);
                    }
                    finally
                    {
                        _write.Release();
                    }
                }

                PendingRun? pending;
                lock (_gate)
                {
                    pending = _pending;
                    _pending = null;
                }

                if (pending is not null)
                {
                    await UpdateAsync(pending, changes, _shutdown.Token);
                }

                changes.Flush();
                lock (_gate)
                {
                    if (_pending is null)
                    {
                        _runner = Task.CompletedTask;
                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Arcadia is closing; the next start continues.
            Stop();
        }
        catch (Exception e)
        {
            Log($"Artwork: Durchlauf abgebrochen: {e}");
            Stop();
            Failed?.Invoke(this, e);
        }
    }

    /// <summary>An error ended the run: the queued libraries wait for the next scan, which starts a new run.</summary>
    private void Stop()
    {
        lock (_gate)
        {
            _pending = null;
            _runner = Task.CompletedTask;
        }
    }

    private void ReadIndex(ChangeBatcher changes)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            // What "Artwork ändern" offered before isn't shown by any view now.
            _cache.ClearAllSuggestions();
            foreach (var artwork in _cache.All())
            {
                _records[artwork.GameKey] = artwork;
                Show(artwork, changes);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log($"Artwork: Cache nicht lesbar: {e.GetType().Name}: {e.Message}");
        }

        _indexed = true;
        ReleaseGarbage();
        Log($"Artwork: {_records.Count} Spiele im Cache, in {watch.ElapsedMilliseconds} ms gelesen");
    }

    private async Task UpdateAsync(PendingRun pending, ChangeBatcher changes, CancellationToken cancel)
    {
        var watch = Stopwatch.StartNew();
        var run = new RunState { Reload = pending.Reload, Overwrite = pending.Overwrite };
        foreach (var (library, source) in pending.Libraries)
        {
            if (source is null)
            {
                continue;
            }

            var lookup = source.Look();
            foreach (var game in library.Games)
            {
                cancel.ThrowIfCancellationRequested();
                await _write.WaitAsync(cancel);
                try
                {
                    await UpdateGameAsync(game, lookup, run, changes, cancel);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // One game's images can't be written now (a full disk, a locked file): it keeps what it shows.
                    Log($"Artwork: {game.Key} nicht gespeichert: {e.GetType().Name}: {e.Message}");
                }
                finally
                {
                    _write.Release();
                }
            }

            if (library is { IsFound: true, ScanFailed: false })
            {
                await _write.WaitAsync(cancel);
                try
                {
                    RemoveLeftGames(library, changes);
                }
                finally
                {
                    _write.Release();
                }
            }
        }

        if (run.Copied + run.Downloaded > ManyImages)
        {
            ReleaseGarbage();
        }

        Log($"Artwork: {(run.Reload ? "neu geladen, " : string.Empty)}{run.Copied} kopiert, {run.Downloaded} geladen, {run.Missing} ohne Bild in {watch.ElapsedMilliseconds} ms"
            + (run.Offline ? "; keine Verbindung, der Rest folgt beim nächsten Einlesen" : string.Empty));
    }

    /// <summary>
    /// Reading the cache at start, or copying many images, leaves tens of MB of garbage (file handles, JSON, the copies) that
    /// the runtime would keep for a long while; with 5,000 games that cost 50 MB of the 400 MB budget (#487, measured). One
    /// collection right after such a run gives it back.
    /// </summary>
    private static void ReleaseGarbage() => GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

    private const int ManyImages = 100;

    /// <summary>Brings one game's images not set by hand up to date; the caller holds <see cref="_write"/>.</summary>
    private async Task UpdateGameAsync(Game game, IArtworkLookup lookup, RunState run, ChangeBatcher changes, CancellationToken cancel)
    {
        var key = game.Key;
        var before = RecordOf(key);
        if (before.Unreadable)
        {
            Log($"Artwork: Metadaten von {key} nicht lesbar; die Bilder darin gelten als von Hand gesetzt");
        }

        var artwork = before with { Unreadable = false };
        var written = false;
        foreach (var kind in Kinds)
        {
            var image = artwork.Of(kind);
            var size = image is null ? null : CheckedSize(key, kind, image);
            if (image is not null && size is null)
            {
                // Broken or gone: missing, also when it was set by hand (nothing the player chose can be lost, T487-14).
                artwork = artwork.With(kind, null);
                image = null;
            }

            if (image is { SetByHand: true } && run.Overwrite)
            {
                // "Von Hand gesetzte Artworks überschreiben": Arcadia chooses again; an own file stays offered, not in use.
                image = image.Source == ArtworkSource.File ? null : image with { SetByHand = false };
                size = image is null ? null : size;
                artwork = artwork.With(kind, image);
            }

            if (image is { SetByHand: true })
            {
                continue;
            }

            var pick = run.Reload
                ? await PickAsync(game, kind, lookup.For(game, kind), null, null, run, cancel) ?? (size is null ? null : new Pick.Keep())
                : await PickAsync(game, kind, lookup.For(game, kind), image, size, run, cancel);
            written |= pick is Pick.Take;
            artwork = Apply(artwork, kind, pick, run);
        }

        // The same original copied again (its copy was broken) leaves the metadata alike but the file new.
        if (written || artwork != before)
        {
            if (artwork.IsEmpty)
            {
                _records.Remove(key);
            }
            else
            {
                _records[key] = artwork;
            }

            _cache.WriteMetadata(artwork);
            Show(artwork, changes);
        }
    }

    private static readonly ArtworkKind[] Kinds = [ArtworkKind.Cover, ArtworkKind.Hero];

    private CachedArtwork Apply(CachedArtwork artwork, ArtworkKind kind, Pick? pick, RunState run)
    {
        switch (pick)
        {
            case Pick.Keep:
                return artwork;
            case Pick.Take take:
                var file = _cache.WriteImage(artwork.GameKey, kind, take.Content, ArtworkCache.ExtensionOf(take.Content)!);
                _checked[(artwork.GameKey, kind)] = take.Size;
                _versions[(artwork.GameKey, kind)] = _versions.GetValueOrDefault((artwork.GameKey, kind)) + 1;
                if (take.Candidate.Download is null)
                {
                    run.Copied++;
                }
                else
                {
                    run.Downloaded++;
                }

                return artwork.With(kind, new CachedImage(file, ArtworkSource.Launcher, take.Candidate.Origin, take.Stamp));
            default:
                run.Missing++;
                if (artwork.Of(kind) is not null)
                {
                    _cache.DeleteImage(artwork.GameKey, kind);
                }

                return artwork.With(kind, null);
        }
    }

    /// <summary>
    /// The image to show: the first candidate that fits, else the first image at all; the cached image wherever it stands
    /// for a candidate (same original) or would be downloaded again.
    /// </summary>
    private async Task<Pick?> PickAsync(
        Game game, ArtworkKind kind, IReadOnlyList<ArtworkCandidate> candidates, CachedImage? cached, ImageSize? cachedSize, RunState run, CancellationToken cancel)
    {
        Pick? fallback = null;
        var replaced = IsReplaced(cached, candidates);
        foreach (var candidate in candidates)
        {
            Pick? option;
            ImageSize size;
            if (candidate.File is { } path)
            {
                if (LocalStamp(path) is not { } stamp)
                {
                    continue;
                }

                if (cached is not null && cachedSize is { } same && cached.Origin == candidate.Origin && cached.Stamp == stamp)
                {
                    (option, size) = (new Pick.Keep(), same);
                }
                else if (ReadLocal(path) is { } content && ImageCheck.Of(new MemoryStream(content)) is { } local)
                {
                    (option, size) = (new Pick.Take(candidate, content, local, stamp), local);
                }
                else
                {
                    LogOnce(game, run, $"Artwork: {Name(kind)} von {game.Key} aus {candidate.Origin} ist kein lesbares Bild");
                    continue;
                }
            }
            else if (candidate.Download is { } url)
            {
                if (cached is not null && cachedSize is { } held && (cached.Stamp == url.AbsoluteUri || (ArtworkFormat.Fits(held, kind, _tolerance) && !replaced)))
                {
                    // Nothing earlier fits and the cache holds an image the launcher didn't replace: no download for what is there already.
                    (option, size) = (new Pick.Keep(), held);
                }
                else if (run.Offline || run.Failed.Contains(url))
                {
                    continue;
                }
                else if (await DownloadAsync(game, kind, url, run, cancel) is { } content && ImageCheck.Of(new MemoryStream(content)) is { } downloaded)
                {
                    (option, size) = (new Pick.Take(candidate, content, downloaded, url.AbsoluteUri), downloaded);
                }
                else
                {
                    run.Failed.Add(url);
                    continue;
                }
            }
            else
            {
                continue;
            }

            if (ArtworkFormat.Fits(size, kind, _tolerance))
            {
                return option;
            }

            fallback ??= option;
        }

        return fallback ?? (cachedSize is not null ? new Pick.Keep() : null);
    }

    /// <summary>
    /// The launcher no longer offers the address the cached image came from but another one of the same origin: it changed
    /// the image (e.g. Epic's catalog names new art, #488). A second host of the same files that is still offered is no change.
    /// </summary>
    private static bool IsReplaced(CachedImage? cached, IReadOnlyList<ArtworkCandidate> candidates) =>
        cached is not null
        && candidates.Any(c => c.Download is not null && c.Origin == cached.Origin)
        && !candidates.Any(c => c.Download?.AbsoluteUri == cached.Stamp);

    /// <summary>Length and time of a launcher's file, to tell it changed; null if it is missing or too large.</summary>
    private static string? LocalStamp(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists && file.Length <= ImageCheck.MaxBytes ? $"{file.Length}:{file.LastWriteTimeUtc.Ticks}" : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static byte[]? ReadLocal(string path)
    {
        try
        {
            var content = File.ReadAllBytes(path);
            return content.Length <= ImageCheck.MaxBytes ? content : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// The image at <paramref name="url"/>, at most <see cref="ImageCheck.MaxBytes"/>; null if there is none, it is too large,
    /// or the network fails (the run then downloads nothing more).
    /// </summary>
    private async Task<byte[]?> DownloadAsync(Game game, ArtworkKind kind, Uri url, RunState run, CancellationToken cancel)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
        {
            LogOnce(game, run, $"Artwork: {Name(kind)} von {game.Key}: nur https wird geladen");
            return null;
        }

        using var timeout = new CancellationTokenSource(DownloadTimeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancel, timeout.Token);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone or HttpStatusCode.Forbidden)
            {
                LogOnce(game, run, $"Artwork: {Name(kind)} von {game.Key} gibt es auf {url.Host} nicht");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                LogOnce(game, run, $"Artwork: {Name(kind)} von {game.Key}: {url.Host} antwortet {(int)response.StatusCode}");
                return null;
            }

            if (response.Content.Headers.ContentLength > ImageCheck.MaxBytes)
            {
                LogOnce(game, run, $"Artwork: {Name(kind)} von {game.Key} ist größer als 20 MB");
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(linked.Token);
            var content = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, linked.Token)) > 0)
            {
                if (content.Length + read > ImageCheck.MaxBytes)
                {
                    LogOnce(game, run, $"Artwork: {Name(kind)} von {game.Key} ist größer als 20 MB");
                    return null;
                }

                content.Write(buffer, 0, read);
            }

            if (ImageCheck.Of(content) is null)
            {
                LogOnce(game, run, $"Artwork: {Name(kind)} von {game.Key} von {url.Host} ist kein lesbares Bild");
                return null;
            }

            return content.ToArray();
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            LogOnce(game, run, $"Artwork: {Name(kind)} von {game.Key}: {url.Host} antwortet nicht in {DownloadTimeout.TotalSeconds:0} s");
            GoOffline(run);
            return null;
        }
        catch (HttpRequestException e)
        {
            LogOnce(game, run, $"Artwork: {Name(kind)} von {game.Key}: keine Verbindung zu {url.Host} ({e.HttpRequestError})");
            GoOffline(run);
            return null;
        }
        catch (IOException e)
        {
            // The connection broke off during the answer.
            LogOnce(game, run, $"Artwork: {Name(kind)} von {game.Key}: Verbindung zu {url.Host} abgebrochen ({e.GetType().Name})");
            GoOffline(run);
            return null;
        }
    }

    /// <summary>One line per game and run, however many of its images or addresses failed: the first failure says why.</summary>
    private void LogOnce(Game game, RunState run, string line)
    {
        if (run.Logged.Add(game.Key))
        {
            Log(line);
        }
    }

    /// <summary>A failed connection is tried again at the next scan; this run downloads nothing more.</summary>
    private static void GoOffline(RunState run)
    {
        run.Offline = true;
    }

    /// <summary>The cached image's size if it is a whole image; checked once per session, unless the view found it broken.</summary>
    private ImageSize? CheckedSize(string key, ArtworkKind kind, CachedImage image)
    {
        if (_broken.TryRemove((key, kind), out _))
        {
            _checked.Remove((key, kind));
            return null;
        }

        if (_checked.TryGetValue((key, kind), out var size))
        {
            return size;
        }

        if (ImageCheck.OfFile(_cache.PathOf(key, image)) is not { } found)
        {
            Log($"Artwork: {Name(kind)} von {key} im Cache ist kein lesbares Bild und wird neu geladen");
            return null;
        }

        _checked[(key, kind)] = found;
        return found;
    }

    /// <summary>A game no longer in its launcher's library loses its automatic images; those set by hand stay for its return (#13).</summary>
    private void RemoveLeftGames(Library.LauncherLibrary library, ChangeBatcher changes)
    {
        var launcherId = library.Launcher.Id;
        var present = library.Games.Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var (key, artwork) in _records.Where(r => Game.LauncherIdOf(r.Key) == launcherId && !present.Contains(r.Key)).ToList())
        {
            var kept = artwork;
            foreach (var kind in Kinds)
            {
                if (kept.Of(kind) is { SetByHand: false })
                {
                    _cache.DeleteImage(key, kind);
                    _checked.Remove((key, kind));
                    kept = kept.With(kind, null);
                }
            }

            if (kept == artwork)
            {
                continue;
            }

            _cache.WriteMetadata(kept);
            if (kept.IsEmpty)
            {
                _records.Remove(key);
            }
            else
            {
                _records[key] = kept;
            }

            Show(kept, changes);
            Log($"Artwork: {key} ist nicht mehr in der Bibliothek, seine automatischen Bilder sind gelöscht");
        }
    }

    private void Show(CachedArtwork artwork, ChangeBatcher changes)
    {
        ArtworkFile? FileOf(ArtworkKind kind) =>
            artwork.Of(kind) is { } image && CheckedSize(artwork.GameKey, kind, image) is { } size
                ? new ArtworkFile(artwork.GameKey, kind, _cache.PathOf(artwork.GameKey, image), size, _versions.GetValueOrDefault((artwork.GameKey, kind)))
                : null;

        var shown = new GameArtwork(FileOf(ArtworkKind.Cover), FileOf(ArtworkKind.Hero));
        if (shown is { Cover: null, Hero: null })
        {
            _shown.TryRemove(artwork.GameKey, out _);
        }
        else
        {
            _shown[artwork.GameKey] = shown;
        }

        changes.Add(artwork.GameKey);
    }

    private static string Name(ArtworkKind kind) => kind == ArtworkKind.Cover ? "Cover" : "Hintergrund";

    private abstract record Pick
    {
        public sealed record Keep : Pick;

        public sealed record Take(ArtworkCandidate Candidate, byte[] Content, ImageSize Size, string Stamp) : Pick;
    }

    /// <summary>The libraries a run goes over, and whether it is "Alle Artworks neu laden" (#490).</summary>
    private sealed record PendingRun(IReadOnlyList<LibraryArtwork> Libraries, bool Reload, bool Overwrite);

    private sealed class RunState
    {
        /// <summary>Every image is taken from its source again, also one already cached.</summary>
        public bool Reload { get; init; }

        /// <summary>Images set by hand give way too.</summary>
        public bool Overwrite { get; init; }

        public HashSet<string> Logged { get; } = new(StringComparer.Ordinal);

        // An address that gave no image is not asked again in this run, but by the next: a rescan may find what the server
        // added meanwhile. One that gave an image is asked again for another game naming it (Epic's editions, #488).
        public HashSet<Uri> Failed { get; } = [];

        public bool Offline { get; set; }

        public int Copied { get; set; }

        public int Downloaded { get; set; }

        public int Missing { get; set; }
    }

    /// <summary>Collects changed games and hands them on at most every <see cref="ChangeBatch"/>, and at the end.</summary>
    private sealed class ChangeBatcher(ArtworkService owner)
    {
        private readonly HashSet<string> _keys = new(StringComparer.Ordinal);
        private long _since = Stopwatch.GetTimestamp();

        public void Add(string key)
        {
            _keys.Add(key);
            if (Stopwatch.GetElapsedTime(_since) >= ChangeBatch)
            {
                Flush();
            }
        }

        public void Flush()
        {
            _since = Stopwatch.GetTimestamp();
            if (_keys.Count == 0)
            {
                return;
            }

            var keys = _keys.ToList();
            _keys.Clear();
            owner.Changed?.Invoke(owner, keys);
        }
    }
}
