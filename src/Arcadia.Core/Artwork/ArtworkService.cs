using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork;

/// <summary>
/// Brings every game's cover and background into the cache and says which games' images changed (#13, #487, #489). After
/// each scan it runs once over the libraries in the background: for every image not set by hand it asks the sources in the
/// order of "Reihenfolge für Artwork" (the launcher's images in their order, SteamGridDB's best rated) and keeps the first
/// that fits the format within "Zuschneiden erlaubt", else the first image at all; an image already cached stays unless an
/// earlier or changed file of the launcher replaces it, so a download happens only for what is missing. SteamGridDB is asked
/// only with a usable key, by the game's store id, and only for an image the cache doesn't hold fitting; a game it answered
/// for is not asked again in this session, unless the player searches again ("Jetzt suchen", a new key).
/// </summary>
/// <remarks>
/// One run at a time, off the UI thread; a scan during a run queues the newest libraries for the next run, so a run cut off
/// (Arcadia closed) is simply taken up by the next. What the player does in "Artwork ändern" (#490, ArtworkService.ByHand.cs)
/// waits for the game the run is at, never for the whole run: each game is written under one lock. <see cref="Of"/> may be called from any thread; <see cref="Changed"/> and
/// <see cref="Failed"/> are raised on the run's thread. Failures of single images are logged and leave the placeholder; no
/// download outlives <see cref="DownloadTimeout"/>, and after a timeout or with no network the run downloads nothing more;
/// a key SteamGridDB turns down, or no connection to it, ends its part of the run. <see cref="SearchChanged"/> is raised on
/// the run's thread too.
/// </remarks>
public sealed partial class ArtworkService : IDisposable
{
    /// <summary>The longest a download may take (#13); then the image waits for the next scan.</summary>
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan ChangeBatch = TimeSpan.FromMilliseconds(250);

    private readonly ArtworkCache _cache;
    private readonly HttpClient _http;
    private readonly TimeProvider _time;
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

    // Games SteamGridDB answered for in this session (found or not): not asked again until the player searches again.
    private readonly HashSet<(string Key, ArtworkKind Kind)> _askedSteamGridDb = [];

    private PendingRun? _pending;
    private bool _pendingByPlayer;
    private bool _pendingSearchAgain;
    private bool _searchingAgain;
    private Task _runner = Task.CompletedTask;
    private ArtworkSearch _search = ArtworkSearch.NotYet;
    private long _searchShownAt;

    public ArtworkService(ArtworkCache cache, HttpClient http, TimeProvider time)
    {
        _cache = cache;
        _http = http;
        _time = time;
    }

    public Action<string> Log { get; init; } = _ => { };

    /// <summary>"Reihenfolge für Artwork" and "Zuschneiden erlaubt", read at the start of every run (#489).</summary>
    public Func<ArtworkSettings> Settings { get; init; } = () => new ArtworkSettings();

    /// <summary>SteamGridDB with the player's key (#489); null: launcher images only.</summary>
    public SteamGridDbKey? SteamGridDb { get; init; }

    /// <summary>What "Fehlende Cover und Hintergründe" shows (#489): the counts, the run going on, the last search's result.</summary>
    public ArtworkSearch Search
    {
        get
        {
            lock (_gate)
            {
                return _search;
            }
        }
    }

    /// <summary>The games of the last libraries with images, e.g. for "Alle Artworks neu laden?"; 0 before the first scan.</summary>
    public int GameCount
    {
        get
        {
            lock (_gate)
            {
                return _lastLibraries?.Where(l => l.Source is not null).SelectMany(l => l.Library.Games).Select(g => g.Key).Distinct(StringComparer.Ordinal).Count() ?? 0;
            }
        }
    }

    /// <summary>The game's id at a store SteamGridDB knows, from its launcher's images (#489); null if it has none.</summary>
    internal ArtworkStoreId? StoreIdOf(Game game) => _sources.GetValueOrDefault(game.LauncherId)?.Look().StoreIdOf(game);

    /// <summary><see cref="Search"/> changed; raised on the run's thread, during a run at most every 250 ms.</summary>
    public event EventHandler? SearchChanged;

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

