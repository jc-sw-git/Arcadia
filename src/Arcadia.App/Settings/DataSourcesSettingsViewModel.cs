using Arcadia.App.Input;
using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Metadata;
using Arcadia.Core.Settings;
using Avalonia.Threading;

namespace Arcadia.App.Settings;

/// <summary>
/// The category "Datenquellen" (#489): where covers and backgrounds come from. Groups "STEAMGRIDDB" (the key,
/// <see cref="SteamGridDbKeyRows"/>), "REIHENFOLGE FÜR ARTWORK" and "ARTWORK" (<see cref="ArtworkSourceRows"/>,
/// <see cref="ArtworkReloadRows"/>, <see cref="ArtworkNameRows"/>), "IGDB" (the access, <see cref="IgdbAccessRows"/>, #494),
/// "REIHENFOLGE FÜR METADATEN" (<see cref="MetadataSourceRows"/>) and "METADATEN" (<see cref="MetadataRows"/>, #493). A key
/// SteamGridDB or an access IGDB turned down is a warning beside the category's name.
/// </summary>
public sealed class DataSourcesSettingsViewModel : SettingsCategoryViewModel, IDraggableRows
{
    private readonly SteamGridDbKey _key;
    private readonly ArtworkService _artwork;
    private readonly SteamGridDbKeyRows _keyRows;
    private readonly ArtworkSourceRows _sourceRows;
    private readonly ArtworkReloadRows _reloadRows;
    private readonly ArtworkNameRows _nameRows;
    private readonly MetadataService? _metadata;
    private readonly MetadataRows? _metadataRows;
    private readonly IgdbAccessRows? _igdbRows;
    private readonly MetadataSourceRows? _metadataSourceRows;
    private bool _disposed;

    /// <param name="keyPersists">The key is kept across a restart (Windows' DPAPI); else only until Arcadia closes.</param>
    /// <param name="metadata">The games' metadata (#493); null: the group "METADATEN" is left out.</param>
    public DataSourcesSettingsViewModel(
        SteamGridDbKey key, ArtworkService artwork, IDataSourcesHost host, SettingsChanges changes, TimeProvider time, bool keyPersists, MetadataService? metadata = null)
        : base(AppTexts.DataSourcesTitle, "M4 6a8 3 0 1 0 16 0a8 3 0 1 0 -16 0 M4 6v6a8 3 0 0 0 16 0v-6 M4 12v6a8 3 0 0 0 16 0v-6")
    {
        _key = key;
        _artwork = artwork;
        _keyRows = new SteamGridDbKeyRows(
            key, artwork, host, editor => Editor = editor, time, keyPersists, () => changes.Current.Artwork.WithValidValues().IsOn(ArtworkSourceKind.SteamGridDb));
        _sourceRows = new ArtworkSourceRows(artwork, key, changes, Show);
        _reloadRows = new ArtworkReloadRows(artwork, changes, question => Dialog = question);
        _nameRows = new ArtworkNameRows(artwork, changes, host.Toast, host.OpenWaitingList);
        _metadata = metadata;
        _metadataRows = metadata is null ? null : new MetadataRows(metadata, question => Dialog = question);
        _igdbRows = metadata?.Igdb is { } igdb ? new IgdbAccessRows(igdb, metadata, host, editor => Editor = editor, time) : null;
        _metadataSourceRows = metadata is null ? null : new MetadataSourceRows(metadata, changes, Show);
        if (metadata is not null)
        {
            metadata.ProgressChanged += OnChanged;
            if (metadata.Igdb is { } access)
            {
                access.Changed += OnChanged;
            }
        }

        _key.Changed += OnChanged;
        _artwork.SearchChanged += OnChanged;
        _artwork.WaitingChanged += OnChanged;
        FocusMoved += (_, _) => ShowL3Action();
        Show();
    }

