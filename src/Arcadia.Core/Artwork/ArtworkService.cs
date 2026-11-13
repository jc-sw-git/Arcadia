using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork;

/// <summary>
/// Brings every game's cover and background into the cache and says which games' images changed (#13, #487, #489). After
/// each scan it runs once over the libraries in the background: for every image not set by hand it asks the sources switched
/// on, in the order of "Reihenfolge für Artwork" (the launcher's images in their order, SteamGridDB's best rated) and keeps
/// the first that fits the format (<see cref="ArtworkFormat"/>), else the first image at all; an image already cached stays
/// unless an earlier or changed file of the launcher replaces it, so a download happens only for what is missing.
/// SteamGridDB is asked only with a usable key, by the game's store id, only for an image the cache doesn't hold fitting, and
/// on its own only for a game new to the library (#506, <see cref="KnownGames"/>); every other game only when the player
/// asks ("Jetzt suchen", "Alle Artworks neu laden") or the first usable key arrives. An image of a source switched off is
/// kept aside, not shown, and in use again when the source is switched on.
/// </summary>
/// <remarks>
/// One run at a time, off the UI thread; a scan during a run queues the newest libraries for the next run, so a run cut off
/// (Arcadia closed) is simply taken up by the next. What the player does in "Artwork ändern" (#490, ArtworkService.ByHand.cs)
/// waits for the game the run is at, never for the whole run: each game is written under one lock. <see cref="Of"/> may be called from any thread; <see cref="Changed"/> and
/// <see cref="Failed"/> are raised on the run's thread. Failures of single images are logged and leave the placeholder; no
/// download outlives <see cref="DownloadTimeout"/>, and after a timeout or no connection the run downloads nothing more from that host, while other hosts (another launcher's images, SteamGridDB's) still load;
/// a key SteamGridDB turns down, or no connection to it, ends its part of the run. <see cref="SearchChanged"/> is raised on
/// the run's thread too.
/// </remarks>
public sealed partial class ArtworkService : IDisposable
{
    /// <summary>The longest a download may take (#13); then the image waits for the next search (#506).</summary>
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

    // The sizes of images kept for a source switched off, as _checked holds those in use.
    private readonly Dictionary<(string Key, ArtworkKind Kind), ImageSize> _checkedOff = [];

    // Where each shown automatic image came from, so one of a source switched off disappears at once (#506).
    private readonly ConcurrentDictionary<string, (ArtworkSource? Cover, ArtworkSource? Hero)> _shownFrom = new(StringComparer.Ordinal);
    private IReadOnlySet<ArtworkSource> _offNow = new HashSet<ArtworkSource>();

    private KnownGames? _known;

    // Games SteamGridDB doesn't know by their launcher's id in this session: found by name (#491); read by "Artwork ändern" too.
    private readonly ConcurrentDictionary<string, bool> _unknownById = new(StringComparer.Ordinal);

    private PendingRun? _pending;
    private RunTrigger? _running;
    private CancellationTokenSource? _runCancel;
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

    /// <summary>"Reihenfolge für Artwork", read at the start of every run (#489).</summary>
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

