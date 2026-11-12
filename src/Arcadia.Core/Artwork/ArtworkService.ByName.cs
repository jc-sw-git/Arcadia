using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork;

/// <summary>A game on the waiting list "Warten auf deine Auswahl" (#491) with the games SteamGridDB found by its name.</summary>
public sealed record WaitingGame(Game Game, string LauncherName, IReadOnlyList<SteamGridDbHit> Hits);

/// <summary>
/// The search by name (#491): a game SteamGridDB doesn't know by a launcher's id is searched by its name once; one
/// matching game is taken, several wait for the player ("Selbst auswählen") or the first is taken ("Ersten nehmen").
/// The match is kept per game across restarts (<see cref="NameMatchStore"/>): a chosen game is asked by its SteamGridDB
/// game from then on, a skipped one never again. A chosen game's images are automatic (D5): order, "Zuschneiden
/// erlaubt" and "Alle Artworks neu laden" apply as to any.
/// </summary>
public sealed partial class ArtworkService
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Game, int Hit), ArtworkFile> _hitCovers = new();
    private NameMatchStore? _names;

    private NameMatchStore Names => _names ??= new NameMatchStore(Path.Combine(_cache.Root, NameMatchStore.FileName)) { Log = Log };

    /// <summary>The waiting list changed: a game came on it or left it, or the libraries changed. Raised on the thread that changed it.</summary>
    public event EventHandler? WaitingChanged;

    /// <summary>
    /// The games of the last libraries waiting for the player's choice, in the order they came on the list; empty before
    /// the first scan. Any thread.
    /// </summary>
    public IReadOnlyList<WaitingGame> Waiting
    {
        get
        {
            IReadOnlyList<LibraryArtwork>? libraries;
            lock (_gate)
            {
                libraries = _lastLibraries;
            }

            var matches = Names.All;
            return libraries is null
                ? []
                : libraries
                    .SelectMany(l => l.Library.Games.Select(g => (Game: g, Launcher: l.Library.Launcher.DisplayName)))
                    .DistinctBy(e => e.Game.Key, StringComparer.Ordinal)
                    .Select(e => (e.Game, e.Launcher, Match: matches.GetValueOrDefault(e.Game.Key)))
                    .Where(e => e.Match is { State: NameMatchState.Waiting })
                    .OrderBy(e => e.Match!.Position)
                    .Select(e => new WaitingGame(e.Game, e.Launcher, e.Match!.Hits))
                    .ToList();
        }
    }

    /// <summary>
    /// A on a hit: the game is that SteamGridDB game from now on, leaves the waiting list and gets its images at once, by
    /// order and "Zuschneiden erlaubt" like any automatic image; an image set by hand stays.
    /// </summary>
    public async Task ChooseAsync(Game game, SteamGridDbHit hit, CancellationToken cancel = default)
    {
        Names.Set(game.Key, NameMatch.Chosen(hit.Id));
        Log($"Artwork: {game.Key} ist SteamGridDB-Spiel {hit.Id} (gewählt)");
        WaitingChanged?.Invoke(this, EventArgs.Empty);

        // Off the caller's thread: downloads and decoding never hold the UI.
        await Task.Run(() => UpdateOneAsync(game, cancel), cancel);
    }

    /// <summary>Y: the game leaves the waiting list, keeps what it shows and is never searched by name again.</summary>
    public void Skip(Game game)
    {
        Names.Set(game.Key, NameMatch.Skipped);
        Log($"Artwork: {game.Key} übersprungen, wird nicht mehr nach Namen gesucht");
        WaitingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// "Ersten nehmen" chosen while games wait: each takes its first hit at once, and a run brings their images.
    /// </summary>
    /// <returns>
    /// How many games of the libraries took their first hit, as the row counted them; games waiting outside the libraries
    /// (D7) take theirs too, uncounted.
    /// </returns>
    public int TakeFirstForAllWaiting()
    {
        var waiting = Names.All.Where(e => e.Value.State == NameMatchState.Waiting).ToList();
        if (waiting.Count == 0)
        {
            return 0;
        }

        var shown = Waiting.Count;
        Names.Set(waiting.Select(e => KeyValuePair.Create(e.Key, NameMatch.Chosen(e.Value.Hits[0].Id))));
        Log($"Artwork: {waiting.Count} wartende Spiele nehmen den ersten Treffer");
        WaitingChanged?.Invoke(this, EventArgs.Empty);
        IReadOnlyList<LibraryArtwork>? libraries;
        lock (_gate)
        {
            libraries = _lastLibraries;
        }

        if (libraries is not null)
        {
            // Not new to the library: a scan alone wouldn't ask SteamGridDB for them (#506).
            SearchFor(libraries, waiting.Select(e => e.Key));
        }

        return shown;
    }

    /// <summary>
    /// The cover of a hit for its tile on the waiting list: SteamGridDB's best rated that fits, downloaded into the place
    /// for offered images; null if it has none or it can't be loaded now (the tile keeps its placeholder). Off the UI thread.
    /// </summary>
    public async Task<ArtworkFile?> HitCoverAsync(Game game, SteamGridDbHit hit, CancellationToken cancel = default)
    {
        if (SteamGridDb is not { IsUsable: true } steamGridDb)
        {
            return null;
        }

        // Once per hit and session: going back to a game asks SteamGridDB again for nothing, and writes no new file.
        if (_hitCovers.TryGetValue((game.Key, hit.Id), out var known) && File.Exists(known.Path))
        {
            return known;
        }

        var cover = await DownloadHitCoverAsync(game, hit, steamGridDb, cancel);
        if (cover is not null)
        {
            _hitCovers[(game.Key, hit.Id)] = cover;
        }

        return cover;
    }

    private async Task<ArtworkFile?> DownloadHitCoverAsync(Game game, SteamGridDbHit hit, SteamGridDbKey steamGridDb, CancellationToken cancel)
    {

        var run = NewRunState();
        var answer = await steamGridDb.ImagesOfGameAsync(hit.Id, ArtworkKind.Cover, cancel);
        var best = answer.Images.FirstOrDefault(i => ArtworkFormat.Fits(new ImageSize(i.Width, i.Height), ArtworkKind.Cover, run.Tolerance)) ?? answer.Images.FirstOrDefault();
        if (best is null
            || await DownloadAsync(game, ArtworkKind.Cover, best.Url, run, cancel) is not { } content
            || ImageCheck.Of(new MemoryStream(content)) is not { } size
            || ArtworkCache.ExtensionOf(content) is not { } extension)
        {
            return null;
        }

        try
        {
            return new ArtworkFile(game.Key, ArtworkKind.Cover, _cache.WriteSuggestion(game.Key, ArtworkKind.Cover, content, extension), size, 0);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log($"Artwork: Treffer-Cover für {game.Key} nicht gespeichert: {e.GetType().Name}");
            return null;
        }
    }

    /// <summary>
    /// SteamGridDB's images for "Artwork ändern" (D6): by the store id, else by the game chosen by name; a game waiting or
    /// skipped, or never matched, gets none. No search by name from here. Any thread.
    /// </summary>
    internal async Task<SteamGridDbAnswer> SuggestionImagesAsync(Game game, ArtworkKind kind, CancellationToken cancel)
    {
        var steamGridDb = SteamGridDb!;
        if (!_unknownById.ContainsKey(game.Key) && StoreIdOf(game) is { } storeId)
        {
            var byId = await steamGridDb.ImagesAsync(storeId, kind, cancel);
            if (byId.Outcome != SteamGridDbOutcome.NotFound)
            {
                return byId;
            }

            _unknownById[game.Key] = true;
        }

        return Names.Of(game.Key) is { State: NameMatchState.Chosen, GameId: { } gameId }
            ? await steamGridDb.ImagesOfGameAsync(gameId, kind, cancel)
            : SteamGridDbAnswer.Of(SteamGridDbOutcome.NotFound);
    }

    /// <summary>
    /// The SteamGridDB game the run asks for a game it doesn't know by a launcher's id: the one chosen before, else what a
    /// search by the game's name finds (searched at most once per game and run). Null while it waits, after a skip, with
    /// no hit or once SteamGridDB failed.
    /// </summary>
    private async Task<int?> MatchedGameAsync(Game game, RunState run, CancellationToken cancel)
    {
        switch (Names.Of(game.Key))
        {
            case { State: NameMatchState.Chosen, GameId: { } chosen }:
                return chosen;
            case { State: NameMatchState.Waiting or NameMatchState.Skipped }:
                return null;
        }

        var term = SteamGridDbNames.SearchTermOf(game.Title);
        if (run.NoHits.Contains(game.Key) || term.Length is 0 or > SteamGridDbClient.MaxNameLength)
        {
            return null;
        }

        var search = await SteamGridDb!.SearchAsync(term, cancel);
        run.SteamGridDbAsked++;
        if (StopsSteamGridDb(search.Outcome, run))
        {
            return null;
        }

        switch (SteamGridDbNames.Decide(game.Title, search.Hits))
        {
            case NameSearchResult.One(var hit):
                Names.Set(game.Key, NameMatch.Chosen(hit.Id));
                Log($"Artwork: {game.Key} nach Namen gefunden, SteamGridDB-Spiel {hit.Id}");
                return hit.Id;
            case NameSearchResult.Several(var hits) when run.SeveralHits == SeveralHits.TakeFirst:
                Names.Set(game.Key, NameMatch.Chosen(hits[0].Id));
                Log($"Artwork: {game.Key} nach Namen {hits.Count} Treffer, der erste genommen");
                return hits[0].Id;
            case NameSearchResult.Several(var hits):
                Names.Set(game.Key, NameMatch.Waiting(hits, Names.NextPosition));
                Log($"Artwork: {game.Key} nach Namen {hits.Count} Treffer, wartet auf die Auswahl");
                WaitingChanged?.Invoke(this, EventArgs.Empty);
                return null;
            default:
                run.NoHits.Add(game.Key);
                return null;
        }
    }

    /// <summary>One game's images brought up to date at once, as a run would, e.g. after the player chose its SteamGridDB game.</summary>
    private async Task UpdateOneAsync(Game game, CancellationToken cancel)
    {
        var lookup = _sources.TryGetValue(game.LauncherId, out var source) ? source.Look() : NoImages;
        var changes = new ChangeBatcher(this);
        await _write.WaitAsync(cancel);
        try
        {
            await UpdateGameAsync(game, lookup, NewRunState(SteamGridDb is { IsUsable: true }), changes, cancel);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log($"Artwork: {game.Key} nicht gespeichert: {e.GetType().Name}: {e.Message}");
        }
        finally
        {
            _write.Release();
        }

        changes.Flush();
    }
}