    /// <summary>L3 lifts a source of "Reihenfolge für Artwork" (#506); while one is lifted, ▲ ▼ move it and B puts it back.</summary>
    public override bool Handle(UiAction action) =>
        (_metadataSourceRows?.IsReordering != true && _sourceRows.Handle(action, FocusedRow))
        || (!_sourceRows.IsReordering && _metadataSourceRows?.Handle(action, FocusedRow) == true);

    public bool CanDrag(SettingsRowViewModel row) => row is ArtworkSourceRowViewModel or MetadataSourceRowViewModel;

    /// <summary>The rows the dragged one moves among: the other sources of its own order.</summary>
    public bool IsInDragList(SettingsRowViewModel row) =>
        _metadataSourceRows?.IsReordering == true ? row is MetadataSourceRowViewModel : row is ArtworkSourceRowViewModel;

    public void BeginDrag(SettingsRowViewModel row)
    {
        switch (row)
        {
            case ArtworkSourceRowViewModel source:
                Focus(source);
                _sourceRows.Lift(source);
                break;
            case MetadataSourceRowViewModel metadataSource when _metadataSourceRows is not null:
                Focus(metadataSource);
                _metadataSourceRows.Lift(metadataSource);
                break;
        }
    }

    public void DragTo(int rowsAbove)
    {
        if (_metadataSourceRows?.IsReordering == true)
        {
            _metadataSourceRows.DragTo(rowsAbove);
        }
        else
        {
            _sourceRows.DragTo(rowsAbove);
        }
    }

    public void Drop()
    {
        _sourceRows.Drop();
        _metadataSourceRows?.Drop();
    }

    /// <summary>"Alle Artworks neu laden?" or "Alle Metadaten neu laden?": [A] reloads, [B] closes it.</summary>
    public override void HandleDialog(UiAction action)
    {
        if (Dialog is SettingsQuestionViewModel question)
        {
            question.Handle(action);
        }
    }

    /// <summary>
    /// B while the key is written drops it (the editor takes B itself); another category puts a lifted source back; nothing
    /// else goes a level up here.
    /// </summary>
    public override void Leave()
    {
        Editor?.Drop();
        _sourceRows.PutBack();
        _metadataSourceRows?.PutBack();
    }

    private void ShowL3Action() => L3ActionText = FocusedRow is ArtworkSourceRowViewModel or MetadataSourceRowViewModel ? AppTexts.Move : null;

    public override void Dispose()
    {
        _disposed = true;
        Editor?.Drop();
        _key.Changed -= OnChanged;
        _artwork.SearchChanged -= OnChanged;
        _artwork.WaitingChanged -= OnChanged;
        if (_metadata is not null)
        {
            _metadata.ProgressChanged -= OnChanged;
            if (_metadata.Igdb is { } access)
            {
                access.Changed -= OnChanged;
            }
        }
    }

    /// <summary>From the run's thread or the key's: the rows follow on the UI thread while the category is shown.</summary>
    private void OnChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (!_disposed)
        {
            Show();
        }
    });

    private void Show()
    {
        _keyRows.Refresh();
        _sourceRows.Refresh();
        _reloadRows.Refresh();
        _nameRows.Refresh();
        _metadataRows?.Refresh();
        _igdbRows?.Refresh();
        _metadataSourceRows?.Refresh();
        IsReordering = _sourceRows.IsReordering || _metadataSourceRows?.IsReordering == true;
        Marker = _key.State == SteamGridDbKeyState.Rejected || _metadata?.Igdb?.State == Core.Metadata.Igdb.IgdbAccessState.Rejected ? HealthSeverity.Warning : null;
        SetRows([.. _keyRows.Rows, .. _sourceRows.OrderRows, _sourceRows.MissingRow, _reloadRows.ReloadRow, _sourceRows.CropRow, _reloadRows.OverwriteRow,
            _nameRows.SeveralHitsRow, _nameRows.WaitingRow, .. _igdbRows?.Rows ?? [], .. _metadataSourceRows?.Rows ?? [], .. _metadataRows?.Rows ?? []]);
    }
}
