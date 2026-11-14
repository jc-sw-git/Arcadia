using Arcadia.App.Keyboard;
using Arcadia.App.Settings;
using Arcadia.Core.UserData;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The window's part of own tags (#495): the tag window over the detail page or the library, the tag list over the
/// settings, a game's detail page opened from the list and back, and every page following a change of tags at once.
/// </summary>
public partial class MainViewModel : IOwnTagsHost
{
    // The tag list a detail page was opened from (D8): B on the page brings it back on the same tag's games.
    private TagListViewModel? _tagListBehindDetail;

    /// <summary>The player's own tags, kept in user-data.json beside the own titles.</summary>
    public OwnTags Tags { get; }

    /// <summary>"Tags" of one game, from its detail page or context menu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTagWindowOpen))]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    [NotifyPropertyChangedFor(nameof(OpenTextEditor))]
    public partial TagWindowViewModel? TagWindow { get; private set; }

    public bool IsTagWindowOpen => TagWindow is not null;

    /// <summary>"Eigene Tags" in "Datenquellen", over the settings.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTagListOpen))]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    [NotifyPropertyChangedFor(nameof(OpenTextEditor))]
    public partial TagListViewModel? TagList { get; private set; }

    public bool IsTagListOpen => TagList is not null;

    /// <summary>The open tag dialog, which takes every press before the page under it.</summary>
    private OwnTagsDialogViewModel? TagDialog => (OwnTagsDialogViewModel?)TagWindow ?? TagList;

    partial void OnTagWindowChanged(TagWindowViewModel? oldValue, TagWindowViewModel? newValue) => oldValue?.Dispose();

    // Not disposed while a detail page opened from it keeps it for the way back; it holds no keyboard then.
    partial void OnTagListChanged(TagListViewModel? oldValue, TagListViewModel? newValue)
    {
        if (oldValue != _tagListBehindDetail)
        {
            oldValue?.Dispose();
        }
    }

    /// <summary>What the detail page shows of a game: its metadata and own tags, own ones first (#493, #495).</summary>
    public DetailMetadata DetailInfoOf(string gameKey) => new(MetadataOf(gameKey), Tags.Of(gameKey));

    void IGameActionsOwner.EditTags(GameTileViewModel tile)
    {
        if (Tags.CanChange)
        {
            TagWindow = new TagWindowViewModel(tile, this);
        }
    }

    IReadOnlyList<string> IGameActionsOwner.OwnTagsOf(Game game) => Tags.Of(game.Key);

    void IDataSourcesHost.OpenOwnTags()
    {
        if (Tags.CanChange)
        {
            TagList = new TagListViewModel(this);
        }
    }

    IReadOnlyDictionary<string, Game> IOwnTagsHost.GamesInLibrary() =>
        Session.Libraries.SelectMany(l => l.Games).DistinctBy(g => g.Key).ToDictionary(g => g.Key, StringComparer.Ordinal);

    string IOwnTagsHost.LauncherNameOf(Game game) =>
        LauncherTabs.Items.FirstOrDefault(t => t.Launcher.Id == game.LauncherId)?.Launcher.DisplayName ?? game.LauncherId;

    OnScreenKeyboardViewModel IOwnTagsHost.TakeKeyboard(IKeyboardTarget editor) => SettingsKeyboard.Take(editor, Input.UsesController);

    void IOwnTagsHost.GiveBackKeyboard(IKeyboardTarget editor) => SettingsKeyboard.GiveBack(editor);

    void IOwnTagsHost.EditorChanged() => OnPropertyChanged(nameof(OpenTextEditor));

    /// <summary>B or the ✕: the tag window back to where it was opened; the tag list back to its row in "Datenquellen".</summary>
    void IOwnTagsHost.Close(OwnTagsDialogViewModel dialog)
    {
        if (dialog == TagWindow)
        {
            TagWindow = null;
        }
        else if (dialog == TagList)
        {
            TagList = null;
            Settings?.Selected?.FocusRow(DataSourcesSettingsViewModel.OwnTagsRowKey);
        }
    }

    /// <summary>
    /// A on a game in the tag list: its detail page in place of the settings, turning through the tag's games only; the
    /// game's own launcher tab isn't opened (D8). B on the page brings the list back (<see cref="CloseDetail"/>).
    /// </summary>
    void IOwnTagsHost.ShowGame(TagListViewModel list, IReadOnlyList<Game> games, int index)
    {
        _tagListBehindDetail = list;
        TagList = null;
        Settings = null;
        Detail = new GameDetailViewModel(null, games, index, null, CreateDetailTile, _actions, this, LauncherTabs, createViewOptions: null, _time, focus: null, DetailInfoOf);
    }

    /// <summary>B on a detail page opened from the tag list: "Datenquellen" with the list on the tag's games, the focus on the game shown last.</summary>
    private bool ReturnToTagList()
    {
        if (_tagListBehindDetail is not { } list)
        {
            return false;
        }

        var shown = Detail?.Current.Tile.Game.Key;
        Detail = null;
        ShowSettings(typeof(DataSourcesSettingsViewModel));
        Settings?.Selected?.FocusRow(DataSourcesSettingsViewModel.OwnTagsRowKey);
        TagList = list;
        _tagListBehindDetail = null;
        list.Refresh();
        if (shown is not null)
        {
            list.ReturnTo(shown);
        }

        return true;
    }

    /// <summary>
    /// The search closed over a detail page opened from the tag list: the page again, still turning through the tag's
    /// games, with B back to the list; the list itself if the game left the library meanwhile.
    /// </summary>
    private bool ReturnFromSearchToTagGame(GameDetailViewModel detail)
    {
        if (_tagListBehindDetail is null)
        {
            return false;
        }

        Detail = detail;
        if (!((IOwnTagsHost)this).GamesInLibrary().ContainsKey(detail.Current.Tile.Game.Key))
        {
            ReturnToTagList();
            return true;
        }

        ShowOwnTags();
        return true;
    }

    /// <summary>
    /// A tag was made, ticked, renamed, merged, deleted or put back after a failed write: the dialogs, the detail page's
    /// tags and its row "Tags" follow at once, without a scan.
    /// </summary>
    private void WatchOwnTags()
    {
        Tags.Changed += (_, _) => OnUiThread(ShowOwnTags);
        // A renamed, merged or deleted tag follows into every tab's saved filter (#466 case 17).
        Tags.Renamed += (_, renamed) => OnUiThread(() =>
        {
            SaveView(settings => settings.WithOwnTagRenamed(renamed.Name, renamed.To));
            Library.FactsChanged();
        });
    }

    private static void OnUiThread(Action action)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(action);
        }
    }

    private void ShowOwnTags()
    {
        _filterFacts.TagsChanged();
        Library.FactsChanged();
        TagWindow?.Refresh();
        TagList?.Refresh();
        _tagListBehindDetail?.Refresh();
        if (Detail is { } detail)
        {
            detail.ShowMetadata([detail.Current.Tile.Game.Key]);
            detail.RebuildActions();
        }

        Settings?.Categories.OfType<DataSourcesSettingsViewModel>().FirstOrDefault()?.OwnTagsChanged();
    }
}
