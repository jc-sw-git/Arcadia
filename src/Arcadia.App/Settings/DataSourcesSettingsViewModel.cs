using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Avalonia.Threading;

namespace Arcadia.App.Settings;

/// <summary>
/// The category "Datenquellen" (#489): where covers and backgrounds come from. Groups "STEAMGRIDDB" (the key,
/// <see cref="SteamGridDbKeyRows"/>), "REIHENFOLGE FÜR ARTWORK" and "ARTWORK" (<see cref="ArtworkSourceRows"/>); #43 adds
/// the metadata providers. A key SteamGridDB turned down is a warning beside the category's name.
/// </summary>
public sealed class DataSourcesSettingsViewModel : SettingsCategoryViewModel
{
    private readonly SteamGridDbKey _key;
    private readonly ArtworkService _artwork;
    private readonly SteamGridDbKeyRows _keyRows;
    private readonly ArtworkSourceRows _sourceRows;
    private bool _disposed;

    /// <param name="keyPersists">The key is kept across a restart (Windows' DPAPI); else only until Arcadia closes.</param>
    public DataSourcesSettingsViewModel(SteamGridDbKey key, ArtworkService artwork, IDataSourcesHost host, SettingsChanges changes, TimeProvider time, bool keyPersists)
        : base(AppTexts.DataSourcesTitle, "M4 6a8 3 0 1 0 16 0a8 3 0 1 0 -16 0 M4 6v6a8 3 0 0 0 16 0v-6 M4 12v6a8 3 0 0 0 16 0v-6")
    {
        _key = key;
        _artwork = artwork;
        _keyRows = new SteamGridDbKeyRows(key, artwork, host, editor => Editor = editor, time, keyPersists);
        _sourceRows = new ArtworkSourceRows(artwork, key, changes, Show);
        _key.Changed += OnChanged;
        _artwork.SearchChanged += OnChanged;
        Show();
    }

    /// <summary>B while the key is written drops it (the editor takes B itself); nothing else goes a level up here.</summary>
    public override void Leave() => Editor?.Drop();

    public override void Dispose()
    {
        _disposed = true;
        Editor?.Drop();
        _key.Changed -= OnChanged;
        _artwork.SearchChanged -= OnChanged;
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
        Marker = _key.State == SteamGridDbKeyState.Rejected ? HealthSeverity.Warning : null;
        SetRows([.. _keyRows.Rows, .. _sourceRows.Rows]);
    }
}