    /// <summary>
    /// Searches the last libraries again at once, SteamGridDB also for games it already answered for in this session:
    /// "Jetzt suchen" (<paramref name="byPlayer"/>, its result is <see cref="ArtworkSearch.Result"/>) or a new key. Nothing
    /// before the first scan; while a run goes, after it.
    /// </summary>
    /// <returns>False before the first scan: there is nothing to search yet.</returns>
    public bool SearchAgain(bool byPlayer)
    {
        IReadOnlyList<LibraryArtwork> last;
        lock (_gate)
        {
            if (_lastLibraries is null)
            {
                return false;
            }

            last = _lastLibraries;
            if (_pendingSearchAgain || _searchingAgain)
            {
                // Pressed again while that search waits or goes: it asks no game twice.
                _pendingByPlayer |= byPlayer && _pendingSearchAgain;
                return true;
            }

            _pendingSearchAgain = true;
            _pendingByPlayer |= byPlayer;
        }

        Start(new PendingRun(last, Reload: false, Overwrite: false));
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
                bool byPlayer;
                lock (_gate)
                {
                    pending = _pending;
                    byPlayer = _pendingByPlayer;
                    _pending = null;
                    _pendingByPlayer = false;
                    _searchingAgain = _pendingSearchAgain;
                    if (_pendingSearchAgain || pending is { Reload: true })
                    {
                        _pendingSearchAgain = false;
                        _askedSteamGridDb.Clear();
                    }
                }

                if (pending is not null)
                {
                    await UpdateAsync(pending, byPlayer, changes, _shutdown.Token);
                }

                changes.Flush();
                lock (_gate)
                {
                    _searchingAgain = false;
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
            _pendingByPlayer = false;
            _searchingAgain = false;
            _runner = Task.CompletedTask;
            _search = _search with { Running = null };
        }

        SearchChanged?.Invoke(this, EventArgs.Empty);
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

    private async Task UpdateAsync(PendingRun pending, bool byPlayer, ChangeBatcher changes, CancellationToken cancel)
    {
        var watch = Stopwatch.StartNew();
        var run = NewRunState(SteamGridDb is { IsUsable: true }) with { Reload = pending.Reload, Overwrite = pending.Overwrite };
        var libraries = pending.Libraries;
        var searched = libraries.Where(l => l.Source is not null).SelectMany(l => l.Library.Games).Where(g => run.Reload || IsMissingAny(g.Key)).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        ShowSearch(new ArtworkRun(0, searched.Count, byPlayer, run.Reload), force: true);
        foreach (var (library, source) in libraries)
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

                if (searched.Contains(game.Key))
                {
                    run.Searched++;
                    ShowSearch(new ArtworkRun(run.Searched, searched.Count, byPlayer, run.Reload), force: false);
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
            + (run.SteamGridDbAsked > 0 ? $"; SteamGridDB {run.SteamGridDbAsked} gefragt" : string.Empty)
            + (run.Offline ? "; keine Verbindung, der Rest folgt beim nächsten Einlesen" : string.Empty)
            + (run.SteamGridDbStop is { } stop ? $"; SteamGridDB übersprungen ({stop})" : string.Empty));
        EndSearch(libraries, run, byPlayer);
    }

    private bool IsMissingAny(string key) => _shown.GetValueOrDefault(key) is not { Cover: not null, Hero: not null };

    /// <summary>The run's progress; while it goes, at most every <see cref="ChangeBatch"/>.</summary>
    private void ShowSearch(ArtworkRun running, bool force)
    {
        if (!force && Stopwatch.GetElapsedTime(_searchShownAt) < ChangeBatch && running.Done < running.Total)
        {
            return;
        }

        _searchShownAt = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            _search = _search with { Running = running };
        }

        SearchChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The counts after the run, over every game of the libraries; a run the player started keeps its result.</summary>
    private void EndSearch(IReadOnlyList<LibraryArtwork> libraries, RunState run, bool byPlayer)
    {
        var games = libraries.SelectMany(l => l.Library.Games).Select(g => g.Key).Distinct(StringComparer.Ordinal).ToList();
        var missingCovers = games.Count(key => _shown.GetValueOrDefault(key)?.Cover is null);
        var missingHeroes = games.Count(key => _shown.GetValueOrDefault(key)?.Hero is null);
        lock (_gate)
        {
            _search = new ArtworkSearch(
                missingCovers,
                missingHeroes,
                null,
                byPlayer ? new ArtworkSearchResult(run.FoundCovers, run.FoundHeroes, run.WithSteamGridDb, run.SteamGridDbStop) : _search.Result);
        }

        SearchChanged?.Invoke(this, EventArgs.Empty);
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
                ? await PickAsync(game, kind, lookup, null, null, run, cancel) ?? (size is null ? null : new Pick.Keep())
                : await PickAsync(game, kind, lookup, image, size, run, cancel);
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

                if (kind == ArtworkKind.Cover)
                {
                    run.FoundCovers++;
                }
                else
                {
                    run.FoundHeroes++;
                }

                return artwork.With(kind, new CachedImage(file, take.Source, take.Candidate.Origin, take.Stamp));
            default:
                run.Missing++;
                if (artwork.Of(kind) is not null)
                {
                    _cache.DeleteImage(artwork.GameKey, kind);
                }

                return artwork.With(kind, null);
        }
    }

