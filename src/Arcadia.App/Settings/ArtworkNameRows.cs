using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Settings;

namespace Arcadia.App.Settings;

/// <summary>
/// "Bei mehreren Treffern" and, indented below it, "Warten auf deine Auswahl" in the group "ARTWORK" of "Datenquellen"
/// (#491). Switching to "Ersten nehmen" gives every waiting game its first hit at once (one toast); switching back puts
/// none of them back (D3). The waiting row is always shown: greyed out, but focusable, while no game waits or with
/// "Ersten nehmen"; A opens the waiting list.
/// </summary>
public sealed class ArtworkNameRows
{
    public const string SeveralHitsKey = "artwork-several-hits";

    public const string WaitingKey = "artwork-waiting";

    private const int ShownWaitingGames = 8;

    private readonly ArtworkService _artwork;
    private readonly SettingsChanges _changes;
    private readonly ChoiceRowViewModel _severalHits;
    private readonly ActionRowViewModel _waiting;

    /// <param name="toast">A short message, e.g. how many games took their first hit.</param>
    /// <param name="openWaitingList">A on "Warten auf deine Auswahl" while games wait.</param>
    public ArtworkNameRows(ArtworkService artwork, SettingsChanges changes, Action<HealthSeverity, string> toast, Action openWaitingList)
    {
        _artwork = artwork;
        _changes = changes;
        _severalHits = new ChoiceRowViewModel(
            SeveralHitsKey,
            AppTexts.DataSourcesSeveralHits,
            () => (int)changes.Current.Artwork.WithValidValues().SeveralHits,
            value =>
            {
                var choice = (SeveralHits)value;
                changes.Apply(s => s with { Artwork = s.Artwork.WithValidValues() with { SeveralHits = choice } }, AppTexts.DataSourcesSeveralHits);
                if (choice == SeveralHits.TakeFirst && artwork.TakeFirstForAllWaiting() is > 0 and var taken)
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
            if (IsAsking && _artwork.Waiting.Count > 0)
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

    /// <summary>The choice or the waiting list changed; UI thread.</summary>
    public void Refresh()
    {
        _severalHits.Refresh();
        var waiting = IsAsking ? _artwork.Waiting : [];
        var open = waiting.Count > 0;
        _waiting.IsEnabled = open;
        _waiting.IsDimmed = !open;
        _waiting.CountBadge = open ? waiting.Count.ToString(Services.Formatting.Culture) : null;
        _waiting.Subtitle = !IsAsking ? AppTexts.DataSourcesWaitingOnlyAsk : open ? AppTexts.DataSourcesWaitingSubtitle : AppTexts.DataSourcesWaitingNone;
        _waiting.Info = !IsAsking ? [new InfoText(AppTexts.DataSourcesWaitingInfoOff)]
            : !open ? [new InfoText(AppTexts.DataSourcesWaitingInfoNone)]
            :
            [
                new InfoText(AppTexts.DataSourcesWaitingInfo),
                new InfoFacts(
                    waiting.Count == 1 ? AppTexts.WaitingGamesOne : AppTexts.WaitingGames(waiting.Count),
                    WaitingFacts(waiting)),
            ];
    }

    /// <summary>The first games waiting, the rest in one line: a long list would not fit the panel and costs a frame per change.</summary>
    private static IReadOnlyList<InfoFact> WaitingFacts(IReadOnlyList<WaitingGame> waiting)
    {
        var facts = waiting.Take(ShownWaitingGames)
            .Select(w => new InfoFact(w.Game.Title, AppTexts.WaitingHits(launcher: w.LauncherName, count: w.Hits.Count)))
            .ToList();
        if (waiting.Count > ShownWaitingGames)
        {
            facts.Add(new InfoFact(AppTexts.DataSourcesWaitingMore(waiting.Count - ShownWaitingGames), string.Empty, IsMuted: true));
        }

        return facts;
    }
}
