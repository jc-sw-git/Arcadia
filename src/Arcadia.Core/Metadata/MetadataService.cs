using System.Collections.Concurrent;
using System.Net;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Metadata;

/// <summary>
/// The games' metadata (#43, #493). <see cref="Open"/> reads the cache in the background, so what was loaded once shows at
/// once and offline. After every scan (<see cref="Update"/>) one background run reads the launchers' local files and then
/// asks each launcher's store for the games it hasn't answered for yet, one request at a time within the store's limit,
/// 10 s each, waiting on HTTP 429. No network, a timeout or a server error skip the game silently until the next scan.
/// "Alle Metadaten neu laden" (<see cref="ReloadAll"/>) asks again for every game; the old data stays until replaced.
/// </summary>
public sealed class MetadataService : IDisposable
{
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The largest answer read; Steam's pages are a few hundred KB at most.</summary>
    public const int MaxAnswer = 4 * 1024 * 1024;

    /// <summary>The wait after HTTP 429 without a usable <c>Retry-After</c>.</summary>
    public static readonly TimeSpan ThrottledWait = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan LongestThrottledWait = TimeSpan.FromMinutes(15);

    private readonly MetadataCache _cache;
    private readonly HttpClient _http;
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly object _gate = new();

    // What the views show, readable from any thread.
    private readonly ConcurrentDictionary<string, MergedMetadata> _shown = new(StringComparer.Ordinal);

    // The run's own state: only the run touches it.
    private readonly Dictionary<string, CachedMetadata> _records = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reloadLeft = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _nextRequest = new(StringComparer.Ordinal);
    private bool _indexed;
    private bool _cacheWriteLogged;

    private Pending? _pending;
    private IReadOnlyList<LibraryMetadata>? _lastLibraries;
    private Task _runner = Task.CompletedTask;
    private MetadataProgress _progress = MetadataProgress.NotYet;

    public MetadataService(MetadataCache cache, HttpClient http, TimeProvider time)
    {
        _cache = cache;
        _http = http;
        _time = time;
    }

    public Action<string> Log { get; init; } = _ => { };

    /// <summary>The metadata of these games changed (any thread).</summary>
    public event EventHandler<IReadOnlyCollection<string>>? Changed;

    /// <summary><see cref="Progress"/> changed (any thread).</summary>
    public event EventHandler? ProgressChanged;

    /// <summary>"Alle Metadaten neu laden" reached the store for no game at all: no network (any thread).</summary>
    public event EventHandler? ReloadFailed;

    /// <summary>An unexpected error ended a run; the next scan starts a new one (any thread).</summary>
    public event EventHandler<Exception>? Failed;

    public MetadataProgress Progress
    {
        get
        {
            lock (_gate)
            {
                return _progress;
            }
        }
    }

    /// <summary>The games of the last libraries a store is asked for, e.g. for "Alle Metadaten neu laden?"; 0 before the first scan.</summary>
    public int GameCount
    {
        get
        {
            lock (_gate)
            {
                return _lastLibraries?.Where(l => l.Source?.Store is not null).Sum(l => l.Library.Games.Count) ?? 0;
            }
        }
    }

    /// <summary>What the detail page shows of the game; <see cref="MergedMetadata.None"/> while nothing is known.</summary>
    public MergedMetadata Of(string gameKey) => _shown.GetValueOrDefault(gameKey) ?? MergedMetadata.None;

    /// <summary>Reads the cache in the background; nothing is fetched before the first scan.</summary>
    public void Open() => Start(null);

    /// <summary>The libraries were read (again): their local data and, in the background, the store answers still missing.</summary>
    public void Update(IReadOnlyList<LibraryMetadata> libraries) => Start(new Pending(libraries, Reload: false));

