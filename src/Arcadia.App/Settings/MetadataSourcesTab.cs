using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Metadata;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Core.UserData;

namespace Arcadia.App.Settings;

/// <summary>
/// The tab "Metadaten" of "Datenquellen" (#538): "IGDB" (the access, <see cref="IgdbAccessRows"/>, #494), "REIHENFOLGE FÜR
/// METADATEN" (<see cref="MetadataSourceRows"/>), "METADATEN" (<see cref="MetadataRows"/> and the metadata's own
/// <see cref="SeveralHitsRows"/>) and "EIGENE DATEN" ("Eigene Tags", #495; "Eigene Titel" while user-data.json was
/// unreadable at the start, #496).
/// </summary>
public sealed class MetadataSourcesTab : IDataSourcesTab
{
    private readonly MetadataService _metadata;
    private readonly IgdbAccessRows? _igdbRows;
    private readonly MetadataSourceRows _sourceRows;
    private readonly MetadataRows _metadataRows;
    private readonly SeveralHitsRows _severalHits;
    private readonly OwnTags? _ownTags;
    private readonly ActionRowViewModel? _ownTagsRow;
    private readonly InfoRowViewModel? _userDataRow;

    /// <param name="showEditor">A value of the access being written with the on-screen keyboard, or null when it closes.</param>
    /// <param name="ask">A question over the page ("Alle Metadaten neu laden?"), or null to close it.</param>
    /// <param name="orderChanged">The order or a lift changed: the category puts the rows anew.</param>
    /// <param name="ownTags">The player's own tags (#495); null: the row "Eigene Tags" is left out.</param>
    public MetadataSourcesTab(
        MetadataService metadata, ArtworkService artwork, IDataSourcesHost host, SettingsChanges changes, TimeProvider time,
        Action<TextEditorViewModel?> showEditor, Action<SettingsQuestionViewModel?> ask, Action orderChanged, bool userDataUnreadable, OwnTags? ownTags)
    {
        _metadata = metadata;
        _igdbRows = metadata.Igdb is { } igdb ? new IgdbAccessRows(igdb, metadata, host, showEditor, time) : null;
        _sourceRows = new MetadataSourceRows(metadata, changes, orderChanged, host.BadgeOf);
        _metadataRows = new MetadataRows(metadata, ask);
        _severalHits = new SeveralHitsRows(WaitingKind.Metadata, artwork, changes, host.Toast, host.OpenWaitingList, metadata);
        _userDataRow = userDataUnreadable ? UserDataRow() : null;
        _ownTags = ownTags;
        _ownTagsRow = ownTags is null ? null : new ActionRowViewModel(DataSourcesSettingsViewModel.OwnTagsRowKey, string.Empty, host.OpenOwnTags)
        {
            Group = AppTexts.DataSourcesUserDataGroup,
            Subtitle = AppTexts.DataSourcesOwnTagsSubtitle,
            ActionText = AppTexts.DataSourcesOwnTagsAction,
            Info = [new InfoText(AppTexts.DataSourcesOwnTagsInfo)],
            DimmedNote = AppTexts.UserDataUnreadable,
        };
    }

    public string Name => AppTexts.DataSourcesMetadataTab;

    // A page with lines of text and a folded corner.
    public string Icon => "M6 2.5h8.5l4 4v15h-12.5z M14 2.5v4.5h4.5 M9 11.5h6.5 M9 15h6.5 M9 18.5h4";

    public IReadOnlyList<SettingsRowViewModel> Rows { get; private set; } = [];

    public bool HasWarning => _metadata.Igdb?.State == IgdbAccessState.Rejected || _userDataRow is not null;

    public bool IsReordering => _sourceRows.IsReordering;

    public void Refresh()
    {
        _igdbRows?.Refresh();
        _sourceRows.Refresh();
        _metadataRows.Refresh();
        _severalHits.Refresh();
        if (_ownTags is not null && _ownTagsRow is not null)
        {
            // While user-data.json is unreadable the row is greyed with the reason, so nothing typed is lost (D9).
            // Unreadable, the number would claim there are none: the row shows none.
            _ownTagsRow.Title = _ownTags.CanChange ? AppTexts.DataSourcesOwnTags(Formatting.Count(_ownTags.All.Count)) : AppTexts.OwnTagsTitle;
            _ownTagsRow.IsEnabled = _ownTags.CanChange;
            _ownTagsRow.IsDimmed = !_ownTags.CanChange;
        }

        Rows =
        [
            .. _igdbRows?.Rows ?? [], .. _sourceRows.Rows, .. _metadataRows.Rows, .. _severalHits.Rows,
            .. _ownTagsRow is null ? Array.Empty<SettingsRowViewModel>() : [_ownTagsRow],
            .. _userDataRow is null ? Array.Empty<SettingsRowViewModel>() : [_userDataRow],
        ];
    }

    /// <summary>L3 lifts a source of "Reihenfolge für Metadaten"; while one is lifted, ▲ ▼ move it and B puts it back.</summary>
    public bool Handle(UiAction action, SettingsRowViewModel? focused) => _sourceRows.Handle(action, focused);

    public bool CanDrag(SettingsRowViewModel row) => row is MetadataSourceRowViewModel;

    public void BeginDrag(SettingsRowViewModel row)
    {
        if (row is MetadataSourceRowViewModel source)
        {
            _sourceRows.Lift(source);
        }
    }

    public void DragTo(int rowsAbove) => _sourceRows.DragTo(rowsAbove);

    public void Drop() => _sourceRows.Drop();

    public void PutBack() => _sourceRows.PutBack();

    // The file is read only at the start, so the row never changes while Arcadia runs.
    private static InfoRowViewModel UserDataRow() => new(DataSourcesSettingsViewModel.UserDataRowKey, AppTexts.DataSourcesUserData)
    {
        Group = AppTexts.DataSourcesUserDataGroup,
        Subtitle = AppTexts.DataSourcesUserDataUnreadable,
        Severity = HealthSeverity.Warning,
        Info = [new InfoText(AppTexts.DataSourcesUserDataInfo)],
    };
}
