using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork;

/// <summary>A game on the waiting list "Warten auf deine Auswahl" (#491) with the games SteamGridDB found by its name.</summary>
/// <param name="Match">Its match: the kinds sought and which hits are known to have an image of its tile kind (#506).</param>
public sealed record WaitingGame(Game Game, string LauncherName, IReadOnlyList<SteamGridDbHit> Hits, NameMatch Match);

/// <summary>What asking SteamGridDB for a hit's images said (#506).</summary>
public enum HitCheck
{
    /// <summary>It has an image of the kind asked for.</summary>
    HasImage,

    /// <summary>It has none: the hit is dropped.</summary>
    NoImage,

    /// <summary>No network or no answer: the hit is left out for now and checked again later.</summary>
    NoConnection,

    /// <summary>The key was turned down (or there is no usable one): nothing more is asked with it.</summary>
    KeyRejected,
}

/// <summary>
/// The search by name (#491): a game SteamGridDB doesn't know by a launcher's id is searched by its name once; one
/// matching game is taken, several wait for the player ("Selbst auswählen") or the first is taken ("Ersten nehmen").
/// The match is kept per game across restarts (<see cref="NameMatchStore"/>): a chosen game is asked by its SteamGridDB
/// game from then on, a skipped one never again. A chosen game's images are automatic (D5): order, "Zuschneiden
/// erlaubt" and "Alle Artworks neu laden" apply as to any. Before a hit is offered or taken, SteamGridDB is asked whether it
/// has an image of the kind sought (#506); a hit without one is dropped.
/// </summary>
public sealed partial class ArtworkService
{
    // Per session: what SteamGridDB has of a hit (found or not; a failure is asked again) and the tile images downloaded.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(int Hit, ArtworkKind Kind), SteamGridDbAnswer> _hitImages = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Game, int Hit, ArtworkKind Kind), ArtworkFile> _hitTiles = new();
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
                    .Select(e => new WaitingGame(e.Game, e.Launcher, e.Match!.Hits, e.Match))
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
            Update(libraries);
        }

        return shown;
    }

    /// <summary>
    /// Whether SteamGridDB has an image of <paramref name="kind"/> for a hit (#506), asked once per hit and session (a
    /// failure is asked again next time). Without a usable key nothing is sent. Any thread.
    /// </summary>
    public async Task<HitCheck> CheckHitAsync(SteamGridDbHit hit, ArtworkKind kind, CancellationToken cancel = default) =>
        (await HitImagesAsync(hit, kind, cancel)).Outcome switch
        {
            SteamGridDbOutcome.KeyRejected => HitCheck.KeyRejected,
            SteamGridDbOutcome.NoConnection => HitCheck.NoConnection,
            _ when _hitImages.TryGetValue((hit.Id, kind), out var answer) && answer.Images.Count > 0 => HitCheck.HasImage,
            _ => HitCheck.NoImage,
        };

    /// <summary>
    /// A hit of a waiting game checked on the waiting list (#506): one without an image of the game's tile kind leaves the
    /// game's hits for good; the game stays on the list without hits until it is skipped or an own search finds it (the
    /// player is looking at it). One with an image counts as checked from then on. A failure changes nothing: it is
    /// checked again the next time.
    /// </summary>
    public async Task<HitCheck> CheckWaitingHitAsync(Game game, SteamGridDbHit hit, CancellationToken cancel = default)
    {
        if (Names.Of(game.Key) is not { State: NameMatchState.Waiting } match)
        {
            return HitCheck.NoImage;
        }

        if (match.Checked.Contains(hit.Id))
        {
            return HitCheck.HasImage;
        }

        var check = await CheckHitAsync(hit, match.TileKind, cancel);
        if (check is HitCheck.HasImage or HitCheck.NoImage && Names.Of(game.Key) is { State: NameMatchState.Waiting } now)
        {
            KeepCheck(game, hit, check, now);
        }

        return check;
    }

    private void KeepCheck(Game game, SteamGridDbHit hit, HitCheck check, NameMatch match)
    {
        if (check == HitCheck.HasImage)
        {
            Names.Set(game.Key, match with { Checked = match.Checked.Append(hit.Id).ToHashSet() });
            return;
        }

        Names.Set(game.Key, match with { Hits = [.. match.Hits.Where(h => h.Id != hit.Id)] });
        WaitingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// "Eigener Suchbegriff" (#506): the games SteamGridDB finds by a term the player typed, at most
    /// <see cref="SteamGridDbClient.MaxOwnTermResults"/>, for the waiting list to check and show the first
    /// <see cref="SteamGridDbClient.MaxOwnTermHits"/> with images; nothing is kept. Any thread.
    /// </summary>
    public async Task<SteamGridDbSearch> SearchByTermAsync(string term, CancellationToken cancel = default)
    {
        if (SteamGridDb is not { IsUsable: true } steamGridDb)
        {
            return new SteamGridDbSearch(SteamGridDbOutcome.KeyRejected, []);
        }

        Log("Artwork: eigene Suche nach Namen");
        return await steamGridDb.SearchAsync(SteamGridDbNames.SearchTermOf(term), cancel, SteamGridDbClient.MaxOwnTermResults);
    }

    /// <summary>
    /// The image of a hit for its tile on the waiting list: SteamGridDB's best rated of <paramref name="kind"/> that fits,
    /// downloaded into the place for offered images; null if it has none or it can't be loaded now (the tile keeps its
    /// placeholder). Off the UI thread.
    /// </summary>
    public async Task<ArtworkFile?> HitImageAsync(Game game, SteamGridDbHit hit, ArtworkKind kind, CancellationToken cancel = default)
    {
        if (SteamGridDb is not { IsUsable: true })
        {
            return null;
        }

        // Once per hit and session: going back to a game asks SteamGridDB again for nothing, and writes no new file.
        if (_hitTiles.TryGetValue((game.Key, hit.Id, kind), out var known) && File.Exists(known.Path))
        {
            return known;
        }

        var tile = await DownloadHitImageAsync(game, hit, kind, cancel);
        if (tile is not null)
        {
            _hitTiles[(game.Key, hit.Id, kind)] = tile;
        }

        return tile;
    }

    /// <summary>A hit's images of a kind, from this session's answers or asked now; only an answer that settles it is kept.</summary>
    private async Task<SteamGridDbAnswer> HitImagesAsync(SteamGridDbHit hit, ArtworkKind kind, CancellationToken cancel)
    {
        if (_hitImages.TryGetValue((hit.Id, kind), out var known))
        {
            return known;
        }

        if (SteamGridDb is not { IsUsable: true } steamGridDb)
        {
            return SteamGridDbAnswer.Of(SteamGridDbOutcome.KeyRejected);
        }

        var answer = await steamGridDb.ImagesOfGameAsync(hit.Id, kind, cancel);
        if (answer.Outcome is SteamGridDbOutcome.Found or SteamGridDbOutcome.NotFound)
        {
            _hitImages[(hit.Id, kind)] = answer;
        }

        return answer;
    }

    /// <summary>
    /// The images of the SteamGridDB game a run matched by name: the answer its hit check just got is used once, so the
    /// check costs no second request; asked again otherwise ("Jetzt suchen" asks SteamGridDB anew).
    /// </summary>
    private async Task<SteamGridDbAnswer> ImagesOfMatchAsync(int gameId, ArtworkKind kind, CancellationToken cancel) =>
        _hitImages.TryRemove((gameId, kind), out var checkedNow) && checkedNow.Outcome == SteamGridDbOutcome.Found
            ? checkedNow
            : await SteamGridDb!.ImagesOfGameAsync(gameId, kind, cancel);

    private async Task<ArtworkFile?> DownloadHitImageAsync(Game game, SteamGridDbHit hit, ArtworkKind kind, CancellationToken cancel)
    {
        var run = NewRunState();
        var answer = await HitImagesAsync(hit, kind, cancel);
        var best = answer.Images.FirstOrDefault(i => ArtworkFormat.Fits(new ImageSize(i.Width, i.Height), kind, run.Tolerance)) ?? answer.Images.FirstOrDefault();
        if (best is null
            || await DownloadAsync(game, kind, best.Url, run, cancel) is not { } content
            || ImageCheck.Of(new MemoryStream(content)) is not { } size
            || ArtworkCache.ExtensionOf(content) is not { } extension)
        {
            return null;
        }

        try
        {
            return new ArtworkFile(game.Key, kind, _cache.WriteSuggestion(game.Key, kind, content, extension), size, 0);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log($"Artwork: Treffer-Bild für {game.Key} nicht gespeichert: {e.GetType().Name}");
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
    /// <param name="kind">The kind the run asks SteamGridDB for: the first one the game needs from it in this run.</param>
    private async Task<int?> MatchedGameAsync(Game game, ArtworkKind kind, RunState run, CancellationToken cancel)
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

        var sought = SoughtInRun(game, kind);
        if (await HitsWithImagesAsync(search.Hits, NameMatch.TileKindOf(sought), run, cancel) is not { } withImages)
        {
            return null;
        }

        switch (SteamGridDbNames.Decide(game.Title, withImages))
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
                Names.Set(game.Key, NameMatch.Waiting(hits, Names.NextPosition, sought));
                Log($"Artwork: {game.Key} nach Namen {hits.Count} Treffer, wartet auf die Auswahl");
                WaitingChanged?.Invoke(this, EventArgs.Empty);
                return null;
            default:
                run.NoHits.Add(game.Key);
                return null;
        }
    }

    /// <summary>
    /// The hits that have an image of <paramref name="kind"/> (#506), in their order; null once SteamGridDB failed, so a
    /// rejected key or a service that is down costs one request, not one per hit.
    /// </summary>
    private async Task<IReadOnlyList<SteamGridDbHit>?> HitsWithImagesAsync(IReadOnlyList<SteamGridDbHit> hits, ArtworkKind kind, RunState run, CancellationToken cancel)
    {
        var withImages = new List<SteamGridDbHit>(hits.Count);
        foreach (var hit in hits)
        {
            var answer = await HitImagesAsync(hit, kind, cancel);
            run.SteamGridDbAsked++;
            if (StopsSteamGridDb(answer.Outcome, run))
            {
                return null;
            }

            if (answer.Images.Count > 0)
            {
                withImages.Add(hit);
            }
        }

        return withImages;
    }

    /// <summary>
    /// The kinds SteamGridDB is to give the game now (#506, "Gesucht: …"): those whose image is missing or doesn't fit
    /// "Zuschneiden erlaubt" and wasn't set by hand, as a run asks SteamGridDB for them; both if none. Reads what the
    /// views show and the game's metadata, so it needs no lock and no request. Any thread, off the UI thread.
    /// </summary>
    public IReadOnlyList<ArtworkKind> SoughtOf(Game game)
    {
        var tolerance = Settings().WithValidValues().Tolerance;
        var shown = Of(game.Key);
        var stored = _cache.Read(game.Key);
        var sought = Kinds.Where(kind => stored?.Of(kind) is not { SetByHand: true }
            && (shown?.Of(kind) is not { } file || !ArtworkFormat.Fits(file.Size, kind, tolerance))).ToList();
        return sought.Count == 0 ? NameMatch.BothKinds : sought;
    }

    /// <summary>
    /// What a run's search by name seeks: the run goes through the kinds in order and searches at the first one it needs
    /// SteamGridDB for, so a search for the background means the cover is settled; one for the cover seeks the background
    /// too while it is missing or doesn't fit.
    /// </summary>
    private IReadOnlyList<ArtworkKind> SoughtInRun(Game game, ArtworkKind kind) =>
        kind == ArtworkKind.Cover && SoughtOf(game).Contains(ArtworkKind.Hero) ? NameMatch.BothKinds : [kind];

    /// <summary>
    /// A waiting game shown on the waiting list (#506): what it seeks decided again (it may have got an image meanwhile);
    /// when its tile kind changes, its hits count as unchecked for the new kind. Null if it no longer waits.
    /// </summary>
    public NameMatch? WaitingMatchNow(Game game)
    {
        if (Names.Of(game.Key) is not { State: NameMatchState.Waiting } match)
        {
            return null;
        }

        var sought = SoughtOf(game);
        if (sought.SequenceEqual(match.Sought))
        {
            return match;
        }

        var now = match with
        {
            Sought = sought,
            Checked = NameMatch.TileKindOf(sought) == match.TileKind ? match.Checked : System.Collections.Immutable.ImmutableHashSet<int>.Empty,
        };
        Names.Set(game.Key, now);
        return now;
    }

    /// <summary>One game's images brought up to date at once, as a run would, e.g. after the player chose its SteamGridDB game.</summary>
    private async Task UpdateOneAsync(Game game, CancellationToken cancel)
    {
        var lookup = _sources.TryGetValue(game.LauncherId, out var source) ? source.Look() : NoImages;
        var changes = new ChangeBatcher(this);
        await _write.WaitAsync(cancel);
        try
        {
            _askedSteamGridDb.Remove((game.Key, ArtworkKind.Cover));
            _askedSteamGridDb.Remove((game.Key, ArtworkKind.Hero));
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
