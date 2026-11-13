using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.Core.Library;
using Arcadia.Core.Navigation;
using Arcadia.Core.UserData;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// "Eigene Tags" in "Datenquellen" (#495): "Neuer Tag" first, then every tag with the number of its games in the library
/// ("Couch-Koop · 8 Spiele", D4, D5). A shows a tag's games, X renames (another tag's name asks to merge, D6, D7), Y
/// deletes after asking, B returns to the settings. In a tag's games A opens a game's detail page, which turns through
/// these games only (D8); B returns to the tags.
/// </summary>
public sealed partial class TagListViewModel : OwnTagsDialogViewModel
{
    public TagListViewModel(IOwnTagsHost host)
        : base(host)
    {
        Refresh();
    }

    public static string Title => AppTexts.OwnTagsTitle;

    /// <summary>The games of the tag A opened; null on the list of tags.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsGames), nameof(ShowsTags), nameof(Subtitle))]
    public partial TagGamesViewModel? Games { get; private set; }

    public bool ShowsGames => Games is not null;

    public bool ShowsTags => Games is null;

    /// <summary>"12 Tags" over the tags, the tag's name over its games.</summary>
    public string Subtitle => Games?.TagName ?? (Entries.Count == 2 ? AppTexts.DetailTagsOne : AppTexts.OwnTagsCount(Formatting.Count(Entries.Count - 1)));

    public string AcceptHint => IsNewFocused ? AppTexts.TagsHintCreate : AppTexts.TagsHintGames;

    /// <summary>X and Y have nothing to act on with the focus on "Neuer Tag": their hints are greyed.</summary>
    public bool CanChangeFocused => !IsNewFocused;

    public override void Refresh()
    {
        Show();
        Games?.Refresh(GamesOf(Games.TagName));
    }

    /// <summary>Back from a game's detail page: the tag's games with the focus on the game shown last (D8).</summary>
    public void ReturnTo(string gameKey) => Games?.FocusGame(gameKey);

    protected override void HandleList(UiAction action)
    {
        if (Games is { } games)
        {
            HandleGames(games, action);
            return;
        }

        switch (action)
        {
            case UiAction.Back:
                Back();
                break;
            case UiAction.Up or UiAction.Down:
                Step(action);
                break;
            case UiAction.Accept:
                Activate();
                break;
            case UiAction.Search when !IsNewFocused:
                Rename(FocusedEntry.Name!);
                break;
            case UiAction.Status when !IsNewFocused:
                AskDelete(FocusedEntry.Name!, () => Show());
                break;
        }
    }

    protected override void Activate()
    {
        if (!IsNewFocused)
        {
            var name = FocusedEntry.Name!;
            Games = new TagGamesViewModel(name, GamesOf(name), Host.LauncherNameOf, Open);
            return;
        }

        // A tag without games (D4); an existing name takes the focus instead (D2).
        Write(AppTexts.TagsNew, string.Empty, typed =>
        {
            if (Host.Tags.Create(typed) is { } name)
            {
                Show(name);
            }
        });
    }

    protected override void Back()
    {
        if (Games is { } games)
        {
            Games = null;
            Show(games.TagName);
            return;
        }

        Host.Close(this);
    }

    protected override void OnFocusChanged()
    {
        base.OnFocusChanged();
        OnPropertyChanged(nameof(AcceptHint));
        OnPropertyChanged(nameof(CanChangeFocused));
    }

    /// <summary>A click on X's hint.</summary>
    [RelayCommand]
    private void RenameFocused() => HandleList(UiAction.Search);

    /// <summary>A click on A's hint.</summary>
    [RelayCommand]
    private void Accept() => HandleList(UiAction.Accept);

    private void HandleGames(TagGamesViewModel games, UiAction action)
    {
        switch (action)
        {
            case UiAction.Back:
                Back();
                break;
            case UiAction.Up or UiAction.Down:
                games.Step(action == UiAction.Up ? -1 : 1);
                RaiseFocusMoved();
                break;
            case UiAction.Accept:
                games.Open();
                break;
        }
    }

    /// <summary>
    /// X: the keyboard prefilled with the name. An unchanged or empty name changes nothing; another tag's name (ignoring
    /// case) asks whether to merge into it, which keeps that tag's name (D7).
    /// </summary>
    private void Rename(string name) => Write(AppTexts.TagsRename, name, typed =>
    {
        switch (Host.Tags.Rename(name, typed))
        {
            case TagRename.Renamed:
                Show(OwnTags.NameFrom(typed));
                break;
            case TagRename.NeedsMerge when Host.Tags.Find(OwnTags.NameFrom(typed)!) is { } target:
                Ask(
                    AppTexts.TagsMergeQuestion(target.Name),
                    AppTexts.TagsMergeText(from: name, into: target.Name),
                    AppTexts.TagsMergeConfirm,
                    () =>
                    {
                        Host.Tags.Merge(name, target.Name);
                        Show(target.Name);
                    });
                break;
        }
    });