    /// <summary>The launcher's image as a scan would choose it, without SteamGridDB ("Artwork ändern", #490).</summary>
    private Task<Pick?> PickAsync(
        Game game, ArtworkKind kind, IReadOnlyList<ArtworkCandidate> candidates, CachedImage? cached, ImageSize? cachedSize, RunState run, CancellationToken cancel) =>
        PickAsync(game, kind, new FixedCandidates(candidates), cached, cachedSize, run with { Order = [ArtworkSourceKind.Launcher] }, cancel);

    /// <summary>
    /// The image to show: the sources in their order, the first candidate that fits, else the first image at all; the cached
    /// image wherever it stands for a candidate (same original) or would be downloaded again.
    /// </summary>
    private async Task<Pick?> PickAsync(
        Game game, ArtworkKind kind, IArtworkLookup lookup, CachedImage? cached, ImageSize? cachedSize, RunState run, CancellationToken cancel)
    {
        Pick? fallback = null;
        foreach (var source in run.Order)
        {
            if (source == ArtworkSourceKind.SteamGridDb && cached?.Origin == SteamGridDbOrigin
                && cachedSize is { } fromSteamGridDb && ArtworkFormat.Fits(fromSteamGridDb, kind, run.Tolerance))
            {
                // SteamGridDB isn't asked again for an image it gave, so its place in the order is held by the cached one.
                return new Pick.Keep();
            }

            var (candidates, from) = source == ArtworkSourceKind.SteamGridDb
                ? (await SteamGridDbCandidatesAsync(game, kind, lookup, cachedSize, run, cancel), ArtworkSource.SteamGridDb)
                : (lookup.For(game, kind), ArtworkSource.Launcher);
            var replaced = IsReplaced(cached, candidates);
            foreach (var candidate in candidates)
            {
                if (await OptionOfAsync(game, kind, candidate, from, replaced, cached, cachedSize, run, cancel) is not var (option, size))
                {
                    continue;
                }

                if (ArtworkFormat.Fits(size, kind, run.Tolerance))
                {
                    return option;
                }

                fallback ??= option;
            }
        }

        return fallback ?? (cachedSize is not null ? new Pick.Keep() : null);
    }

    /// <summary>What a candidate offers and its size; null if it offers nothing now (gone, unreadable, not downloadable).</summary>
    private async Task<(Pick Option, ImageSize Size)?> OptionOfAsync(
        Game game, ArtworkKind kind, ArtworkCandidate candidate, ArtworkSource from, bool replaced, CachedImage? cached, ImageSize? cachedSize, RunState run, CancellationToken cancel)
    {
        if (candidate.File is { } path)
        {
            if (LocalStamp(path) is not { } stamp)
            {
                return null;
            }

            if (cached is not null && cachedSize is { } same && cached.Origin == candidate.Origin && cached.Stamp == stamp)
            {
                return (new Pick.Keep(), same);
            }

            if (ReadLocal(path) is { } content && ImageCheck.Of(new MemoryStream(content)) is { } local)
            {
                return (new Pick.Take(candidate, from, content, local, stamp), local);
            }

            LogOnce(game, run, $"Artwork: {Name(kind)} von {game.Key} aus {candidate.Origin} ist kein lesbares Bild");
            return null;
        }

        if (candidate.Download is not { } url)
        {
            return null;
        }

        if (cached is not null && cachedSize is { } held && (cached.Stamp == url.AbsoluteUri || (ArtworkFormat.Fits(held, kind, run.Tolerance) && !replaced)))
        {
            // Nothing earlier fits and the cache holds an image the launcher didn't replace: no download for what is there already.
            return (new Pick.Keep(), held);
        }

        if (run.Offline || run.Failed.Contains(url))
        {
            return null;
        }

        if (await DownloadAsync(game, kind, url, run, cancel) is { } downloadedContent && ImageCheck.Of(new MemoryStream(downloadedContent)) is { } downloaded)
        {
            return (new Pick.Take(candidate, from, downloadedContent, downloaded, url.AbsoluteUri), downloaded);
        }

        run.Failed.Add(url);
        return null;
    }

