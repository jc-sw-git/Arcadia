using Arcadia.App.Input;
using Arcadia.ControllerSupport;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Metadata;
using Arcadia.Core.UserData;
using Avalonia.Threading;

namespace Arcadia.App.Settings;

/// <summary>
/// The category "Datenquellen" (#489) with two tabs above its rows (#538): "Artwork" (<see cref="ArtworkSourcesTab"/>) and
/// "Metadaten" (<see cref="MetadataSourcesTab"/>); LT/RT (Q/E) or a click switch them, and the category opens on
/// "Artwork". Only the chosen tab's rows are shown, so opening the category draws half as many. A key SteamGridDB or an
/// access IGDB turned down is a warning beside the category's name and beside its tab's.
/// </summary>
public sealed class DataSourcesSettingsViewModel : SettingsCategoryViewModel, IDraggableRows
{
    private readonly SteamGridDbKey _key;
    private readonly ArtworkService _artwork;
    private readonly MetadataService? _metadata;
    private readonly IReadOnlyList<DataSourcesTabViewModel> _tabs;
    private IDataSourcesTab _shown;
    private bool _disposed;

    /// <summary>The row "Eigene Titel und Tags" (#496), there only while user-data.json was unreadable at the start; the status entry opens it.</summary>
    public const string UserDataRowKey = "user-data";

    /// <summary>The row "Eigene Tags (N)" (#495), which opens the tag list.</summary>
    public const string OwnTagsRowKey = "own-tags";

    /// <param name="keyPersists">The key is kept across a restart (Windows' DPAPI); else only until Arcadia closes.</param>
    /// <param name="metadata">The games' metadata (#493); null: only the tab "Artwork", without tabs above it.</param>
    /// <param name="ownTags">The player's own tags (#495); null: the row "Eigene Tags" is left out.</param>
    public DataSourcesSettingsViewModel(
        SteamGridDbKey key, ArtworkService artwork, IDataSourcesHost host, SettingsChanges changes, TimeProvider time, bool keyPersists, MetadataService? metadata = null,
        bool userDataUnreadable = false, OwnTags? ownTags = null)
        : base(AppTexts.DataSourcesTitle, "M4 6a8 3 0 1 0 16 0a8 3 0 1 0 -16 0 M4 6v6a8 3 0 0 0 16 0v-6 M4 12v6a8 3 0 0 0 16 0v-6")
    {
        _key = key;
        _artwork = artwork;
        _metadata = metadata;
        IDataSourcesTab artworkTab = new ArtworkSourcesTab(key, artwork, host, changes, time, keyPersists, editor => Editor = editor, question => Dialog = question, Show);
        IReadOnlyList<IDataSourcesTab> tabs = metadata is null
            ? [artworkTab]
            : [artworkTab, new MetadataSourcesTab(metadata, artwork, host, changes, time, editor => Editor = editor, question => Dialog = question, Show, userDataUnreadable, ownTags)];
        _tabs = [.. tabs.Select(tab => new DataSourcesTabViewModel(tab, Choose))];
        _shown = artworkTab;
        if (_tabs.Count > 1)
        {
            Head = new DataSourcesHeadViewModel(_tabs);
            TurnText = AppTexts.DataSourcesTabTurn;
        }

        if (metadata is not null)
        {
            metadata.ProgressChanged += OnChanged;
            metadata.WaitingChanged += OnChanged;
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

    /// <summary>The tab whose rows are shown.</summary>
    public IDataSourcesTab ShownTab => _shown;

    public IReadOnlyList<IDataSourcesTab> Tabs => [.. _tabs.Select(t => t.Tab)];

    /// <summary>
    /// A lifted source takes every press until it is put down (L3, ▲ ▼, B); else LT/RT switch the tab and L3 lifts a source
    /// of the tab's order (#506).
    /// </summary>
    public override bool Handle(UiAction action)
    {
        if (!_shown.IsReordering && action is UiAction.PreviousPage or UiAction.NextPage && _tabs.Count > 1)
        {
            Turn(action == UiAction.NextPage ? 1 : -1);
            return true;
        }

        return _shown.Handle(action, FocusedRow);
    }

    public bool CanDrag(SettingsRowViewModel row) => _shown.CanDrag(row);

    /// <summary>The rows the dragged one moves among: the other sources of the shown tab's order.</summary>
    public bool IsInDragList(SettingsRowViewModel row) => _shown.CanDrag(row);

    public void BeginDrag(SettingsRowViewModel row)
    {
        if (_shown.CanDrag(row))
        {
            Focus(row);
            _shown.BeginDrag(row);
        }
    }

    public void DragTo(int rowsAbove) => _shown.DragTo(rowsAbove);

    public void Drop() => _shown.Drop();

    /// <summary>"Alle Artworks neu laden?" or "Alle Metadaten neu laden?": [A] reloads, [B] closes it.</summary>
    public override void HandleDialog(UiAction action)
    {
        if (Dialog is SettingsQuestionViewModel question)
        {
            question.Handle(action);
        }
    }

    /// <summary>
    /// B while a value is written drops it (the editor takes B itself); another category puts a lifted source back; nothing
    /// else goes a level up here.
    /// </summary>
    public override void Leave()
    {
        Editor?.Drop();
        foreach (var tab in _tabs)
        {
            tab.Tab.PutBack();
        }
    }

    /// <summary>The category's tab is chosen: it opens on "Artwork", at its first row.</summary>
    public override void ShowTopLevel()
    {
        ShowTab(_tabs[0].Tab);
        base.ShowTopLevel();
    }

    /// <summary>A row of either tab, e.g. the IGDB access from the status modal: its tab is shown first.</summary>
    public override void FocusRow(string key)
    {
        if (!_shown.Rows.Any(r => r.Key == key) && _tabs.FirstOrDefault(t => t.Tab.Rows.Any(r => r.Key == key)) is { } owner)
        {
            ShowTab(owner.Tab);
        }

        base.FocusRow(key);
    }

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
            _metadata.WaitingChanged -= OnChanged;
            if (_metadata.Igdb is { } access)
            {
                access.Changed -= OnChanged;
            }
        }
    }