    private void Show(string? focusName = null)
    {
        var library = Host.GamesInLibrary();
        ShowEntries(Host.Tags.All, _ => false, tag => GameCount(tag.Games.Count(library.ContainsKey)), focusName);
        OnPropertyChanged(nameof(Subtitle));
    }

    private static string GameCount(int count) => count == 1 ? AppTexts.GameCountOne : AppTexts.GameCountMany(Formatting.Count(count));

    /// <summary>The tag's games in the library, alphabetically by shown title (D3), each with its launcher.</summary>
    private IReadOnlyList<Game> GamesOf(string name)
    {
        if (Host.Tags.Find(name) is not { } tag)
        {
            return [];
        }

        var library = Host.GamesInLibrary();
        var titles = GameSorter.TitleComparer(Formatting.Culture);
        return [.. tag.Games.Select(key => library.GetValueOrDefault(key)).OfType<Game>().OrderBy(game => game.Title, titles).ThenBy(game => game.Key, StringComparer.Ordinal)];
    }

    private void Open(IReadOnlyList<Game> games, int index) => Host.ShowGame(this, games, index);
}

/// <summary>The games of one tag in the tag list (#495): A opens a game's detail page, a click too.</summary>
public sealed partial class TagGamesViewModel : ViewModelBase
{
    private readonly Func<Game, string> _launcherName;
    private readonly Action<IReadOnlyList<Game>, int> _open;
    private int _focus;

    public TagGamesViewModel(string tagName, IReadOnlyList<Game> games, Func<Game, string> launcherName, Action<IReadOnlyList<Game>, int> open)
    {
        TagName = tagName;
        _launcherName = launcherName;
        _open = open;
        Refresh(games);
    }

    public string TagName { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(HasGames))]
    public partial IReadOnlyList<TagGameViewModel> Items { get; private set; } = [];

    public IReadOnlyList<Game> Games { get; private set; } = [];

    /// <summary>"Kein Spiel hat diesen Tag." (D4).</summary>
    public bool IsEmpty => Items.Count == 0;

    public bool HasGames => Items.Count > 0;

    public static string EmptyText => AppTexts.TagsGamesNone;

    public int FocusIndex => _focus;

    /// <summary>A rescan or a tag change: the games anew, the focus on its game where it still is.</summary>
    public void Refresh(IReadOnlyList<Game> games)
    {
        var focused = _focus < Games.Count ? Games[_focus].Key : null;
        Games = games;
        Items = [.. games.Select(game => new TagGameViewModel(game.Title, _launcherName(game), Click))];
        var index = focused is null ? -1 : games.ToList().FindIndex(game => game.Key == focused);
        FocusAt(index >= 0 ? index : Math.Min(_focus, Math.Max(0, games.Count - 1)));
    }

    public void FocusGame(string gameKey)
    {
        var index = Games.ToList().FindIndex(game => game.Key == gameKey);
        if (index >= 0)
        {
            FocusAt(index);
        }
    }

    public void Step(int delta)
    {
        if (Items.Count > 0)
        {
            FocusAt(ListFocus.Cycle(_focus, delta, Items.Count, HeldPress.IsRepeat));
        }
    }

    public void Open()
    {
        if (Items.Count > 0)
        {
            _open(Games, _focus);
        }
    }

    private void FocusAt(int index)
    {
        _focus = index;
        for (var i = 0; i < Items.Count; i++)
        {
            Items[i].IsFocused = i == index;
        }
    }

    private void Click(TagGameViewModel item)
    {
        if (Items.ToList().IndexOf(item) is var index and >= 0)
        {
            FocusAt(index);
            Open();
        }
    }
}

/// <summary>One game of a tag: its shown title and its launcher.</summary>
public sealed partial class TagGameViewModel : ViewModelBase
{
    private readonly Action<TagGameViewModel> _click;

    public TagGameViewModel(string title, string launcherName, Action<TagGameViewModel> click)
    {
        Title = title;
        LauncherName = launcherName;
        _click = click;
    }

    public string Title { get; }

    public string LauncherName { get; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    [RelayCommand]
    private void Click() => _click(this);
}
