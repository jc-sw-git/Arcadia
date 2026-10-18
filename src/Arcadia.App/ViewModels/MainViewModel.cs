using System.Collections.ObjectModel;
using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.Search;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Settings;
using Arcadia.Input;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>The Big Picture library: launcher tabs, a paginated cover grid and the preview panel.</summary>
public partial class MainViewModel : ViewModelBase, IGameActionHost
{
    /// <summary>Must match the column count of the grid in MainWindow.axaml.</summary>
    public const int GridColumns = 8;

    private static readonly TimeSpan UninstallSettleTime = TimeSpan.FromSeconds(5);

    /// <summary>A shorter press of A starts the game; the ring around the preview fills from here on.</summary>
    private static readonly TimeSpan HoldFeedbackDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>Holding A this long opens the detail page. The ring's animation in Theme.axaml lasts the difference.</summary>
    private static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(1);

    private readonly LibraryService _library;
    private readonly LibraryRefresher _refresher;
    private readonly InstallTracker _installs;
    private readonly GameLaunchService _launcher;
    private readonly ControllerSupportService _controllerSupport;
    private readonly ArcadiaUpdateService _updates;
    private readonly SettingsService _settings;
    private readonly TimeProvider _time;
    private readonly HoldGesture _acceptHold;
    private GameTileViewModel? _holdTile;
    private int _requestedPage;

    public MainViewModel(
        LibraryService library,
        GameLaunchService launcher,
        ControllerSupportService controllerSupport,
        ArcadiaUpdateService updates,
        InstallTracker installs,
        SettingsService settings,
        TimeProvider time)
    {
        _library = library;
        _refresher = new LibraryRefresher(library, time);
        _launcher = launcher;
        _controllerSupport = controllerSupport;
        _updates = updates;
        _installs = installs;
        _settings = settings;
        _time = time;
        Status = new StatusBarViewModel(controllerSupport, updates, time);
        Toast = new ToastViewModel(time);
        AppErrors.Occurred += message => Avalonia.Threading.Dispatcher.UIThread.Post(() => Toast.Show(message));
        _controllerSupport.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateControllerSupportState);
        UpdateControllerSupportState();

