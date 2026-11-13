using Arcadia.Core.Artwork;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Metadata;

/// <summary>
/// IGDB's part of the run (#494): after the stores, every game IGDB hasn't answered for is asked, a Steam game by its AppID
/// (never by name, D3), every other game by its launcher title; without a usable access, with IGDB switched off or before
/// the first scan nothing goes. A rejected access, no network, a timeout or a broken answer leave IGDB out for the rest of
/// the run (the next scan tries again); several hits follow "Bei mehreren Treffern": the first is taken, or the game waits
/// for the player (MetadataService.Waiting.cs).
/// </summary>
public sealed partial class MetadataService
{
    private static string IgdbName => CoreTexts.MetadataSourceIgdb;

    private readonly HashSet<string> _igdbReloadLeft = new(StringComparer.Ordinal);

    /// <summary>IGDB is asked at all: there is a usable access and IGDB is switched on.</summary>
    private bool IgdbIsAsked => Igdb is { IsUsable: true } && (Settings() ?? new MetadataSettings()).WithValidValues().IsOn(MetadataSourceKind.Igdb);

    /// <summary>
    /// The games IGDB is asked for: games (no software or tools) of every library but the demo ones, each once; none while
    /// IGDB isn't asked.
    /// </summary>
    private IEnumerable<(Game Game, ArtworkStoreId? StoreId, string Source)> IgdbGames(IReadOnlyList<LibraryMetadata> libraries) =>
        !IgdbIsAsked
            ? []
            : libraries
                .Where(l => !l.MadeUp)
                .SelectMany(l => l.Library.Games.Where(g => g.Type == GameType.Game).Select(g => (g, SafeStoreIdOf(l.Source, g), l.Library.Launcher.DisplayName)))
                .DistinctBy(g => g.g.Key, StringComparer.Ordinal);

    /// <summary>
    /// IGDB still has to answer for the game: never asked, a reload goes on, or its answer was read by an older version
    /// (#541). A game waiting for the player's choice or skipped is not searched by name again, also not by a reload (D8).
    /// </summary>
    private bool IsIgdbMissing(string gameKey) =>
        _records.GetValueOrDefault(gameKey) is var known && known?.IgdbByName is null
        && (_igdbReloadLeft.Contains(gameKey) || known?.Igdb is null || known.Igdb.Reading < IgdbAnswer.Reading);

    private static ArtworkStoreId? SafeStoreIdOf(ILauncherMetadata? source, Game game)
    {
        try
        {
            return source?.StoreIdOf(game) is { Store: ArtworkStoreId.Steam, Id: { } id } steam && IgdbQuery.BySteamAppId(id) is not null ? steam : null;
        }
        catch (Exception e) when (e is ArgumentException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Asks IGDB for every game still missing its answer, in library order; stops for a newer scan.</summary>
    /// <param name="storesReached">Whether a store answered in this run (null: none was asked), for the reload's warning.</param>
    private async Task FetchIgdbAsync(IReadOnlyList<LibraryMetadata> libraries, bool reload, bool? storesReached, CancellationToken cancel)
    {
        var queue = IgdbGames(libraries).Where(g => IsIgdbMissing(g.Game.Key)).ToList();
        bool? igdbReached = null;
        if (queue.Count > 0 && Igdb is { } igdb)
        {
            var run = new FetchRun(queue.Count, queue.Count, _time.GetUtcNow(), reload || _igdbReloadLeft.Count > 0);
            var summary = new IgdbSummary();
            Report(libraries, null, run);
            foreach (var (game, storeId, source) in queue)
            {
                if (HasPending() || !IgdbIsAsked)
                {
                    break;
                }

                var outcome = await FetchIgdbGameAsync(igdb, game, storeId, source, summary, cancel);
                run.Count(outcome);
                _igdbReloadLeft.Remove(game.Key);
                Report(libraries, null, run);
                if (outcome == Outcome.Offline)
                {
                    break;
                }
            }

            igdbReached = run.Answered > 0 || !run.Offline;
            summary.Log(Log, run.Answered, run.Offline);
        }

        if (reload && storesReached != true && igdbReached != true && (storesReached is not null || igdbReached is not null))
        {
            ReloadFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<Outcome> FetchIgdbGameAsync(IgdbAccess igdb, Game game, ArtworkStoreId? storeId, string source, IgdbSummary summary, CancellationToken cancel)
    {
        var known = _records.GetValueOrDefault(game.Key);
        var byName = storeId is null && known?.Igdb?.GameId is null;
        var query = storeId is not null ? IgdbQuery.BySteamAppId(storeId.Id)
            : known?.Igdb?.GameId is { } id ? IgdbQuery.ById(id)
            : IgdbQuery.ByName(game.Title);
        if (query is null)
        {
            return Outcome.NoAddress;
        }

        var lookup = await igdb.QueryAsync(query, cancel);
        switch (lookup.Outcome)
        {
            case IgdbOutcome.Rejected:
                // The access shows its warning; no further request with it in this run.
                summary.Rejected = true;
                return Outcome.Offline;
            case IgdbOutcome.NoConnection:
                return Outcome.Offline;
        }

        IgdbGame? found;
        if (byName)
        {
            var match = IgdbMatching.Of(game.Title, lookup.Games);
            found = match.Taken ?? (match.IsSeveral && SeveralHits() == Arcadia.Core.Settings.SeveralHits.TakeFirst ? match.Hits[0] : null);
            if (found is null)
            {
                if (match.IsSeveral)
                {
                    Wait(known ?? new CachedMetadata(game.Key, source, null, null, null), match.Hits);
                    summary.Several++;
                }
                else
                {
                    // No hit: searched again at the next scan.
                    summary.NotFound++;
                }

                return Outcome.Unusable;
            }
        }
        else
        {
            found = lookup.Games.FirstOrDefault(g => storeId is not null || g.Id == known?.Igdb?.GameId);
            if (found is null && storeId is null && known is not null)
            {
                // IGDB dropped the game it was matched to: searched by name again at the next scan.
                Keep(known with { Igdb = null });
                return Outcome.Unusable;
            }
        }

        // A Steam AppID IGDB doesn't know is remembered as such: no data, and never searched by name (D3).
        var answer = found is null
            ? new IgdbCached(null, null, GameMetadata.Empty, _time.GetUtcNow())
            : new IgdbCached(found.Id, MetadataCheck.CheckedTitle(found.Name), MetadataCheck.Checked(found.Data), _time.GetUtcNow());
        var updated = (known ?? new CachedMetadata(game.Key, source, null, null, null)) with { Igdb = answer };
        Keep(updated);
        var changed = new List<string>();
        Show(game.Key, changed);
        if (changed.Count > 0)
        {
            Changed?.Invoke(this, changed);
        }

        if (found is null)
        {
            summary.NotFound++;
        }

        return Outcome.Answered;
    }

    /// <summary>The counts of one IGDB pass for its log line; never a name or a value of the access.</summary>
    private sealed class IgdbSummary
    {
        public int Several { get; set; }

        public int NotFound { get; set; }

        public bool Rejected { get; set; }

        public void Log(Action<string> log, int answered, bool stopped) =>
            log($"IGDB: {answered} geladen, {NotFound} ohne Treffer, {Several} mit mehreren Treffern{(Rejected ? ", Zugang abgelehnt: IGDB wird übersprungen" : stopped ? ", keine Verbindung: Rest beim nächsten Einlesen" : string.Empty)}");
    }
}
