using Arcadia.Core.Metadata.Igdb;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Metadata;

/// <summary>A game waiting on "Warten auf deine Auswahl" marked "Metadaten" (#494): IGDB's hits for its name, in IGDB's order.</summary>
public sealed record WaitingMetadataGame(Game Game, string LauncherName, IReadOnlyList<IgdbGame> Hits);

/// <summary>
/// The metadata part of "Warten auf deine Auswahl" (#494, D6 to D8): a game IGDB found by name with several hits while
/// "Selbst auswählen" holds keeps its launcher data and waits with its hits (names and data checked, kept in its cache
/// file, so a restart or the game leaving the library and coming back keep it). A on a hit makes it the game's IGDB game:
/// its fields and a doubtful title show at once, without a request, and it is asked by IGDB's id from then on. Y skips it:
/// never searched by name on IGDB again, also not by "Alle Metadaten neu laden". The choice is in memory at once; the
/// file is written in the background (<see cref="WhenIdleAsync"/> waits for it).
/// </summary>
public sealed partial class MetadataService
{
    /// <summary>The most results of an own search term shown: five rows of covers.</summary>
    public const int MaxOwnTermHits = 25;

    private Task _saving = Task.CompletedTask;

    /// <summary>The metadata waiting list changed: a game came on it or left it, or the libraries changed. Raised on the thread that changed it.</summary>
    public event EventHandler? WaitingChanged;

    /// <summary>
    /// The games of the last libraries waiting for the player's choice of their IGDB game, in the order they came on the
    /// list; empty before the first scan. Any thread.
    /// </summary>
    public IReadOnlyList<WaitingMetadataGame> Waiting
    {
        get
        {
            IReadOnlyList<LibraryMetadata>? libraries;
            lock (_gate)
            {
                libraries = _lastLibraries;
            }

            return libraries is null
                ? []
                : libraries
                    .Where(l => !l.MadeUp)
                    .SelectMany(l => l.Library.Games.Where(g => g.Type == GameType.Game).Select(g => (Game: g, Launcher: l.Library.Launcher.DisplayName)))
                    .DistinctBy(e => e.Game.Key, StringComparer.Ordinal)
                    .Select(e => (e.Game, e.Launcher, Match: _records.GetValueOrDefault(e.Game.Key)?.IgdbByName))
                    .Where(e => e.Match is { Skipped: false })
                    .OrderBy(e => e.Match!.Position)
                    .Select(e => new WaitingMetadataGame(e.Game, e.Launcher, e.Match!.Hits))
                    .ToList();
        }
    }

    /// <summary>
    /// A on a hit: the game is that IGDB game from now on and leaves the waiting list; its fields and a doubtful title are
    /// merged from the hit at once, without a request. A game no longer waiting is left as it is. Any thread.
    /// </summary>
    /// <param name="hit">One of the game's hits or of an own search term (<see cref="SearchByTermAsync"/>), already checked.</param>
    public void Choose(Game game, IgdbGame hit)
    {
        if (_records.GetValueOrDefault(game.Key) is not { IgdbByName.Skipped: false } known)
        {
            return;
        }

        var chosen = known with { Igdb = new IgdbCached(hit.Id, hit.Name, hit.Data, _time.GetUtcNow()), IgdbByName = null };
        _records[game.Key] = chosen;
        Log($"IGDB: {game.Key} ist IGDB-Spiel {hit.Id} (gewählt)");
        WaitingChanged?.Invoke(this, EventArgs.Empty);
        Save([chosen]);
    }

    /// <summary>Y: the game leaves the waiting list, keeps its launcher data and is never searched by name on IGDB again (D8).</summary>
    public void Skip(Game game)
    {
        if (_records.GetValueOrDefault(game.Key) is not { IgdbByName.Skipped: false } known)
        {
            return;
        }

        var skipped = known with { IgdbByName = IgdbNameMatch.Skip };
        _records[game.Key] = skipped;
        Log($"IGDB: {game.Key} übersprungen, wird nicht mehr nach Namen gesucht");
        WaitingChanged?.Invoke(this, EventArgs.Empty);
        Save([skipped]);
    }

