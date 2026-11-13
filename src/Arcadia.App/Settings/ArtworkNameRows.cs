using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Metadata;
using Arcadia.Core.Settings;

namespace Arcadia.App.Settings;

/// <summary>
/// "Bei mehreren Treffern" and, indented below it, "Warten auf deine Auswahl" in the group "ARTWORK" of "Datenquellen"
/// (#491), shared by artwork and metadata (#494). Switching to "Ersten nehmen" gives every waiting entry its first hit at
/// once (one toast for both kinds, D7); switching back puts none of them back (D3). The waiting row is always shown: greyed
/// out, but focusable, while no entry waits or with "Ersten nehmen"; A opens the waiting list. Artwork entries count only
/// while SteamGridDB is on; metadata entries always, since choosing one needs no request.
/// </summary>
public sealed class ArtworkNameRows
{
    public const string SeveralHitsKey = "artwork-several-hits";

    public const string WaitingKey = "artwork-waiting";

    private const int ShownWaitingGames = 8;

    private readonly ArtworkService _artwork;
    private readonly MetadataService? _metadata;
    private readonly SettingsChanges _changes;
    private readonly ChoiceRowViewModel _severalHits;
    private readonly ActionRowViewModel _waiting;

    /// <param name="toast">A short message, e.g. how many games took their first hit.</param>
    /// <param name="openWaitingList">A on "Warten auf deine Auswahl" while entries wait.</param>
    /// <param name="metadata">The metadata entries (#494); null: only artwork entries.</param>
    public ArtworkNameRows(ArtworkService artwork, SettingsChanges changes, Action<HealthSeverity, string> toast, Action openWaitingList, MetadataService? metadata = null)
    {
        _artwork = artwork;
        _metadata = metadata;
        _changes = changes;
        _severalHits = new ChoiceRowViewModel(
            SeveralHitsKey,
            AppTexts.DataSourcesSeveralHits,
            () => (int)changes.Current.Artwork.WithValidValues().SeveralHits,
            value =>
            {
                var choice = (SeveralHits)value;
                changes.Apply(s => s with { Artwork = s.Artwork.WithValidValues() with { SeveralHits = choice } }, AppTexts.DataSourcesSeveralHits);
                if (choice == SeveralHits.TakeFirst && TakeFirstForAll() is > 0 and var taken)
                {
                    toast(HealthSeverity.Info, taken == 1 ? AppTexts.DataSourcesTookFirstHitOne : AppTexts.DataSourcesTookFirstHits(taken));
                }

                Refresh();
            },
            value => (SeveralHits)value == SeveralHits.TakeFirst ? AppTexts.DataSourcesSeveralHitsFirst : AppTexts.DataSourcesSeveralHitsAsk,
            (int)SeveralHits.Ask,
            (int)SeveralHits.TakeFirst)
        {
            Group = AppTexts.DataSourcesArtworkGroup,
            Subtitle = AppTexts.DataSourcesSeveralHitsSubtitle,
            ValueInfos = [AppTexts.DataSourcesSeveralHitsAskInfo, AppTexts.DataSourcesSeveralHitsFirstInfo],
            Info = [new InfoText(AppTexts.DataSourcesSeveralHitsInfo)],
        };
        _waiting = new ActionRowViewModel(WaitingKey, AppTexts.DataSourcesWaiting, () =>
        {
            if (IsAsking && Entries().Count > 0)
            {
                openWaitingList();
            }
        })
        {
            Group = AppTexts.DataSourcesArtworkGroup,
            Indent = 1,
            ActionText = AppTexts.DataSourcesWaitingAction,
        };
        Refresh();
    }

    public SettingsRowViewModel SeveralHitsRow => _severalHits;

    public SettingsRowViewModel WaitingRow => _waiting;

    private bool IsAsking => _changes.Current.Artwork.WithValidValues().SeveralHits == SeveralHits.Ask;

    // Switched off, SteamGridDB is searched by name for no game; the waiting games keep their place (#506).
    private bool SteamGridDbOn => _changes.Current.Artwork.WithValidValues().IsOn(ArtworkSourceKind.SteamGridDb);

    /// <summary>The choice or the waiting list changed; UI thread.</summary>
    public void Refresh()
    {
        _severalHits.Refresh();
        var waiting = IsAsking ? Entries() : [];
        var open = waiting.Count > 0;
        var artworkHeldBack = IsAsking && !SteamGridDbOn;
        _waiting.IsEnabled = open;
        _waiting.IsDimmed = !open;
        _waiting.CountBadge = waiting.Count > 0 ? waiting.Count.ToString(Services.Formatting.Culture) : null;
        _waiting.Subtitle = !IsAsking ? AppTexts.DataSourcesWaitingOnlyAsk
            : open ? AppTexts.DataSourcesWaitingSubtitle
            : artworkHeldBack ? AppTexts.DataSourcesWaitingSteamGridDbOff : AppTexts.DataSourcesWaitingNone;
        _waiting.Info = !IsAsking ? [new InfoText(AppTexts.DataSourcesWaitingInfoOff)]
            : !open && artworkHeldBack ? [new InfoText(AppTexts.DataSourcesWaitingInfoSteamGridDbOff)]
            : !open ? [new InfoText(AppTexts.DataSourcesWaitingInfoNone)]
            :
            [
                new InfoText(AppTexts.DataSourcesWaitingInfo),
                new InfoFacts(
                    waiting.Count == 1 ? AppTexts.WaitingGamesOne : AppTexts.WaitingGames(waiting.Count),
                    WaitingFacts(waiting)),
            ];
    }

    /// <summary>
    /// The entries on the waiting list, as it shows them: the artwork entries while SteamGridDB is on, then the metadata
    /// entries; a game waiting for both counts twice.
    /// </summary>
    private List<(string Key, string Title, string LauncherName, bool Metadata)> Entries() =>
    [
        .. (SteamGridDbOn ? _artwork.Waiting : []).Select(w => (w.Game.Key, w.Game.Title, w.LauncherName, false)),
        .. (_metadata?.Waiting ?? []).Select(w => (w.Game.Key, w.Game.Title, w.LauncherName, true)),
    ];

    /// <summary>
    /// Every waiting entry takes its first hit (D7). The count is the games the list showed, a game waiting for both kinds
    /// once, so the one toast says what the player saw.
    /// </summary>
    private int TakeFirstForAll()
    {
        var shown = Entries().Select(e => e.Key).Distinct(StringComparer.Ordinal).Count();
        var taken = _artwork.TakeFirstForAllWaiting() + (_metadata?.TakeFirstForAllWaiting() ?? 0);
        return taken > 0 ? shown : 0;
    }

    /// <summary>The first entries waiting, the rest in one line: a long list would not fit the panel and costs a frame per change.</summary>
    private static IReadOnlyList<InfoFact> WaitingFacts(IReadOnlyList<(string Key, string Title, string LauncherName, bool Metadata)> waiting)
    {
        var facts = waiting.Take(ShownWaitingGames)
            .Select(w => new InfoFact(
                w.Title,
                AppTexts.WaitingEntryLine(launcher: w.LauncherName, marker: w.Metadata ? AppTexts.WaitingMarkerMetadata : AppTexts.WaitingMarkerArtwork)))
            .ToList();
        if (waiting.Count > ShownWaitingGames)
        {
            facts.Add(new InfoFact(AppTexts.DataSourcesWaitingMore(waiting.Count - ShownWaitingGames), string.Empty, IsMuted: true));
        }

        return facts;
    }
}
