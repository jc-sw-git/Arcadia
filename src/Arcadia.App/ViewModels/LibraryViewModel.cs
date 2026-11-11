using System.Collections.ObjectModel;
using Arcadia.App.Filter;
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

    /// <summary>R3, G or a click on the funnel (#44): the tab's filter modal over the library.</summary>
    void ShowFilter(FilterModalViewModel modal);

    /// <summary>The filter modal closes; the library then applies its choice.</summary>
    void CloseFilter(FilterModalViewModel modal);
}

/// <summary>
/// The library page (#210): the selected tab's covers in pages of the grid, the focused game's preview, the view (sort and
/// page size), the tab's filter (#44) and an empty tab's message. It lies under every other page and is never closed.
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
    private readonly InstallFolderCheck _installFolders;
    private readonly Dictionary<LauncherTabViewModel, SortedTab> _sorted = [];
    private int _requestedPage;

    /// <param name="saveView">Saves a changed view, and says so if it can't.</param>
    /// <param name="showStates">Gives a new cover its game's install and run state.</param>
    /// <param name="installFolders">Notices off the UI thread that a focused game's install folder is gone.</param>
    public LibraryViewModel(
        LauncherTabsViewModel tabs,
        SettingsService settings,
        Action<Func<AppSettings, AppSettings>> saveView,
        GameActions actions,
        Action<GameTileViewModel> showStates,
        ILibraryHost host,
        TimeProvider time,
        InstallFolderCheck installFolders)
    {
        _tabs = tabs;
        _settings = settings;
        _saveView = saveView;
        _actions = actions;
        _showStates = showStates;
        _host = host;
        _time = time;
        _installFolders = installFolders;
    }

    public ObservableCollection<GameTileViewModel> PageGames { get; } = [];

    /// <summary>
    /// The places the grid draws <see cref="PageGames"/> in, one per tile at the same index. They stay when the page or tab
    /// changes and only take the new tiles, so the covers' controls are bound again instead of built again (#229).
    /// </summary>
    public ObservableCollection<TileSlotViewModel> Slots { get; } = [];

    public ObservableCollection<PageButtonViewModel> PageButtons { get; } = [];

    [ObservableProperty]
    public partial GameTileViewModel? FocusedGame { get; private set; }

    /// <summary>The selected tab has no games, or its launcher is no longer found (#136): it says so instead of covers.</summary>
    [ObservableProperty]
    public partial EmptyTabViewModel? EmptyTab { get; private set; }

    /// <summary>An empty tab's line beside its name; a tab with entries shows <see cref="Counts"/> instead.</summary>
    [ObservableProperty]
    public partial string CountText { get; private set; } = string.Empty;

    /// <summary>The count line beside the tab's name (#141): per type, with the installed ones; none on an empty tab.</summary>
    [ObservableProperty]
    public partial TypeCountLine Counts { get; private set; } = TypeCountLine.None;

    [ObservableProperty]
    public partial string ViewSummary { get; private set; } = string.Empty;

    /// <summary>The tab has a category to filter by (#44): the funnel names R3 and opens the modal.</summary>
    [ObservableProperty]
    public partial bool CanFilter { get; private set; }

    /// <summary>The tab is filtered: the funnel is filled, in the accent colour, with <see cref="FilterHitsText"/>.</summary>
    [ObservableProperty]
    public partial bool IsFiltered { get; private set; }

    /// <summary>"19 von 412" at the funnel while <see cref="IsFiltered"/>.</summary>
    [ObservableProperty]
    public partial string FilterHitsText { get; private set; } = string.Empty;

    /// <summary>The filter hides every entry of the tab: the library says so instead of covers (#44).</summary>
    [ObservableProperty]
    public partial bool FilterHidesAll { get; private set; }

    /// <summary>"Steam hat 412 Einträge, der Filter blendet gerade alle aus." under the no-hits message.</summary>
    [ObservableProperty]
    public partial string FilterHidesAllText { get; private set; } = string.Empty;

    public static string NoHitsTitle => AppTexts.LibraryNoFilterHits;

    [ObservableProperty]
    public partial string RangeText { get; private set; } = string.Empty;

    /// <summary>The library's sort order, as the detail page names it.</summary>
    public string SortLabel => Formatting.SortLabel(_settings.Current.LibrarySort);

    /// <summary>The grid, A on a cover, ⧉, LB/RB, LT/RT and L3; false for ≡ and X, which open pages over it.</summary>
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
            case UiAction.Accept when FilterHidesAll:
                // The no-hits message's only button, "Filter ändern".
                OpenFilter();
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
            // R3 is the filter here (#44); the on-screen keyboard's layout menu only where a keyboard is open.
            case UiAction.KeyboardLayout:
                OpenFilter();
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
        // A tab whose launcher went (switched off, gone from the scan) lets go of its sorted games, also when none is left.
        foreach (var gone in _sorted.Keys.Where(t => !_tabs.Items.Contains(t)).ToList())
        {
            _sorted.Remove(gone);
        }

        if (_tabs.SelectedTab is not null)
        {
            Refresh(FocusedGame is { } focused ? PageGames.IndexOf(focused) : 0, FocusedGame?.Game.Key);
            return;
        }

        FocusedGame = null;
        PageGames.Clear();
        TileSlotViewModel.Fill(Slots, PageGames);
        PageButtons.Clear();
        var tabs = _tabs.Items;
        EmptyTab = EmptyTab is { HasNoLauncher: true } shown && tabs.Count == 0 ? shown : null;
        CountText = RangeText = ViewSummary = string.Empty;
        Counts = TypeCountLine.None;
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

        var pageSize = PageSizes.Effective(_settings.Current.PreferredPageSize, Shown(tab).Games.Count);
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

        // A game the tab's filter hides (from the search, which ignores the filter): the tab from its first page.
        var index = Carousel.Find(ShownGames(tab), game.Key, 0);
        if (index >= 0)
        {
            ShowAt(index);
        }
        else
        {
            ShowTab();
        }
    }

    /// <summary>
    /// The tab's games its filter lets through (#44), in the library's sort order, as the detail page turns through them.
    /// Each tab is sorted and filtered once and kept while its games, the sort and its filter stay the same (#228): a page
    /// change or the detail page's way in and out would otherwise sort the whole tab again. A rescan hands a tab a new list
    /// (also for a changed playtime or "Zuletzt gespielt"), so comparing the list itself is enough.
    /// </summary>
    public IReadOnlyList<Game> ShownGames(LauncherTabViewModel tab) => Shown(tab).Games;

    private ShownTab Shown(LauncherTabViewModel tab)
    {
        var sort = _settings.Current.LibrarySort;
        if (!_sorted.TryGetValue(tab, out var sorted) || !ReferenceEquals(sorted.Source, tab.Games) || sorted.Sort != sort)
        {
            sorted = new SortedTab(tab.Games, sort, GameSorter.Sort(tab.Games, sort, Formatting.Culture), TypeCountLine.Of(tab.Games),
                FilterOffer.Of(tab.Games), null);
        }

        // What the tab doesn't offer (a kind switched off, a rescan) drops out of the saved filter, which stays as it was (#44 case 17).
        var filter = _settings.Current.FilterOf(tab.Launcher.Id).Within(sorted.Offer);
        if (sorted.Shown is not { } shown || !shown.Filter.Equals(filter))
        {
            shown = new ShownTab(filter, filter.Apply(sorted.Games));
            sorted = sorted with { Shown = shown };
        }

        _sorted[tab] = sorted;
        return shown;
    }

    /// <summary>The "Ansicht" dialog of the selected tab; <paramref name="forDetailPage"/> adds what it means there.</summary>
    public ViewOptionsViewModel? CreateViewOptions(bool forDetailPage) => _tabs.SelectedTab is not { } tab ? null : ViewOptionsViewModel.ForLibrary(
        tab.Name,
        // The page sizes count only what the filter lets through (#44).
        Shown(tab).Games.Count,
        _settings.Current.LibrarySort,
        _settings.Current.PreferredPageSize,
        sort => ChangeView(settings => settings with { LibrarySort = sort }),
        size => ChangeView(settings => settings with { PreferredPageSize = size }),
        forDetailPage ? AppTexts.ViewOptionsForLibraryInfo : null);

    [RelayCommand]
    private void OpenViewOptions()
    {
        if (_tabs.SelectedTab is not null)
        {
            _host.ShowViewOptions(this, () => CreateViewOptions(forDetailPage: false));
        }
    }

    /// <summary>
    /// R3, G, a click on the funnel or on "Filter ändern" (#44): the modal counts the tab's entries as they are now and,
    /// once closed, its choice is the tab's filter. Nothing happens in a tab without a category to filter by.
    /// </summary>
    [RelayCommand]
    private void OpenFilter()
    {
        if (_tabs.SelectedTab is not { } tab || EmptyTab is not null)
        {
            return;
        }

        var offer = FilterOffer.Of(tab.Games);
        if (offer.HasAny)
        {
            var saved = _settings.Current.FilterOf(tab.Launcher.Id);
            _host.ShowFilter(new FilterModalViewModel(tab.Name, tab.Games, offer, saved.Within(offer), modal =>
            {
                _host.CloseFilter(modal);
                ApplyFilter(tab, modal.WasReset ? modal.Chosen : modal.Chosen.Over(saved, offer));
            }));
        }
    }

    /// <summary>The modal closed: the choice is saved for the tab, and the tab shows its hits from the first page.</summary>
    private void ApplyFilter(LauncherTabViewModel tab, GameFilter filter)
    {
        _saveView(settings => settings.WithFilter(tab.Launcher.Id, filter));
        if (tab == _tabs.SelectedTab)
        {
            _requestedPage = 0;
            Refresh(focusIndex: 0);
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

        var hits = Shown(tab).Games.Count;
        var window = PageWindow.For(hits, PageSizes.Effective(_settings.Current.PreferredPageSize, hits), page);
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

        // Uninstalled in the launcher while Arcadia wasn't looking: the directory is gone. Asked off the UI thread, as a
        // network share or a sleeping drive can take seconds to answer (#227); the answer counts while the cover has the focus.
        if (tile.Game is { IsInstalled: true, InstallDirectory: { } directory })
        {
            _installFolders.Check(directory, () =>
            {
                if (FocusedGame == tile)
                {
                    _host.LibraryOutdated();
                }
            });
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
        var shownTab = Shown(tab);
        var snapshot = _sorted[tab];
        var sorted = shownTab.Games;
        var pageSize = PageSizes.Effective(view.PreferredPageSize, sorted.Count);
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

        TileSlotViewModel.Fill(Slots, PageGames);
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
            : EmptyTab is { } shown && shown.Tab == tab && shown.IsMissing == !tab.IsFound && shown.IsScanFailed == tab.ScanFailed ? shown
            : new EmptyTabViewModel(tab, () => _host.OpenLauncherSettings(tab.Launcher.Id), () => _host.Rescan(tab.Launcher));
        // The page number is the pager's below the covers (owner, 2026-10-26).
        CountText = EmptyTab?.Summary ?? string.Empty;
        // The count line keeps counting the whole tab; the funnel says how many the filter lets through (#44).
        Counts = EmptyTab is null ? snapshot.Counts : TypeCountLine.None;
        CanFilter = EmptyTab is null && snapshot.Offer.HasAny;
        IsFiltered = CanFilter && shownTab.Filter.IsActive;
        FilterHitsText = IsFiltered ? AppTexts.LibraryFilteredOf(Formatting.Count(sorted.Count), Formatting.Count(tab.Count)) : string.Empty;
        FilterHidesAll = EmptyTab is null && sorted.Count == 0;
        FilterHidesAllText = FilterHidesAll
            ? tab.Count == 1 ? AppTexts.LibraryFilterHidesTheOne(tab.Name) : AppTexts.LibraryFilterHidesAll(tab.Name, Formatting.Count(tab.Count))
            : string.Empty;
        RangeText = window.Length == 0 ? string.Empty : AppTexts.LibraryPageRange(window.Start + 1, window.Start + window.Length, sorted.Count);
        ViewSummary = AppTexts.LibrarySortAndPageSize(Formatting.SortLabel(view.LibrarySort), pageSize);
    }

    /// <summary>A tab's games sorted, what they were sorted from, its count line, what it offers to filter and what its filter shows.</summary>
    private sealed record SortedTab(IReadOnlyList<Game> Source, SortOrder Sort, IReadOnlyList<Game> Games, TypeCountLine Counts, FilterOffer Offer, ShownTab? Shown);

    /// <summary>The tab's filter as it applies, and the sorted games it lets through.</summary>
    private sealed record ShownTab(GameFilter Filter, IReadOnlyList<Game> Games);
}