    /// <summary>"Ersten nehmen" chosen while games wait: each takes its first hit at once (D7).</summary>
    /// <returns>How many games of the libraries took their first hit, as the list showed them; games waiting outside the libraries take theirs too, uncounted.</returns>
    public int TakeFirstForAllWaiting()
    {
        var shown = Waiting.Count;
        var taken = new List<CachedMetadata>();
        foreach (var known in _records.Values.Where(r => r.IgdbByName is { Skipped: false, Hits.Count: > 0 }))
        {
            var first = known.IgdbByName!.Hits[0];
            var chosen = known with { Igdb = new IgdbCached(first.Id, first.Name, first.Data, _time.GetUtcNow()), IgdbByName = null };
            _records[known.GameKey] = chosen;
            taken.Add(chosen);
        }

        if (taken.Count == 0)
        {
            return 0;
        }

        Log($"IGDB: {taken.Count} wartende Spiele nehmen den ersten Treffer");
        WaitingChanged?.Invoke(this, EventArgs.Empty);
        Save(taken);
        return shown;
    }

    /// <summary>
    /// "Eigener Suchbegriff" on a metadata entry: IGDB's main games for a term the player typed, at most
    /// <see cref="MaxOwnTermHits"/>, names and data checked; nothing is kept. Without a usable access nothing is sent
    /// (<see cref="IgdbOutcome.Rejected"/>). Any thread.
    /// </summary>
    public async Task<IgdbLookup> SearchByTermAsync(string term, CancellationToken cancel = default)
    {
        if (Igdb is not { IsUsable: true } igdb)
        {
            return IgdbLookup.Of(IgdbOutcome.Rejected);
        }

        if (IgdbQuery.ByName(term) is not { } query)
        {
            return new IgdbLookup(IgdbOutcome.Found, []);
        }

        Log("IGDB: eigene Suche nach Namen");
        var lookup = await igdb.QueryAsync(query, cancel);
        return lookup.Outcome != IgdbOutcome.Found
            ? lookup
            : new IgdbLookup(IgdbOutcome.Found, [.. CheckedHits(lookup.Games.Where(g => IgdbGameType.IsMainGame(g.Type))).Take(MaxOwnTermHits)]);
    }

    /// <summary>Several hits while "Selbst auswählen" holds: the game waits behind every game waiting now, with its launcher data.</summary>
    private void Wait(CachedMetadata known, IReadOnlyList<IgdbGame> hits)
    {
        var position = _records.Values.Select(r => r.IgdbByName is { Skipped: false } m ? m.Position + 1 : 1).DefaultIfEmpty(1).Max();
        Keep(known with { IgdbByName = IgdbNameMatch.Waiting([.. CheckedHits(hits)], position) });
        Log($"IGDB: {known.GameKey} nach Namen {hits.Count} Treffer, wartet auf die Auswahl");
        WaitingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A hit as it may be shown and taken: its name and data checked like any answer; one without a usable name is left out.</summary>
    private static IEnumerable<IgdbGame> CheckedHits(IEnumerable<IgdbGame> hits) =>
        hits.Select(hit => MetadataCheck.CheckedTitle(hit.Name) is { Length: > 0 } name ? hit with { Name = name, Data = MetadataCheck.Checked(hit.Data) } : null)
            .OfType<IgdbGame>();

    /// <summary>The changed games written and merged off the caller's thread, one after another; a failure is logged and reported.</summary>
    private void Save(IReadOnlyList<CachedMetadata> changedRecords)
    {
        lock (_gate)
        {
            _saving = _saving.ContinueWith(_ => SaveNow(changedRecords), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }

    private void SaveNow(IReadOnlyList<CachedMetadata> changedRecords)
    {
        try
        {
            var changed = new List<string>();
            foreach (var record in changedRecords)
            {
                Persist(_records.GetValueOrDefault(record.GameKey) ?? record);
                Show(record.GameKey, changed);
            }

            if (changed.Count > 0)
            {
                Changed?.Invoke(this, changed);
            }
        }
        catch (Exception e)
        {
            Log($"Metadaten: Auswahl nicht übernommen: {e}");
            Failed?.Invoke(this, e);
        }
    }
}
