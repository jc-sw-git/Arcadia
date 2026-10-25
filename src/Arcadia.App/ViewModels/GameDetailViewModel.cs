using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The detail page (#11): a carousel through the games of a tab in the library's sort order, or
/// through the hits of the search (#12), the shown game in the middle and its neighbours cut off
/// at the sides. LT/RT turn it. B asks <see cref="IPageHost"/> to close it; ≡ and X are left to the window (#210).
/// </summary>
public sealed partial class GameDetailViewModel : ViewModelBase, IPage
{
    /// <summary>
    /// Pages kept on each side of the middle. One is seen; the second slides into view while
    /// the carousel turns.
    /// </summary>
    private const int Reach = 2;

    private readonly Func<Game, GameTileViewModel> _createTile;
    private readonly IGameActionHost _actions;
    private readonly IPageHost _host;
    private readonly LauncherTabsViewModel _tabs;
    private readonly Func<ViewOptionsViewModel?>? _createViewOptions;

    /// <param name="tabName">Null when the page was opened from the search; then it has no sort order either.</param>
    /// <param name="createViewOptions">The library's "Ansicht" dialog for Y; null from the search, whose hits keep their order.</param>
    public GameDetailViewModel(
        string? tabName,
        IReadOnlyList<Game> games,
        int index,
        string? sortLabel,
        Func<Game, GameTileViewModel> createTile,
        IGameActionHost actions,
        IPageHost host,
        LauncherTabsViewModel tabs,
        Func<ViewOptionsViewModel?>? createViewOptions)
    {
        TabName = tabName;
        _createTile = createTile;
        _actions = actions;
        _host = host;
        _tabs = tabs;
        _createViewOptions = createViewOptions;
        Games = games;
        Index = index;
        SortLabel = sortLabel;
        PreviousCommand = new RelayCommand(() => Rotate(-1));
        NextCommand = new RelayCommand(() => Rotate(1));
        BuildPages();
    }

    /// <summary>Raised after the carousel turned by one game (-1 back, 1 forward), so the view can slide it.</summary>
    public event Action<int>? Rotated;

    public string? TabName { get; }

    /// <summary>Opened from a library tab: LB/RB switch the tab and Y changes the sort.</summary>
    public bool IsFromLibrary => TabName is not null;

    public bool IsFromSearch => TabName is null;

    /// <summary>The tab's games in the library's sort order, or the search hits by relevance.</summary>
    public IReadOnlyList<Game> Games { get; private set; }

    /// <summary>Index of the shown game in <see cref="Games"/>.</summary>
    public int Index { get; private set; }

    public ObservableCollection<GameDetailPageViewModel> Pages { get; } = [];

    [ObservableProperty]
    public partial GameDetailPageViewModel Current { get; private set; } = null!;

    [ObservableProperty]
    public partial string? SortLabel { get; private set; }

    /// <summary>"3 von 31".</summary>
    [ObservableProperty]
    public partial string PositionText { get; private set; } = string.Empty;

    public bool HasNeighbours => Games.Count > 1;

    public IRelayCommand PreviousCommand { get; }

    public IRelayCommand NextCommand { get; }

    /// <summary>
    /// D-pad and A act on the buttons of the shown game, LT/RT turn the carousel, B closes the page. From a tab LB/RB switch
    /// it and Y changes the sort; the hits of the search belong to no tab and keep its order. False for ≡ and X.
    /// </summary>
    public bool Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Back:
                _host.Close(this);
                break;
            case UiAction.PreviousPage:
                Rotate(-1);
                break;
            case UiAction.NextPage:
                Rotate(1);
                break;
            case UiAction.PreviousTab when IsFromLibrary:
                _tabs.Cycle(-1);
                break;
            case UiAction.NextTab when IsFromLibrary:
                _tabs.Cycle(1);
                break;
            case UiAction.ViewOptions:
                OpenViewOptions();
                break;
            case UiAction.PreviousTab or UiAction.NextTab or UiAction.Options:
                // No context menu here: its actions are buttons on the page (owner, 2026-10-17).
                break;
            case UiAction.Settings or UiAction.Search:
                return false;
            default:
                Current.Handle(action);
                break;
        }

        return true;
    }

    /// <summary>Y or the sort button beside the position: the library's "Ansicht" dialog, from a tab only.</summary>
    [RelayCommand]
    private void OpenViewOptions()
    {
        if (_createViewOptions is { } create)
        {
            _host.ShowViewOptions(this, create);
        }
    }

    /// <summary>Its buttons act on the press; a held A on them is not a hold.</summary>
    public void Release(UiAction action)
    {
    }

    public void CancelHeldButtons()
    {
    }

    /// <summary>The games of the carousel, the ones at the sides included.</summary>
    public IEnumerable<GameTileViewModel> ShownTiles => Pages.Select(p => p.Tile);

    public void InputKindChanged(InputKind kind)
    {
    }

    /// <summary>
    /// Shows the next (1) or previous (-1) game. Only one page is built; the others move one place
    /// along, so the turn starts without delay.
    /// </summary>
    public void Rotate(int delta)
    {
        if (Games.Count < 2)
        {
            return;
        }

        Index = Carousel.At(Index, delta, Games.Count)!.Value;
        foreach (var page in Pages.ToList())
        {
            page.Offset -= delta;
            if (Math.Abs(page.Offset) > Reach)
            {
                Pages.Remove(page);
            }
        }

        Pages.Add(CreatePage(delta * Reach));
        Current = Pages.First(p => p.IsCentre);
        PositionText = Position();
        Rotated?.Invoke(delta);
    }

    /// <summary>
    /// Takes over a re-sorted or rescanned list. The shown game stays in the middle, its
    /// neighbours follow the new order. False if the list is empty and the page has nothing to show.
    /// </summary>
    public bool Update(IReadOnlyList<Game> games, string? sortLabel)
    {
        var index = Carousel.Find(games, Current.Tile.Game.Key, Index);
        if (index < 0)
        {
            return false;
        }

        var focusedKind = Current.FocusedKind;
        Games = games;
        Index = index;
        SortLabel = sortLabel;
        OnPropertyChanged(nameof(HasNeighbours));
        BuildPages();
        Current.FocusKind(focusedKind);
        return true;
    }

    /// <summary>The buttons depend on whether the Steam Controller mode is shown.</summary>
    public void RebuildActions()
    {
        foreach (var page in Pages)
        {
            page.RebuildActions();
        }
    }

    private void BuildPages()
    {
        Pages.Clear();
        for (var offset = -Reach; offset <= Reach; offset++)
        {
            if (Carousel.At(Index, offset, Games.Count) is not null)
            {
                Pages.Add(CreatePage(offset));
            }
        }

        Current = Pages.First(p => p.IsCentre);
        PositionText = Position();
    }

    private GameDetailPageViewModel CreatePage(int offset) =>
        new(_createTile(Games[Carousel.At(Index, offset, Games.Count)!.Value]), offset, _actions, Rotate);

    private string Position() => $"{Index + 1} von {Games.Count}";
}
