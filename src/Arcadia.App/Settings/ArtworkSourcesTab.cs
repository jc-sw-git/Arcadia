using Arcadia.App.Input;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Settings;

namespace Arcadia.App.Settings;

/// <summary>
/// The tab "Artwork" of "Datenquellen" (#489, #538): where covers and backgrounds come from. Groups "STEAMGRIDDB" (the
/// key, <see cref="SteamGridDbKeyRows"/>), "REIHENFOLGE FÜR ARTWORK" and "ARTWORK" (<see cref="ArtworkSourceRows"/>,
/// <see cref="ArtworkReloadRows"/>, the artwork's <see cref="SeveralHitsRows"/>).
/// </summary>
public sealed class ArtworkSourcesTab : IDataSourcesTab
{
    private readonly SteamGridDbKey _key;
    private readonly SteamGridDbKeyRows _keyRows;
    private readonly ArtworkSourceRows _sourceRows;
    private readonly ArtworkReloadRows _reloadRows;
    private readonly SeveralHitsRows _severalHits;

    /// <param name="showEditor">The key being written with the on-screen keyboard, or null when it closes.</param>
    /// <param name="ask">A question over the page ("Alle Artworks neu laden?"), or null to close it.</param>
    /// <param name="orderChanged">The order or a lift changed: the category puts the rows anew.</param>
    public ArtworkSourcesTab(
        SteamGridDbKey key, ArtworkService artwork, IDataSourcesHost host, SettingsChanges changes, TimeProvider time, bool keyPersists,
        Action<TextEditorViewModel?> showEditor, Action<SettingsQuestionViewModel?> ask, Action orderChanged)
    {
        _key = key;
        _keyRows = new SteamGridDbKeyRows(
            key, artwork, host, showEditor, time, keyPersists, () => changes.Current.Artwork.WithValidValues().IsOn(ArtworkSourceKind.SteamGridDb));
        _sourceRows = new ArtworkSourceRows(artwork, key, changes, orderChanged);
        _reloadRows = new ArtworkReloadRows(artwork, changes, ask);
        _severalHits = new SeveralHitsRows(WaitingKind.Artwork, artwork, changes, host.Toast, host.OpenWaitingList);
    }

    public string Name => AppTexts.DataSourcesArtworkTab;

    // A picture: frame, mountains and sun.
    public string Icon => "M3.5 5.5h17v13h-17z M3.5 15.5l5-5 4.5 4.5 2.5-2.5 5 5 M15 9a1.5 1.5 0 1 0 3 0a1.5 1.5 0 1 0 -3 0";

    public IReadOnlyList<SettingsRowViewModel> Rows { get; private set; } = [];

    public bool HasWarning => _key.State == SteamGridDbKeyState.Rejected;

    public bool IsReordering => _sourceRows.IsReordering;

    public void Refresh()
    {
        _keyRows.Refresh();
        _sourceRows.Refresh();
        _reloadRows.Refresh();
        _severalHits.Refresh();
        Rows =
        [
            .. _keyRows.Rows, .. _sourceRows.OrderRows, _sourceRows.MissingRow, _reloadRows.ReloadRow, _reloadRows.OverwriteRow,
            .. _severalHits.Rows,
        ];
    }

    /// <summary>L3 lifts a source of "Reihenfolge für Artwork" (#506); while one is lifted, ▲ ▼ move it and B puts it back.</summary>
    public bool Handle(UiAction action, SettingsRowViewModel? focused) => _sourceRows.Handle(action, focused);

    public bool CanDrag(SettingsRowViewModel row) => row is ArtworkSourceRowViewModel;

    public void BeginDrag(SettingsRowViewModel row)
    {
        if (row is ArtworkSourceRowViewModel source)
        {
            _sourceRows.Lift(source);
        }
    }

    public void DragTo(int rowsAbove) => _sourceRows.DragTo(rowsAbove);

    public void Drop() => _sourceRows.Drop();

    public void PutBack() => _sourceRows.PutBack();
}