    /// <summary>
    /// The games of the last libraries a run looks at (with images of their launcher, or any with a usable SteamGridDB key),
    /// e.g. for "Alle Artworks neu laden?"; 0 before the first scan.
    /// </summary>
    public int GameCount
    {
        get
        {
            var withSteamGridDb = SteamGridDb is { IsUsable: true };
            lock (_gate)
            {
                return _lastLibraries?.Where(l => l.Source is not null || (withSteamGridDb && !l.MadeUp)).SelectMany(l => l.Library.Games).Select(g => g.Key).Distinct(StringComparer.Ordinal).Count() ?? 0;
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

    /// <summary>
    /// The game's cached images; null while it has none (or the cache isn't read yet). An automatic image of a source
    /// switched off isn't shown, from the moment it is switched off (#506).
    /// </summary>
    public GameArtwork? Of(string gameKey)
    {
        if (_shown.GetValueOrDefault(gameKey) is not { } shown)
        {
            return null;
        }

        var off = Volatile.Read(ref _offNow);
        if (off.Count == 0 || !_shownFrom.TryGetValue(gameKey, out var from))
        {
            return shown;
        }

        var cover = from.Cover is { } coverFrom && off.Contains(coverFrom) ? null : shown.Cover;
        var hero = from.Hero is { } heroFrom && off.Contains(heroFrom) ? null : shown.Hero;
        return (cover, hero) switch
        {
            (null, null) => null,
            _ when cover == shown.Cover && hero == shown.Hero => shown,
            _ => new GameArtwork(cover, hero),
        };
    }

    /// <summary>Reads what the cache holds, in the background, so the covers show before the first scan ends.</summary>
    public void Open()
    {
        Volatile.Write(ref _offNow, OffSources(Settings().WithValidValues()));
        Start(null);
    }

    /// <summary>
    /// The libraries were read (again): brings their images into the cache in the background, and removes the automatic
    /// images of games no longer in a library that was read whole. SteamGridDB is asked only for games new to the library.
    /// </summary>
    public void Update(IReadOnlyList<LibraryArtwork> libraries) => Start(new PendingRun(libraries, RunTrigger.Rescan));

    /// <summary>
    /// The order or a source's switch changed (#506): images of a source switched off disappear at
    /// once, and a run picks again among the images on disk and the launchers' own; it searches nothing.
    /// </summary>
    public void SettingsChanged()
    {
        Volatile.Write(ref _offNow, OffSources(Settings().WithValidValues()));
        Changed?.Invoke(this, [.. _shown.Keys]);
        IReadOnlyList<LibraryArtwork>? libraries;
        lock (_gate)
        {
            libraries = _lastLibraries;
        }

        if (libraries is not null)
        {
            Update(libraries);
        }
    }

    /// <summary>
    /// "Suche abbrechen" (#506): the search going on stops after the request in flight; what it found stays, the games it
    /// didn't reach wait for the next "Jetzt suchen". False when no search runs.
    /// </summary>
    public bool CancelSearch()
    {
        lock (_gate)
        {
            if (_runCancel is not { } running || Search.Running is null)
            {
                return false;
            }

            // A search queued behind it goes too; the libraries still get their launchers' images.
            if (_pending is { } queued)
            {
                _pending = queued with { Trigger = RunTrigger.Rescan };
            }

            running.Cancel();
            return true;
        }
    }

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
        Start(new PendingRun(libraries, RunTrigger.Reload, Overwrite: overwriteSetByHand));
        return true;
    }

    /// <summary>
    /// Searches every game of the last libraries still missing an image at once, SteamGridDB also for games it answered
    /// for before: "Jetzt suchen" (<paramref name="byPlayer"/>, its result is <see cref="ArtworkSearch.Result"/>) or the first
    /// usable key (#506). Nothing before the first scan; while a run goes, after it.
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
            if (_pending is { Trigger: RunTrigger.FirstKey or RunTrigger.Player } queued)
            {
                // Pressed again while that search waits: it asks no game twice.
                _pending = byPlayer ? queued with { Trigger = RunTrigger.Player } : queued;
                return true;
            }

            if (_running is RunTrigger.FirstKey or RunTrigger.Player && _pending is null)
            {
                return true;
            }
        }

        Start(new PendingRun(last, byPlayer ? RunTrigger.Player : RunTrigger.FirstKey));
        return true;
    }

    /// <summary>These games are asked at SteamGridDB in the next run, e.g. after their SteamGridDB game was chosen (#491).</summary>
    internal void SearchFor(IReadOnlyList<LibraryArtwork> libraries, IEnumerable<string> gameKeys) =>
        Start(new PendingRun(libraries, RunTrigger.Rescan) { AskAlso = gameKeys.ToHashSet(StringComparer.Ordinal) });

    /// <summary>The run going on now and those queued behind it; for the tests.</summary>
    public Task WhenIdleAsync()
    {
        lock (_gate)
        {
            return _runner;
        }
    }

    /// <summary>
    /// The view couldn't decode the image (damaged inside): it counts as missing. The next scan copies a launcher's local file
    /// again; an image to download comes back with the next search, as rescans go online for no game (#506). An image
    /// "Artwork ändern" only offers is no game's image: the offer just shows nothing.
    /// </summary>
    public void ReportBroken(ArtworkFile file)
    {
        if (Of(file.GameKey)?.Of(file.Kind)?.Path != file.Path)
        {
            return;
        }

        _broken[(file.GameKey, file.Kind)] = true;
        Log($"Artwork: {Name(file.Kind)} von {file.GameKey} ist beschädigt; eine Datei des Launchers wird beim nächsten Einlesen neu kopiert, ein Download bei der nächsten Suche");
    }

    public void Dispose() => _shutdown.Cancel();

