using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>What the library asks of the window beyond <see cref="IPageHost"/>: the ways off an empty tab, and what follows a new view.</summary>
public interface ILibraryHost : IPageHost
{
    /// <summary>"Launcher-Einstellungen" on an empty tab; <paramref name="launcherId"/> is its launcher, null when none is active.</summary>
    void OpenLauncherSettings(string? launcherId);

    /// <summary>"Neu einlesen" on an empty tab.</summary>
    void Rescan(LauncherInfo launcher);

    /// <summary>The focused game's install folder is gone: it was uninstalled in the launcher while Arcadia wasn't looking.</summary>
    void LibraryOutdated();

    /// <summary>The page was built again (rescan, new tab or page) while A may be held on one of its covers.</summary>
    void PageRebuilt();

    /// <summary>The sort or page size changed: a detail page from the library and the open view dialog follow.</summary>
    void ViewChanged();
}

/// <summary>
/// The library page (#210): the selected tab's covers in pages of the grid, the focused game's preview, the view (sort and
/// page size) and an empty tab's message. It lies under every other page and is never closed.
/// </summary>
/// <remarks>
/// UI thread only. It owns no timers: the hold of A on a cover is the one <see cref="GameActions"/>' of the window.
/// </remarks>
public sealed partial class LibraryViewModel : ViewModelBase, IPage
{
    /// <summary>Must match the column count of the grid in MainWindow.axaml.</summary>
    public const int GridColumns = 8;

    private readonly LauncherTabsViewModel _tabs;
    private readonly SettingsService _settings;
    private readonly Action<Func<AppSettings, AppSettings>> _saveView;
    private readonly GameActions _actions;
    private readonly Action<GameTileViewModel> _showStates;
    private readonly ILibraryHost _host;
    private readonly TimeProvider _time;
    private readonly Dictionary<LauncherTabViewModel, SortedTab> _sorted = [];
    private int _requestedPage;

    /// <param name="saveView">Saves a changed view, and says so if it can't.</param>
    /// <param name="showStates">Gives a new cover its game's install and run state.</param>
    public LibraryViewModel(
        LauncherTabsViewModel tabs,
        SettingsService settings,
        Action<Func<AppSettings, AppSettings>> saveView,
        GameActions actions,
        Action<GameTileViewModel> showStates,
        ILibraryHost host,
        TimeProvider time)
    {
        _tabs = tabs;
        _settings = settings;
        _saveView = saveView;
        _actions = actions;
        _showStates = showStates;
        _host = host;
        _time = time;
    }

    public ObservableCollection<GameTileViewModel> PageGames { get; } = [];

    public ObservableCollection<PageButtonViewModel> PageButtons { get; } = [];

    [ObservableProperty]
    public partial GameTileViewModel? FocusedGame { get; private set; }

    /// <summary>The selected tab has no games, or its launcher is no longer found (#136): it says so instead of covers.</summary>
    [ObservableProperty]
    public partial EmptyTabViewModel? EmptyTab { get; private set; }

