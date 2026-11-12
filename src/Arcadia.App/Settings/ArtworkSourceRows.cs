using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;

namespace Arcadia.App.Settings;

/// <summary>
/// The groups "REIHENFOLGE FÜR ARTWORK" and "ARTWORK" of "Datenquellen" (#489): the sources in their order (A moves one a
/// place up, the top one down), "Fehlende Cover und Hintergründe" with its progress and "Jetzt suchen", and "Zuschneiden
/// erlaubt". A changed order or tolerance applies from the next search.
/// </summary>
public sealed class ArtworkSourceRows
{
    public const string MissingKey = "artwork-missing";

    private static string OrderGroup => AppTexts.DataSourcesOrderGroup;

    private static string ArtworkGroup => AppTexts.DataSourcesArtworkGroup;

    private readonly ArtworkService _artwork;
    private readonly SteamGridDbKey _key;
    private readonly SettingsChanges _changes;
    private readonly Dictionary<ArtworkSourceKind, ActionRowViewModel> _sources = [];
    private readonly ActionRowViewModel _missing;
    private readonly ChoiceRowViewModel _crop;

    // "Jetzt suchen" was pressed and its run hasn't shown yet: the row says "Läuft …" at once.
    private bool _searchAsked;
    private ArtworkSearchResult? _resultAtAsk;

    /// <param name="orderChanged">The sources' order changed: the category puts the rows anew.</param>
    public ArtworkSourceRows(ArtworkService artwork, SteamGridDbKey key, SettingsChanges changes, Action orderChanged)
    {
        _artwork = artwork;
        _key = key;
        _changes = changes;
        foreach (var source in ArtworkSettings.DefaultOrder)
        {
            _sources[source] = new ActionRowViewModel(SourceKey(source), NameOf(source), () =>
            {
                Move(source);
                orderChanged();
            })
            {
                Group = OrderGroup,
                ActionText = AppTexts.DataSourcesMove,
                PanelTitle = NameOf(source).ToUpperInvariant(),
                Info = InfoOf(source),
            };
        }

        _missing = new ActionRowViewModel(MissingKey, AppTexts.DataSourcesMissing, SearchNow)
        {
            Group = ArtworkGroup,
            Info = [new InfoText(AppTexts.DataSourcesMissingInfo), new InfoText(AppTexts.DataSourcesMissingInfoNow)],
        };
        _crop = new ChoiceRowViewModel(
            "artwork-crop",
            AppTexts.DataSourcesCrop,
            () => (int)changes.Current.Artwork.WithValidValues().Crop,
            value => changes.Apply(s => s with { Artwork = s.Artwork.WithValidValues() with { Crop = (CropAllowance)value } }, AppTexts.DataSourcesCrop),
            value => (CropAllowance)value switch
            {
                CropAllowance.Little => AppTexts.DataSourcesCropLittle,
                CropAllowance.Much => AppTexts.DataSourcesCropMuch,
                _ => AppTexts.DataSourcesCropNormal,
            },
            (int)CropAllowance.Little,
            (int)CropAllowance.Much)
        {
            Group = ArtworkGroup,
            Subtitle = AppTexts.DataSourcesCropSubtitle,
            Info =
            [
                new InfoText(AppTexts.DataSourcesCropInfo),
                new InfoText(AppTexts.DataSourcesCropInfoOrder),
                new InfoSection(AppTexts.DataSourcesCropChoiceHeading, [new InfoText(AppTexts.DataSourcesCropChoice)]),
                new InfoSection(AppTexts.DataSourcesWhenHeading, [new InfoText(AppTexts.DataSourcesCropWhen)]),
            ],
        };
        Refresh();
    }

    /// <summary>The sources in their order.</summary>
    public IReadOnlyList<SettingsRowViewModel> OrderRows => [.. _changes.Current.Artwork.WithValidValues().Order.Select(source => _sources[source])];

    /// <summary>"Fehlende Cover und Hintergründe", first in the group "ARTWORK".</summary>
    public SettingsRowViewModel MissingRow => _missing;

    public SettingsRowViewModel CropRow => _crop;

    public static string SourceKey(ArtworkSourceKind source) => "artwork-source-" + source.ToString().ToLowerInvariant();

