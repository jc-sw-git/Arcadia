using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.Core.Library;
using Arcadia.Core.Metadata;
using Arcadia.Core.Navigation;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The detail page (#11, #499): a carousel through the games of a tab in the library's sort order, or through the hits of
/// the search (#12). The shown game's cover stands in front beside its title, the games before and after it in stacks to
/// the sides (<see cref="Covers"/>); LT/RT turn it and the shown game's page (<see cref="Current"/>) keeps the open area
/// and the focus. B asks <see cref="IPageHost"/> to close it; ≡ and X are left to the window (#210).
/// </summary>
public sealed partial class GameDetailViewModel : ViewModelBase, IPage
{
    private readonly TurnJump _jump;
    private readonly Func<Game, GameTileViewModel> _createTile;
    private readonly IGameActionHost _actions;
    private readonly IPageHost _host;
    private readonly LauncherTabsViewModel _tabs;
    private readonly Func<ViewOptionsViewModel?>? _createViewOptions;
    private readonly Func<string, MergedMetadata> _metadataOf;

    // A held LT/RT turns only the covers; the page follows on release (#499).
    private bool _pageBehind;

    /// <param name="tabName">Null when the page was opened from the search; then it has no sort order either.</param>
    /// <param name="createViewOptions">The library's "Ansicht" dialog for L3; null from the search, whose hits keep their order.</param>
    /// <param name="focus">The focus of the page this one replaces (another tab, #499); null starts on "Spielen".</param>
    /// <param name="metadataOf">The metadata of a game by its key (#493); none without it.</param>
    public GameDetailViewModel(
        string? tabName,
        IReadOnlyList<Game> games,
        int index,
        string? sortLabel,
        Func<Game, GameTileViewModel> createTile,
        IGameActionHost actions,
        IPageHost host,
        LauncherTabsViewModel tabs,
        Func<ViewOptionsViewModel?>? createViewOptions,
        TimeProvider time,
        DetailFocus? focus = null,
        Func<string, MergedMetadata>? metadataOf = null)
    {
        _metadataOf = metadataOf ?? (_ => MergedMetadata.None);
        TabName = tabName;
        _jump = new TurnJump(time, JumpTime);
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
        BackCommand = new RelayCommand(() => Handle(UiAction.Back));
        BuildCovers(focus ?? DetailFocus.Start);
    }

    /// <summary>How long title and content take to jump in after a turn (#499); the covers move as long.</summary>
    public static TimeSpan JumpTime { get; } = TimeSpan.FromMilliseconds(420);

    /// <summary>
    /// Raised after the shown game changed when title and content should jump in, with the token the next turn cancels
    /// (#506): a quick turn raises nothing and stops the running jump, so the page shows the newest game at once.
    /// </summary>
    public event Action<CancellationToken>? Jumped;

    /// <summary>RS or Shift+W/S: the view scrolls the info box, -1 up, 1 down (#506); in "Infos" ▲ ▼ too, its text (#493).</summary>
    public event Action<int>? InfoScrollRequested;

    /// <summary>
    /// Set by the view: the focus is on a row and its info box has more than it shows, so RS scrolls it and the footer
    /// names RS. In the rail and the button row the box isn't the focused thing's, so RS does nothing there.
    /// </summary>
    [ObservableProperty]
    public partial bool InfoOverflows { get; set; }

    public string? TabName { get; }

    /// <summary>Opened from a library tab: LB/RB switch the tab and L3 changes the sort.</summary>
    public bool IsFromLibrary => TabName is not null;

    public bool IsFromSearch => TabName is null;

    /// <summary>The tab's games in the library's sort order, or the search hits by relevance.</summary>
    public IReadOnlyList<Game> Games { get; private set; }

    /// <summary>Index of the shown game in <see cref="Games"/>.</summary>
    public int Index { get; private set; }

    /// <summary>The covers of the shown game and of its neighbours in the stacks.</summary>
    public ObservableCollection<DetailCoverViewModel> Covers { get; } = [];

    /// <summary>The shown game's page: title, buttons, areas. While LT/RT are held it keeps the game shown before.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Previous), nameof(Next))]
    public partial GameDetailPageViewModel Current { get; private set; } = null!;

    /// <summary>
    /// How many covers each stack shows: two with five games or more, one with two to four (two games: the other on both
    /// sides), none for a single game. More would show a game twice.
    /// </summary>
    public int StackDepth => Games.Count >= 5 ? 2 : Games.Count >= 2 ? 1 : 0;

    /// <summary>The game before the shown one, named under the left stack; null without neighbours.</summary>
    public GameTileViewModel? Previous => Neighbour(-1);

    public GameTileViewModel? Next => Neighbour(1);

    [ObservableProperty]
    public partial string? SortLabel { get; private set; }

    /// <summary>"3 von 31".</summary>
    [ObservableProperty]
    public partial string PositionText { get; private set; } = string.Empty;

    public bool HasNeighbours => Games.Count > 1;

    /// <summary>A held button stops when the focus moves between buttons, rail and rows (#476).</summary>
    object? IPage.Layout => Current.Zone;

    public IRelayCommand PreviousCommand { get; }

    public IRelayCommand NextCommand { get; }

    /// <summary>A click on the footer's B hint does what B does: from the rows to the rail, else back to where the page was opened from.</summary>
    public IRelayCommand BackCommand { get; }

    /// <summary>
    /// D-pad and A act on the buttons of the shown game, LT/RT turn the carousel, B closes the page. From a tab LB/RB switch
    /// it and L3 changes the sort; the hits of the search belong to no tab and keep its order. False for ≡ and X.
    /// </summary>
    public bool Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Back:
                if (!Current.Handle(action))
                {
                    _host.Close(this);
                }

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
            case UiAction.SecondaryUp or UiAction.SecondaryDown when InfoOverflows:
                // RS (Shift+W/S) scrolls the info box (#506); the focus stays where it is.
                InfoScrollRequested?.Invoke(action == UiAction.SecondaryUp ? -1 : 1);
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

    /// <summary>L3 or the sort button beside the position: the library's "Ansicht" dialog, from a tab only.</summary>
    [RelayCommand]
    private void OpenViewOptions()
    {
        if (_createViewOptions is { } create)
        {
            _host.ShowViewOptions(this, create);
        }
    }

    /// <summary>LT/RT let go after a hold: the page shows the game the covers turned to (#499). Its buttons act on the press.</summary>
    public void Release(UiAction action)
    {
        if (action is UiAction.PreviousPage or UiAction.NextPage)
        {
            CatchUp();
        }
    }

    public void CancelHeldButtons() => CatchUp();

    /// <summary>The page went: a jump still running stops.</summary>
    public void StopJump() => _jump.Stop();

    /// <summary>The games of the carousel, the ones in the stacks included.</summary>
    public IEnumerable<GameTileViewModel> ShownTiles => Covers.Select(c => c.Tile);

    public void InputKindChanged(InputKind kind)
    {
    }

    /// <summary>
    /// Shows the next (1) or previous (-1) game. One cover is built, coming in invisible at the far end; the others move one
    /// place along. A held LT/RT turns only the covers, the page follows on release, so the text doesn't flicker (#499).
    /// </summary>
    public void Rotate(int delta)
    {
        // Held LT/RT go round without a stop (#476): the carousel shows no first or last game, so a stop there would
        // seem to come at random.
        if (Games.Count < 2)
        {
            return;
        }

        Index = Carousel.At(Index, delta, Games.Count)!.Value;
        var reach = StackDepth + 1;
        foreach (var cover in Covers.ToList())
        {
            cover.Offset -= delta;
            if (Math.Abs(cover.Offset) > reach)
            {
                Covers.Remove(cover);
            }
        }

        Covers.Add(new(_createTile(Games[Carousel.At(Index, delta * reach, Games.Count)!.Value]), delta * reach));
        PositionText = Position();
        OnPropertyChanged(nameof(Previous));
        OnPropertyChanged(nameof(Next));
        if (HeldPress.IsRepeat)
        {
            _pageBehind = true;
            return;
        }

        ShowFront();
    }

    /// <summary>
    /// Takes over a re-sorted or rescanned list. The shown game stays in the middle, its neighbours follow the new order,
    /// and the page keeps its area and focus. False if the list is empty and the page has nothing to show.
    /// </summary>
    public bool Update(IReadOnlyList<Game> games, string? sortLabel)
    {
        var index = Carousel.Find(games, Current.Tile.Game.Key, Index);
        if (index < 0)
        {
            return false;
        }

        var focus = Current.Focus;
        Games = games;
        Index = index;
        SortLabel = sortLabel;
        OnPropertyChanged(nameof(HasNeighbours));
        OnPropertyChanged(nameof(StackDepth));
        BuildCovers(focus);
        return true;
    }

    /// <summary>The metadata of <paramref name="keys"/> changed (#493): the shown game's shows at once.</summary>
    public void ShowMetadata(IReadOnlyCollection<string> keys)
    {
        var key = Current.Tile.Game.Key;
        if (keys.Contains(key))
        {
            Current.ShowMetadata(new DetailMetadata(_metadataOf(key)));
        }
    }

    /// <summary>The buttons and rows depend on whether the Steam Controller mode and the mouse mode apply.</summary>
    public void RebuildActions() => Current.RebuildActions();

    private void BuildCovers(DetailFocus focus)
    {
        Covers.Clear();
        var reach = Games.Count < 2 ? 0 : StackDepth + 1;
        for (var offset = -reach; offset <= reach; offset++)
        {
            Covers.Add(new(_createTile(Games[Carousel.At(Index, offset, Games.Count)!.Value]), offset));
        }

        _pageBehind = false;
        ShowPage(focus);
        PositionText = Position();
    }

    private void CatchUp()
    {
        if (_pageBehind)
        {
            _pageBehind = false;
            ShowFront();
        }
    }

    /// <summary>The page of the game in front, with the focus of the page before.</summary>
    private void ShowFront()
    {
        ShowPage(Current.Focus);
        if (_jump.Turn() is { } jump)
        {
            Jumped?.Invoke(jump);
        }
    }

    private void ShowPage(DetailFocus focus)
    {
        var front = Covers.First(c => c.Offset == 0).Tile;
        if (Current is { } shown)
        {
            shown.Detach();
            shown.ReadScrollRequested -= OnReadScrollRequested;
        }

        var page = new GameDetailPageViewModel(front, _actions, focus, new DetailMetadata(_metadataOf(front.Game.Key)));
        page.ReadScrollRequested += OnReadScrollRequested;
        Current = page;
    }

    private void OnReadScrollRequested(int direction) => InfoScrollRequested?.Invoke(direction);

    private GameTileViewModel? Neighbour(int side) =>
        Games.Count < 2 ? null : Covers.FirstOrDefault(c => c.Offset == side)?.Tile;

    private string Position() => AppTexts.DetailPosition(position: Index + 1, count: Games.Count);
}