    /// <summary>"Alle Metadaten neu laden": asks the store again for every game; own titles and tags are no metadata and stay.</summary>
    /// <returns>False before the first scan or while a reload runs: no second run starts.</returns>
    public bool ReloadAll()
    {
        IReadOnlyList<LibraryMetadata>? libraries;
        lock (_gate)
        {
            libraries = _lastLibraries;
            if (libraries is null || _progress.Reload || _pending is { Reload: true })
            {
                return false;
            }
        }

        Log("Metadaten: alle neu laden");
        Start(new Pending(libraries, Reload: true));
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

    public void Dispose() => _shutdown.Cancel();

    private void Start(Pending? run)
    {
        lock (_gate)
        {
            if (run is not null)
            {
                _pending = _pending is { } queued ? run with { Reload = run.Reload || queued.Reload } : run;
                _lastLibraries = run.Libraries;
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
            if (!_indexed)
            {
                ReadCache();
                _indexed = true;
            }

            while (TakePending() is { } pending)
            {
                if (pending.Reload)
                {
                    foreach (var game in StoreGames(pending.Libraries))
                    {
                        _reloadLeft.Add(game.Game.Key);
                    }
                }

                ReadLocal(pending.Libraries);
                await FetchAsync(pending.Libraries, pending.Reload, _shutdown.Token);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Arcadia closes: what was fetched is in the cache, the rest comes with the next scan.
        }
        catch (Exception e)
        {
            Log($"Metadaten: Lauf abgebrochen: {e}");
            Failed?.Invoke(this, e);
        }
        finally
        {
            SetProgress(running: false, reload: false, remaining: null);
            lock (_gate)
            {
                // A run queued while this one ended still needs a runner.
                if (_pending is not null && !_shutdown.IsCancellationRequested)
                {
                    _runner = Task.Run(RunAsync);
                }
            }
        }
    }

    private Pending? TakePending()
    {
        lock (_gate)
        {
            var pending = _pending;
            _pending = null;
            return pending;
        }
    }

    private bool HasPending()
    {
        lock (_gate)
        {
            return _pending is not null;
        }
    }

    private void ReadCache()
    {
        var changed = new List<string>();
        foreach (var cached in _cache.ReadAll(Log))
        {
            _records[cached.GameKey] = cached;
            Show(cached, changed);
        }

        Changed?.Invoke(this, changed);
    }

    /// <summary>The launchers' local files, one look per launcher; a launcher that fails costs only its own data.</summary>
    private void ReadLocal(IReadOnlyList<LibraryMetadata> libraries)
    {
        var changed = new List<string>();
        foreach (var (library, source) in libraries)
        {
            if (source is null || library.Games.Count == 0)
            {
                continue;
            }

            IMetadataLookup lookup;
            try
            {
                lookup = source.Look(library.Games);
            }
            catch (Exception e)
            {
                Log($"Metadaten: lokale Daten von {library.Launcher.Id} nicht lesbar: {e}");
                continue;
            }

            foreach (var game in library.Games)
            {
                var local = Local(lookup, game);
                var known = _records.GetValueOrDefault(game.Key);
                if (known is not null && Same(known.Local, local) && known.Source == library.Launcher.DisplayName)
                {
                    continue;
                }

                if (known is null && local is null)
                {
                    continue;
                }

                var updated = (known ?? new CachedMetadata(game.Key, library.Launcher.DisplayName, null, null, null)) with
                {
                    Source = library.Launcher.DisplayName,
                    Local = local,
                };
                Keep(updated);
                Show(updated, changed);
            }
        }

        if (changed.Count > 0)
        {
            Changed?.Invoke(this, changed);
        }
    }

    private GameMetadata? Local(IMetadataLookup lookup, Game game)
    {
        try
        {
            return lookup.Of(game) is { } found ? MetadataCheck.Checked(found) : null;
        }
        catch (Exception e)
        {
            Log($"Metadaten: lokale Daten von {game.Key} nicht lesbar: {e.GetType().Name}: {e.Message}");
            return null;
        }
    }

    private static IEnumerable<(Game Game, IMetadataStore Store, string Source)> StoreGames(IReadOnlyList<LibraryMetadata> libraries) =>
        libraries
            .Where(l => l.Source?.Store is not null)
            .SelectMany(l => l.Library.Games.Select(g => (g, l.Source!.Store!, l.Library.Launcher.DisplayName)))
            .Where(g => CheckedAddress(SafeAddressOf(g.Item2, g.g)) is not null)
            .DistinctBy(g => g.g.Key, StringComparer.Ordinal);

    private bool IsMissing(Game game) => _reloadLeft.Contains(game.Key) || _records.GetValueOrDefault(game.Key)?.Store is null;

    /// <summary>
    /// Asks the stores for every game still missing an answer, in library order. Stops for a newer scan (which then asks
    /// for the rest, none twice) and at the first failed connection (no network: the next scan tries again).
    /// </summary>
    private async Task FetchAsync(IReadOnlyList<LibraryMetadata> libraries, bool reload, CancellationToken cancel)
    {
        var all = StoreGames(libraries).ToList();
        var queue = all.Where(g => IsMissing(g.Game)).ToList();
        var run = new FetchRun(all.Count, queue.Count, _time.GetUtcNow(), reload || _reloadLeft.Count > 0);
        Report(run, all);
        if (queue.Count == 0)
        {
            return;
        }

        foreach (var (game, store, source) in queue)
        {
            if (HasPending())
            {
                break;
            }

            var outcome = await FetchGameAsync(game, store, source, run, cancel);
            run.Count(outcome);
            if (outcome != Outcome.Throttled)
            {
                _reloadLeft.Remove(game.Key);
            }

            Report(run, all);
            if (outcome == Outcome.Offline)
            {
                break;
            }
        }

        run.LogSummary(Log);
        if (reload && run.Answered == 0 && run.Offline)
        {
            ReloadFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<Outcome> FetchGameAsync(Game game, IMetadataStore store, string source, FetchRun run, CancellationToken cancel)
    {
        if (CheckedAddress(SafeAddressOf(store, game)) is not { } address)
        {
            _reloadLeft.Remove(game.Key);
            return Outcome.NoAddress;
        }

        while (true)
        {
            await PaceAsync(game.LauncherId, store.Limit, cancel);
            var (outcome, answer, retryAfter) = await RequestAsync(address, cancel);
            if (outcome == Outcome.Throttled)
            {
                run.Throttled++;
                await Task.Delay(retryAfter ?? ThrottledWait, _time, cancel);
                continue;
            }

            if (outcome != Outcome.Answered)
            {
                return outcome;
            }

            GameMetadata? parsed;
            try
            {
                parsed = store.Parse(game, answer);
            }
            catch (Exception e)
            {
                Log($"Metadaten: Antwort für {game.Key} nicht lesbar: {e.GetType().Name}");
                parsed = null;
            }

            if (parsed is null)
            {
                return Outcome.Unusable;
            }

            var known = _records.GetValueOrDefault(game.Key) ?? new CachedMetadata(game.Key, source, null, null, null);
            var updated = known with { Source = source, Store = MetadataCheck.Checked(parsed), StoreAt = _time.GetUtcNow() };
            Keep(updated);
            var changed = new List<string>();
            Show(updated, changed);
            if (changed.Count > 0)
            {
                Changed?.Invoke(this, changed);
            }

            return Outcome.Answered;
        }
    }

    private static Uri? SafeAddressOf(IMetadataStore store, Game game)
    {
        try
        {
            return store.AddressOf(game);
        }
        catch (Exception e) when (e is ArgumentException or FormatException or UriFormatException)
        {
            return null;
        }
    }

    /// <summary>Only an <c>https</c> address without user name, password or another port goes out.</summary>
    private static Uri? CheckedAddress(Uri? address) =>
        address is { IsAbsoluteUri: true } && address.Scheme == Uri.UriSchemeHttps && address.UserInfo.Length == 0 && address.IsDefaultPort
            ? address
            : null;

    /// <summary>Waits until the store's gap since its last request has passed; the gap holds across runs.</summary>
    private async Task PaceAsync(string launcherId, RequestLimit limit, CancellationToken cancel)
    {
        var now = _time.GetUtcNow();
        if (_nextRequest.TryGetValue(launcherId, out var next) && next > now)
        {
            await Task.Delay(next - now, _time, cancel);
            now = _time.GetUtcNow();
        }

        _nextRequest[launcherId] = now + limit.Gap;
    }

    private async Task<(Outcome Outcome, byte[] Answer, TimeSpan? RetryAfter)> RequestAsync(Uri address, CancellationToken cancel)
    {
        using var timeout = new CancellationTokenSource(RequestTimeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancel, timeout.Token);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return (Outcome.Throttled, [], RetryAfterOf(response));
            }

            if (!response.IsSuccessStatusCode)
            {
                return (Outcome.Failed, [], null);
            }

            if (response.Content.Headers.ContentLength > MaxAnswer)
            {
                return (Outcome.Unusable, [], null);
            }

            await using var body = await response.Content.ReadAsStreamAsync(linked.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await body.ReadAsync(chunk, linked.Token)) > 0)
            {
                if (buffer.Length + read > MaxAnswer)
                {
                    return (Outcome.Unusable, [], null);
                }

                buffer.Write(chunk, 0, read);
            }

            return (Outcome.Answered, buffer.ToArray(), null);
        }
        catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
        {
            return (Outcome.Failed, [], null);
        }
        catch (HttpRequestException e) when (e.StatusCode is null)
        {
            return (Outcome.Offline, [], null);
        }
        catch (HttpRequestException)
        {
            return (Outcome.Failed, [], null);
        }
        catch (IOException)
        {
            return (Outcome.Failed, [], null);
        }
    }

    private TimeSpan? RetryAfterOf(HttpResponseMessage response)
    {
        var wait = response.Headers.RetryAfter switch
        {
            { Delta: { } delta } => delta,
            { Date: { } date } => date - _time.GetUtcNow(),
            _ => (TimeSpan?)null,
        };
        return wait is { } found && found > TimeSpan.Zero ? (found < LongestThrottledWait ? found : LongestThrottledWait) : null;
    }

    private void Keep(CachedMetadata cached)
    {
        _records[cached.GameKey] = cached;
        try
        {
            _cache.Write(cached);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Shown anyway; without a cache file the game is asked again after the next start.
            if (!_cacheWriteLogged)
            {
                _cacheWriteLogged = true;
                Log($"Metadaten: Cache nicht schreibbar: {e.GetType().Name}: {e.Message}");
            }
        }
    }

    private void Show(CachedMetadata cached, List<string> changed)
    {
        var merged = MergedMetadata.Of([new ProviderMetadata(cached.Source, cached.Data)]);
        if (_shown.TryGetValue(cached.GameKey, out var shown) && shown == merged)
        {
            return;
        }

        if (merged.IsEmpty)
        {
            _shown.TryRemove(cached.GameKey, out _);
        }
        else
        {
            _shown[cached.GameKey] = merged;
        }

        changed.Add(cached.GameKey);
    }

    private static bool Same(GameMetadata? a, GameMetadata? b) =>
        ReferenceEquals(a, b) || (a is not null && b is not null && a.Description == b.Description && a.ShortDescription == b.ShortDescription
            && a.Genres.SequenceEqual(b.Genres) && a.Developers.SequenceEqual(b.Developers) && a.Publishers.SequenceEqual(b.Publishers)
            && a.ReleaseDate == b.ReleaseDate && a.Tags.SequenceEqual(b.Tags) && a.AgeRatings.SequenceEqual(b.AgeRatings));

    private void Report(FetchRun run, IReadOnlyList<(Game Game, IMetadataStore Store, string Source)> all)
    {
        var missing = all.Count(g => IsMissing(g.Game));
        lock (_gate)
        {
            _progress = new MetadataProgress(missing, all.Count, missing > 0 && run.Left > 0, run.Reload && run.Left > 0, missing > 0 && run.Left > 0 ? run.Remaining(_time.GetUtcNow(), all.FirstOrDefault().Store?.Limit, missing) : null);
        }

        ProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetProgress(bool running, bool reload, TimeSpan? remaining)
    {
        lock (_gate)
        {
            _progress = _progress with { Running = running, Reload = reload, Remaining = remaining };
        }

        ProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed record Pending(IReadOnlyList<LibraryMetadata> Libraries, bool Reload);

    private enum Outcome
    {
        Answered,
        Unusable,
        Failed,
        Offline,
        Throttled,
        NoAddress,
    }

    /// <summary>One pass over the queue: its counts for the progress row and one log line at its end.</summary>
    private sealed class FetchRun(int total, int queued, DateTimeOffset started, bool reload)
    {
        public int Total { get; } = total;

        public bool Reload { get; } = reload;

        public int Left { get; private set; } = queued;

        public int Answered { get; private set; }

        public int Skipped { get; private set; }

        public int Throttled { get; set; }

        public bool Offline { get; private set; }

        public void Count(Outcome outcome)
        {
            Left--;
            switch (outcome)
            {
                case Outcome.Answered:
                    Answered++;
                    break;
                case Outcome.Offline:
                    Offline = true;
                    Left = 0;
                    break;
                default:
                    Skipped++;
                    break;
            }
        }

        /// <summary>The longer of the store's gap and the gap seen so far, for every game still missing.</summary>
        public TimeSpan Remaining(DateTimeOffset now, RequestLimit? limit, int missing)
        {
            var gap = limit?.Gap ?? TimeSpan.Zero;
            var done = Answered + Skipped;
            if (done >= 3)
            {
                var seen = (now - started) / done;
                gap = seen > gap ? seen : gap;
            }

            return gap * missing;
        }

        public void LogSummary(Action<string> log)
        {
            if (Answered + Skipped + Throttled == 0 && !Offline)
            {
                return;
            }

            log($"Metadaten: {Answered} geladen, {Skipped} übersprungen{(Throttled > 0 ? $", {Throttled}× gedrosselt (HTTP 429)" : string.Empty)}{(Offline ? ", keine Verbindung: Rest beim nächsten Einlesen" : string.Empty)}");
        }
    }
}