        _acceptHold = new HoldGesture(HoldFeedbackDelay, HoldTime, time, action => Avalonia.Threading.Dispatcher.UIThread.Post(action));
        // The tile has the focus already: the press started on it.
        _acceptHold.Tapped += () => WithHoldTile(Start);
        _acceptHold.Held += () => WithHoldTile(ShowDetails);
        _acceptHold.FeedbackChanged += holding => WithHoldTile(tile => tile.IsHolding = holding);
    }

    public StatusBarViewModel Status { get; }

    public ToastViewModel Toast { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsOpen))]
    public partial SettingsViewModel? Settings { get; private set; }

    public bool IsSettingsOpen => Settings is not null;

    [ObservableProperty]
    public partial bool ShowControllerSupport { get; private set; }

    /// <summary>The Steam Controller mode of the focused game: "Xbox 360 Controller (SISR)" or "Steam Input".</summary>
    [ObservableProperty]
    public partial string FocusedControllerSupportText { get; private set; } = string.Empty;

    partial void OnFocusedGameChanged(GameTileViewModel? value) => UpdateControllerSupportState();

    /// <summary>Raised when Arcadia should bring its window back to the front.</summary>
    public event EventHandler? ForegroundRequested;

    public ObservableCollection<LauncherTabViewModel> Tabs { get; } = [];

    public ObservableCollection<GameTileViewModel> PageGames { get; } = [];

    public ObservableCollection<PageButtonViewModel> PageButtons { get; } = [];

    [ObservableProperty]
    public partial LauncherTabViewModel? SelectedTab { get; private set; }

    [ObservableProperty]
    public partial GameTileViewModel? FocusedGame { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewOptionsOpen))]
    public partial ViewOptionsViewModel? ViewOptions { get; private set; }

    public bool IsViewOptionsOpen => ViewOptions is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameMenuOpen))]
    public partial GameMenuViewModel? GameMenu { get; private set; }

    public bool IsGameMenuOpen => GameMenu is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirmationOpen))]
    public partial ConfirmationViewModel? Confirmation { get; private set; }

    public bool IsConfirmationOpen => Confirmation is not null;

    /// <summary>The detail page; it takes the place of the cover grid while open, and of the search when opened from there.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailOpen), nameof(IsLibraryShown), nameof(IsSearchShown), nameof(IsTabStripShown))]
    public partial GameDetailViewModel? Detail { get; private set; }

    public bool IsDetailOpen => Detail is not null;

    public bool IsLibraryShown => Detail is null;

    /// <summary>The search over all launchers (#12); it covers the library while open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchShown))]
    public partial SearchViewModel? Search { get; private set; }

    /// <summary>The search is open and not hidden behind a detail page opened from it.</summary>
    public bool IsSearchShown => Search is not null && Detail is null;

    /// <summary>A detail page opened from the search belongs to no tab, so the tabs are hidden there.</summary>
    public bool IsTabStripShown => Detail?.IsFromSearch != true;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; } = true;

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    public partial string CountText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ViewSummary { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string RangeText { get; private set; } = string.Empty;

    public async Task LoadAsync()
    {
        IsLoading = true;
        var libraries = await _library.LoadAsync();
        IsLoading = false;
        ApplyLibraries(libraries);
    }

    /// <summary>
    /// Rescans the library while games handed to a launcher Arcadia can't watch are still being
    /// installed, and reports the ones that finished. Called periodically.
    /// </summary>
    public async Task CheckPendingInstallsAsync()
    {
        if (!_installs.HasPending || IsLoading)
        {
            return;
        }

        var libraries = await _library.LoadAsync();
        var finished = _installs.CompletePending(libraries.SelectMany(l => l.Games));
        if (finished.Count == 0)
        {
            return;
        }

        ApplyLibraries(libraries);
        Toast.Show(finished.Count == 1 ? $"„{finished[0].Title}“ ist jetzt installiert." : $"{finished.Count} Spiele sind jetzt installiert.");
    }

    /// <summary>
    /// Asks the watched launchers what their installations are doing — also the ones started in
    /// the launcher itself — and updates the covers. Rescans the library when one completed.
    /// </summary>
    public async Task PollInstallsAsync()
    {
        if (IsLoading || await _installs.PollAsync() is not { } results)
        {
            return;
        }

        foreach (var tile in PageGames.Concat(Detail?.Pages.Select(p => p.Tile) ?? []).Concat(Search?.Results ?? []))
        {
            tile.InstallState = _installs.StateOf(tile.Game.Key);
        }

        if (results.Count > 0)
        {
            await ReportInstallResultsAsync(results);
        }
    }

    private async Task ReportInstallResultsAsync(IReadOnlyList<InstallResult> results)
    {
        string TitleOf(string key) =>
            Tabs.SelectMany(t => t.Games).FirstOrDefault(g => g.Key == key)?.Title ?? "Spiel";

        if (results.Any(r => r.Outcome == InstallOutcome.Completed))
        {
            await RefreshLibraryAsync(force: true);
        }

        if (results.Any(r => r.Outcome == InstallOutcome.Uninstalled))
        {
            // Epic logs the uninstall when it starts; the manifest disappears a moment later.
            await RefreshLibraryAsync(force: true);
            Task.Delay(UninstallSettleTime, _time).ContinueWith(
                _ => RefreshLibraryAsync(force: true),
                TaskScheduler.FromCurrentSynchronizationContext()).Unwrap().Forget("Bibliothek aktualisieren");
        }

        var completed = results.Count(r => r.Outcome == InstallOutcome.Completed);
        var (key, outcome) = results[^1];
        Toast.Show(outcome switch
        {
            InstallOutcome.Uninstalled => $"„{TitleOf(key)}“ wurde deinstalliert.",
            InstallOutcome.Completed when completed > 1 => $"{completed} Spiele sind jetzt installiert.",
            InstallOutcome.Completed => $"„{TitleOf(key)}“ ist jetzt installiert.",
            InstallOutcome.Cancelled => $"Installation von „{TitleOf(key)}“ abgebrochen.",
            _ => $"Installation von „{TitleOf(key)}“ ist fehlgeschlagen – bitte im Launcher nachsehen.",
        });
    }

    /// <summary>
    /// Rescans all launchers and takes over what changed — games installed or uninstalled outside
    /// Arcadia. Called when the window becomes active again, when a selected game's install
    /// directory is gone, and after install or uninstall events. Without <paramref name="force"/>
    /// it skips if the last rescan was only seconds ago.
    /// </summary>
    public async Task RefreshLibraryAsync(bool force = false)
    {
        if (IsLoading)
        {
            return;
        }

        if (await _refresher.RefreshAsync(CurrentLibraries, force) is { } libraries)
        {
            ApplyLibraries(libraries);
        }
    }

    private IReadOnlyList<LauncherLibrary> CurrentLibraries() =>
        Tabs.Select(t => new LauncherLibrary(t.Launcher, t.Games)).ToList();

    /// <summary>
    /// Shows the scanned libraries: one tab per launcher in the scanned order, launchers that are
    /// gone lose their tab. Keeps tab, page and focused game where they were.
    /// </summary>
    private void ApplyLibraries(IReadOnlyList<LauncherLibrary> libraries)
    {
        foreach (var gone in Tabs.Where(t => libraries.All(l => l.Launcher.Id != t.Launcher.Id)).ToList())
        {
            Tabs.Remove(gone);
        }

        for (var i = 0; i < libraries.Count; i++)
        {
            var library = libraries[i];
            var index = IndexOfTab(library.Launcher.Id);
            if (index < 0)
            {
                Tabs.Insert(i, new LauncherTabViewModel(library, ChooseTab));
                continue;
            }

            Tabs[index].Games = library.Games;
            if (index != i)
            {
                Tabs.Move(index, i);
            }
        }

        IsEmpty = Tabs.Count == 0;
        UpdateSearch();
        if (SelectedTab is null || !Tabs.Contains(SelectedTab))
        {
            SelectedTab = null;
            FocusedGame = null;
            ViewOptions = null;
            if (Detail?.IsFromLibrary == true)
            {
                Detail = null;
            }

            PageGames.Clear();
            PageButtons.Clear();
            CountText = RangeText = ViewSummary = string.Empty;
            if (Tabs.Count > 0)
            {
                // SelectTab, not ChooseTab: a launcher missing from this scan must not overwrite the user's choice.
                SelectTab(Tabs.FirstOrDefault(t => t.Launcher.Id == _settings.Current.LastTabLauncherId) ?? Tabs[0]);
            }

            return;
        }

        var focusIndex = FocusedGame is { } focused ? PageGames.IndexOf(focused) : 0;
        Refresh(focusIndex, FocusedGame?.Game.Key);
        UpdateDetail();
    }

    private int IndexOfTab(string launcherId)
    {
        for (var i = 0; i < Tabs.Count; i++)
        {
            if (Tabs[i].Launcher.Id == launcherId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>A button or key was pressed. Only A in the library waits for the release (<see cref="Release"/>).</summary>
    public void Handle(UiAction action)
    {
        if (action != UiAction.Accept)
        {
            _acceptHold.Cancel();
        }

        if (Settings is { } settings)
        {
            if (!settings.Handle(action))
            {
                Settings = null;
            }

            return;
        }

        if (Confirmation is { } confirmation)
        {
            if (!confirmation.Handle(action))
            {
                // Closed first: the confirmed action may open the next dialog.
                Confirmation = null;
            }

            return;
        }

        if (GameMenu is { } menu)
        {
            if (!menu.Handle(action) && GameMenu == menu)
            {
                GameMenu = null;
            }

            return;
        }

        if (ViewOptions is { } dialog)
        {
            if (!dialog.Handle(action))
            {
                ViewOptions = null;
            }

            return;
        }

        if (Detail is { } detail)
        {
            HandleDetail(detail, action);
            return;
        }

        if (Search is { } search)
        {
            HandleSearch(search, action);
            return;
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
                    _holdTile = game;
                    _acceptHold.Press();
                }

                break;
            case UiAction.Options:
                OpenMenu();
                break;
            case UiAction.PreviousTab:
                CycleTab(-1);
                break;
            case UiAction.NextTab:
                CycleTab(1);
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
            case UiAction.Settings:
                OpenSettings();
                break;
            case UiAction.Search:
                OpenSearch();
                break;
        }
    }

    private void HandleDetail(GameDetailViewModel detail, UiAction action)
    {
        switch (action)
        {
            case UiAction.Back:
                CloseDetail();
                break;
            case UiAction.PreviousTab or UiAction.NextTab or UiAction.ViewOptions when detail.IsFromSearch:
                // The hits belong to no tab and are sorted by relevance.
                break;
            case UiAction.PreviousPage:
                detail.Rotate(-1);
                break;
            case UiAction.NextPage:
                detail.Rotate(1);
                break;
            case UiAction.PreviousTab:
                CycleTab(-1);
                break;
            case UiAction.NextTab:
                CycleTab(1);
                break;
            case UiAction.ViewOptions:
                OpenViewOptions();
                break;
            case UiAction.Settings:
                OpenSettings();
                break;
            case UiAction.Options:
                // No context menu here: its actions are buttons on the page (owner, 2026-10-17).
                break;
            default:
                detail.Handle(action);
                break;
        }
    }

    /// <summary>Opens the detail page on <paramref name="key"/> in the selected tab, or among the hits while the search is open.</summary>
    private void OpenDetail(string key)
    {
        if (Search is { } search)
        {
            var hit = Carousel.Find(search.Hits, key, 0);
            Detail = hit < 0 ? null : new GameDetailViewModel(null, search.Hits, hit, null, CreateDetailTile, this);
            return;
        }

        if (SelectedTab is not { } tab)
        {
            return;
        }

        var games = SortedGames(tab);
        var index = Carousel.Find(games, key, 0);
        Detail = index < 0 ? null : new GameDetailViewModel(tab.Name, games, index, SortLabel(_settings.Current.LibrarySort), CreateDetailTile, this);
    }

    /// <summary>
    /// Back to the library, on the page of the game shown last and with the focus on it; or back
    /// to the search, with the focus on that game among the hits.
    /// </summary>
    [RelayCommand]
    private void CloseDetail()
    {
        if (Detail is { IsFromSearch: true } fromSearch && Search is { } search)
        {
            Detail = null;
            search.FocusHit(fromSearch.Current.Tile.Game.Key);
            return;
        }

        if (Detail is not { } detail || SelectedTab is not { } tab)
        {
            Detail = null;
            return;
        }

        Detail = null;
        var pageSize = PageSizes.Effective(_settings.Current.PreferredPageSize, tab.Count);
        _requestedPage = detail.Index / pageSize;
        Refresh(focusIndex: detail.Index % pageSize);
    }

    /// <summary>The list changed (rescan, new sort): the detail page keeps its game in the middle.</summary>
    private void UpdateDetail()
    {
        if (Detail is { IsFromLibrary: true } detail && SelectedTab is { } tab && !detail.Update(SortedGames(tab), SortLabel(_settings.Current.LibrarySort)))
        {
            Detail = null;
        }
    }

    private GameTileViewModel CreateDetailTile(Game game) =>
        new(game, LauncherOf(game), _time.GetLocalNow(), _ => { }, _ => { }) { InstallState = _installs.StateOf(game.Key) };

    /// <summary>Every shown game comes from a tab, so its launcher has one.</summary>
    private LauncherInfo LauncherOf(Game game) => Tabs.First(t => t.Launcher.Id == game.LauncherId).Launcher;

    /// <summary>X in the library: the search over all launchers, with the on-screen keyboard.</summary>
    [RelayCommand]
    private void OpenSearch()
    {
        if (Tabs.Count == 0)
        {
            return;
        }

        Search = new SearchViewModel(
            CurrentLibraries(),
            KeyboardLayout.Find(_settings.Current.KeyboardLayout),
            layout => SaveView(settings => settings with { KeyboardLayout = layout.Id }),
            CreateSearchTile);
    }

    /// <summary>Back to the library, where it was before the search.</summary>
    [RelayCommand]
    private void CloseSearch()
    {
        _acceptHold.Cancel();
        Search = null;
    }

    /// <summary>
    /// A hit behaves like a cover in the library: A starts on release, held opens the details, ⧉ opens
    /// its menu. B closes the layout menu first, then the search.
    /// </summary>
    private void HandleSearch(SearchViewModel search, UiAction action)
    {
        if (search.Keyboard.IsLayoutMenuOpen)
        {
            search.Handle(action);
            return;
        }

        switch (action)
        {
            case UiAction.Back:
                CloseSearch();
                break;
            case UiAction.Accept when search.FocusedResult is { } hit:
                _holdTile = hit;
                _acceptHold.Press();
                break;
            case UiAction.Options when search.FocusedResult is { } hit:
                GameMenu = new GameMenuViewModel(hit, GameActionViewModel.For(hit, this));
                break;
            default:
                search.Handle(action);
                break;
        }
    }

    /// <summary>Text from a real keyboard while the search is shown.</summary>
    public void TypeInSearch(string text)
    {
        if (IsSearchShown && !IsSettingsOpen && !IsConfirmationOpen && !IsGameMenuOpen && Search is { Keyboard.IsLayoutMenuOpen: false } search)
        {
            _acceptHold.Cancel();
            search.Type(text);
        }
    }

    /// <summary>The mouse wheel over the hits.</summary>
    public void ScrollSearch(int delta)
    {
        _acceptHold.Cancel();
        Search?.Scroll(delta);
    }

    /// <summary>Right click on a hit: focus it and open its menu.</summary>
    public void OpenMenuForHit(GameTileViewModel tile)
    {
        if (!IsSearchShown || Search is not { } search || !search.Results.Contains(tile) || IsSettingsOpen || IsConfirmationOpen || IsGameMenuOpen)
        {
            return;
        }

        tile.FocusCommand.Execute(null);
        GameMenu = new GameMenuViewModel(tile, GameActionViewModel.For(tile, this));
    }

    private GameTileViewModel CreateSearchTile(Game game, Action<GameTileViewModel> focus) =>
        new(game, LauncherOf(game), _time.GetLocalNow(), focus, tile =>
        {
            focus(tile);
            Start(tile);
        })
        { InstallState = _installs.StateOf(game.Key) };

    /// <summary>A rescan: same input, fresh hits; a detail page opened from the search keeps its game.</summary>
    private void UpdateSearch()
    {
        if (Search is not { } search)
        {
            return;
        }

        if (Tabs.Count == 0)
        {
            Detail = Detail?.IsFromSearch == true ? null : Detail;
            CloseSearch();
            return;
        }

        search.Update(CurrentLibraries());
        KeepHoldOnRebuiltPage();
        if (Detail is { IsFromSearch: true } detail && !detail.Update(search.Hits, null))
        {
            Detail = null;
        }
    }

    private IReadOnlyList<Game> SortedGames(LauncherTabViewModel tab) =>
        GameSorter.Sort(tab.Games, _settings.Current.LibrarySort, Formatting.Culture);

    /// <summary>A button or key was let go.</summary>
    public void Release(UiAction action)
    {
        if (action == UiAction.Accept)
        {
            _acceptHold.Release();
        }
    }

    /// <summary>Drops a held A, e.g. when the window loses focus and the release may never arrive.</summary>
    public void CancelHold() => _acceptHold.Cancel();

    /// <summary>
    /// A rescan can rebuild the page or the hits while A is down, e.g. right after a game ended. The
    /// press moves to the same game's new tile; only a game that's no longer shown drops it.
    /// </summary>
    private void KeepHoldOnRebuiltPage()
    {
        if (_holdTile is not { } held)
        {
            return;
        }

        IEnumerable<GameTileViewModel> shown = IsSearchShown ? Search!.Results : PageGames;
        _holdTile = shown.FirstOrDefault(t => t.Game.Key == held.Game.Key);
        if (_holdTile is null)
        {
            _acceptHold.Cancel();
            return;
        }

        _holdTile.IsHolding = held.IsHolding;
    }

    private void WithHoldTile(Action<GameTileViewModel> action)
    {
        if (_holdTile is { } tile)
        {
            action(tile);
        }
    }

    /// <summary>Opens the context menu of the focused game.</summary>
    [RelayCommand]
    private void OpenMenu()
    {
        if (FocusedGame is { } tile)
        {
            GameMenu = new GameMenuViewModel(tile, GameActionViewModel.For(tile, this));
        }
    }

    /// <summary>Right click on a cover: focus it and open its menu.</summary>
    public void OpenMenuFor(GameTileViewModel tile)
    {
        if (!PageGames.Contains(tile) || IsSettingsOpen || IsViewOptionsOpen || IsConfirmationOpen || IsDetailOpen || Search is not null)
        {
            return;
        }

        Focus(tile);
        OpenMenu();
    }

    [RelayCommand]
    private void CloseMenu() => GameMenu = null;

    /// <summary>A click on an entry of the menu.</summary>
    [RelayCommand]
    private void InvokeMenuAction(GameActionViewModel action)
    {
        var menu = GameMenu;
        action.Invoke();
        if (!action.KeepsMenuOpen && GameMenu == menu)
        {
            GameMenu = null;
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        var confirmation = Confirmation;
        Confirmation = null;
        confirmation?.Confirm();
    }

    [RelayCommand]
    private void CancelConfirmation() => Confirmation = null;

    bool IGameActionHost.ShowControllerMode => ShowControllerSupport;

    public string ControllerModeOf(Game game) =>
        _controllerSupport.Settings.ControllerSupportFor(game) ? ControllerModes.Sisr : ControllerModes.Steam;

    void IGameActionHost.Play(GameTileViewModel tile) => Start(tile);

    public void ShowDetails(GameTileViewModel tile) => OpenDetail(tile.Game.Key);

    void IGameActionHost.OpenInLauncher(GameTileViewModel tile) =>
        HandOver(tile, tile.Game.OpenInLauncher, $"{tile.LauncherName} wird geöffnet …", $"Demo-Spiel: Hier würde jetzt {tile.LauncherName} geöffnet.");


    void IGameActionHost.ToggleControllerMode(GameTileViewModel tile) =>
        _controllerSupport.SetModeFor(tile.Game, !_controllerSupport.Settings.ControllerSupportFor(tile.Game));

    void IGameActionHost.Uninstall(GameTileViewModel tile)
    {
        if (tile.Game.Uninstall is not { } uninstall)
        {
            // Only the launcher's own window can do it.
            HandOver(tile, tile.Game.OpenInLauncher, $"„{tile.Title}“ lässt sich nur in {tile.LauncherName} deinstallieren – {tile.LauncherName} wird geöffnet …", $"Demo-Spiel: Hier würde jetzt {tile.LauncherName} geöffnet.");
            return;
        }

        Confirmation = new ConfirmationViewModel(
            $"„{tile.Title}“ deinstallieren?",
            $"{tile.LauncherName} entfernt das Spiel von diesem PC und fragt eventuell selbst noch einmal nach. Installieren lässt es sich danach jederzeit wieder.",
            "Deinstallieren",
            () => HandOver(tile, uninstall, $"Deinstallation von „{tile.Title}“ wird in {tile.LauncherName} geöffnet …", $"Demo-Spiel: Hier würde jetzt „{tile.Title}“ deinstalliert."));
    }

    /// <summary>Hands one of the game's launcher actions to its launcher and says so.</summary>
    private void HandOver(GameTileViewModel tile, LaunchTarget? target, string handedOver, string demo)
    {
        try
        {
            Toast.Show(GameLaunchService.Open(target) ? handedOver
                : target is LaunchTarget.None ? demo
                : $"{tile.LauncherName} bietet das für „{tile.Title}“ nicht an.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Toast.Show($"{tile.LauncherName} konnte nicht geöffnet werden: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenSettings() => Settings = new SettingsViewModel(_controllerSupport, _updates, _updates.InstallAsync);

    [RelayCommand]
    private void CloseSettings() => Settings = null;

    partial void OnSettingsChanged(SettingsViewModel? oldValue, SettingsViewModel? newValue) => oldValue?.Dispose();

    /// <summary>Switches the focused game between "Xbox 360 Controller (SISR)" and "Steam Input".</summary>
    [RelayCommand]
    private void CycleControllerSupport()
    {
        if (FocusedGame?.Game is not { } game)
        {
            return;
        }

        _controllerSupport.SetModeFor(game, !_controllerSupport.Settings.ControllerSupportFor(game));
    }

    private void UpdateControllerSupportState()
    {
        var support = _controllerSupport;

        // The mode only matters with a Steam Controller; other pads never go through SISR (#72).
        if (ShowControllerSupport != support.SteamControllerConnected)
        {
            ShowControllerSupport = support.SteamControllerConnected;
            Detail?.RebuildActions();
        }

        if (FocusedGame?.Game is { } game)
        {
            FocusedControllerSupportText = ControllerModeOf(game);
        }
    }

    [RelayCommand]
    private void PreviousTab() => CycleTab(-1);

    [RelayCommand]
    private void NextTab() => CycleTab(1);

    [RelayCommand]
    private void PreviousPage() => GoToPage(_requestedPage - 1);

    [RelayCommand]
    private void NextPage() => GoToPage(_requestedPage + 1);

    [RelayCommand]
    private void OpenViewOptions()
    {
        if (SelectedTab is not null)
        {
            ViewOptions = CreateViewOptions();
        }
    }

    [RelayCommand]
    private void CloseViewOptions() => ViewOptions = null;

    [RelayCommand]
    private void ActivateFocused()
    {
        if (FocusedGame is { } game)
        {
            Activate(game);
        }
    }

    /// <summary>The user picked <paramref name="tab"/>; Arcadia opens with it next time.</summary>
    private void ChooseTab(LauncherTabViewModel tab)
    {
        SelectTab(tab);
        SaveView(settings => settings with { LastTabLauncherId = tab.Launcher.Id });

        // The detail page stays open and shows the tab's first game.
        if (Detail is not null)
        {
            if (PageGames.FirstOrDefault() is { } first)
            {
                OpenDetail(first.Game.Key);
            }
            else
            {
                Detail = null;
            }
        }
    }

    private void SelectTab(LauncherTabViewModel tab)
    {
        if (SelectedTab is { } previous)
        {
            previous.IsSelected = false;
        }

        tab.IsSelected = true;
        SelectedTab = tab;
        _requestedPage = 0;
        Refresh(focusIndex: 0);
    }

    private void CycleTab(int delta)
    {
        if (Tabs.Count == 0 || SelectedTab is null)
        {
            return;
        }

        var index = (Tabs.IndexOf(SelectedTab) + delta + Tabs.Count) % Tabs.Count;
        ChooseTab(Tabs[index]);
    }

    private void GoToPage(int page)
    {
        if (SelectedTab is null)
        {
            return;
        }

        var window = PageWindow.For(SelectedTab.Count, PageSizes.Effective(_settings.Current.PreferredPageSize, SelectedTab.Count), page);
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
            RefreshLibraryAsync().Forget("Bibliothek aktualisieren");
        }
    }

    private void Activate(GameTileViewModel tile)
    {
        Focus(tile);
        Start(tile);
    }

    /// <summary>Starts the game, or installs it, or says why neither is possible right now.</summary>
    private void Start(GameTileViewModel tile)
    {
        if (tile.InstallState is { } install)
        {
            Toast.Show(install.Phase == InstallPhase.AwaitingConfirmation
                ? $"Die Installation von „{tile.Title}“ wartet auf deine Bestätigung in {tile.LauncherName}."
                : install.Phase == InstallPhase.Uninstalling
                    ? $"„{tile.Title}“ wird gerade deinstalliert."
                    : $"„{tile.Title}“ wird bereits {(tile.IsInstalled ? "aktualisiert" : "installiert")} – Pausieren und Abbrechen in {tile.LauncherName}.");
            return;
        }

        if (!tile.IsInstalled)
        {
            Install(tile);
            return;
        }

        LaunchAsync(tile).Forget("Spielstart");
    }

    private async Task LaunchAsync(GameTileViewModel tile)
    {
        var progress = new Progress<LaunchStage>(stage => OnLaunchStage(tile, stage));

        // Games that use Steam Input themselves would get every input twice while SISR runs.
        var result = await _launcher.TryStartAsync(tile.Game, token => _controllerSupport.BeginGameAsync(tile.Game, token), progress);
        switch (result)
        {
            case { Outcome: LaunchOutcome.Failed, Error: { } error }:
                Toast.Show($"„{tile.Title}“ konnte nicht gestartet werden: {error.Message}");
                break;
            case { Outcome: LaunchOutcome.FailedUnexpectedly }:
                Toast.Show($"„{tile.Title}“ konnte wegen eines unerwarteten Fehlers nicht gestartet werden – Details stehen in arcadia.log.");
                break;
        }
    }

    private void Install(GameTileViewModel tile)
    {
        try
        {
            if (GameLaunchService.Install(tile.Game))
            {
                tile.InstallState = _installs.Started(tile.Game);
                Toast.Show($"Installation von „{tile.Title}“ wird in {tile.LauncherName} geöffnet …");
            }
            else
            {
                Toast.Show(tile.Game.Install is LaunchTarget.None
                    ? $"Demo-Spiel: Hier würde jetzt „{tile.Title}“ installiert."
                    : $"„{tile.Title}“ kann nur direkt in {tile.LauncherName} installiert werden.");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Toast.Show($"Installation von „{tile.Title}“ konnte nicht geöffnet werden: {ex.Message}");
        }
    }

    private void OnLaunchStage(GameTileViewModel tile, LaunchStage stage)
    {
        switch (stage)
        {
            case LaunchStage.StartingLauncher:
                Toast.Show($"{tile.LauncherName} wird im Hintergrund gestartet …", sticky: true);
                Task.Delay(TimeSpan.FromSeconds(2), _time).ContinueWith(
                    _ => ForegroundRequested?.Invoke(this, EventArgs.Empty),
                    TaskScheduler.FromCurrentSynchronizationContext()).Forget("Fenster nach vorn holen");
                break;
            case LaunchStage.LauncherNotReady:
                Toast.Show($"{tile.LauncherName} reagiert nicht – „{tile.Title}“ wird trotzdem angefragt …");
                ForegroundRequested?.Invoke(this, EventArgs.Empty);
                break;
            case LaunchStage.LauncherReady:
                // Starting a launcher can steal the foreground; take it back before the game opens.
                ForegroundRequested?.Invoke(this, EventArgs.Empty);
                break;
            case LaunchStage.Starting:
                Toast.Show(tile.UpdateAvailable ? $"„{tile.Title}“ wird gestartet – {tile.LauncherName} installiert ggf. vorher das Update …" : $"„{tile.Title}“ wird gestartet …");
                break;
            case LaunchStage.NotStartable:
                Toast.Show($"Demo-Spiel: Hier würde jetzt „{tile.Title}“ gestartet.");
                break;
            case LaunchStage.NotConfirmed:
                Toast.Show($"„{tile.Title}“ ist nicht gestartet – bitte in {tile.LauncherName} nachsehen.");
                break;
        }
    }

    /// <summary>
    /// Builds the current page again. The focus goes to the game of <paramref name="focusKey"/> if it
    /// is still on the page, else to the place <paramref name="focusIndex"/>.
    /// </summary>
    private void Refresh(int focusIndex, string? focusKey = null)
    {
        if (SelectedTab is not { } tab)
        {
            return;
        }

        var view = _settings.Current;
        var pageSize = PageSizes.Effective(view.PreferredPageSize, tab.Count);
        var sorted = SortedGames(tab);
        var window = PageWindow.For(sorted.Count, pageSize, _requestedPage);
        _requestedPage = window.PageIndex;

        var now = _time.GetLocalNow();
        PageGames.Clear();
        foreach (var game in sorted.Skip(window.Start).Take(window.Length))
        {
            PageGames.Add(new GameTileViewModel(game, tab.Launcher, now, Focus, Activate) { InstallState = _installs.StateOf(game.Key) });
        }

        KeepHoldOnRebuiltPage();
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

        var installed = tab.Games.Count(g => g.IsInstalled);
        var installedText = installed == tab.Count ? string.Empty : $" · {installed} installiert";
        CountText = $"{tab.Count} Spiele{installedText} · Seite {window.PageIndex + 1} von {window.PageCount}";
        RangeText = window.Length == 0 ? string.Empty : $"{window.Start + 1}–{window.Start + window.Length} von {sorted.Count}";
        ViewSummary = $"{SortLabel(view.LibrarySort)}  ·  {pageSize} pro Seite";
    }

    private ViewOptionsViewModel CreateViewOptions() => new(
        SelectedTab!.Name,
        SelectedTab.Count,
        _settings.Current.LibrarySort,
        _settings.Current.PreferredPageSize,
        sort => ChangeView(settings => settings with { LibrarySort = sort }),
        size => ChangeView(settings => settings with { PreferredPageSize = size }),
        Detail is null ? null : "Gilt für die Bibliothek; die Detailseite zeigt immer ein Spiel. Das aktuelle Spiel bleibt in der Mitte, die Nachbarn ordnen sich nach der neuen Sortierung.");

    private void ChangeView(Func<AppSettings, AppSettings> change)
    {
        SaveView(change);
        _requestedPage = 0;
        Refresh(focusIndex: 0);
        UpdateDetail();
        RebuildViewOptions();
    }

    /// <summary>
    /// The change applies at once; if settings.json can't be written (locked, read-only), it only
    /// lasts until Arcadia closes, or until the next save that succeeds.
    /// </summary>
    private void SaveView(Func<AppSettings, AppSettings> change)
    {
        try
        {
            _settings.Update(change);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Ansicht nicht gespeichert: {ex.Message}");
            Toast.Show("Die Ansicht konnte nicht gespeichert werden und gilt nur bis zum Beenden – Details stehen in arcadia.log.");
        }
    }

    private void RebuildViewOptions()
    {
        if (ViewOptions is { } current)
        {
            var position = current.FocusPosition;
            ViewOptions = CreateViewOptions();
            ViewOptions.FocusPosition = position;
        }
    }

    private static string SortLabel(SortOrder sort) => (sort.Field, sort.Direction) switch
    {
        (SortField.Name, SortDirection.Ascending) => "Name A–Z",
        (SortField.Name, SortDirection.Descending) => "Name Z–A",
        (SortField.InstalledAt, SortDirection.Descending) => "Installiert · neueste zuerst",
        (SortField.InstalledAt, SortDirection.Ascending) => "Installiert · älteste zuerst",
        (SortField.LastPlayed, SortDirection.Descending) => "Zuletzt gespielt · neueste zuerst",
        _ => "Zuletzt gespielt · älteste zuerst",
    };
}
