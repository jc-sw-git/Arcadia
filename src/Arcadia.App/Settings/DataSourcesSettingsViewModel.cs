using Arcadia.App.Input;
using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;
using Avalonia.Threading;

namespace Arcadia.App.Settings;

/// <summary>
/// The category "Datenquellen" (#489): where covers and backgrounds come from. Groups "STEAMGRIDDB" (the key,
/// <see cref="SteamGridDbKeyRows"/>), "REIHENFOLGE FÜR ARTWORK" and "ARTWORK" (<see cref="ArtworkSourceRows"/>,
/// <see cref="ArtworkReloadRows"/>, <see cref="ArtworkNameRows"/>); #43 adds
/// the metadata providers. A key SteamGridDB turned down is a warning beside the category's name.
/// </summary>
public sealed class DataSourcesSettingsViewModel : SettingsCategoryViewModel, IDraggableRows
{
    private readonly SteamGridDbKey _key;
    private readonly ArtworkService _artwork;
    private readonly SteamGridDbKeyRows _keyRows;
    private readonly ArtworkSourceRows _sourceRows;
    private readonly ArtworkReloadRows _reloadRows;
    private readonly ArtworkNameRows _nameRows;
    private bool _disposed;

    /// <param name="keyPersists">The key is kept across a restart (Windows' DPAPI); else only until Arcadia closes.</param>
    public DataSourcesSettingsViewModel(SteamGridDbKey key, ArtworkService artwork, IDataSourcesHost host, SettingsChanges changes, TimeProvider time, bool keyPersists)
        : base(AppTexts.DataSourcesTitle, "M4 6a8 3 0 1 0 16 0a8 3 0 1 0 -16 0 M4 6v6a8 3 0 0 0 16 0v-6 M4 12v6a8 3 0 0 0 16 0v-6")
    {
        _key = key;
        _artwork = artwork;
        _keyRows = new SteamGridDbKeyRows(
            key, artwork, host, editor => Editor = editor, time, keyPersists, () => changes.Current.Artwork.WithValidValues().IsOn(ArtworkSourceKind.SteamGridDb));
        _sourceRows = new ArtworkSourceRows(artwork, key, changes, Show);
        _reloadRows = new ArtworkReloadRows(artwork, changes, question => Dialog = question);
        _nameRows = new ArtworkNameRows(artwork, changes, host.Toast, host.OpenWaitingList);
        _key.Changed += OnChanged;
        _artwork.SearchChanged += OnChanged;
        _artwork.WaitingChanged += OnChanged;
        FocusMoved += (_, _) => ShowL3Action();
        Show();
    }

    /// <summary>L3 lifts a source of "Reihenfolge für Artwork" (#506); while one is lifted, ▲ ▼ move it and B puts it back.</summary>
    public override bool Handle(UiAction action) => _sourceRows.Handle(action, FocusedRow);

    public bool CanDrag(SettingsRowViewModel row) => row is ArtworkSourceRowViewModel;

    public bool IsInDragList(SettingsRowViewModel row) => row is ArtworkSourceRowViewModel;

    public void BeginDrag(SettingsRowViewModel row)
    {
        if (row is ArtworkSourceRowViewModel source)
        {
            Focus(source);
            _sourceRows.Lift(source);
        }
    }

    public void DragTo(int rowsAbove) => _sourceRows.DragTo(rowsAbove);

    public void Drop() => _sourceRows.Drop();

    /// <summary>"Alle Artworks neu laden?": [A] reloads, [B] closes it.</summary>
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
    }

    private void ShowL3Action() => L3ActionText = FocusedRow is ArtworkSourceRowViewModel ? AppTexts.Move : null;

    public override void Dispose()
    {
        _disposed = true;
        Editor?.Drop();
        _key.Changed -= OnChanged;
        _artwork.SearchChanged -= OnChanged;
        _artwork.WaitingChanged -= OnChanged;
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
        IsReordering = _sourceRows.IsReordering;
        Marker = _key.State == SteamGridDbKeyState.Rejected ? HealthSeverity.Warning : null;
        SetRows([.. _keyRows.Rows, .. _sourceRows.OrderRows, _sourceRows.MissingRow, _reloadRows.ReloadRow, _sourceRows.CropRow, _reloadRows.OverwriteRow,
            _nameRows.SeveralHitsRow, _nameRows.WaitingRow]);
    }
}