    /// <summary>The key, the order or the search changed; UI thread.</summary>
    public void Refresh()
    {
        var order = _changes.Current.Artwork.WithValidValues().Order;
        for (var place = 0; place < order.Count; place++)
        {
            var row = _sources[order[place]];
            row.Title = AppTexts.DataSourcesOrderPlace(place: place + 1, source: NameOf(order[place]));
        }

        var steamGridDb = _sources[ArtworkSourceKind.SteamGridDb];
        steamGridDb.IsDimmed = !_key.IsUsable;
        steamGridDb.Subtitle = _key.IsUsable ? AppTexts.DataSourcesSteamGridDbSubtitle : AppTexts.DataSourcesSteamGridDbSkipped;
        _sources[ArtworkSourceKind.Launcher].Subtitle = AppTexts.DataSourcesLauncherImagesSubtitle;

        var search = _artwork.Search;
        if (search.Running is not null || !ReferenceEquals(search.Result, _resultAtAsk))
        {
            _searchAsked = false;
        }

        var running = search.Running is not null || _searchAsked;
        _missing.IsEnabled = !running;
        _missing.IsWorking = running;
        _missing.ActionText = running ? AppTexts.DataSourcesSearchBusy : AppTexts.DataSourcesSearchNow;
        _missing.Progress = search.Running is { Total: > 0 } going ? 100.0 * going.Done / going.Total : running ? 0 : null;
        _missing.Subtitle = search switch
        {
            { Running: { Reload: true } reload } => AppTexts.DataSourcesReloadRunning(done: reload.Done, total: reload.Total),
            { Running: { } run } => AppTexts.DataSourcesSearchRunning(done: run.Done, total: run.Total),
            _ when _searchAsked => AppTexts.DataSourcesSearchBusy,
            { Result: { WithSteamGridDb: false } } => AppTexts.DataSourcesSearchLauncherOnly,
            { Result: { } result } => AppTexts.DataSourcesSearchFound(covers: result.Covers, heroes: result.Heroes),
            { MissingCovers: 0, MissingHeroes: 0 } when search == ArtworkSearch.NotYet => AppTexts.DataSourcesMissingNotYet,
            { MissingCovers: 0, MissingHeroes: 0 } => AppTexts.DataSourcesMissingNone,
            _ => AppTexts.DataSourcesMissingCounts(covers: search.MissingCovers, heroes: search.MissingHeroes),
        };
        _crop.Refresh();
    }

    private void SearchNow()
    {
        if (_artwork.Search.Running is not null || _searchAsked)
        {
            return;
        }

        _resultAtAsk = _artwork.Search.Result;
        _searchAsked = _artwork.SearchAgain(byPlayer: true);
        Refresh();
    }

    /// <summary>A place up; the top one a place down, so with two sources A swaps them.</summary>
    private void Move(ArtworkSourceKind source)
    {
        var order = _changes.Current.Artwork.WithValidValues().Order.ToList();
        var from = order.IndexOf(source);
        var to = from > 0 ? from - 1 : from + 1;
        if (to >= order.Count)
        {
            return;
        }

        (order[from], order[to]) = (order[to], order[from]);
        _changes.Apply(s => s with { Artwork = s.Artwork.WithValidValues() with { Order = order } }, AppTexts.DataSourcesOrderGroup);
        Refresh();
    }

    private static string NameOf(ArtworkSourceKind source) => source == ArtworkSourceKind.SteamGridDb ? AppTexts.DataSourcesSteamGridDb : AppTexts.DataSourcesLauncherImages;

    private static IReadOnlyList<InfoBlock> InfoOf(ArtworkSourceKind source) => source == ArtworkSourceKind.SteamGridDb
        ?
        [
            new InfoText(AppTexts.DataSourcesSteamGridDbInfo),
            new InfoSection(AppTexts.DataSourcesOrderHeading, [new InfoText(AppTexts.DataSourcesOrderInfo)]),
        ]
        :
        [
            new InfoText(AppTexts.DataSourcesLauncherImagesInfo),
            new InfoSection(AppTexts.DataSourcesWhoDeliversHeading, [new InfoText(AppTexts.DataSourcesWhoDelivers)]),
            new InfoSection(AppTexts.DataSourcesOrderHeading, [new InfoText(AppTexts.DataSourcesOrderInfo)]),
        ];
}
