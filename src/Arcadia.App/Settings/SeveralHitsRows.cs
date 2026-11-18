using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Metadata;
using Arcadia.Core.Settings;

namespace Arcadia.App.Settings;

/// <summary>
/// "Bei mehreren Treffern" and, indented below it, "Warten auf deine Auswahl" (#491), once in the tab "Artwork" for
/// SteamGridDB and once in "Metadaten" for IGDB (#538), each with its own choice and waiting list. Switching to "Ersten
/// nehmen" gives every waiting game of that kind its first hit at once (one toast, D7); switching back puts none of them
/// back (D3). The waiting row is always shown: greyed out, but focusable, while no game waits or with "Ersten nehmen";
/// A opens the waiting list. Artwork games count only while SteamGridDB is on; metadata games always, since choosing one
/// needs no request.
/// </summary>
public sealed class SeveralHitsRows
{
    private const int ShownWaitingGames = 8;

    private readonly WaitingKind _kind;
    private readonly ArtworkService _artwork;
    private readonly MetadataService? _metadata;
    private readonly SettingsChanges _changes;
    private readonly ChoiceRowViewModel _severalHits;
    private readonly ActionRowViewModel _waiting;

    /// <param name="toast">A short message, e.g. how many games took their first hit.</param>
    /// <param name="openWaitingList">A on "Warten auf deine Auswahl" while games wait.</param>
    /// <param name="metadata">The metadata's waiting games; needed for <see cref="WaitingKind.Metadata"/>.</param>
    public SeveralHitsRows(
        WaitingKind kind, ArtworkService artwork, SettingsChanges changes, Action<HealthSeverity, string> toast, Action<WaitingKind> openWaitingList, MetadataService? metadata = null)
    {
        _kind = kind;
        _artwork = artwork;
        _metadata = metadata;
        _changes = changes;
        var group = kind == WaitingKind.Artwork ? AppTexts.DataSourcesArtworkGroup : AppTexts.DataSourcesMetadataGroup;
        _severalHits = new ChoiceRowViewModel(
            SeveralHitsKey(kind),
            AppTexts.DataSourcesSeveralHits,
            () => (int)Choice,
            value =>
            {
                var choice = (SeveralHits)value;
                Save(choice);
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
            Group = group,
            Subtitle = AppTexts.DataSourcesSeveralHitsSubtitle,
            ValueInfos = kind == WaitingKind.Artwork
                ? [AppTexts.DataSourcesSeveralHitsAskInfo, AppTexts.DataSourcesSeveralHitsFirstInfo]
                : [AppTexts.MetadataSeveralHitsAskInfo, AppTexts.MetadataSeveralHitsFirstInfo],
            Info = [new InfoText(kind == WaitingKind.Artwork ? AppTexts.DataSourcesSeveralHitsInfo : AppTexts.MetadataSeveralHitsInfo)],
        };
        _waiting = new ActionRowViewModel(WaitingKey(kind), AppTexts.DataSourcesWaiting, () =>
        {
            if (IsAsking && Entries().Count > 0)
            {
                openWaitingList(kind);
            }
        })
        {
            Group = group,
            Indent = 1,
            ActionText = AppTexts.DataSourcesWaitingAction,
        };
        Refresh();
    }

    public static string SeveralHitsKey(WaitingKind kind) => kind == WaitingKind.Artwork ? "artwork-several-hits" : "metadata-several-hits";

    public static string WaitingKey(WaitingKind kind) => kind == WaitingKind.Artwork ? "artwork-waiting" : "metadata-waiting";

    public SettingsRowViewModel SeveralHitsRow => _severalHits;

    public SettingsRowViewModel WaitingRow => _waiting;

    public IReadOnlyList<SettingsRowViewModel> Rows => [_severalHits, _waiting];

    private SeveralHits ArtworkChoice => _changes.Current.Artwork.WithValidValues().SeveralHits;

    private SeveralHits Choice => _kind == WaitingKind.Artwork ? ArtworkChoice : _changes.Current.Metadata.SeveralHitsOr(ArtworkChoice);

    private bool IsAsking => Choice == SeveralHits.Ask;

    // Switched off, SteamGridDB is searched by name for no game; the waiting games keep their place (#506).
    private bool HeldBack => _kind == WaitingKind.Artwork && !_changes.Current.Artwork.WithValidValues().IsOn(ArtworkSourceKind.SteamGridDb);

    /// <summary>The choice or the waiting list changed; UI thread.</summary>
    public void Refresh()
    {
        _severalHits.Refresh();
        var waiting = IsAsking ? Entries() : [];
        var open = waiting.Count > 0;
        var heldBack = IsAsking && HeldBack;
        _waiting.IsEnabled = open;
        _waiting.IsDimmed = !open;
        _waiting.CountBadge = open ? Services.Formatting.Count(waiting.Count) : null;
        _waiting.Subtitle = !IsAsking ? AppTexts.DataSourcesWaitingOnlyAsk
            : open ? AppTexts.DataSourcesWaitingSubtitle
            : heldBack ? AppTexts.DataSourcesWaitingSteamGridDbOff : AppTexts.DataSourcesWaitingNone;
        _waiting.Info = !IsAsking ? [new InfoText(AppTexts.DataSourcesWaitingInfoOff)]
            : !open && heldBack ? [new InfoText(AppTexts.DataSourcesWaitingInfoSteamGridDbOff)]
            : !open ? [new InfoText(AppTexts.DataSourcesWaitingInfoNone)]
            :
            [
                new InfoText(_kind == WaitingKind.Artwork ? AppTexts.DataSourcesWaitingInfo : AppTexts.MetadataWaitingInfo),
                new InfoFacts(
                    waiting.Count == 1 ? AppTexts.WaitingGamesOne : AppTexts.WaitingGames(waiting.Count),
                    WaitingFacts(waiting)),
            ];
    }

    private void Save(SeveralHits choice)
    {
        if (_kind == WaitingKind.Artwork)
        {
            _changes.Apply(s => s with { Artwork = s.Artwork.WithValidValues() with { SeveralHits = choice } }, AppTexts.DataSourcesSeveralHits);
        }
        else
        {
            _changes.Apply(s => s with { Metadata = s.Metadata.WithValidValues() with { SeveralHits = choice } }, AppTexts.DataSourcesSeveralHits);
        }
    }

    /// <summary>The games on this kind's waiting list, as it shows them: artwork games only while SteamGridDB is on.</summary>
    private List<(string Title, string LauncherName)> Entries() => _kind == WaitingKind.Artwork
        ? [.. (HeldBack ? [] : _artwork.Waiting).Select(w => (w.Game.Title, w.LauncherName))]
        : [.. (_metadata?.Waiting ?? []).Select(w => (w.Game.Title, w.LauncherName))];

    /// <summary>Every waiting game of this kind takes its first hit (D7); the count is the games the list showed.</summary>
    private int TakeFirstForAll()
    {
        var shown = Entries().Count;
        var taken = _kind == WaitingKind.Artwork ? _artwork.TakeFirstForAllWaiting() : _metadata?.TakeFirstForAllWaiting() ?? 0;
        return taken > 0 ? shown : 0;
    }

    /// <summary>The first games waiting, the rest in one line: a long list would not fit the panel and costs a frame per change.</summary>
    private static IReadOnlyList<InfoFact> WaitingFacts(IReadOnlyList<(string Title, string LauncherName)> waiting)
    {
        var facts = waiting.Take(ShownWaitingGames).Select(w => new InfoFact(w.Title, w.LauncherName)).ToList();
        if (waiting.Count > ShownWaitingGames)
        {
            facts.Add(new InfoFact(AppTexts.DataSourcesWaitingMore(waiting.Count - ShownWaitingGames), string.Empty, IsMuted: true));
        }

        return facts;
    }
}