    /// <summary>A tag was made, renamed, merged or deleted (#495): "Eigene Tags (N)" follows; UI thread.</summary>
    public void OwnTagsChanged()
    {
        if (!_disposed)
        {
            Show();
        }
    }

    private void ShowL3Action() => L3ActionText = FocusedRow is not null && _shown.CanDrag(FocusedRow) ? AppTexts.Move : null;

    /// <summary>LT/RT: the other tab, at its first row; a value being written or a lifted source can't be left this way.</summary>
    private void Turn(int delta)
    {
        var index = _tabs.ToList().FindIndex(t => t.Tab == _shown);
        Choose(_tabs[(index + delta + _tabs.Count) % _tabs.Count].Tab);
    }

    /// <summary>A tab chosen by LT/RT or a click: its rows from the first one.</summary>
    private void Choose(IDataSourcesTab tab)
    {
        if (tab == _shown || IsEditing || _shown.IsReordering)
        {
            return;
        }

        ShowTab(tab);
        base.ShowTopLevel();
    }

    private void ShowTab(IDataSourcesTab tab)
    {
        if (tab == _shown)
        {
            return;
        }

        _shown.PutBack();
        _shown = tab;
        Show();
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
        foreach (var tab in _tabs)
        {
            tab.Tab.Refresh();
            tab.IsSelected = tab.Tab == _shown;
            tab.HasWarning = tab.Tab.HasWarning;
        }

        IsReordering = _shown.IsReordering;
        Marker = _tabs.Any(t => t.HasWarning) ? HealthSeverity.Warning : null;
        SetRows(_shown.Rows);
    }
}