    /// <summary>
    /// SteamGridDB's image for the game (#489): its best rated that fits by the size SteamGridDB names, else its best rated
    /// at all, so only one is downloaded. None without a usable key, without a store id, while the cache holds a fitting
    /// image, for a game it answered for in this session, or once SteamGridDB failed in this run.
    /// </summary>
    private async Task<IReadOnlyList<ArtworkCandidate>> SteamGridDbCandidatesAsync(
        Game game, ArtworkKind kind, IArtworkLookup lookup, ImageSize? cachedSize, RunState run, CancellationToken cancel)
    {
        if (SteamGridDb is not { } steamGridDb || !run.SteamGridDbUsable
            || cachedSize is { } held && ArtworkFormat.Fits(held, kind, run.Tolerance)
            || _askedSteamGridDb.Contains((game.Key, kind))
            || lookup.StoreIdOf(game) is not { } storeId)
        {
            return [];
        }

        var answer = await steamGridDb.ImagesAsync(storeId, kind, cancel);
        run.SteamGridDbAsked++;
        switch (answer.Outcome)
        {
            case SteamGridDbOutcome.KeyRejected or SteamGridDbOutcome.NoConnection:
                // Not one request per game: the rest of the run does without SteamGridDB, the next scan tries again.
                run.SteamGridDbUsable = false;
                run.SteamGridDbStop = answer.Outcome;
                return [];
            case SteamGridDbOutcome.NotFound:
                _askedSteamGridDb.Add((game.Key, kind));
                return [];
        }

        _askedSteamGridDb.Add((game.Key, kind));
        var best = answer.Images.FirstOrDefault(i => ArtworkFormat.Fits(new ImageSize(i.Width, i.Height), kind, run.Tolerance)) ?? answer.Images.FirstOrDefault();
        return best is null ? [] : [new ArtworkCandidate(SteamGridDbOrigin, Download: best.Url)];
    }

    /// <summary>The <see cref="CachedImage.Origin"/> of SteamGridDB's images.</summary>
    public const string SteamGridDbOrigin = "steamgriddb";

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

        public sealed record Take(ArtworkCandidate Candidate, ArtworkSource Source, byte[] Content, ImageSize Size, string Stamp) : Pick;
    }

    /// <summary>Candidates already known, as a lookup.</summary>
    private sealed class FixedCandidates(IReadOnlyList<ArtworkCandidate> candidates) : IArtworkLookup
    {
        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) => candidates;
    }

    /// <summary>The libraries a run goes over, and whether it is "Alle Artworks neu laden" (#490).</summary>
    private sealed record PendingRun(IReadOnlyList<LibraryArtwork> Libraries, bool Reload, bool Overwrite);

    /// <summary>A run's state with the order and tolerance as set now (#489).</summary>
    private RunState NewRunState(bool steamGridDbUsable = false)
    {
        var settings = Settings().WithValidValues();
        return new RunState { Order = settings.Order, Tolerance = settings.Tolerance, SteamGridDbUsable = steamGridDbUsable, WithSteamGridDb = steamGridDbUsable };
    }

    private sealed record RunState
    {
        /// <summary>Every image is taken from its source again, also one already cached.</summary>
        public bool Reload { get; init; }

        /// <summary>Images set by hand give way too.</summary>
        public bool Overwrite { get; init; }

        public IReadOnlyList<ArtworkSourceKind> Order { get; init; } = ArtworkSettings.DefaultOrder;

        public double Tolerance { get; init; } = ArtworkFormat.NormalTolerance;

        /// <summary>A usable key at the start, and SteamGridDB hasn't failed in this run.</summary>
        public bool SteamGridDbUsable { get; set; }

        /// <summary>Why SteamGridDB was left out for the rest of the run; null while it wasn't.</summary>
        public SteamGridDbOutcome? SteamGridDbStop { get; set; }

        public bool WithSteamGridDb { get; init; }

        public int SteamGridDbAsked { get; set; }

        public int FoundCovers { get; set; }

        public int FoundHeroes { get; set; }

        /// <summary>Games done of those that missed an image at the start.</summary>
        public int Searched { get; set; }

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