    private void Start(PendingRun? run)
    {
        lock (_gate)
        {
            if (run is not null)
            {
                // A search or reload queued stays one when a scan comes after it; the newest libraries go.
                _pending = _pending is { } queued
                    ? run with
                    {
                        Trigger = (RunTrigger)Math.Max((int)run.Trigger, (int)queued.Trigger),
                        Overwrite = run.Overwrite || queued.Overwrite,
                        AskAlso = run.AskAlso.Union(queued.AskAlso).ToHashSet(StringComparer.Ordinal),
                    }
                    : run;
                _lastLibraries = run.Libraries;
                foreach (var (library, source, _) in run.Libraries)
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

        if (run is not null)
        {
            // New libraries: a waiting game may have left or come back.
            WaitingChanged?.Invoke(this, EventArgs.Empty);
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
                CancellationTokenSource? cancel = null;
                lock (_gate)
                {
                    pending = _pending;
                    _pending = null;
                    _running = pending?.Trigger;
                    if (pending is not null)
                    {
                        cancel = _runCancel = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                    }
                }

                try
                {
                    if (pending is not null)
                    {
                        await UpdateAsync(pending, changes, cancel!.Token);
                    }
                }
                finally
                {
                    lock (_gate)
                    {
                        _running = null;
                        _runCancel = null;
                    }

                    cancel?.Dispose();
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

            // Read here, off the UI thread, before the waiting list asks for it (#491).
            _ = Names.All;
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
        var trigger = pending.Trigger;
        var byPlayer = trigger == RunTrigger.Player;
        var settings = Settings().WithValidValues();
        Volatile.Write(ref _offNow, OffSources(settings));
        var libraries = pending.Libraries;
        var madeUp = MadeUpGames(libraries);
        var run = NewRunState(SteamGridDb is { IsUsable: true }) with { Reload = trigger == RunTrigger.Reload, Overwrite = pending.Overwrite, MadeUp = madeUp };
        var keys = libraries.SelectMany(l => l.Library.Games).Select(g => g.Key).Distinct(StringComparer.Ordinal).ToList();
        var newGames = Known.NewOf(keys);

        // A scan asks SteamGridDB only for games new to the library (#506); a search the player or a first key started, all.
        run.Asks = trigger == RunTrigger.Rescan ? newGames.Union(pending.AskAlso).ToHashSet(StringComparer.Ordinal) : null;
        var searched = (trigger switch
        {
            RunTrigger.Rescan when !run.AsksSteamGridDb => [],
            RunTrigger.Rescan => keys.Where(run.Asks!.Contains),
            RunTrigger.Reload => keys,
            _ => keys.Where(IsMissingAny),
        }).Where(key => !madeUp.Contains(key)).ToHashSet(StringComparer.Ordinal);
        run.ShowsProgress = trigger != RunTrigger.Rescan || searched.Count > 0;
        if (run.ShowsProgress)
        {
            Log($"Artwork-Suche gestartet ({NameOf(trigger)}): {searched.Count} Spiele");
            ShowSearch(run, new ArtworkRun(0, searched.Count, byPlayer, run.Reload), force: true);
        }

        var cancelled = false;
        try
        {
            foreach (var (library, source, _) in libraries)
            {
                var lookup = source?.Look() ?? NoImages;
                foreach (var game in library.Games)
                {
                    cancel.ThrowIfCancellationRequested();
                    if (IsVisited(source, game.Key, run))
                    {
                        await UpdateGameLockedAsync(game, lookup, run, changes, cancel);
                    }

                    if (newGames.Contains(game.Key))
                    {
                        Known.Add(game.Key);
                    }

                    if (searched.Contains(game.Key))
                    {
                        run.Searched++;
                        ShowSearch(run, new ArtworkRun(run.Searched, searched.Count, byPlayer, run.Reload), force: false);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (!_shutdown.IsCancellationRequested)
        {
            // "Suche abbrechen": the new games it didn't reach wait for "Jetzt suchen", like every other game.
            cancelled = true;
            foreach (var key in newGames)
            {
                Known.Add(key);
            }
        }
        finally
        {
            // Closing Arcadia saves only the games the run reached: the others are still new at the next start.
            Known.Save();
        }

        if (run.Copied + run.Downloaded > ManyImages)
        {
            ReleaseGarbage();
        }

        if (run.ShowsProgress)
        {
            Log($"Artwork-Suche {(cancelled ? "abgebrochen" : "beendet")} ({NameOf(trigger)}): {run.Searched} von {searched.Count} Spielen, {run.FoundCovers} Cover, {run.FoundHeroes} Hintergründe");
        }

        if (run.ShowsProgress || run.Copied + run.Downloaded > 0)
        {
            Log($"Artwork: {(run.Reload ? "neu geladen, " : string.Empty)}{run.Copied} kopiert, {run.Downloaded} geladen, {run.Missing} ohne Bild in {watch.ElapsedMilliseconds} ms"
                + (run.SteamGridDbAsked > 0 ? $"; SteamGridDB {run.SteamGridDbAsked} gefragt" : string.Empty)
                + (run.OfflineHosts.Count > 0 ? $"; keine Verbindung zu {string.Join(", ", run.OfflineHosts.Order(StringComparer.Ordinal))}, der Rest folgt bei der nächsten Suche" : string.Empty)
                + (run.SteamGridDbStop is { } stop ? $"; SteamGridDB übersprungen ({stop})" : string.Empty));
        }

        EndSearch(libraries, run, byPlayer && !cancelled, cancelled);
    }

    /// <summary>One game under <see cref="_write"/>; one whose images can't be written now keeps what it shows.</summary>
    private async Task UpdateGameLockedAsync(Game game, IArtworkLookup lookup, RunState run, ChangeBatcher changes, CancellationToken cancel)
    {
        await _write.WaitAsync(cancel);
        try
        {
            await UpdateGameAsync(game, lookup, run, changes, cancel);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A full disk, a locked file.
            Log($"Artwork: {game.Key} nicht gespeichert: {e.GetType().Name}: {e.Message}");
        }
        finally
        {
            _write.Release();
        }
    }

    private KnownGames Known => _known ??= new KnownGames(Path.Combine(_cache.Root, KnownGames.FileName)) { Log = Log };

    private static string NameOf(RunTrigger trigger) => trigger switch
    {
        RunTrigger.FirstKey => "erster Schlüssel", // not a UI text (logged)
        RunTrigger.Player => "Jetzt suchen", // not a UI text (logged)
        RunTrigger.Reload => "Neu laden", // not a UI text (logged)
        _ => "neue Spiele", // not a UI text (logged)
    };

    /// <summary>
    /// A game is looked at when its launcher has images, SteamGridDB may be asked, or the cache holds images of it (a source
    /// switched off may have to be put aside); so a run without a key doesn't read the cache for every game of a launcher
    /// without images of its own (#491).
    /// </summary>
    private bool IsVisited(ILauncherArtwork? source, string key, RunState run) => source is not null || run.AsksSteamGridDb || _records.ContainsKey(key);

    private static readonly IArtworkLookup NoImages = new FixedCandidates([]);

    /// <summary>The demo games: no data source searches for them, so they count as neither searched nor missing.</summary>
    private static HashSet<string> MadeUpGames(IReadOnlyList<LibraryArtwork> libraries) =>
        libraries.Where(l => l.MadeUp).SelectMany(l => l.Library.Games).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);

    private bool IsMissingAny(string key) => Of(key) is not { Cover: not null, Hero: not null };

    private static IReadOnlySet<ArtworkSource> OffSources(ArtworkSettings settings) =>
        settings.Off.Select(source => source == ArtworkSourceKind.SteamGridDb ? ArtworkSource.SteamGridDb : ArtworkSource.Launcher).ToHashSet();

    /// <summary>The progress of a run that searches; while it goes, at most every <see cref="ChangeBatch"/>.</summary>
    private void ShowSearch(RunState run, ArtworkRun running, bool force)
    {
        if (!run.ShowsProgress)
        {
            return;
        }

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

    /// <summary>
    /// The counts after the run, over every game of the libraries; a run the player started keeps its result, a cancelled
    /// one shows the counts.
    /// </summary>
    private void EndSearch(IReadOnlyList<LibraryArtwork> libraries, RunState run, bool byPlayer, bool cancelled)
    {
        var games = libraries.Where(l => !l.MadeUp).SelectMany(l => l.Library.Games).Select(g => g.Key).Distinct(StringComparer.Ordinal).ToList();
        var missingCovers = games.Count(key => Of(key)?.Cover is null);
        var missingHeroes = games.Count(key => Of(key)?.Hero is null);
        lock (_gate)
        {
            _search = new ArtworkSearch(
                missingCovers,
                missingHeroes,
                null,
                byPlayer ? new ArtworkSearchResult(run.FoundCovers, run.FoundHeroes, run.WithSteamGridDb, run.SteamGridDbStop) : cancelled ? null : _search.Result);
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
        try
        {
            foreach (var kind in Kinds)
            {
                // A source switched off during the run isn't asked from here on.
                run.Follow(Settings().WithValidValues());
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

                var off = artwork.OffOf(kind);
                var offSize = off is null ? null : CheckedOffSize(key, kind, off);
                if (off is not null && offSize is null)
                {
                    artwork = artwork.WithOff(kind, null);
                    off = null;
                }

                // Its source is switched off (#506): the slot is free for the pick. The image stays in use until the pick
                // is known, so an image kept for a source switched on swaps with it instead of being lost.
                var switchedOff = image is not null && !run.IsOn(image.Source) ? image : null;
                var pickedSize = switchedOff is null ? size : null;
                var pick = run.Reload
                    ? await PickAsync(game, kind, lookup, null, null, null, null, run, cancel) ?? (pickedSize is null ? null : new Pick.Keep())
                    : await PickAsync(game, kind, lookup, switchedOff is null ? image : null, pickedSize, off, offSize, run, cancel);
                written |= pick is Pick.Take or Pick.SetOn;
                artwork = Apply(artwork, kind, pick, run);
                if (switchedOff is not null && artwork.Of(kind) == switchedOff)
                {
                    // Nothing took its place: kept aside for when its source is switched on.
                    artwork = SetOff(artwork, kind, switchedOff, size!.Value);
                    written = true;
                }
            }
        }
        finally
        {
            // Also when the search is cancelled between the cover and the background: the metadata names what is on disk.
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
    }

    /// <summary>An automatic image of another source than <paramref name="next"/>'s: kept aside when it gives way (#506).</summary>
    private static bool IsKeptAside(CachedImage image, ArtworkSource next) => !image.SetByHand && image.Source != ArtworkSource.File && image.Source != next;

    /// <summary>Puts the image in use aside (its source switched off, or it gave way); an image kept before for the kind goes.</summary>
    private CachedArtwork SetOff(CachedArtwork artwork, ArtworkKind kind, CachedImage image, ImageSize size)
    {
        var kept = _cache.SetOff(artwork.GameKey, kind, image);
        _checked.Remove((artwork.GameKey, kind));
        _checkedOff[(artwork.GameKey, kind)] = size;
        Log($"Artwork: {Name(kind)} von {artwork.GameKey} zurückgelegt");
        return artwork.With(kind, null).WithOff(kind, kept);
    }

    /// <summary>The size of an image kept for a source switched off; checked once per session.</summary>
    private ImageSize? CheckedOffSize(string key, ArtworkKind kind, CachedImage image)
    {
        if (_checkedOff.TryGetValue((key, kind), out var size))
        {
            return size;
        }

        if (ImageCheck.OfFile(_cache.PathOf(key, image)) is not { } found)
        {
            return null;
        }

        _checkedOff[(key, kind)] = found;
        return found;
    }

    private static readonly ArtworkKind[] Kinds = [ArtworkKind.Cover, ArtworkKind.Hero];

    private CachedArtwork Apply(CachedArtwork artwork, ArtworkKind kind, Pick? pick, RunState run)
    {
        switch (pick)
        {
            case Pick.Keep:
                return artwork;
            case Pick.SetOn on:
            {
                // Back in use without a copy or download (#506); an automatic image of another source in use is kept aside.
                var key = artwork.GameKey;
                var inUse = artwork.Of(kind);
                var inUseSize = _checked.TryGetValue((key, kind), out var held) ? held : (ImageSize?)null;
                _checkedOff.Remove((key, kind));
                _checked[(key, kind)] = on.Size;
                _versions[(key, kind)] = _versions.GetValueOrDefault((key, kind)) + 1;
                if (inUse is not null && IsKeptAside(inUse, on.Off.Source) && inUseSize is { } size)
                {
                    var (restored, kept) = _cache.Swap(key, kind, inUse, on.Off);
                    _checkedOff[(key, kind)] = size;
                    return artwork.With(kind, restored).WithOff(kind, kept);
                }

                return artwork.With(kind, _cache.SetOn(key, kind, on.Off)).WithOff(kind, null);
            }

            case Pick.Take take:
                if (artwork.OffOf(kind) is { } stale && stale.Source == take.Source)
                {
                    // The source's newer image replaces the one kept for it.
                    _cache.DeleteOffImage(artwork.GameKey, kind);
                    _checkedOff.Remove((artwork.GameKey, kind));
                    artwork = artwork.WithOff(kind, null);
                }

                if (artwork.Of(kind) is { } replaced && IsKeptAside(replaced, take.Source) && _checked.TryGetValue((artwork.GameKey, kind), out var replacedSize))
                {
                    // Another source's image gives way (a changed order): kept, so changing back needs no download.
                    artwork = SetOff(artwork, kind, replaced, replacedSize);
                }

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
        PickAsync(game, kind, new FixedCandidates(candidates), cached, cachedSize, null, null, run with { Order = [ArtworkSourceKind.Launcher] }, cancel);

    /// <summary>
    /// The image to show: the sources switched on in their order, the first candidate that fits, else the first image at
    /// all; the cached image wherever it stands for a candidate (same original) or would be downloaded again, and the image
    /// kept for a source switched off (<paramref name="off"/>, #506) the same way once its source is on again.
    /// </summary>
    private async Task<Pick?> PickAsync(
        Game game, ArtworkKind kind, IArtworkLookup lookup, CachedImage? cached, ImageSize? cachedSize, CachedImage? off, ImageSize? offSize,
        RunState run, CancellationToken cancel)
    {
        Pick? fallback = null;
        foreach (var source in run.Order)
        {
            if (source == ArtworkSourceKind.SteamGridDb)
            {
                // SteamGridDB isn't asked again for an image it gave, so its place in the order is held by the one on disk;
                // where it isn't asked for the game now, that one stands as its image also when it no longer fits.
                var held = cached?.Origin == SteamGridDbOrigin && cachedSize is { } inUse ? (new Pick.Keep(), inUse)
                    : off?.Origin == SteamGridDbOrigin && offSize is { } kept ? (new Pick.SetOn(off, kept), kept)
                    : ((Pick, ImageSize)?)null;
                if (held is var (option, size))
                {
                    if (ArtworkFormat.Fits(size, kind))
                    {
                        return option;
                    }

                    if (!run.AsksSteamGridDbFor(game.Key))
                    {
                        fallback ??= option;
                        continue;
                    }
                }
            }

            var (candidates, from) = source == ArtworkSourceKind.SteamGridDb
                ? (await SteamGridDbCandidatesAsync(game, kind, lookup, cachedSize, run, cancel), ArtworkSource.SteamGridDb)
                : (lookup.For(game, kind), ArtworkSource.Launcher);
            var replaced = IsReplaced(cached, candidates);
            foreach (var candidate in candidates)
            {
                if (off is not null && offSize is { } offFits && off.Source == from && IsSameOriginal(off, candidate))
                {
                    if (ArtworkFormat.Fits(offFits, kind))
                    {
                        return new Pick.SetOn(off, offFits);
                    }

                    fallback ??= new Pick.SetOn(off, offFits);
                    continue;
                }

                if (await OptionOfAsync(game, kind, candidate, from, replaced, cached, cachedSize, run, cancel) is not var (option, size))
                {
                    continue;
                }

                if (ArtworkFormat.Fits(size, kind))
                {
                    return option;
                }

                fallback ??= option;
            }
        }

        return fallback ?? (cachedSize is not null ? new Pick.Keep() : null);
    }

    /// <summary>The candidate is the original <paramref name="image"/> was copied or downloaded from, unchanged.</summary>
    private static bool IsSameOriginal(CachedImage image, ArtworkCandidate candidate) =>
        image.Origin == candidate.Origin
        && (candidate.File is { } path ? LocalStamp(path) == image.Stamp : candidate.Download?.AbsoluteUri == image.Stamp);

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

        if (cached is not null && cachedSize is { } held && (cached.Stamp == url.AbsoluteUri || (ArtworkFormat.Fits(held, kind) && !replaced)))
        {
            // Nothing earlier fits and the cache holds an image the launcher didn't replace: no download for what is there already.
            return (new Pick.Keep(), held);
        }

        // A scan goes online only for new games (#506, Q7): the launchers' images to download wait for a search too.
        if (run.OfflineHosts.Contains(url.IdnHost) || run.Failed.Contains(url) || !run.GoesOnlineFor(game.Key))
        {
            return null;
        }

        if (await DownloadAsync(game, kind, url, run, cancel) is { } downloadedContent && ImageCheck.Of(new MemoryStream(downloadedContent)) is { } downloaded)
        {
            return (new Pick.Take(candidate, from, downloadedContent, downloaded, url.AbsoluteUri), downloaded);
        }

        // SteamGridDB answered but its image didn't come: asked again at the next "Jetzt suchen" (#506), not on its own.
        run.Failed.Add(url);
        return null;
    }

    /// <summary>
    /// SteamGridDB's image for the game (#489): its best rated in 16 : 9 for a background (#562), else
    /// its best rated that fits by the size SteamGridDB names, else its best rated at all, so only one is downloaded. Asked by the store id, else by the game it matched by name (#491,
    /// ArtworkService.ByName.cs). None without a usable key, with SteamGridDB switched off, while the cache holds a fitting
    /// image, for a game this run doesn't search (#506), or once SteamGridDB failed in this run.
    /// </summary>
    private async Task<IReadOnlyList<ArtworkCandidate>> SteamGridDbCandidatesAsync(
        Game game, ArtworkKind kind, IArtworkLookup lookup, ImageSize? cachedSize, RunState run, CancellationToken cancel)
    {
        if (SteamGridDb is null || !run.AsksSteamGridDbFor(game.Key)
            || cachedSize is { } held && ArtworkFormat.Fits(held, kind))
        {
            return [];
        }

        if (await SteamGridDbImagesAsync(game, kind, lookup, run, cancel) is not { } answer)
        {
            return [];
        }

        var best = answer.Images.MinBy(i => ArtworkFormat.RankOf(new ImageSize(i.Width, i.Height), kind));
        return best is null ? [] : [new ArtworkCandidate(SteamGridDbOrigin, Download: best.Url)];
    }

    /// <summary>
    /// What SteamGridDB answered for the game's images (found, or not found by anything Arcadia knows of it); null while it
    /// can't say yet (waiting for the player's choice, skipped, no hit this run) or after it failed: not asked again then.
    /// </summary>
    private async Task<SteamGridDbAnswer?> SteamGridDbImagesAsync(Game game, ArtworkKind kind, IArtworkLookup lookup, RunState run, CancellationToken cancel)
    {
        var steamGridDb = SteamGridDb!;
        if (!_unknownById.ContainsKey(game.Key) && lookup.StoreIdOf(game) is { } storeId)
        {
            var byId = await steamGridDb.ImagesAsync(storeId, kind, cancel);
            run.SteamGridDbAsked++;
            if (StopsSteamGridDb(byId.Outcome, run))
            {
                return null;
            }

            if (byId.Outcome == SteamGridDbOutcome.Found)
            {
                return byId;
            }

            // It doesn't know the game by its launcher's id: from now on it is a game found by name (D2).
            _unknownById[game.Key] = true;
        }

        if (await MatchedGameAsync(game, kind, run, cancel) is not { } gameId)
        {
            return null;
        }

        var byName = await ImagesOfMatchAsync(gameId, kind, cancel);
        run.SteamGridDbAsked++;
        return StopsSteamGridDb(byName.Outcome, run) ? null : byName;
    }

    /// <summary>A key turned down or no connection: not one request per game, the rest of the run does without SteamGridDB.</summary>
    private static bool StopsSteamGridDb(SteamGridDbOutcome outcome, RunState run)
    {
        if (outcome is not (SteamGridDbOutcome.KeyRejected or SteamGridDbOutcome.NoConnection))
        {
            return false;
        }

        run.SteamGridDbUsable = false;
        run.SteamGridDbStop = outcome;
        return true;
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
    /// or the connection fails (the run then downloads nothing more from that host).
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
            if (GoOffline(run, url))
            {
                Log($"Artwork: {Name(kind)} von {game.Key}: {url.Host} antwortet nicht in {DownloadTimeout.TotalSeconds:0} s");
            }

            return null;
        }
        catch (HttpRequestException e)
        {
            if (GoOffline(run, url))
            {
                Log($"Artwork: {Name(kind)} von {game.Key}: keine Verbindung zu {url.Host} ({e.HttpRequestError})");
            }

            return null;
        }
        catch (IOException e)
        {
            // The connection broke off during the answer.
            if (GoOffline(run, url))
            {
                Log($"Artwork: {Name(kind)} von {game.Key}: Verbindung zu {url.Host} abgebrochen ({e.GetType().Name})");
            }

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

    /// <summary>
    /// A failed connection is tried again at the next search (#506); this run downloads nothing more from that host, so a host that
    /// hangs costs one timeout, not one per game, and keeps no other host's images away. True the first time per host: its
    /// line is written whatever the game logged before (e.g. its other image missing), so the log names the host that failed.
    /// </summary>
    private static bool GoOffline(RunState run, Uri url) => run.OfflineHosts.Add(url.IdnHost);

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

    private void Show(CachedArtwork artwork, ChangeBatcher changes)
    {
        ArtworkFile? FileOf(ArtworkKind kind) =>
            artwork.Of(kind) is { } image && CheckedSize(artwork.GameKey, kind, image) is { } size
                ? new ArtworkFile(artwork.GameKey, kind, _cache.PathOf(artwork.GameKey, image), size, _versions.GetValueOrDefault((artwork.GameKey, kind)))
                : null;

        ArtworkSource? AutomaticFrom(CachedImage? image) => image is { SetByHand: false } ? image.Source : null;

        var shown = new GameArtwork(FileOf(ArtworkKind.Cover), FileOf(ArtworkKind.Hero));
        if (shown is { Cover: null, Hero: null })
        {
            _shown.TryRemove(artwork.GameKey, out _);
            _shownFrom.TryRemove(artwork.GameKey, out _);
        }
        else
        {
            _shownFrom[artwork.GameKey] = (AutomaticFrom(artwork.Cover), AutomaticFrom(artwork.Hero));
            _shown[artwork.GameKey] = shown;
        }

        changes.Add(artwork.GameKey);
    }

    private static string Name(ArtworkKind kind) => kind == ArtworkKind.Cover ? "Cover" : "Hintergrund";

    private abstract record Pick
    {
        public sealed record Keep : Pick;

        public sealed record Take(ArtworkCandidate Candidate, ArtworkSource Source, byte[] Content, ImageSize Size, string Stamp) : Pick;

        /// <summary>The image kept for its source while it was off is in use again.</summary>
        public sealed record SetOn(CachedImage Off, ImageSize Size) : Pick;
    }

    /// <summary>Candidates already known, as a lookup.</summary>
    private sealed class FixedCandidates(IReadOnlyList<ArtworkCandidate> candidates) : IArtworkLookup
    {
        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind) => candidates;
    }

    /// <summary>
    /// What started a run (#506), weakest first: a queued run takes the strongest of those that wait. A scan asks SteamGridDB
    /// only for new games; the others for every game missing an image ("Alle Artworks neu laden": every game).
    /// </summary>
    private enum RunTrigger
    {
        Rescan,
        FirstKey,
        Player,
        Reload,
    }

    /// <summary>The libraries a run goes over and what started it; <see cref="AskAlso"/>: games SteamGridDB is asked for in a scan.</summary>
    private sealed record PendingRun(IReadOnlyList<LibraryArtwork> Libraries, RunTrigger Trigger, bool Overwrite = false)
    {
        public IReadOnlySet<string> AskAlso { get; init; } = new HashSet<string>();
    }

    /// <summary>A run's state with the order and switches as set now (#489, #506).</summary>
    private RunState NewRunState(bool steamGridDbUsable = false)
    {
        var settings = Settings().WithValidValues();
        var run = new RunState
        {
            SeveralHits = settings.SeveralHits,
            SteamGridDbUsable = steamGridDbUsable,
        };
        run.Follow(settings);
        run.WithSteamGridDb = run.AsksSteamGridDb;
        return run;
    }

    private sealed record RunState
    {
        /// <summary>Every image is taken from its source again, also one already cached.</summary>
        public bool Reload { get; init; }

        /// <summary>Images set by hand give way too.</summary>
        public bool Overwrite { get; init; }

        /// <summary>The sources switched on, in their order; followed during the run (<see cref="Follow"/>).</summary>
        public IReadOnlyList<ArtworkSourceKind> Order { get; set; } = ArtworkSettings.DefaultOrder;

        /// <summary>"Bei mehreren Treffern" (#491).</summary>
        public SeveralHits SeveralHits { get; init; }

        /// <summary>The games the run may go online for (SteamGridDB, a launcher's download); null: every game (#506).</summary>
        public IReadOnlySet<string>? Asks { get; set; }

        /// <summary>A run that searches shows its progress; a scan without new games doesn't (#506).</summary>
        public bool ShowsProgress { get; set; }

        /// <summary>SteamGridDB may be asked at all: a usable key, switched on, and it hasn't failed in this run.</summary>
        public bool AsksSteamGridDb => SteamGridDbUsable && Order.Contains(ArtworkSourceKind.SteamGridDb);

        /// <summary>The demo games, which SteamGridDB is never asked for.</summary>
        public IReadOnlySet<string> MadeUp { get; init; } = new HashSet<string>();

        public bool AsksSteamGridDbFor(string gameKey) => AsksSteamGridDb && GoesOnlineFor(gameKey) && !MadeUp.Contains(gameKey);

        /// <summary>Any request to the network for the game, a launcher's image to download included (#506).</summary>
        public bool GoesOnlineFor(string gameKey) => Asks is null || Asks.Contains(gameKey);

        /// <summary>The automatic images of <paramref name="source"/> may be in use; an own file always.</summary>
        public bool IsOn(ArtworkSource source) => source switch
        {
            ArtworkSource.Launcher => Order.Contains(ArtworkSourceKind.Launcher),
            ArtworkSource.SteamGridDb => Order.Contains(ArtworkSourceKind.SteamGridDb),
            _ => true,
        };

        /// <summary>The settings as they are now: a source switched off during the run isn't asked from the next game on.</summary>
        public void Follow(ArtworkSettings settings)
        {
            Order = settings.OnInOrder;
        }

        /// <summary>Games searched by name in this run that SteamGridDB found nothing for: searched again at the next search (#506).</summary>
        public HashSet<string> NoHits { get; } = new(StringComparer.Ordinal);

        /// <summary>A usable key at the start, and SteamGridDB hasn't failed in this run.</summary>
        public bool SteamGridDbUsable { get; set; }

        /// <summary>Why SteamGridDB was left out for the rest of the run; null while it wasn't.</summary>
        public SteamGridDbOutcome? SteamGridDbStop { get; set; }

        public bool WithSteamGridDb { get; set; }

        public int SteamGridDbAsked { get; set; }

        public int FoundCovers { get; set; }

        public int FoundHeroes { get; set; }

        /// <summary>Games done of those that missed an image at the start.</summary>
        public int Searched { get; set; }

        public HashSet<string> Logged { get; } = new(StringComparer.Ordinal);

        // An address that gave no image is not asked again in this run, but by the next: a rescan may find what the server
        // added meanwhile. One that gave an image is asked again for another game naming it (Epic's editions, #488).
        public HashSet<Uri> Failed { get; } = [];

        /// <summary>Hosts a download couldn't reach in this run: asked again at the next search (#506).</summary>
        public HashSet<string> OfflineHosts { get; } = new(StringComparer.OrdinalIgnoreCase);

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
