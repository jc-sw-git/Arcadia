using Arcadia.App.Input;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;

namespace Arcadia.App.Settings;

/// <summary>
/// A source of "REIHENFOLGE FÜR ARTWORK" (#489, #506): ◀ ▶ (and A) switch it on or off, L3 lifts it to move it.
/// </summary>
public sealed class ArtworkSourceRowViewModel(string key, string title, ArtworkSourceKind source, Func<bool> get, Action<bool> set)
    : SwitchRowViewModel(key, title, get, set)
{
    public ArtworkSourceKind Source { get; } = source;
}

/// <summary>
/// The groups "REIHENFOLGE FÜR ARTWORK" and "ARTWORK" of "Datenquellen" (#489, #506): the sources in their order, each
/// switched on or off, L3 or the mouse lift one, ▲ ▼ or dragging move it, L3 or letting go put it down, B puts it back, as
/// in the launcher list; "Fehlende Cover und Hintergründe" with its progress, "Jetzt suchen" and "Suche abbrechen". A
/// changed order or switch applies at once to the images on disk and starts no search.
/// </summary>
public sealed class ArtworkSourceRows
{
    public const string MissingKey = "artwork-missing";

    private static string OrderGroup => AppTexts.DataSourcesOrderGroup;

    private static string ArtworkGroup => AppTexts.DataSourcesArtworkGroup;

    private readonly ArtworkService _artwork;
    private readonly SteamGridDbKey _key;
    private readonly SettingsChanges _changes;
    private readonly Action _orderChanged;
    private readonly Dictionary<ArtworkSourceKind, ArtworkSourceRowViewModel> _sources = [];
    private readonly ActionRowViewModel _missing;

    // "Jetzt suchen" was pressed and its run hasn't shown yet: the row says "Läuft …" at once.
    private bool _searchAsked;
    private ArtworkSearchResult? _resultAtAsk;

    private readonly SourceOrder<ArtworkSourceKind> _order;

    /// <param name="orderChanged">The sources' order or a lift changed: the category puts the rows anew.</param>
    public ArtworkSourceRows(ArtworkService artwork, SteamGridDbKey key, SettingsChanges changes, Action orderChanged)
    {
        _artwork = artwork;
        _key = key;
        _changes = changes;
        _orderChanged = orderChanged;
        _order = new SourceOrder<ArtworkSourceKind>(
            () => Settings.Order,
            order => Apply(s => s with { Order = order }, AppTexts.DataSourcesOrderGroup),
            () =>
            {
                Refresh();
                _orderChanged();
            });
        foreach (var source in ArtworkSettings.DefaultOrder)
        {
            _sources[source] = new ArtworkSourceRowViewModel(SourceKey(source), NameOf(source), source, () => Settings.IsOn(source), on => Switch(source, on))
            {
                Group = OrderGroup,
                PanelTitle = NameOf(source).ToUpperInvariant(),
                Info = InfoOf(source),
                ValueInfos = [AppTexts.DataSourcesSourceOffInfo, AppTexts.DataSourcesSourceOnInfo],
            };
        }

        _missing = new ActionRowViewModel(MissingKey, AppTexts.DataSourcesMissing, SearchOrCancel)
        {
            Group = ArtworkGroup,
        };
        Refresh();
    }

    /// <summary>The sources in their order, a lifted one where it stands now.</summary>
    public IReadOnlyList<SettingsRowViewModel> OrderRows => [.. _order.Arrangement().Select(source => _sources[source])];

    /// <summary>"Fehlende Cover und Hintergründe", first in the group "ARTWORK".</summary>
    public SettingsRowViewModel MissingRow => _missing;

    /// <summary>A source is lifted: ▲ ▼ move it until it is put down.</summary>
    public bool IsReordering => _order.IsLifted;

    public static string SourceKey(ArtworkSourceKind source) => "artwork-source-" + source.ToString().ToLowerInvariant();

    private ArtworkSettings Settings => _changes.Current.Artwork.WithValidValues();

    private bool SteamGridDbOn => Settings.IsOn(ArtworkSourceKind.SteamGridDb);

    /// <summary>The key, the order or the search changed; UI thread.</summary>
    public void Refresh()
    {
        var order = _order.Arrangement();
        for (var place = 0; place < order.Count; place++)
        {
            var row = _sources[order[place]];
            row.Title = AppTexts.DataSourcesOrderPlace(place: place + 1, source: NameOf(order[place]));
            row.IsLifted = _order.Lifted == row.Source;
            row.Refresh();
        }

        var steamGridDb = _sources[ArtworkSourceKind.SteamGridDb];
        steamGridDb.IsDimmed = !_key.IsUsable || !SteamGridDbOn;
        steamGridDb.Subtitle = !SteamGridDbOn ? AppTexts.DataSourcesSourceOffSubtitle
            : _key.IsUsable ? AppTexts.DataSourcesSteamGridDbSubtitle : AppTexts.DataSourcesSteamGridDbSkipped;
        var launcher = _sources[ArtworkSourceKind.Launcher];
        launcher.IsDimmed = !Settings.IsOn(ArtworkSourceKind.Launcher);
        launcher.Subtitle = launcher.IsDimmed ? AppTexts.DataSourcesSourceOffSubtitle : AppTexts.DataSourcesLauncherImagesSubtitle;

        RefreshMissing();
    }

