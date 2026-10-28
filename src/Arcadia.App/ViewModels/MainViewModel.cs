using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Search;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.App.Setup;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Search;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The Big Picture window (#155): routes each press to the topmost layer, keeps the rules between the pages (library,
/// detail page, search, settings, first start), holds the dialogs over them and wires the parts. The pages handle their
/// own input (<see cref="IPage"/>); the library's loading, installs and running games are <see cref="LibrarySession"/>'s.
/// </summary>
public partial class MainViewModel : ViewModelBase, IGameActionsOwner, ILibraryHost, ILauncherSettingsHost, IDisposable
{
    private readonly ControllerSupportService _controllerSupport;
    private readonly ArcadiaUpdateService _updates;
    private readonly SettingsService _settings;
    private readonly LauncherLogos _logos;
    private readonly FirstRunSetup _firstRun;
    private readonly TimeProvider _time;
    private readonly GameActions _actions;
    private Func<ViewOptionsViewModel?>? _createViewOptions;

    // The page whose options the dialog shows.
    private IPage? _viewOptionsPage;
    private InputKind _input = InputKind.Gamepad;

    /// <summary>The page on top when the last page opened or closed.</summary>
    private IPage? _topPage;

    /// <summary>The pages open or kept behind another when the last page opened or closed: every one Main hasn't let go of.</summary>
    private IReadOnlyList<IPage> _livePages = [];

    private bool _shownDefaultMouseModeAllowed = true;

    public MainViewModel(
        LibraryService library,
        GameLaunchService launcher,
        ControllerSupportService controllerSupport,
        ArcadiaUpdateService updates,
        InstallTracker installs,
        RunningGamesTracker running,
        LaunchedGames launchedGames,
        SettingsService settings,
        LauncherLogos logos,
        FirstRunSetup firstRun,
        TimeProvider time,
        InstallFolderCheck installFolders)
    {
        _logos = logos;
        _controllerSupport = controllerSupport;
        _updates = updates;
        _settings = settings;
        _firstRun = firstRun;
        _time = time;
        LauncherTabs = new LauncherTabsViewModel(logos.BadgeOf);
        Status = new StatusBarViewModel(controllerSupport, updates, time);
        Toast = new ToastViewModel(time);
        Session = new LibrarySession(library, installs, running, launchedGames, time);
        Session.LibrariesChanged += (_, _) => ApplyLibraries(Session.Libraries);
        Session.LaunchersRead += (_, _) => RefreshLauncherSettings();
        Session.InstallStatesChanged += (_, _) => UpdateInstallStates();
        Session.RunStatesChanged += (_, _) => UpdateRunStates();
        Session.Noticed += (_, notice) => ShowNotice(notice);
        _actions = new GameActions(this, launcher, controllerSupport, installs, running, launchedGames, settings, Toast, time);
        _actions.ForegroundRequested += (_, e) => ForegroundRequested?.Invoke(this, e);
        Library = new LibraryViewModel(LauncherTabs, settings, SaveView, _actions, ShowStates, this, time, installFolders);
        Library.PropertyChanged += OnLibraryChanged;
        LauncherTabs.Chosen += OnTabChosen;
        if (firstRun.IsPending)
        {
            // Shown from the start, so the library never flashes up before it; its launchers follow once they are read.
            FirstRun = new FirstRunViewModel(library.Launchers, logos.BadgeOf, CompleteFirstRun);
        }

        AppErrors.Occurred += message => Avalonia.Threading.Dispatcher.UIThread.Post(() => Toast.Show(message));
        _controllerSupport.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateControllerSupportState);
        UpdateControllerSupportState();
    }

    public StatusBarViewModel Status { get; }

    public ToastViewModel Toast { get; }

    /// <summary>Loading and rescanning the launchers, installs and running games; Main shows what it reads and notices.</summary>
    public LibrarySession Session { get; }

    /// <summary>The launcher tabs, shared by the library, the detail page and the search.</summary>
    public LauncherTabsViewModel LauncherTabs { get; }

    /// <summary>The cover grid of the selected tab, under every other page.</summary>
    public LibraryViewModel Library { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsOpen))]
    public partial SettingsPageViewModel? Settings { get; private set; }

    public bool IsSettingsOpen => Settings is not null;

    /// <summary>The first start's question which launchers Arcadia uses (#139), in place of the library until "Fertig".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFirstRunShown))]
    public partial FirstRunViewModel? FirstRun { get; private set; }

    public bool IsFirstRunShown => FirstRun is not null;

    /// <summary>The per-game mode ("Arcadia" or "Steam-Eingabe") shows only with a Steam Controller connected (#213).</summary>
    [ObservableProperty]
    public partial bool ShowControllerSupport { get; private set; }

    /// <summary>
    /// Pan's emulation is on but doesn't run (#242): the mode is greyed out with a triangle and says "Steam-Eingabe", as
    /// that is what a game gets; [A] on it opens the controller settings on the row that says why.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ControllerModeWorks), nameof(ControllerModeUnavailableReason))]
    public partial bool ControllerModeBroken { get; private set; }

    /// <summary>"Arcadia" can be chosen and is what a game gets.</summary>
    public bool ControllerModeWorks => !ControllerModeBroken;

    /// <summary>The mode's tooltip while it doesn't work, else null.</summary>
    public string? ControllerModeUnavailableReason => ControllerModeBroken ? ControllerModes.PanNotRunning : null;

    /// <summary>
    /// Right before a game starts: the App unplugs Arcadia's pad for a game on "Steam-Eingabe" (#213). Set once by the
    /// App (and the tests); without it nothing happens.
    /// </summary>
    public Func<CancellationToken, Task>? BeforeGameStarts { get; set; }

    /// <summary>Dialogs show an ✕ in their corner while mouse or keyboard is in use; with the controller B closes them.</summary>
    [ObservableProperty]
    public partial bool ShowsCloseButtons { get; private set; }

    /// <summary>
    /// The hints show the controller's buttons; after a key press or a click the keyboard's keys and the mouse (#127).
    /// The mouse merely moving changes nothing, so a nudged mouse doesn't swap the hints under the controller.
    /// </summary>
    [ObservableProperty]
    public partial bool UsesController { get; private set; } = true;

    partial void OnUsesControllerChanged(bool value) => InputHints.UsesController = value;

    /// <summary>The Steam Controller mode of the library's focused game: "Arcadia" or "Steam-Eingabe".</summary>
    [ObservableProperty]
    public partial string FocusedControllerSupportText { get; private set; } = string.Empty;

    /// <summary>"Individueller Mausmodus" shows on games ("Von Hand" with "Individuell").</summary>
    [ObservableProperty]
    public partial bool ShowGameMouseMode { get; private set; }

    /// <summary>The library's focused game's "Individueller Mausmodus": "Standard", "Erlaubt" or "Deaktiviert".</summary>
    [ObservableProperty]
    public partial string FocusedGameMouseModeText { get; private set; } = string.Empty;

    /// <summary>Raised when Arcadia should bring its window back to the front.</summary>
    public event EventHandler? ForegroundRequested;

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

    /// <summary>
    /// Pan's question before taking the Steam Controller over, or its firmware dialog (#242): over everything, the first
    /// start and the settings included, and [B] doesn't close it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TakeoverConsent), nameof(FirmwareAttempt), nameof(IsPanDialogOpen), nameof(IsDialogOpen))]
    public partial ViewModelBase? PanDialog { get; private set; }

    public TakeoverConsentViewModel? TakeoverConsent => PanDialog as TakeoverConsentViewModel;

    public FirmwareAttemptViewModel? FirmwareAttempt => PanDialog as FirmwareAttemptViewModel;

    public bool IsPanDialogOpen => PanDialog is not null;

    /// <summary>A Pan dialog opened: the App brings Arcadia to the front, as it may be behind a game.</summary>
    public event EventHandler? PanDialogShown;

    /// <summary>A dialog or menu over the page takes the input.</summary>
    public bool IsDialogOpen => IsPanDialogOpen || IsFirstRunShown || IsSettingsOpen || IsConfirmationOpen || IsGameMenuOpen || IsViewOptionsOpen;

    /// <summary>The detail page; it takes the place of the cover grid while open, and of the search when opened from there.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailOpen), nameof(IsLibraryShown), nameof(IsSearchShown), nameof(IsTabStripShown), nameof(IsEmptyTabShown), nameof(AreGamesShown))]
    public partial GameDetailViewModel? Detail { get; private set; }

    public bool IsDetailOpen => Detail is not null;

    public bool IsLibraryShown => Detail is null;

    /// <summary>The selected tab has no games, or no launcher is active (#136): the library says so instead of covers.</summary>
    public bool IsEmptyTabShown => IsLibraryShown && Library.EmptyTab is not null;

    /// <summary>The library shows covers: the preview, the grid, the pages and their buttons in the footer.</summary>
    public bool AreGamesShown => IsLibraryShown && Library.EmptyTab is null;

    /// <summary>The search over all launchers (#12); it covers the library while open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchShown))]
    public partial SearchViewModel? Search { get; private set; }

    /// <summary>The search is open and not hidden behind a detail page opened from it.</summary>
    public bool IsSearchShown => Search is not null && Detail is null;

    /// <summary>A detail page opened from the search belongs to no tab, so the tabs are hidden there.</summary>
    public bool IsTabStripShown => Detail?.IsFromSearch != true;

    /// <summary>
    /// A detail page from the library that X left for the search; B in the search brings it back (#127). It is covered, not
    /// closed, so it stays among the live pages while kept here.
    /// </summary>
    private GameDetailViewModel? DetailBehindSearch
    {
        get;
        set
        {
            field = value;
            OnPagesChanged();
        }
    }

    /// <summary>The file and folder dialogs over the window (#138); set once the window is there.</summary>
    public ISystemDialogs? Dialogs { get; set; }

    /// <summary>The pages open, on top first; the library is always there, at the bottom.</summary>
    private IEnumerable<IPage> OpenPages() => new IPage?[] { FirstRun, Settings, Detail, Search, Library }.OfType<IPage>();

    /// <summary>A gamepad button was pressed: the hints show the controller's buttons, the search its button legend.</summary>
    public void HandleGamepad(UiAction action)
    {
        NoteInput(InputKind.Gamepad);
        Handle(action);
    }

    /// <summary>A key went down on a real keyboard: the search puts its on-screen keyboard and legend away.</summary>
    public void NoteKeyboard() => NoteInput(InputKind.Keyboard);

    /// <summary>The mouse was used: a search opened next shows no on-screen keyboard; an open one keeps it, without the legend.</summary>
    public void NotePointer() => NoteInput(InputKind.Pointer);

    private void NoteInput(InputKind kind)
    {
        _input = kind;
        ShowsCloseButtons = kind != InputKind.Gamepad;
        UsesController = kind == InputKind.Gamepad;
        foreach (var page in OpenPages())
        {
            page.InputKindChanged(kind);
        }
    }

    /// <summary>The mouse moved: dialogs show their ✕. Unlike a click, it leaves the search's legend alone.</summary>
    public void NotePointerMoved() => ShowsCloseButtons = true;

    /// <summary>
    /// A button or key was pressed: the topmost layer gets it, once. The first start and the settings cover the whole
    /// window; under them a dialog takes it before the page; what the page leaves (≡, X) opens a page over it. A press
    /// that closes a dialog doesn't reach the page below. A held press ends with <see cref="Release"/>.
    /// </summary>
    public void Handle(UiAction action)
    {
        switch (PanDialog)
        {
            case TakeoverConsentViewModel consent:
                consent.Handle(action);
                return;
            case FirmwareAttemptViewModel firmware:
                if (!firmware.Handle(action))
                {
                    ClosePanDialog(firmware);
                }

                return;
        }

        _actions.Pressed(action);
        var page = OpenPages().First();
        if (!page.CoversWindow && HandleDialog(action))
        {
            return;
        }

        if (page.Handle(action))
        {
            return;
        }

        switch (action)
        {
            case UiAction.Settings:
                OpenSettings();
                break;
            case UiAction.Search:
                OpenSearch();
                break;
        }
    }

    /// <summary>The open dialog takes the press; false if none is open.</summary>
    private bool HandleDialog(UiAction action)
    {
        if (Confirmation is { } confirmation)
        {
            if (!confirmation.Handle(action))
            {
                // Closed first: the confirmed action may open the next dialog.
                Confirmation = null;
            }

            return true;
        }

        if (GameMenu is { } menu)
        {
            if (!menu.Handle(action) && GameMenu == menu)
            {
                GameMenu = null;
            }

            return true;
        }

        if (ViewOptions is { } dialog)
        {
            if (!dialog.Handle(action))
            {
                ViewOptions = null;
            }

            return true;
        }

        return false;
    }

    /// <summary>A button or key was let go.</summary>
    public void Release(UiAction action)
    {
        (PanDialog as TakeoverConsentViewModel)?.Release(action);
        (PanDialog as FirmwareAttemptViewModel)?.Release(action);
        _actions.Release(action);
        foreach (var page in OpenPages())
        {
            page.Release(action);
        }
    }

    /// <summary>Drops held buttons, e.g. when the window loses focus and their release may never arrive.</summary>
    public void CancelHold()
    {
        TakeoverConsent?.CancelHold();
        FirmwareAttempt?.CancelHold();
        _actions.CancelHold();
        foreach (var page in OpenPages())
        {
            page.CancelHeldButtons();
        }
    }

    /// <summary>
    /// The left mouse button went down on a cover or hit: held 1 s it opens the details, with the
    /// same ring as A (owner, 2026-10-19). Let go sooner, it was a click, which only picks the game.
    /// </summary>
    public void PressTile(GameTileViewModel tile)
    {
        if (IsSearchShown)
        {
            if (!IsDialogOpen)
            {
                Search!.PressHit(tile);
            }

            return;
        }

        if (!IsSettingsOpen && !IsViewOptionsOpen && !IsConfirmationOpen && !IsDetailOpen && Search is null)
        {
            Library.Press(tile);
        }
    }

    /// <summary>A mouse button was let go anywhere.</summary>
    public void ReleasePointer() => _actions.ReleasePointer();

    /// <summary>Right click on a cover: focus it and open its menu.</summary>
    public void OpenMenuFor(GameTileViewModel tile)
    {
        if (!IsSettingsOpen && !IsViewOptionsOpen && !IsConfirmationOpen && !IsDetailOpen && Search is null)
        {
            Library.OpenMenuAt(tile);
        }
    }

    /// <summary>Text from a real keyboard while the search is shown.</summary>
    public void TypeInSearch(string text)
    {
        if (IsSearchShown && !IsDialogOpen)
        {
            Search!.TypeFromKeyboard(text);
        }
    }

    /// <summary>The mouse wheel over the hits.</summary>
    public void ScrollSearch(int delta) => Search?.ScrollByWheel(delta);

    /// <summary>Right click on a hit: focus it and open its menu.</summary>
    public void OpenMenuForHit(GameTileViewModel tile)
    {
        if (IsSearchShown && !IsDialogOpen)
        {
            Search!.OpenMenuFor(tile);
        }
    }

    /// <summary>1 … 0 on the keyboard: launcher tab 1 … 10, like LB/RB also on the detail page (#127).</summary>
    public void ChooseTabNumber(int index)
    {
        var tabs = LauncherTabs.Items;
        if (IsDialogOpen || Detail?.IsFromSearch == true || index >= tabs.Count || tabs[index] == LauncherTabs.SelectedTab)
        {
            return;
        }

        _actions.CancelHold();
        LauncherTabs.Choose(tabs[index]);
    }

    /// <summary>Shows the scanned libraries in the tabs and on every page. Keeps tab, page and focused game where they were.</summary>
    private void ApplyLibraries(IReadOnlyList<LauncherLibrary> libraries)
    {
        FirstRun?.Show(libraries);
        LauncherTabs.Show(libraries);
        UpdateSearch();
        if (LauncherTabs.SelectedTab is null)
        {
            ViewOptions = null;
            if (Detail?.IsFromLibrary == true)
            {
                Detail = null;
            }
        }

        Library.ShowTabs();
        UpdateDetail();
        RefreshLauncherSettings();
    }

    /// <summary>
    /// A launcher was renamed, got another keyword or logo (#137, #138): tabs, covers, the empty tab and the search show it
    /// at once; nothing needs to be read again.
    /// </summary>
    private void ApplyLauncherLooks()
    {
        LauncherTabs.Restyle(Session.Launchers.Info);
        Library.Restyle();
        UpdateSearch();
    }

    /// <summary>"Fertig" in the first start's question: only the launchers switched on there keep their tab.</summary>
    private void CompleteFirstRun(IReadOnlyList<string> active)
    {
        try
        {
            _firstRun.Complete(active);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Launcherauswahl nicht gespeichert: {ex.Message}");
            Toast.Show("Die Launcherauswahl konnte nicht gespeichert werden und gilt nur bis zum Beenden – Details stehen in arcadia.log.");
        }

        AppLog.Write($"Ersteinrichtung abgeschlossen, aktive Launcher: {string.Join(", ", active)}");
        FirstRun = null;
        Session.ShowChosenLaunchers();
    }

    private void RefreshLauncherSettings() => Settings?.Categories.OfType<LauncherSettingsViewModel>().FirstOrDefault()?.Refresh();

    /// <summary>The user picked <paramref name="tab"/>; Arcadia opens with it next time. A detail page stays open on the tab's first game.</summary>
    private void OnTabChosen(LauncherTabViewModel tab)
    {
        Library.ShowTab();
        SaveView(settings => settings with { LastTabLauncherId = tab.Launcher.Id });
        if (Detail is not null)
        {
            if (Library.PageGames.FirstOrDefault() is { } first)
            {
                OpenDetail(first.Game.Key);
            }
            else
            {
                Detail = null;
            }
        }
    }

    /// <summary>Opens the detail page on <paramref name="key"/> in the selected tab, or among the hits while the search is open.</summary>
    private void OpenDetail(string key)
    {
        if (Search is { } search)
        {
            var hit = Carousel.Find(search.Hits, key, 0);
            Detail = hit < 0 ? null : new GameDetailViewModel(null, search.Hits, hit, null, CreateDetailTile, _actions, this, LauncherTabs, createViewOptions: null);
            return;
        }

        if (LauncherTabs.SelectedTab is not { } tab)
        {
            return;
        }

        var games = Library.SortedGames(tab);
        var index = Carousel.Find(games, key, 0);
        Detail = index < 0 ? null : new GameDetailViewModel(
            tab.Name, games, index, Library.SortLabel, CreateDetailTile, _actions, this, LauncherTabs, () => Library.CreateViewOptions(forDetailPage: true));
    }

    /// <summary>
    /// B: always back to the library (owner, 2026-10-21), on the page of the game shown last and with the focus on it.
    /// From the search that is the tab of the game's launcher, and the search closes.
    /// </summary>
    [RelayCommand]
    private void CloseDetail()
    {
        if (Detail is { IsFromSearch: true } fromSearch)
        {
            Detail = null;
            DetailBehindSearch = null;
            CloseSearch();
            Library.Show(fromSearch.Current.Tile.Game);
            return;
        }

        var detail = Detail;
        Detail = null;
        if (detail is not null)
        {
            Library.ShowAt(detail.Index);
        }
    }

    /// <summary>The list changed (rescan, new sort): the detail page keeps its game in the middle.</summary>
    private void UpdateDetail()
    {
        if (Detail is { IsFromLibrary: true } detail && LauncherTabs.SelectedTab is { } tab && !detail.Update(Library.SortedGames(tab), Library.SortLabel))
        {
            Detail = null;
        }
    }

    private GameTileViewModel CreateDetailTile(Game game) => CreateTile(game, _ => { }, _ => { });

    private GameTileViewModel CreateTile(Game game, Action<GameTileViewModel> focus, Action<GameTileViewModel> activate)
    {
        var tile = new GameTileViewModel(game, LauncherTabs.LauncherOf(game), LauncherTabs.BadgeOf(game), _time.GetLocalNow(), focus, activate);
        ShowStates(tile);
        return tile;
    }

    /// <summary>A new cover takes its game's install and run state.</summary>
    private void ShowStates(GameTileViewModel tile)
    {
        tile.InstallState = Session.InstallStateOf(tile.Game.Key);
        tile.RunState = Session.RunStateOf(tile.Game.Key);
    }

    /// <summary>The covers of every open page, also of one covered by another.</summary>
    private IEnumerable<GameTileViewModel> ShownTiles() => OpenPages().SelectMany(p => p.ShownTiles);

    private void UpdateInstallStates()
    {
        foreach (var tile in ShownTiles())
        {
            tile.InstallState = Session.InstallStateOf(tile.Game.Key);
        }
    }

    /// <summary>A game is starting, its start failed, it runs or ended: every shown cover takes its run state again.</summary>
    private void UpdateRunStates()
    {
        foreach (var tile in ShownTiles())
        {
            tile.RunState = Session.RunStateOf(tile.Game.Key);
        }
    }

    private void ShowNotice(LibraryNotice notice)
    {
        var (text, sticky) = LibraryToasts.For(notice, LauncherTabs);
        Toast.Show(text, sticky);
    }

    /// <summary>
    /// X: the search over all launchers, with the on-screen keyboard. From a detail page opened from the search, back to
    /// the search with its text; from any other detail page, the search over it, and B there leads back to it (#127).
    /// </summary>
    [RelayCommand]
    private void OpenSearch()
    {
        if (!LauncherTabs.HasGames)
        {
            return;
        }

        if (Detail is { IsFromSearch: true } fromSearch && Search is { } open)
        {
            Detail = null;
            open.FocusHit(fromSearch.Current.Tile.Game.Key);
            return;
        }

        if (Detail is { } detail)
        {
            DetailBehindSearch = detail;
            Detail = null;
        }

        // Opened with the gamepad, the search starts with the on-screen keyboard; by key or click without.
        Search = new SearchViewModel(
            LauncherTabs,
            _actions,
            this,
            KeyboardLayout.Find(_settings.Current.KeyboardLayout),
            layout => SaveView(settings => settings with { KeyboardLayout = layout.Id }),
            CreateTile,
            _logos.BadgeOf,
            _input == InputKind.Gamepad,
            _time,
            action => Avalonia.Threading.Dispatcher.UIThread.Post(action));
    }

    /// <summary>Back to the library, where it was before the search.</summary>
    private void CloseSearch()
    {
        if (ViewOptions is not null && _viewOptionsPage is not null && _viewOptionsPage == Search)
        {
            ViewOptions = null;
        }

        Search = null;

        // Back to the detail page the search was opened from, if its game is still in the tab.
        if (DetailBehindSearch is { } detail)
        {
            DetailBehindSearch = null;
            if (LauncherTabs.SelectedTab is { } tab && detail.Update(Library.SortedGames(tab), Library.SortLabel))
            {
                Detail = detail;
            }
        }
    }

    /// <summary>A rescan: same input, fresh hits; a detail page opened from the search keeps its game.</summary>
    private void UpdateSearch()
    {
        if (Search is not { } search)
        {
            return;
        }

        if (!LauncherTabs.HasGames)
        {
            Detail = Detail?.IsFromSearch == true ? null : Detail;
            CloseSearch();
            return;
        }

        search.Update();
        RebuildViewOptions();
        KeepHoldOnRebuiltPage();
        if (Detail is { IsFromSearch: true } detail && !detail.Update(search.Hits, null))
        {
            Detail = null;
        }
    }

    /// <summary>A rescan rebuilt the page or the hits: a press held meanwhile moves to its game's new tile.</summary>
    private void KeepHoldOnRebuiltPage() => _actions.KeepHoldOn(IsSearchShown ? Search!.Results : Library.PageGames);

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

    [RelayCommand]
    private void OpenSettings() => ShowSettings(first: null);

    /// <summary>The header's Steam Controller symbols: straight to the category the message belongs to.</summary>
    [RelayCommand]
    private void OpenControllerSettings() => ShowSettings(typeof(ControllerSettingsViewModel));

    /// <summary>The header's Arcadia update symbol.</summary>
    [RelayCommand]
    private void OpenAboutSettings() => ShowSettings(typeof(AboutSettingsViewModel));

    /// <summary>The settings page (#15) in place of the library, which stays as it is underneath for the way back.</summary>
    private void ShowSettings(Type? first, string? launcherId = null)
    {
        var changes = new SettingsChanges(_settings);
        SettingsCategoryViewModel[] categories =
        [
            new LauncherSettingsViewModel(this, changes, _time, launcherId),
            new ControllerSettingsViewModel(_controllerSupport, changes, _pan, GameInTheWay, _time),
            new AboutSettingsViewModel(_updates, _updates.InstallAsync),
        ];
        Settings = new SettingsPageViewModel(categories, changes, this, categories.FirstOrDefault(c => c.GetType() == first));
    }

    [RelayCommand]
    private void CloseSettings() => Settings = null;

    partial void OnSettingsChanged(SettingsPageViewModel? value)
    {
        OnPagesChanged();
        UpdateGameMouseModeState();
        UpdatePanState();
    }

    partial void OnFirstRunChanged(FirstRunViewModel? value) => OnPagesChanged();

    partial void OnDetailChanged(GameDetailViewModel? value) => OnPagesChanged();

    partial void OnSearchChanged(SearchViewModel? value) => OnPagesChanged();

    /// <summary>
    /// The one lifecycle of every page (#228, the contract on <see cref="IPage"/>), run after each page opens, closes or is
    /// kept behind another. The page that lost the top has its held buttons and the hold of A or ≡ dropped, so their
    /// release can't type, start a game or open details on the page now shown. A page neither open nor kept is closed:
    /// Main lets go of it and disposes it if it is disposable.
    /// </summary>
    private void OnPagesChanged()
    {
        var open = OpenPages().ToList();
        var top = open[0];
        if (top != _topPage)
        {
            _topPage?.CancelHeldButtons();
            _actions.CancelHold();
            _topPage = top;
        }

        IReadOnlyList<IPage> live = DetailBehindSearch is { } kept ? [.. open, kept] : open;
        var closed = _livePages.Except(live).ToList();
        _livePages = live;
        foreach (var page in closed.OfType<IDisposable>())
        {
            page.Dispose();
        }
    }

    /// <summary>Arcadia is closing: every page still open is disposed, and the library's delayed rescans stop.</summary>
    public void Dispose()
    {
        foreach (var page in _livePages.OfType<IDisposable>())
        {
            page.Dispose();
        }

        _livePages = [];
        Session.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnLibraryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LibraryViewModel.FocusedGame):
                UpdateControllerSupportState();
                UpdateGameMouseModeState();
                break;
            case nameof(LibraryViewModel.EmptyTab):
                OnPropertyChanged(nameof(IsEmptyTabShown));
                OnPropertyChanged(nameof(AreGamesShown));
                break;
        }
    }

    /// <summary>Switches the focused game's "Individueller Mausmodus": Standard → Erlaubt → Deaktiviert.</summary>
    [RelayCommand]
    private void CycleGameMouseMode()
    {
        if (Library.FocusedGame?.Game is { } game)
        {
            CycleGameMouseModeOf(game);
        }
    }

    private void CycleGameMouseModeOf(Game game)
    {
        try
        {
            _settings.Update(s => s with { MouseMode = s.MouseMode.WithGameChoice(game.Key, GameMouseModes.Next(s.MouseMode.GameChoiceFor(game.Key))) });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Individueller Mausmodus nicht gespeichert: {ex.Message}");
            Toast.Show("Die Änderung konnte nicht gespeichert werden und gilt nur bis zum Beenden – Details stehen in arcadia.log.");
        }

        UpdateGameMouseModeState();
    }

    private bool _steamControllerSeenBySdl;

    private void UpdateGameMouseModeState()
    {
        var mouseMode = _settings.Current.MouseMode;
        var show = mouseMode.Activation == MouseModeActivation.Manual && mouseMode.InGames == MouseModeInGames.PerGame;
        // The detail page's button names what "Standard" means, so a changed default rebuilds it too.
        if (ShowGameMouseMode != show || _shownDefaultMouseModeAllowed != mouseMode.PerGameDefaultAllowed)
        {
            ShowGameMouseMode = show;
            _shownDefaultMouseModeAllowed = mouseMode.PerGameDefaultAllowed;
            Detail?.RebuildActions();
        }

        if (Library.FocusedGame?.Game is { } game)
        {
            FocusedGameMouseModeText = GameMouseModes.Of(mouseMode.GameChoiceFor(game.Key), mouseMode.PerGameDefaultAllowed);
        }
    }

    /// <summary>
    /// Switches the focused game between "Arcadia" and "Steam-Eingabe"; not while it runs (#95). While Pan's emulation
    /// doesn't run it opens the controller settings on the row that says why (#242).
    /// </summary>
    [RelayCommand]
    private void CycleControllerSupport()
    {
        if (ControllerModeBroken)
        {
            ShowPanProblem();
            return;
        }

        if (Library.FocusedGame is not { IsRunningOrStarting: false, Game: var game })
        {
            return;
        }

        _controllerSupport.SetModeFor(game, !_settings.Current.UsesArcadiaPad(game.Key));
    }

    private IPanEmulationView _pan = new NoPanEmulation();

    /// <summary>Pan's emulation (#242), once at the start: the mode, the header and the controller settings follow it.</summary>
    public void ConnectPan(IPanEmulationView pan)
    {
        _pan = pan;
        pan.Changed += () => Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePanState);
        pan.Takeover.ConsentNeeded += parts => Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowTakeoverConsent(parts));
        pan.Takeover.Changed += () => Avalonia.Threading.Dispatcher.UIThread.Post(OnTakeoverChanged);
        pan.FirmwareAttempt.Started += () => Avalonia.Threading.Dispatcher.UIThread.Post(ShowFirmwareAttempt);
        UpdatePanState();
    }

    private void UpdatePanState()
    {
        var status = _pan.Status;
        Status.ShowPan(status);
        var broken = _settings.Current.PanEnabled && status.HasProblem;
        if (ControllerModeBroken != broken)
        {
            ControllerModeBroken = broken;
            Detail?.RebuildActions();
        }

        UpdateControllerSupportState();
    }

    /// <summary>The controller settings with the row focused that says why Pan's emulation doesn't run; B goes back.</summary>
    private void ShowPanProblem()
    {
        ShowSettings(typeof(ControllerSettingsViewModel));
        if (Settings?.Selected is ControllerSettingsViewModel controller)
        {
            controller.FocusRow(controller.PanWarningKey ?? PanRows.MainKey);
        }
    }

    /// <summary>The title of a game that runs or is being started, for "Erst änderbar, wenn „…“ beendet ist" (#242).</summary>
    private string? GameInTheWay() =>
        Session.Libraries.SelectMany(library => library.Games).FirstOrDefault(game => Session.RunStateOf(game.Key) is not null)?.Title;

    /// <summary>SDL sees a Steam Controller or no longer does (#212); the per-game mode shows only while it does (#213).</summary>
    public void SetSteamControllerSeen(bool seen)
    {
        _steamControllerSeenBySdl = seen;
        UpdateControllerSupportState();
    }

    private void UpdateControllerSupportState()
    {
        // The mode only matters with a Steam Controller; other pads are never emulated (#72, #213). With Pan off nothing
        // is emulated, so there is no mode either (#242).
        var show = _steamControllerSeenBySdl
            && (_controllerSupport.StartsSisr || (_settings.Current.PanEnabled && _pan.Status.State != PanEmulationState.Unsupported));
        if (ShowControllerSupport != show)
        {
            ShowControllerSupport = show;
            Detail?.RebuildActions();
        }

        if (Library.FocusedGame?.Game is { } game)
        {
            FocusedControllerSupportText = _actions.ControllerModeOf(game);
        }
    }

    /// <summary>The "Ansicht" dialog or the search's filter; <paramref name="create"/> rebuilds it after each choice.</summary>
    private void ShowViewOptions(Func<ViewOptionsViewModel?> create, IPage page)
    {
        _createViewOptions = create;
        _viewOptionsPage = page;
        ViewOptions = create();
    }

    [RelayCommand]
    private void CloseViewOptions() => ViewOptions = null;

    // A closed dialog lets go of its page and factory, so a closed search isn't kept alive by them.
    partial void OnViewOptionsChanged(ViewOptionsViewModel? value)
    {
        if (value is null)
        {
            _createViewOptions = null;
            _viewOptionsPage = null;
        }
    }

    private void RebuildViewOptions()
    {
        if (ViewOptions is { } current && _createViewOptions?.Invoke() is { } rebuilt)
        {
            rebuilt.FocusPosition = current.FocusPosition;
            ViewOptions = rebuilt;
        }
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

    bool IGameActionsOwner.ShowControllerMode => ShowControllerSupport;

    bool IGameActionsOwner.ControllerModeBroken => ControllerModeBroken;

    void IGameActionsOwner.ShowPanProblem() => ShowPanProblem();

    Task IGameActionsOwner.BeforeGameStartsAsync(CancellationToken cancellationToken) =>
        BeforeGameStarts?.Invoke(cancellationToken) ?? Task.CompletedTask;

    bool IGameActionsOwner.ShowGameMouseMode => ShowGameMouseMode;

    GameFolders IGameActionsOwner.GameFolders => Session.GameFolders;

    void IGameActionsOwner.ShowDetails(GameTileViewModel tile) => OpenDetail(tile.Game.Key);

    void IGameActionsOwner.Confirm(ConfirmationViewModel confirmation) => Confirmation = confirmation;

    /// <summary>A question from outside the pages, e.g. "Pan aktualisieren" at start (#243); dropped while another dialog is open.</summary>
    public void Ask(ConfirmationViewModel confirmation)
    {
        if (!IsDialogOpen)
        {
            Confirmation = confirmation;
        }
    }

    /// <summary>"Arcadia jetzt beenden" in a dialog; the App closes the window, as its close button would.</summary>
    public event EventHandler? QuitRequested;

    /// <summary>Steam runs and the real puck was visible: Pan asks before it hides (#242, owner's design 2026-10-28).</summary>
    public void ShowTakeoverConsent(SteamControllerParts parts)
    {
        if (_pan.Takeover.State != TakeoverState.Asking)
        {
            return;
        }

        var takeover = _pan.Takeover;
        TakeoverConsentViewModel? dialog = null;
        dialog = new TakeoverConsentViewModel(parts, takeOver =>
        {
            takeover.Answer(takeOver);
            ClosePanDialog(dialog!);
        }, _time, action => Avalonia.Threading.Dispatcher.UIThread.Post(action));
        ShowPanDialog(dialog);
    }

    /// <summary>Steam tries a firmware update through the copy: Arcadia holds it until Steam gave up (#242).</summary>
    public void ShowFirmwareAttempt()
    {
        if (FirmwareAttempt is not null || !_pan.FirmwareAttempt.IsActive)
        {
            return;
        }

        var dialog = new FirmwareAttemptViewModel(
            _pan.FirmwareAttempt,
            _pan.Connected,
            _pan.ReleaseForFirmwareAsync,
            () => QuitRequested?.Invoke(this, EventArgs.Empty),
            _time,
            action => Avalonia.Threading.Dispatcher.UIThread.Post(action));
        dialog.Closed += () => ClosePanDialog(dialog);
        ShowPanDialog(dialog);
    }

    private void ShowPanDialog(ViewModelBase dialog)
    {
        CancelHold();
        (PanDialog as IDisposable)?.Dispose();
        PanDialog = dialog;
        PanDialogShown?.Invoke(this, EventArgs.Empty);
    }

    private void ClosePanDialog(ViewModelBase dialog)
    {
        if (PanDialog == dialog)
        {
            (dialog as IDisposable)?.Dispose();
            PanDialog = null;
        }
    }

    /// <summary>
    /// The question went (switched off meanwhile): its dialog goes too. Taken over again in the settings: a toast says so,
    /// as the row that did it disappears.
    /// </summary>
    private void OnTakeoverChanged()
    {
        var state = _pan.Takeover.State;
        if (TakeoverConsent is { } consent && state != TakeoverState.Asking)
        {
            ClosePanDialog(consent);
        }

        if (state == TakeoverState.Granted && _takeover is TakeoverState.Declined or TakeoverState.Released)
        {
            var parts = _pan.Takeover.Asked is not SteamControllerParts.None and var asked ? asked : _pan.Connected;
            Toast.Show($"Pan: {SteamControllerPartsNames.Names(parts)} übernommen");
        }

        _takeover = state;
    }

    private TakeoverState _takeover;

    /// <summary>"OK" on the note after the release for a firmware update.</summary>
    [RelayCommand]
    private void CloseFirmwareNote()
    {
        if (FirmwareAttempt is { IsReleased: true } firmware)
        {
            ClosePanDialog(firmware);
        }
    }

    /// <summary>The pointer went down on a held button (Pan's dialogs, the settings row): held as [A] would be.</summary>
    public void PressHold(HoldButtonViewModel button)
    {
        switch (PanDialog)
        {
            case TakeoverConsentViewModel consent:
                consent.Press(button);
                return;
            case FirmwareAttemptViewModel firmware:
                firmware.Press(button);
                return;
        }

        if (Settings?.Selected.Rows.OfType<HoldRowViewModel>().FirstOrDefault(row => row.Hold == button) is { } row)
        {
            row.PressByPointer();
        }
    }

    void IGameActionsOwner.CycleGameMouseMode(Game game) => CycleGameMouseModeOf(game);

    void IGameActionsOwner.RunStatesChanged() => UpdateRunStates();

    void IPageHost.Close(IPage page)
    {
        if (page == Search)
        {
            CloseSearch();
        }
        else if (page == Detail)
        {
            CloseDetail();
        }
        else if (page == Settings)
        {
            Settings = null;
        }
    }

    void IPageHost.ShowViewOptions(IPage page, Func<ViewOptionsViewModel?> create) => ShowViewOptions(create, page);

    void IPageHost.ViewOptionsChanged() => RebuildViewOptions();

    void IPageHost.ShowMenu(GameTileViewModel tile, bool atCover) =>
        GameMenu = new GameMenuViewModel(tile, GameActionViewModel.For(tile, _actions)) { AtCover = atCover };

    void ILibraryHost.OpenLauncherSettings(string? launcherId) => ShowSettings(typeof(LauncherSettingsViewModel), launcherId);

    void ILibraryHost.Rescan(LauncherInfo launcher) => Session.RescanAsync(launcher).Forget("Neu einlesen");

    void ILibraryHost.LibraryOutdated() => Session.RefreshLibraryAsync().Forget("Bibliothek aktualisieren");

    void ILibraryHost.PageRebuilt() => KeepHoldOnRebuiltPage();

    void ILibraryHost.ViewChanged()
    {
        UpdateDetail();
        RebuildViewOptions();
    }

    LauncherRegistry ILauncherSettingsHost.Launchers => Session.Launchers;

    LauncherLibrary? ILauncherSettingsHost.LibraryOf(string launcherId) => LauncherTabs.Find(launcherId)?.Library;

    DateTimeOffset? ILauncherSettingsHost.ScannedAt(string launcherId) => Session.ScannedAt(launcherId);

    void ILauncherSettingsHost.LauncherChoiceChanged() => Session.ShowChosenLaunchers();

    void ILauncherSettingsHost.RescanLaunchers() => Session.RescanAsync(null).Forget("Launcher neu einlesen");

    LauncherLogos ILauncherSettingsHost.Logos => _logos;

    void ILauncherSettingsHost.LauncherLooksChanged() => ApplyLauncherLooks();

    void ILauncherSettingsHost.RescanLauncher(string launcherId) =>
        Session.RescanAsync(Session.Launchers.Info(launcherId)).Forget("Launcher neu einlesen");

    OnScreenKeyboardViewModel ILauncherSettingsHost.CreateKeyboard(IKeyboardTarget target)
    {
        var keyboard = new OnScreenKeyboardViewModel(
            KeyboardLayout.Find(_settings.Current.KeyboardLayout),
            target,
            _time,
            action => Avalonia.Threading.Dispatcher.UIThread.Post(action))
        {
            UsesController = UsesController,
        };
        keyboard.LayoutChosen += layout => SaveView(settings => settings with { KeyboardLayout = layout.Id });
        return keyboard;
    }

    void ILauncherSettingsHost.Notify(string message) => Toast.Show(message);
}
