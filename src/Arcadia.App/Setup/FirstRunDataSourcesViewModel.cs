using Arcadia.App.Settings;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Metadata;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Core.Settings;
using Avalonia.Threading;

namespace Arcadia.App.Setup;

/// <summary>
/// The first start's step "Datenquellen" (#497), after the launchers: the SteamGridDB key and the IGDB access with the same
/// rows as the settings (<see cref="SteamGridDbKeyRows"/>, <see cref="IgdbAccessRows"/>), then "Überspringen" (or
/// "Fertig" once a key or access is there). It is shown as the settings page with this one category, so typing, pasting,
/// the info panel and the browser pages work exactly as there. B goes back to the launcher page; ≡, Enter or the last row
/// end it. A key taken here starts its fetch at once, in the background; ending the step starts nothing again.
/// </summary>
public sealed class FirstRunDataSourcesViewModel : SettingsCategoryViewModel
{
    public const string FinishRowKey = "first-run-finish";

    private readonly SteamGridDbKey _key;
    private readonly ArtworkService _artwork;
    private readonly IgdbAccess? _igdb;
    private readonly SteamGridDbKeyRows _keyRows;
    private readonly IgdbAccessRows? _igdbRows;
    private readonly ActionRowViewModel _finish;
    private readonly Action _back;
    private bool _disposed;

    /// <param name="keyPersists">The key is kept across a restart (Windows' DPAPI); else only until Arcadia closes.</param>
    /// <param name="metadata">The games' metadata (#493); without IGDB in it the group "IGDB" is left out.</param>
    /// <param name="back">B: back to the launcher page.</param>
    /// <param name="finish">≡, Enter or the last row: the first start ends and the library shows.</param>
    public FirstRunDataSourcesViewModel(
        SteamGridDbKey key, ArtworkService artwork, IDataSourcesHost host, SettingsChanges changes, TimeProvider time, bool keyPersists, MetadataService? metadata,
        Action back, Action finish)
        : base(AppTexts.DataSourcesTitle, "M4 6a8 3 0 1 0 16 0a8 3 0 1 0 -16 0 M4 6v6a8 3 0 0 0 16 0v-6 M4 12v6a8 3 0 0 0 16 0v-6")
    {
        _key = key;
        _artwork = artwork;
        _back = back;
        _keyRows = new SteamGridDbKeyRows(
            key, artwork, host, editor => Editor = editor, time, keyPersists, () => changes.Current.Artwork.WithValidValues().IsOn(ArtworkSourceKind.SteamGridDb));
        _igdb = metadata?.Igdb;
        _igdbRows = metadata is { Igdb: { } igdb } ? new IgdbAccessRows(igdb, metadata, host, editor => Editor = editor, time) : null;
        _finish = new ActionRowViewModel(FinishRowKey, AppTexts.FirstRunSkip, finish)
        {
            IsPrimary = true,
            ShortcutPad = "≡",
            ShortcutKeys = "Enter",
        };
        _key.Changed += OnChanged;
        if (_igdb is not null)
        {
            _igdb.Changed += OnChanged;
        }

        Show();
    }

    /// <summary>What ≡ does here, for the footer: "Überspringen", or "Fertig" once a key or access is there.</summary>
    public string FinishText => _finish.Title;

    /// <summary>B on the rows (the editor takes B itself while a value is typed): back to the launcher page.</summary>
    public override bool Back()
    {
        _back();
        return true;
    }

    public override void Leave() => Editor?.Drop();

    public override void Dispose()
    {
        _disposed = true;
        Editor?.Drop();
        _key.Changed -= OnChanged;
        if (_igdb is not null)
        {
            _igdb.Changed -= OnChanged;
        }
    }

    /// <summary>From the key's or the access's thread: the rows follow on the UI thread while the step is shown.</summary>
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
        _igdbRows?.Refresh();
        var hasAny = _key.State != SteamGridDbKeyState.None || _igdb is { State: not IgdbAccessState.None };
        _finish.Title = hasAny ? AppTexts.Done : AppTexts.FirstRunSkip;
        _finish.Subtitle = hasAny ? AppTexts.FirstRunDataSourcesDoneSubtitle : AppTexts.FirstRunSkipSubtitle;
        _finish.ActionText = _finish.Title;
        _finish.Info =
        [
            new InfoText(hasAny ? AppTexts.FirstRunDataSourcesDoneInfo : AppTexts.FirstRunDataSourcesWithoutInfo),
            new InfoSection(AppTexts.FirstRunDataSourcesLaterHeading, [new InfoText(AppTexts.FirstRunDataSourcesLaterInfo)]),
        ];
        OnPropertyChanged(nameof(FinishText));
        Marker = _key.State == SteamGridDbKeyState.Rejected || _igdb?.State == IgdbAccessState.Rejected ? ControllerSupport.HealthSeverity.Warning : null;
        SetRows([.. _keyRows.Rows, .. _igdbRows?.Rows ?? [], _finish]);
    }
}