    private void RefreshMissing()
    {
        var search = _artwork.Search;
        if (search.Running is not null || !ReferenceEquals(search.Result, _resultAtAsk))
        {
            _searchAsked = false;
        }

        // A search going on is cancelled from here; a reload from its own row, so this one waits.
        var searching = search.Running is { Reload: false } || _searchAsked;
        var reloading = search.Running is { Reload: true };
        _missing.IsEnabled = !reloading && (searching || SteamGridDbOn);
        _missing.IsDimmed = !_missing.IsEnabled;
        _missing.IsWorking = searching || reloading;
        _missing.ActionText = searching ? AppTexts.DataSourcesSearchCancel : AppTexts.DataSourcesSearchNow;
        _missing.Progress = search.Running is { Total: > 0 } going ? 100.0 * going.Done / going.Total : searching || reloading ? 0 : null;
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
        _missing.Info = SteamGridDbOn || searching
            ? [new InfoText(AppTexts.DataSourcesMissingInfo), new InfoText(AppTexts.DataSourcesMissingInfoNow)]
            : [new InfoText(AppTexts.DataSourcesMissingInfoSteamGridDbOff), new InfoText(AppTexts.DataSourcesMissingInfo)];
    }

    /// <summary>L3 on a source lifts it; while one is lifted, ▲ ▼ move it, L3 drops it, B puts it back (<see cref="SourceOrder{TSource}"/>).</summary>
    public bool Handle(UiAction action, SettingsRowViewModel? focused) =>
        _order.Handle(action, focused is ArtworkSourceRowViewModel row ? row.Source : null);

    public void Lift(ArtworkSourceRowViewModel row) => _order.Lift(row.Source);

    /// <summary>The dragged source is over the place below <paramref name="rowsAbove"/> of the other sources.</summary>
    public void DragTo(int rowsAbove) => _order.PlaceAt(rowsAbove);

    /// <summary>The lifted source stays where it stands; the order is saved, the images follow it at once.</summary>
    public void Drop() => _order.Drop();

    /// <summary>B while a source is lifted, or another category: it goes back to where it was.</summary>
    public void PutBack() => _order.PutBack();

    private void SearchOrCancel()
    {
        if (_artwork.Search.Running is { Reload: false } || _searchAsked)
        {
            _artwork.CancelSearch();
            _searchAsked = false;
            Refresh();
            return;
        }

        if (_artwork.Search.Running is not null || !SteamGridDbOn)
        {
            return;
        }

        _resultAtAsk = _artwork.Search.Result;
        _searchAsked = _artwork.SearchAgain(byPlayer: true);
        Refresh();
    }

    private void Switch(ArtworkSourceKind source, bool on)
    {
        if (_order.IsLifted)
        {
            return;
        }

        Apply(s => s with { Off = on ? [.. s.Off.Where(o => o != source)] : [.. s.Off.Where(o => o != source), source] }, NameOf(source));
        Refresh();
    }

    /// <summary>Saves the change and lets the artwork follow it at once, without a search (#506).</summary>
    private void Apply(Func<ArtworkSettings, ArtworkSettings> change, string what)
    {
        _changes.Apply(s => s with { Artwork = change(s.Artwork.WithValidValues()) }, what);
        _artwork.SettingsChanged();
    }

    private static string NameOf(ArtworkSourceKind source) => source == ArtworkSourceKind.SteamGridDb ? AppTexts.DataSourcesSteamGridDb : AppTexts.DataSourcesLauncherImages;

    private static IReadOnlyList<InfoBlock> InfoOf(ArtworkSourceKind source) =>
    [
        .. source == ArtworkSourceKind.SteamGridDb
            ? (IReadOnlyList<InfoBlock>)[new InfoText(AppTexts.DataSourcesSteamGridDbInfo)]
            : [new InfoText(AppTexts.DataSourcesLauncherImagesInfo), new InfoSection(AppTexts.DataSourcesWhoDeliversHeading, [new InfoText(AppTexts.DataSourcesWhoDelivers)])],
        new InfoSection(AppTexts.DataSourcesOrderHeading, [new InfoText(AppTexts.DataSourcesOrderInfo), new InfoText(AppTexts.DataSourcesOrderMove)]),
        new InfoSection(AppTexts.DataSourcesWhenHeading, [new InfoText(AppTexts.DataSourcesOrderWhen)]),
    ];
}
