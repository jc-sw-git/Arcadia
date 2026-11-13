using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.Launchers.Abstractions;
using Arcadia.Core.Metadata;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Core.Settings;

namespace Arcadia.App.Settings;

/// <summary>A source of "REIHENFOLGE FÜR METADATEN" (#494): ◀ ▶ (and A) switch it on or off, L3 lifts it to move it.</summary>
public sealed class MetadataSourceRowViewModel(string key, string title, MetadataSourceKind source, Func<bool> get, Action<bool> set)
    : SwitchRowViewModel(key, title, get, set)
{
    public MetadataSourceKind Source { get; } = source;
}

/// <summary>
/// The group "REIHENFOLGE FÜR METADATEN" of the tab "Metadaten" (#494): "Launcher-Daten" and "IGDB" in their order, each
/// switched on or off, moved as the artwork's sources (<see cref="SourceOrder{TSource}"/>). A change applies at once: the
/// fields are merged again from the cache, without a request. The info box of "Launcher-Daten" has a box per launcher
/// that delivers metadata, with its logo, name and what it delivers (#538).
/// </summary>
public sealed class MetadataSourceRows
{
    private readonly MetadataService _metadata;
    private readonly IgdbAccess? _igdb;
    private readonly SettingsChanges _changes;
    private readonly SourceOrder<MetadataSourceKind> _order;
    private readonly Dictionary<MetadataSourceKind, MetadataSourceRowViewModel> _sources = [];
    private readonly Func<LauncherInfo, LauncherBadge> _badgeOf;
    private IReadOnlyList<(LauncherInfo Launcher, ILauncherMetadata Source)> _shownProviders = [];

    /// <param name="orderChanged">The order or a lift changed: the category puts the rows anew.</param>
    /// <param name="badgeOf">A launcher's logo as its tab shows it.</param>
    public MetadataSourceRows(MetadataService metadata, SettingsChanges changes, Action orderChanged, Func<LauncherInfo, LauncherBadge> badgeOf)
    {
        _metadata = metadata;
        _badgeOf = badgeOf;
        _igdb = metadata.Igdb;
        _changes = changes;
        _order = new SourceOrder<MetadataSourceKind>(
            () => Settings.Order,
            order => Apply(s => s with { Order = order }, AppTexts.DataSourcesMetadataOrderGroup),
            () =>
            {
                Refresh();
                orderChanged();
            });
        foreach (var source in MetadataSettings.DefaultOrder)
        {
            _sources[source] = new MetadataSourceRowViewModel(SourceKey(source), NameOf(source), source, () => Settings.IsOn(source), on => Switch(source, on))
            {
                Group = AppTexts.DataSourcesMetadataOrderGroup,
                PanelTitle = NameOf(source).ToUpperInvariant(),
                Info = InfoOf(source, []),
                ValueInfos = [AppTexts.DataSourcesMetadataSourceOffInfo, AppTexts.DataSourcesSourceOnInfo],
            };
        }

        Refresh();
    }

    /// <summary>The sources in their order, a lifted one where it stands now.</summary>
    public IReadOnlyList<SettingsRowViewModel> Rows => [.. _order.Arrangement().Select(source => _sources[source])];

    public bool IsReordering => _order.IsLifted;

    public static string SourceKey(MetadataSourceKind source) => "metadata-source-" + source.ToString().ToLowerInvariant();

    private MetadataSettings Settings => _changes.Current.Metadata.WithValidValues();

    /// <summary>The access or the order changed; UI thread.</summary>
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

        var launcher = _sources[MetadataSourceKind.Launcher];
        ShowProviders(launcher);
        launcher.IsDimmed = !Settings.IsOn(MetadataSourceKind.Launcher);
        launcher.Subtitle = launcher.IsDimmed ? AppTexts.DataSourcesMetadataSourceOffSubtitle : AppTexts.DataSourcesLauncherDataSubtitle;
        var igdb = _sources[MetadataSourceKind.Igdb];
        var usable = _igdb?.IsUsable == true;
        igdb.IsDimmed = !usable || !Settings.IsOn(MetadataSourceKind.Igdb);
        igdb.Subtitle = !Settings.IsOn(MetadataSourceKind.Igdb) ? AppTexts.DataSourcesMetadataSourceOffSubtitle
            : usable ? AppTexts.DataSourcesIgdbSourceSubtitle : AppTexts.DataSourcesIgdbSkipped;
    }

    /// <summary>L3 on a source lifts it; while one is lifted, ▲ ▼ move it, L3 drops it, B puts it back.</summary>
    public bool Handle(UiAction action, SettingsRowViewModel? focused) =>
        _order.Handle(action, focused is MetadataSourceRowViewModel row ? row.Source : null);

    public void Lift(MetadataSourceRowViewModel row) => _order.Lift(row.Source);

    public void DragTo(int rowsAbove) => _order.PlaceAt(rowsAbove);

    public void Drop() => _order.Drop();

    public void PutBack() => _order.PutBack();

    private void Switch(MetadataSourceKind source, bool on)
    {
        if (_order.IsLifted)
        {
            return;
        }

        Apply(s => s with { Off = on ? [.. s.Off.Where(o => o != source)] : [.. s.Off.Where(o => o != source), source] }, NameOf(source));
        Refresh();
    }

    /// <summary>Saves the change and merges the fields again from the cache, without a request.</summary>
    private void Apply(Func<MetadataSettings, MetadataSettings> change, string what)
    {
        _changes.Apply(s => s with { Metadata = change(s.Metadata.WithValidValues()) }, what);
        _metadata.SettingsChanged();
    }

    /// <summary>The launchers that deliver metadata changed (a scan): "Launcher-Daten" shows a box for each.</summary>
    private void ShowProviders(MetadataSourceRowViewModel launcher)
    {
        var providers = _metadata.Providers;
        if (providers.Select(p => p.Launcher).SequenceEqual(_shownProviders.Select(p => p.Launcher)))
        {
            return;
        }

        _shownProviders = providers;
        launcher.Info = InfoOf(MetadataSourceKind.Launcher, providers);
    }

    private IReadOnlyList<InfoBlock> InfoOf(MetadataSourceKind source, IReadOnlyList<(LauncherInfo Launcher, ILauncherMetadata Source)> providers) =>
    [
        new InfoText(source == MetadataSourceKind.Igdb ? AppTexts.DataSourcesIgdbSourceInfo : AppTexts.DataSourcesLauncherDataInfo),
        .. providers.Select(p => new InfoLauncherBox(p.Launcher.DisplayName, _badgeOf(p.Launcher), [new InfoText(p.Source.Summary), new InfoText(p.Source.About)])),
        new InfoSection(AppTexts.DataSourcesOrderHeading, [new InfoText(AppTexts.DataSourcesMetadataOrderInfo), new InfoText(AppTexts.DataSourcesOrderMove)]),
        new InfoSection(AppTexts.DataSourcesWhenHeading, [new InfoText(AppTexts.DataSourcesMetadataOrderWhen)]),
    ];

    private static string NameOf(MetadataSourceKind source) => source == MetadataSourceKind.Igdb ? AppTexts.DataSourcesIgdb : AppTexts.DataSourcesLauncherData;
}