    [ObservableProperty]
    public partial string CountText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ViewSummary { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string RangeText { get; private set; } = string.Empty;

    /// <summary>The library's sort order, as the detail page names it.</summary>
    public string SortLabel => Formatting.SortLabel(_settings.Current.LibrarySort);

    /// <summary>The grid, A on a cover, ⧉, LB/RB, LT/RT and Y; false for ≡ and X, which open pages over it.</summary>
    public bool Handle(UiAction action)
    {
        if (EmptyTab is { } empty && action is UiAction.Left or UiAction.Right or UiAction.Accept)
        {
            empty.Handle(action);
            return true;
        }

        switch (action)
        {
            case UiAction.Up:
                MoveFocus(-GridColumns);
                break;
            case UiAction.Down:
                MoveFocus(GridColumns);
                break;
            case UiAction.Left:
                MoveFocus(-1);
                break;
            case UiAction.Right:
                MoveFocus(1);
                break;
            case UiAction.Accept:
                // Tapped starts the game, held opens the details; both are decided by the release or the timer.
                if (FocusedGame is { } game)
                {
                    _actions.Press(game, UiAction.Accept);
                }

                break;
            case UiAction.Options:
                OpenMenu();
                break;
            case UiAction.PreviousTab:
                _tabs.Cycle(-1);
                break;
            case UiAction.NextTab:
                _tabs.Cycle(1);
                break;
            case UiAction.PreviousPage:
                GoToPage(_requestedPage - 1);
                break;
            case UiAction.NextPage:
                GoToPage(_requestedPage + 1);
                break;
            case UiAction.ViewOptions:
                OpenViewOptions();
                break;
            default:
                return false;
        }

        return true;
    }

    /// <summary>The hold of A on a cover is <see cref="GameActions"/>'; the page holds no buttons of its own.</summary>
    public void Release(UiAction action)
    {
    }

    public void CancelHeldButtons()
    {
    }

    public IEnumerable<GameTileViewModel> ShownTiles => PageGames;

    public void InputKindChanged(InputKind kind)
    {
    }

    /// <summary>
    /// The left mouse button went down on a cover: held 1 s it opens the details, with the same ring as A (owner,
    /// 2026-10-19). Let go sooner, it was a click, which only picks the game.
    /// </summary>
    public void Press(GameTileViewModel tile)
    {
        if (PageGames.Contains(tile))
        {
            tile.FocusCommand.Execute(null);
            _actions.Press(tile, button: null);
        }
    }

    /// <summary>Right click on a cover: focus it and open its menu there.</summary>
    public void OpenMenuAt(GameTileViewModel tile)
    {
        if (PageGames.Contains(tile))
        {
            Focus(tile);
            OpenMenu(atCover: true);
        }
    }

    /// <summary>
    /// The tabs were read again or restyled: the page is built again, the focus stays on its game or its place. Without a
    /// selected tab (none was there yet, or it went), the one used last is selected, or the library says no launcher is active.
    /// </summary>
    public void ShowTabs()
    {
        if (_tabs.SelectedTab is not null)
        {
            Refresh(FocusedGame is { } focused ? PageGames.IndexOf(focused) : 0, FocusedGame?.Game.Key);
            return;
        }

        FocusedGame = null;
        PageGames.Clear();
        PageButtons.Clear();
        var tabs = _tabs.Items;
        EmptyTab = EmptyTab is { HasNoLauncher: true } shown && tabs.Count == 0 ? shown : null;
        CountText = RangeText = ViewSummary = string.Empty;
        if (tabs.Count > 0)
        {
            // Selected, not chosen: a launcher missing from this scan must not overwrite the user's choice.
            _tabs.Select(_tabs.Find(_settings.Current.LastTabLauncherId ?? string.Empty) ?? tabs[0]);
            ShowTab();
        }
        else
        {
            EmptyTab ??= new EmptyTabViewModel(null, () => _host.OpenLauncherSettings(null), rescan: null);
        }
    }

    /// <summary>A launcher was renamed or got another logo (#137, #138): covers and the empty tab show it.</summary>
    public void Restyle()
    {
        EmptyTab = EmptyTab is { HasNoLauncher: true } shown ? shown : null;
        Refresh(FocusedGame is { } focused ? PageGames.IndexOf(focused) : 0, FocusedGame?.Game.Key);
    }

    /// <summary>The library shows the selected tab from its first page.</summary>
    public void ShowTab()
    {
        _requestedPage = 0;
        Refresh(focusIndex: 0);
    }

    /// <summary>The selected tab on the page of its game at <paramref name="index"/> in the sort order, with the focus on it.</summary>
    public void ShowAt(int index)
    {
        if (_tabs.SelectedTab is not { } tab)
        {
            return;
        }

        var pageSize = PageSizes.Effective(_settings.Current.PreferredPageSize, tab.Count);
        _requestedPage = index / pageSize;
        Refresh(focusIndex: index % pageSize);
    }

    /// <summary>The tab of <paramref name="game"/>'s launcher, on its page and with the focus on it.</summary>
    public void Show(Game game)
    {
        if (_tabs.Find(game.LauncherId) is not { } tab)
        {
            return;
        }

        if (tab != _tabs.SelectedTab)
        {
            _tabs.Choose(tab);
        }

        var index = Carousel.Find(SortedGames(tab), game.Key, 0);
        if (index >= 0)
        {
            ShowAt(index);
        }
    }

    /// <summary>
    /// The tab's games in the library's sort order, as the detail page turns through them. Each tab is sorted once and kept
    /// while its games and the sort stay the same (#228): a page change or the detail page's way in and out would otherwise
    /// sort the whole tab again. A rescan hands a tab a new list (also for a changed playtime or "Zuletzt gespielt"), so
    /// comparing the list itself is enough.
    /// </summary>
    public IReadOnlyList<Game> SortedGames(LauncherTabViewModel tab) => Sorted(tab).Games;

    private SortedTab Sorted(LauncherTabViewModel tab)
    {
        var sort = _settings.Current.LibrarySort;
        if (_sorted.TryGetValue(tab, out var sorted) && ReferenceEquals(sorted.Source, tab.Games) && sorted.Sort == sort)
        {
            return sorted;
        }

        // A tab whose launcher went (switched off, gone from the scan) won't be asked again.
        foreach (var gone in _sorted.Keys.Where(t => !_tabs.Items.Contains(t)).ToList())
        {
            _sorted.Remove(gone);
        }

        sorted = new SortedTab(tab.Games, sort, GameSorter.Sort(tab.Games, sort, Formatting.Culture), tab.Games.Count(g => g.IsInstalled));
        _sorted[tab] = sorted;
        return sorted;
    }

    /// <summary>The "Ansicht" dialog of the selected tab; <paramref name="forDetailPage"/> adds what it means there.</summary>
    public ViewOptionsViewModel? CreateViewOptions(bool forDetailPage) => _tabs.SelectedTab is not { } tab ? null : ViewOptionsViewModel.ForLibrary(
        tab.Name,
        tab.Count,
        _settings.Current.LibrarySort,
        _settings.Current.PreferredPageSize,
        sort => ChangeView(settings => settings with { LibrarySort = sort }),
        size => ChangeView(settings => settings with { PreferredPageSize = size }),
        forDetailPage ? "Gilt für die Bibliothek; die Detailseite zeigt immer ein Spiel. Das aktuelle Spiel bleibt in der Mitte, die Nachbarn ordnen sich nach der neuen Sortierung." : null);

    [RelayCommand]
    private void OpenViewOptions()
    {
        if (_tabs.SelectedTab is not null)
        {
            _host.ShowViewOptions(this, () => CreateViewOptions(forDetailPage: false));
        }
    }

    /// <summary>Opens the context menu of the focused game over the preview's "Optionen" button (⧉, R, a click on it).</summary>
    [RelayCommand]
    private void OpenMenu() => OpenMenu(atCover: false);

    private void OpenMenu(bool atCover)
    {
        if (FocusedGame is { } tile)
        {
            _host.ShowMenu(tile, atCover);
        }
    }

    [RelayCommand]
    private void PreviousPage() => GoToPage(_requestedPage - 1);

    [RelayCommand]
    private void NextPage() => GoToPage(_requestedPage + 1);

    private void ChangeView(Func<AppSettings, AppSettings> change)
    {
        _saveView(change);
        _requestedPage = 0;
        Refresh(focusIndex: 0);
        _host.ViewChanged();
    }

    private void GoToPage(int page)
    {
        if (_tabs.SelectedTab is not { } tab)
        {
            return;
        }

        var window = PageWindow.For(tab.Count, PageSizes.Effective(_settings.Current.PreferredPageSize, tab.Count), page);
        if (window.PageIndex == _requestedPage)
        {
            return;
        }

        _requestedPage = window.PageIndex;
        Refresh(focusIndex: 0);
    }

    private void MoveFocus(int delta)
    {
        if (FocusedGame is null || PageGames.Count == 0)
        {
            return;
        }

        var index = PageGames.IndexOf(FocusedGame);
        var target = index + delta;

        // Left/right move through the page in reading order; up/down stop at the edges.
        if (Math.Abs(delta) == 1)
        {
            target = Math.Clamp(target, 0, PageGames.Count - 1);
        }
        else if (target < 0 || target >= PageGames.Count)
        {
            // Moving down from a partial last row lands on the last tile.
            target = delta > 0 && index / GridColumns < (PageGames.Count - 1) / GridColumns ? PageGames.Count - 1 : index;
        }

        Focus(PageGames[target]);
    }

    private void Focus(GameTileViewModel tile)
    {
        if (FocusedGame is { } previous)
        {
            previous.IsFocused = false;
        }

        tile.IsFocused = true;
        FocusedGame = tile;

        // Uninstalled in the launcher while Arcadia wasn't looking: the directory is gone.
        if (tile.Game is { IsInstalled: true, InstallDirectory: { } directory } && !Directory.Exists(directory))
        {
            _host.LibraryOutdated();
        }
    }

    private void Activate(GameTileViewModel tile)
    {
        Focus(tile);
        _actions.Start(tile);
    }

    /// <summary>
    /// Builds the current page again. The focus goes to the game of <paramref name="focusKey"/> if it
    /// is still on the page, else to the place <paramref name="focusIndex"/>.
    /// </summary>
    private void Refresh(int focusIndex, string? focusKey = null)
    {
        if (_tabs.SelectedTab is not { } tab)
        {
            return;
        }

        var view = _settings.Current;
        var pageSize = PageSizes.Effective(view.PreferredPageSize, tab.Count);
        var snapshot = Sorted(tab);
        var sorted = snapshot.Games;
        var window = PageWindow.For(sorted.Count, pageSize, _requestedPage);
        _requestedPage = window.PageIndex;

        var now = _time.GetLocalNow();
        PageGames.Clear();
        foreach (var game in sorted.Skip(window.Start).Take(window.Length))
        {
            var tile = new GameTileViewModel(game, tab.Launcher, tab.Badge, now, Focus, Activate);
            _showStates(tile);
            PageGames.Add(tile);
        }

        _host.PageRebuilt();
        if (PageGames.Count == 0)
        {
            FocusedGame = null;
        }
        else
        {
            // Straight from the old tile to the new one: a detour over null would restart the hold ring.
            Focus(PageGames.FirstOrDefault(t => t.Game.Key == focusKey) ?? PageGames[Math.Clamp(focusIndex, 0, PageGames.Count - 1)]);
        }

        PageButtons.Clear();
        foreach (var page in PageButtonViewModel.Layout(window.PageIndex, window.PageCount))
        {
            PageButtons.Add(new PageButtonViewModel(page, page == window.PageIndex, GoToPage));
        }

        EmptyTab = tab.Count > 0 ? null
            : EmptyTab is { } shown && shown.Tab == tab && shown.IsMissing == !tab.IsFound ? shown
            : new EmptyTabViewModel(tab, () => _host.OpenLauncherSettings(tab.Launcher.Id), () => _host.Rescan(tab.Launcher));
        var installed = snapshot.InstalledCount;
        var installedText = installed == tab.Count ? string.Empty : $" · {installed} installiert";
        CountText = EmptyTab is { } emptyTab ? emptyTab.Summary : $"{tab.Count} Spiele{installedText} · Seite {window.PageIndex + 1} von {window.PageCount}";
        RangeText = window.Length == 0 ? string.Empty : $"{window.Start + 1}–{window.Start + window.Length} von {sorted.Count}";
        ViewSummary = $"{Formatting.SortLabel(view.LibrarySort)}  ·  {pageSize} pro Seite";
    }

    /// <summary>A tab's games sorted, what they were sorted from, and how many of them are installed.</summary>
    private sealed record SortedTab(IReadOnlyList<Game> Source, SortOrder Sort, IReadOnlyList<Game> Games, int InstalledCount);
}
