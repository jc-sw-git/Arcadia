using System.Collections.ObjectModel;
using Arcadia.App.Filter;
using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Messages;
using Arcadia.App.Search;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.App.Setup;
using Arcadia.App.Status;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Core.Artwork;
using Arcadia.Core.Metadata;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.UserData;
using Arcadia.Core.Search;
using Arcadia.Core.Settings;
using Arcadia.Input;
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
public partial class MainViewModel : ViewModelBase, IGameActionsOwner, ILibraryHost, ILauncherSettingsHost, IWebPageHost, IControllerModeHost, IStatusModalHost, IChangeArtworkHost, IDisposable
{
    private readonly ControllerSupportService _controllerSupport;
    private readonly ArcadiaUpdateService _updates;
    private readonly SettingsService _settings;
    private readonly LauncherLogos _logos;
    private readonly FirstRunSetup _firstRun;
    private readonly TimeProvider _time;
    private readonly GameActions _actions;
    private readonly RepeatGate _repeats = new();
    private readonly ArtworkService? _artwork;
    private readonly MetadataService? _metadata;
    private readonly FilterFactsSource _filterFacts;

    private Func<ViewOptionsViewModel?>? _createViewOptions;

    // The page whose options the dialog shows.
    private IPage? _viewOptionsPage;

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
        InstallFolderCheck installFolders,
        ArtworkService? artwork = null,
        MetadataService? metadata = null,
        GameTitles? titles = null)
    {
        _artwork = artwork;
        _metadata = metadata;
        ArtworkLoader = new ArtworkLoader(artwork);
        _logos = logos;
        _controllerSupport = controllerSupport;
        _updates = updates;
        _settings = settings;
        _firstRun = firstRun;
        _time = time;
        LauncherTabs = new LauncherTabsViewModel(logos.BadgeOf);
        Messages = new RecentMessages(time, AppLog.Write);
        Formatting.ClockFormat = settings.Current.Localization.Clock;
        Status = new StatusBarViewModel(controllerSupport, updates, time, new StatusMessageChanges(Messages, AppLog.Write));
        Status.PropertyChanged += OnStatusChanged;
        Toast = new ToastViewModel(time, Messages);
        Session = new LibrarySession(library, installs, running, launchedGames, time, artwork, metadata, titles);
        WatchUserData(Session.Titles.UserData);
        Tags = new OwnTags(Session.Titles.UserData, Formatting.Culture);
        _filterFacts = new FilterFactsSource(MetadataOf, Tags);
        WatchOwnTags();
        if (artwork is not null)
        {
            artwork.Changed += (_, keys) => Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateArtwork(keys));
            artwork.Failed += (_, e) => Avalonia.Threading.Dispatcher.UIThread.Post(() => AppErrors.Report(e, AppTexts.ErrorContextArtwork));
            WatchSteamGridDb(artwork);
            artwork.Open();
        }

        if (metadata is not null)
        {
            WatchMetadata(metadata);
        }

        WatchLanguageRestart();

        Session.LibrariesChanged += (_, _) => ApplyLibraries(Session.Libraries);
        Session.LaunchersRead += (_, _) => RefreshLauncherSettings();
        Session.InstallStatesChanged += (_, _) => UpdateInstallStates();
        Session.RunStatesChanged += (_, _) => UpdateRunStates();
        Session.StartAgainRequested += (_, gameKey) => StartAgain(gameKey);
        Session.Noticed += (_, notice) => ShowNotice(notice);

        _actions = new GameActions(this, launcher, controllerSupport, installs, running, launchedGames, settings, Toast, time);
        _actions.ForegroundRequested += (_, e) => ForegroundRequested?.Invoke(this, e);
        Input.KindChanged += OnInputKindChanged;
        ControllerMode = new ControllerModeViewModel(controllerSupport, settings, Status, _actions, this);
        PanDialogs = new PanDialogsViewModel(controllerSupport, settings, Toast, time, () => QuitRequested?.Invoke(this, EventArgs.Empty), CancelHold);
        PanDialogs.PropertyChanged += OnPanDialogsChanged;
        Library = new LibraryViewModel(LauncherTabs, settings, SaveView, _actions, ShowStates, this, time, installFolders, _filterFacts);
        SearchKeyboard = CreateSharedKeyboard();
        SettingsKeyboard = CreateSharedKeyboard();
        Library.PropertyChanged += OnLibraryChanged;
        LauncherTabs.Chosen += OnTabChosen;
        if (firstRun.IsPending)
        {
            StartFirstRun(library.Launchers, logos.BadgeOf);
        }

        AppErrors.Occurred += OnAppError;
        ControllerMode.FocusedGameChanged();
        CreateImageShow();
    }

    /// <summary>The toasts and status changes since the start, for "Letzte Meldungen" (#433).</summary>
    public RecentMessages Messages { get; }

    /// <summary>Arcadia's logs, which "Über Arcadia" opens (#433); the tests put a fake in.</summary>
    public ILogFiles Logs { get; set; } = new LogFiles();

    /// <summary>
    /// A log (#433) or a web page (#489) opened by controller in a program of its own: the mouse mode holds on until
    /// Arcadia is back in front.
    /// </summary>
    public event EventHandler? OpenedForMouseByController;

    private static readonly TimeSpan LogOpenTimeout = TimeSpan.FromSeconds(10);

    public StatusBarViewModel Status { get; }

    public ToastViewModel Toast { get; }

    /// <summary>Loading and rescanning the launchers, installs and running games; Main shows what it reads and notices.</summary>
    public LibrarySession Session { get; }

    /// <summary>The launcher tabs, shared by the library, the detail page and the search.</summary>
    public LauncherTabsViewModel LauncherTabs { get; }

    /// <summary>Decodes the cached covers and backgrounds for the views (#487).</summary>
    public ArtworkLoader ArtworkLoader { get; }

    /// <summary>The cover grid of the selected tab, under every other page.</summary>
    public LibraryViewModel Library { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsOpen))]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    public partial SettingsPageViewModel? Settings { get; private set; }

    public bool IsSettingsOpen => Settings is not null;

    /// <summary>The first start's question which launchers Arcadia uses (#139), in place of the library until "Fertig".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFirstRunShown))]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    public partial FirstRunViewModel? FirstRun { get; private set; }

    public bool IsFirstRunShown => FirstRun is not null;

    /// <summary>Which input and which controller are in use: the button hints and the dialogs' ✕ follow it.</summary>
    public InputInUseViewModel Input { get; } = new();

    /// <summary>The Steam Controller mode per game and Pan's emulation behind it.</summary>
    public ControllerModeViewModel ControllerMode { get; }

    /// <summary>Pan's firmware notice and update page, over everything else.</summary>
    public PanDialogsViewModel PanDialogs { get; }

    /// <summary>
    /// Right before a game starts: the App unplugs Arcadia's pad for a game on "Steam-Eingabe" (#213). Set once by the
    /// App (and the tests); without it nothing happens.
    /// </summary>
    public Func<CancellationToken, Task>? BeforeGameStarts { get; set; }

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
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    public partial ViewOptionsViewModel? ViewOptions { get; private set; }

    public bool IsViewOptionsOpen => ViewOptions is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGameMenuOpen))]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    public partial GameMenuViewModel? GameMenu { get; private set; }

    public bool IsGameMenuOpen => GameMenu is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirmationOpen))]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    public partial ConfirmationViewModel? Confirmation { get; private set; }

    public bool IsConfirmationOpen => Confirmation is not null;

    /// <summary>The header's messages in a modal (#166), over the library or the detail page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusModalOpen))]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    public partial StatusModalViewModel? StatusModal { get; private set; }

    public bool IsStatusModalOpen => StatusModal is not null;

    /// <summary>The selected tab's filter (#44), over the library.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterOpen))]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    public partial FilterModalViewModel? FilterModal { get; private set; }

    public bool IsFilterOpen => FilterModal is not null;

    /// <summary>A dialog or menu over the page takes the input.</summary>
    public bool IsDialogOpen => PanDialogs.IsOpen || IsFirstRunShown || IsSettingsOpen || IsConfirmationOpen || IsGameMenuOpen || IsViewOptionsOpen || IsWebSignInOpen
        || IsStatusModalOpen || IsFilterOpen || IsChangeArtworkOpen || IsCustomizeOverviewOpen || IsChangeTitleOpen || IsWaitingListOpen
        || IsTagWindowOpen || IsTagListOpen;

    /// <summary>
    /// The web page Arcadia's browser shows or loads for a launcher (#83): shown over everything for a sign-in, or loaded
    /// unseen. A shown page takes every press, so B ends it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWebSignInOpen))]
    public partial WebPageViewModel? WebPage { get; private set; }

    public bool IsWebSignInOpen => WebPage?.IsShown == true;

    void IWebPageHost.Open(WebPageViewModel page) => WebPage = page;

    /// <summary>The ✕ over a shown page: like B, it cancels the sign-in.</summary>
    [RelayCommand]
    private void CloseWebPage() => WebPage?.Cancel();

    void IWebPageHost.Close(WebPageViewModel page)
    {
        if (WebPage == page)
        {
            WebPage = null;
        }
    }

    /// <summary>The detail page; it takes the place of the cover grid while open, and of the search when opened from there.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailOpen), nameof(IsLibraryShown), nameof(IsSearchShown), nameof(IsTabStripShown), nameof(IsEmptyTabShown),
        nameof(IsViewLineShown), nameof(AreGamesShown), nameof(IsNoHitsShown))]
    public partial GameDetailViewModel? Detail { get; private set; }

    public bool IsDetailOpen => Detail is not null;

    public bool IsLibraryShown => Detail is null;

    /// <summary>The selected tab has no games, or no launcher is active (#136): the library says so instead of covers.</summary>
    public bool IsEmptyTabShown => IsLibraryShown && Library.EmptyTab is not null;

    /// <summary>The tab has entries: "Ansicht" and the filter's funnel beside its name.</summary>
    public bool IsViewLineShown => IsLibraryShown && Library.EmptyTab is null;

    /// <summary>The library shows covers: the preview, the grid, the pages and their buttons in the footer.</summary>
    public bool AreGamesShown => IsViewLineShown && !Library.FilterHidesAll;

    /// <summary>The tab's filter hides all its entries (#44): the library says so, with "Filter ändern".</summary>
    public bool IsNoHitsShown => IsViewLineShown && Library.FilterHidesAll;

    /// <summary>The search over all launchers (#12); it covers the library while open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchShown))]
    public partial SearchViewModel? Search { get; private set; }

    /// <summary>
    /// The search page's on-screen keyboard and the places of its hits, which every search uses again: building their
    /// controls anew for each search took most of the half second between X and the search on screen (#477).
    /// </summary>
    public SharedKeyboard SearchKeyboard { get; }

    /// <inheritdoc cref="SearchKeyboard"/>
    public ObservableCollection<TileSlotViewModel> SearchSlots { get; } = [];

    /// <summary>The on-screen keyboard of the texts written in the settings, which every text uses again (#477).</summary>
    public SharedKeyboard SettingsKeyboard { get; }

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

    /// <summary>
    /// The detail page the settings were opened over, while a game of the tag list shows in its place (#586); it comes back
    /// once the settings close. Covered, not closed, so it stays among the live pages while kept here.
    /// </summary>
    private GameDetailViewModel? DetailBehindSettings
    {
        get;
        set
        {
            field = value;
            OnPagesChanged();
        }
    }

    /// <summary>Opens the dialog again that opened the settings once they close (#586): B goes back where it came from.</summary>
    private Action? _dialogBehindSettings;

    /// <summary>The file and folder dialogs over the window (#138); set once the window is there.</summary>
    public ISystemDialogs? Dialogs { get; set; }

    /// <summary>The pages open, on top first; the library is always there, at the bottom.</summary>
    private IEnumerable<IPage> OpenPages() => new IPage?[] { FirstRun, Settings, Detail, Search, Library }.OfType<IPage>();

    /// <summary>A gamepad button was pressed: the hints show the controller's buttons, the search its button legend.</summary>
    /// <param name="repeat">The button is held and acts again (<see cref="Handle(UiAction, bool)"/>).</param>
    public void HandleGamepad(UiAction action, bool repeat = false)
    {
        NoteInput(InputKind.Gamepad);
        Handle(action, repeat);
    }

    /// <summary>A key went down on a real keyboard: the search puts its on-screen keyboard and legend away.</summary>
    public void NoteKeyboard() => NoteInput(InputKind.Keyboard);

    /// <summary>The mouse was used: a search opened next shows no on-screen keyboard; an open one keeps it, without the legend.</summary>
    public void NotePointer() => NoteInput(InputKind.Pointer);

    /// <summary>The mouse moved: dialogs show their ✕. Unlike a click, it leaves the search's legend alone.</summary>
    public void NotePointerMoved() => Input.NotePointerMoved();

    private void NoteInput(InputKind kind) => Input.Note(kind);

    private void OnInputKindChanged(InputKind kind)
    {
        foreach (var page in OpenPages())
        {
            page.InputKindChanged(kind);
        }
    }

    /// <summary>
    /// A button or key was pressed: the topmost layer gets it, once. The first start and the settings cover the whole
    /// window; under them a dialog takes it before the page; what the page leaves (≡, X) opens a page over it. A press
    /// that closes a dialog doesn't reach the page below. A held press ends with <see cref="Release"/>.
    /// </summary>
    /// <param name="repeat">
    /// The button or key is held and acts again (#476): a step past the end stops there (<see cref="HeldPress"/>). It acts
    /// only where its press did: once the page, a dialog or the page's layout changed, it does nothing until let go.
    /// </param>
    public void Handle(UiAction action, bool repeat = false)
    {
        if (SwallowedByImageShow(action, repeat))
        {
            return;
        }

        if (!_repeats.Allows(action, repeat, ShownLayer()))
        {
            return;
        }

        HeldPress.Run(repeat, () => HandleOnTop(action));
    }

    /// <summary>
    /// What a press acts on: the page on top (by kind, so the detail page turning to another tab's game stays the same),
    /// the dialog over it, and the page's own layout (<see cref="IPage.Layout"/>).
    /// </summary>
    private ShownLayer ShownLayer()
    {
        var page = OpenPages().First();
        object? dialog = (object?)PanDialogs.PanDialog ?? (object?)PanDialogs.PanUpdate ?? (IsWebSignInOpen ? WebPage : null) ?? (object?)TagDialog
            ?? (object?)Confirmation ?? (object?)GameMenu ?? (object?)ViewOptions ?? (object?)StatusModal ?? (object?)FilterModal ?? (object?)ChangeArtwork ?? (object?)CustomizeOverview ?? (object?)ChangeTitle ?? WaitingList;
        return new ShownLayer(page.GetType(), dialog, page.Layout);
    }

    private void HandleOnTop(UiAction action)
    {
        if (PanDialogs.Handle(action))
        {
            return;
        }

        _actions.Pressed(action);
        if (WebPage is { IsShown: true } webPage)
        {
            webPage.Handle(action);
            return;
        }

        // Opened from the settings and shown over them, so it comes before the page that covers the window.
        if (WaitingList is { } waiting)
        {
            waiting.Handle(action);
            return;
        }

        // Over the detail page, the library or the settings, with its own questions and keyboard (#495).
        if (TagDialog is { } tags)
        {
            tags.Handle(action);
            return;
        }

        var page = OpenPages().First();
        if (!page.CoversWindow && HandleDialog(action))
        {
            return;
        }

        // Only where the header is (#166); in the search Y is the keyboard's space, in the settings it does nothing.
        if (action == UiAction.Status && page is LibraryViewModel or GameDetailViewModel)
        {
            OpenStatusModal();
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
            var answer = confirmation.Answer(action);
            if (answer != ConfirmationAnswer.Open)
            {
                // Closed first: the confirmed action may open the next dialog.
                Confirmation = null;
                if (answer == ConfirmationAnswer.Confirmed)
                {
                    confirmation.Confirm();
                }
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

        if (StatusModal is { } status)
        {
            status.Handle(action);
            return true;
        }

        if (FilterModal is { } filter)
        {
            filter.Handle(action);
            return true;
        }

        if (ChangeArtwork is { } artwork)
        {
            artwork.Handle(action);
            return true;
        }

        if (CustomizeOverview is { } customize)
        {
            customize.Handle(action);
            return true;
        }

        if (ChangeTitle is { } changeTitle)
        {
            changeTitle.Handle(action);
            return true;
        }

        return false;
    }

    /// <summary>A button or key was let go.</summary>
    public void Release(UiAction action)
    {
        if (ReleaseSwallowedByImageShow(action))
        {
            return;
        }

        _repeats.Release(action);
        PanDialogs.Release(action);
        _actions.Release(action);
        WaitingList?.Release(action);
        TagDialog?.Release(action);
        ChangeTitle?.Release(action);
        FilterModal?.Release(action);
        foreach (var page in OpenPages())
        {
            page.Release(action);
        }
    }

    /// <summary>Drops held buttons, e.g. when the window loses focus and their release may never arrive.</summary>
    public void CancelHold()
    {
        _repeats.Clear();
        PanDialogs.CancelHold();
        _actions.CancelHold();
        WaitingList?.CancelHeldButtons();
        TagDialog?.CancelHeldButtons();
        ChangeTitle?.CancelHeldButtons();
        FilterModal?.CancelHeldButtons();
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
        ShowLaunchersInFirstRun(libraries);
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

        // The counts and games of a tag cover the library as read now (D5).
        TagList?.Refresh();
        _tagListBehindDetail?.Refresh();
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
                // The new tab's first game keeps the open area and the focus, like a turn of the carousel (#499).
                OpenDetail(first.Game.Key, Detail.Current.Focus);
            }
            else
            {
                Detail = null;
            }
        }
    }

    /// <summary>Opens the detail page on <paramref name="key"/> in the selected tab, or among the hits while the search is open.</summary>
    /// <param name="focus">Where the focus goes; null for a page opened anew: on "Spielen" with "Übersicht" open (#499).</param>
    private void OpenDetail(string key, DetailFocus? focus = null)
    {
        if (Search is { } search)
        {
            var hit = Carousel.Find(search.Hits, key, 0);
            Detail = hit < 0 ? null : new GameDetailViewModel(null, search.Hits, hit, null, CreateDetailTile, _actions, this, LauncherTabs, createViewOptions: null, _time, focus, DetailInfoOf);
            return;
        }

        if (LauncherTabs.SelectedTab is not { } tab)
        {
            return;
        }

        var games = Library.ShownGames(tab);
        var index = Carousel.Find(games, key, 0);
        Detail = index < 0 ? null : new GameDetailViewModel(
            tab.Name, games, index, Library.SortLabel, CreateDetailTile, _actions, this, LauncherTabs, () => Library.CreateViewOptions(forDetailPage: true), _time, focus, DetailInfoOf,
            () => Library.OpenFilterCommand.Execute(null));
    }

    /// <summary>
    /// B: back to where the page was opened from (#586), with the focus on the game shown last: the tag list, the search
    /// with its text and hits, or the library on that game's page.
    /// </summary>
    [RelayCommand]
    private void CloseDetail()
    {
        if (ReturnToTagList() || ReturnToSearch())
        {
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
        if (Detail is { IsFromLibrary: true } detail && LauncherTabs.SelectedTab is { } tab && !detail.Update(Library.ShownGames(tab), Library.SortLabel))
        {
            Detail = null;
        }
        else if (Detail is { IsFromSearch: true } fromSearch)
        {
            // The hits keep their order; each takes its game as read now, e.g. with a changed title (#496). A game that
            // left the library stays until the page closes, as before.
            var current = Session.Libraries.SelectMany(l => l.Games).DistinctBy(g => g.Key).ToDictionary(g => g.Key, StringComparer.Ordinal);
            fromSearch.Update([.. fromSearch.Games.Select(game => current.GetValueOrDefault(game.Key) ?? game)], fromSearch.SortLabel);
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
        tile.Artwork = _artwork?.Of(tile.Game.Key);
        tile.InstallState = Session.InstallStateOf(tile.Game.Key);
        tile.LauncherReportsUpdate = Session.UpdateOf(tile.Game.Key);
        tile.RunState = Session.RunStateOf(tile.Game.Key);
    }

    /// <summary>The covers of every open page, also of one covered by another.</summary>
    private IEnumerable<GameTileViewModel> ShownTiles() => OpenPages().SelectMany(p => p.ShownTiles);

    /// <summary>The cache has other images for these games (#487): every shown cover takes them.</summary>
    private void UpdateArtwork(IReadOnlyCollection<string> gameKeys)
    {
        var changed = gameKeys as ISet<string> ?? gameKeys.ToHashSet(StringComparer.Ordinal);
        foreach (var tile in ShownTiles().Where(t => changed.Contains(t.Game.Key)))
        {
            tile.Artwork = _artwork?.Of(tile.Game.Key);
        }
    }

    private void UpdateInstallStates()
    {
        foreach (var tile in ShownTiles())
        {
            tile.InstallState = Session.InstallStateOf(tile.Game.Key);
            tile.LauncherReportsUpdate = Session.UpdateOf(tile.Game.Key);
        }
    }

    /// <summary>A game is starting, its start failed, it runs or ended: every shown cover takes its run state again.</summary>
    private void StartAgain(string gameKey)
    {
        // The game may not be on any shown page; its own cover is created for the start.
        if (Session.LibraryOf(Game.LauncherIdOf(gameKey))?.Games.FirstOrDefault(g => g.Key == gameKey) is { } game)
        {
            _actions.StartAgain(CreateDetailTile(game));
        }
    }

    private void UpdateRunStates()
    {
        foreach (var tile in ShownTiles())
        {
            tile.RunState = Session.RunStateOf(tile.Game.Key);
        }

        Settings?.Categories.OfType<ControllerSettingsViewModel>().FirstOrDefault()?.RunningGamesChanged();
        RefreshLanguageRestart();
    }

    private void ShowNotice(LibraryNotice notice)
    {
        var (level, text, sticky) = LibraryToasts.For(notice, LauncherTabs);
        Toast.Show(level, text, sticky);
    }

    /// <summary>
    /// X: the search over all launchers, with the on-screen keyboard. From a detail page opened from the search, back to
    /// the search as B does; from any other detail page, the search over it, and B there leads back to it (#127).
    /// </summary>
    [RelayCommand]
    private void OpenSearch()
    {
        if (!LauncherTabs.HasGames)
        {
            return;
        }

        if (ReturnToSearch())
        {
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
            SearchKeyboard,
            SearchSlots,
            CreateTile,
            _logos.BadgeOf,
            Input.Kind == InputKind.Gamepad);
    }

    /// <summary>
    /// A detail page opened from the search closes onto it: its text, hits and keyboard as left, the focus on the game the
    /// page showed last. False on any other page.
    /// </summary>
    private bool ReturnToSearch()
    {
        if (Detail is not { IsFromSearch: true } fromSearch || Search is not { } search)
        {
            return false;
        }

        Detail = null;
        search.FocusHit(fromSearch.Current.Tile.Game.Key);
        return true;
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
            if (ReturnFromSearchToTagGame(detail))
            {
                return;
            }

            if (LauncherTabs.SelectedTab is { } tab && detail.Update(Library.ShownGames(tab), Library.SortLabel))
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

    /// <summary>A click on the header's clock chip (#612) opens "Lokalisierung", where clock and date are set; no button reaches it.</summary>
    [RelayCommand]
    private void OpenClockSettings() => ShowSettings(typeof(LocalizationSettingsViewModel));

    /// <summary>
    /// Y (V) or a click on the header's status chip (#166): only while the header shows a message or the update, and
    /// never over another dialog or menu.
    /// </summary>
    [RelayCommand]
    private void OpenStatusModal()
    {
        if (Status.HasEntries && !IsDialogOpen && Search is null)
        {
            StatusModal = new StatusModalViewModel(Status.Entries, _settings.Current.SortsStatusByNewest, this);
        }
    }

    private void OnStatusChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StatusBarViewModel.Entries))
        {
            StatusModal?.Show(Status.Entries);
        }
    }

    void IStatusModalHost.Close(StatusModalViewModel modal)
    {
        if (StatusModal == modal)
        {
            StatusModal = null;
        }
    }

    void IStatusModalHost.OpenSettingsOf(StatusEntry entry, bool startFix)
    {
        StatusModal = null;
        OpenSettingsOf(entry, startFix);

        // B in the settings opens the status modal again (#586), with the entries as they are then.
        if (Settings is not null)
        {
            _dialogBehindSettings = OpenStatusModal;
        }
    }

    private void OpenSettingsOf(StatusEntry entry, bool startFix)
    {
        switch (entry.Target)
        {
            case StatusTarget.PanProblem:
                ControllerMode.ShowPanProblem();
                break;
            case StatusTarget.ArcadiaUpdate:
                ShowSettings(typeof(AboutSettingsViewModel));
                FocusRowOrTop(Settings?.Selected, AboutSettingsViewModel.InstallKey);
                break;
            case StatusTarget.UserData:
                ShowSettings(typeof(DataSourcesSettingsViewModel));
                FocusRowOrTop(Settings?.Selected, DataSourcesSettingsViewModel.UserDataRowKey);
                break;
            case StatusTarget.SteamGridDbKey:
                ShowSettings(typeof(DataSourcesSettingsViewModel));
                FocusRowOrTop(Settings?.Selected, SteamGridDbKeyRows.KeyRowKey);
                break;
            case StatusTarget.IgdbAccess:
                ShowSettings(typeof(DataSourcesSettingsViewModel));
                FocusRowOrTop(Settings?.Selected, IgdbAccessRows.ClientIdRowKey);
                break;
            case StatusTarget.ControllerIssue:
                var controller = ((IControllerModeHost)this).OpenControllerSettings();
                var row = FocusRowOrTop(controller, PanRows.IssueKey(entry.Id));
                // The fix as A on its row starts it: its question and the progress dialog come from the settings (#413).
                // A row that is busy is disabled, and a fix that runs starts nothing a second time.
                if (startFix && row is ActionRowViewModel { IsEnabled: true, CanActivate: true } fixRow)
                {
                    fixRow.Activate();
                }

                break;
        }
    }

    /// <summary>The row of a message, or the category's top row when the message went meanwhile (#166).</summary>
    private static SettingsRowViewModel? FocusRowOrTop(SettingsCategoryViewModel? category, string key)
    {
        if (category is null || category.Rows.Count == 0)
        {
            return null;
        }

        // First by key: a category with tabs shows the row's tab (#538).
        category.FocusRow(key);
        var row = category.Rows.FirstOrDefault(r => r.Key == key);
        if (row is null)
        {
            category.FocusRow(category.Rows[0].Key);
        }

        return row;
    }

    void IStatusModalHost.InstallUpdate() => InstallUpdateAsync().Forget(AppTexts.ErrorContextUpdateArcadia);

    /// <summary>From the status modal (#166): the settings' row shows the failure, here it is a toast.</summary>
    private async Task InstallUpdateAsync()
    {
        await _updates.InstallAsync();
        if (_updates.LastError is { } error)
        {
            Toast.Show(HealthSeverity.Error, error);
        }
    }

    void IStatusModalHost.OrderChanged(bool newestFirst) => SaveView(settings => settings with { SortsStatusByNewest = newestFirst });

    /// <summary>The settings page (#15) in place of the library, which stays as it is underneath for the way back.</summary>
    /// <param name="keepsWayBack">The settings come back on the way back from a game of the tag list, so a dialog that opened
    /// them before still opens again when they close; any other opening forgets it (#586).</param>
    private void ShowSettings(Type? first, string? launcherId = null, bool keepsWayBack = false)
    {
        if (!keepsWayBack)
        {
            _dialogBehindSettings = null;
        }

        var changes = new SettingsChanges(_settings);
        var launchers = new LauncherSettingsViewModel(this, changes, _time, launcherId);
        var steam = Session.Launchers.SteamInputOwner?.Launcher.Id;
        SettingsCategoryViewModel[] categories =
        [
            launchers,
            new ControllerSettingsViewModel(_controllerSupport, changes, ControllerMode.Pan, GameInTheWay, (level, message) => Toast.Show(level, message),
                (new Func<bool>(() => steam is not null && Session.Launchers.IsFound(steam)), OpenSteamControllerSettings)),
            .. CreateDataSources(changes) is { } dataSources ? [dataSources] : Array.Empty<SettingsCategoryViewModel>(),
            new LocalizationSettingsViewModel(changes, ApplyClock, CreateLanguageRestart()),
            new AboutSettingsViewModel(_updates, _updates.InstallAsync, Messages, changes, Logs, _time, kind => OpenLogAsync(kind).Forget(AppTexts.ErrorContextOpenLog)),
        ];
        Settings = new SettingsPageViewModel(categories, changes, this, categories.FirstOrDefault(c => c.GetType() == first));

        // "Steam-Einstellungen" in the controller settings: straight to Steam's page with "Steam Controller" focused (#315).
        void OpenSteamControllerSettings()
        {
            if (steam is null)
            {
                return;
            }

            Settings?.Open(launchers);
            launchers.ShowPage(steam, LauncherControllerModeRowViewModel.RowKey);
        }
    }

    [RelayCommand]
    private void CloseSettings() => LeaveSettings();

    /// <summary>
    /// B or ✕ on the settings: back to where they were opened from (#586): the page underneath, also when a game of the tag
    /// list covered it meanwhile, and the dialog that opened them.
    /// </summary>
    private void LeaveSettings()
    {
        Settings = null;
        // A game of the tag list still open (the settings were opened anew over it) keeps the page for its own way back;
        // otherwise the page comes back, shown before it is let go so it is never closed in between.
        if (DetailBehindSettings is { } detail && (Detail is null || _tagListBehindDetail is null))
        {
            if (Detail is null && StillShows(detail))
            {
                Detail = detail;
            }

            DetailBehindSettings = null;
        }

        var reopen = _dialogBehindSettings;
        _dialogBehindSettings = null;
        reopen?.Invoke();
    }

    /// <summary>A covered detail page takes its list as it is now; false when its game left that list.</summary>
    private bool StillShows(GameDetailViewModel detail) => detail.IsFromLibrary
        ? LauncherTabs.SelectedTab is { } tab && detail.Update(Library.ShownGames(tab), Library.SortLabel)
        : Search is { } search && detail.Update(search.Hits, null);

    /// <summary>"Uhrzeit" as saved (#553): every time shown from now on follows it, the header clock at once.</summary>
    private void ApplyClock()
    {
        Formatting.ClockFormat = _settings.Current.Localization.Clock;
        Status.UpdateClock();
    }

    /// <summary>Opens a log in the system's default app (#433); a failure is an error toast, never an exception.</summary>
    private async Task OpenLogAsync(LogKind kind)
    {
        var byController = Input.UsesController;
        try
        {
            // Off the UI thread: the shell may take a moment to find the app for .log.
            await Task.Run(() => Logs.Open(kind)).WaitAsync(LogOpenTimeout, _time);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
        {
            // The system's message is English and may name the user's profile: only the log gets it (without the profile).
            AppLog.Write($"Log {kind} nicht geöffnet: {ex.GetType().Name}: {ex.Message}");
            Toast.Show(HealthSeverity.Error, AppTexts.LogOpenFailed(log: LogNames.Of(kind), reason: LogNames.WhyNotOpened(ex)));
            return;
        }

        // Used with the mouse and keyboard: opened by controller, the mouse mode comes on for it, as for a web page (#280).
        if (byController)
        {
            OpenedForMouseByController?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnSettingsChanged(SettingsPageViewModel? value)
    {
        OnPagesChanged();
        UpdateGameMouseModeState();
        ControllerMode.SettingsChanged();

        // A launcher's default for the Steam Controller may have changed (#178): the detail page's button names it.
        if (value is null)
        {
            Detail?.RebuildActions();
        }
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

        IReadOnlyList<IPage> live = [.. open, .. new[] { DetailBehindSearch, DetailBehindSettings }.OfType<IPage>()];
        var closed = _livePages.Except(live).ToList();
        _livePages = live;
        ImageShow?.Restart();
        foreach (var page in closed.OfType<IDisposable>())
        {
            page.Dispose();
        }
    }

    private static readonly TimeSpan UserDataWriteLimit = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Arcadia is closing: every page still open is disposed, the library's delayed rescans and the header's clock stop, and
    /// Main lets go of the static error event and the services, which outlive it in the tests.
    /// </summary>
    public void Dispose()
    {
        foreach (var page in _livePages.OfType<IDisposable>())
        {
            page.Dispose();
        }

        _livePages = [];

        // Closing with the waiting list open cancels its checks and own search instead of leaving them running.
        WaitingList = null;
        ChangeTitle = null;
        TagWindow = null;
        TagList = null;
        _tagListBehindDetail?.Dispose();

        // A title just changed is on disk before Arcadia ends (#496); the write takes milliseconds, a stuck disk doesn't hold the exit.
        Session.Titles.UserData?.WhenWrittenAsync().Wait(UserDataWriteLimit);
        AppErrors.Occurred -= OnAppError;
        StopWatchingLanguageRestart();
        PanDialogs.Dispose();
        ControllerMode.Dispose();
        Status.Dispose();
        Toast.Dispose();
        ImageShow.Dispose();
        SearchKeyboard.Dispose();
        SettingsKeyboard.Dispose();
        Session.Dispose();

        // Arcadia closes: a store request in flight is dropped, not waited for (#493).
        _metadata?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnAppError(string message, Arcadia.Input.ErrorStreak? streak) => Avalonia.Threading.Dispatcher.UIThread.Post(() => Toast.Show(HealthSeverity.Error, message, repeats: streak));

    // Pan's screens cover the window: the dialog layer's state follows them.
    private void OnPanDialogsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PanDialogsViewModel.PanDialog) or nameof(PanDialogsViewModel.PanUpdate))
        {
            OnPropertyChanged(nameof(IsDialogOpen));
        }
    }

    private void OnLibraryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LibraryViewModel.FocusedGame):
                ControllerMode.FocusedGameChanged();
                UpdateGameMouseModeState();
                break;
            case nameof(LibraryViewModel.EmptyTab):
                OnPropertyChanged(nameof(IsEmptyTabShown));
                OnPropertyChanged(nameof(IsViewLineShown));
                OnPropertyChanged(nameof(AreGamesShown));
                OnPropertyChanged(nameof(IsNoHitsShown));
                break;
            case nameof(LibraryViewModel.FilterHidesAll):
                OnPropertyChanged(nameof(AreGamesShown));
                OnPropertyChanged(nameof(IsNoHitsShown));
                break;
        }
    }

    /// <summary>Switches the focused game's "Individueller Mausmodus": Standard → Erlaubt → Deaktiviert.</summary>
    [RelayCommand]
    private void CycleGameMouseMode()
    {
        if (Library.FocusedGame?.Game is { } game)
        {
            StepGameMouseModeOf(game, 1);
        }
    }

    private void StepGameMouseModeOf(Game game, int delta)
    {
        var held = HeldPress.IsRepeat;
        try
        {
            _settings.Update(s => s with { MouseMode = s.MouseMode.WithGameChoice(game.Key, ThreeWayChoice.Step(s.MouseMode.GameChoiceFor(game.Key), delta, held)) });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Individueller Mausmodus nicht gespeichert: {ex.Message}");
            Toast.Show(HealthSeverity.Error, AppTexts.SettingNotSaved);
        }

        UpdateGameMouseModeState();
    }

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

    /// <summary>Pan's emulation (#242), once at the start: the mode, the header, the controller settings and Pan's dialogs follow it.</summary>
    public void ConnectPan(IPanEmulationView pan)
    {
        ControllerMode.ConnectPan(pan);
        PanDialogs.ConnectPan(pan);
    }

    /// <summary>
    /// The title of a game that really runs or is being started, for "Erst änderbar, wenn „…“ beendet ist" (#242). A demo
    /// game shown as running never counts: it can't be ended, so Pan would stay locked for good (#307).
    /// </summary>
    private string? GameInTheWay() =>
        Session.Libraries.SelectMany(library => library.Games).FirstOrDefault(game => Session.RealRunStateOf(game.Key) is not null)?.Title;

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
            Toast.Show(HealthSeverity.Error, AppTexts.ViewNotSaved);
        }
    }

    bool IGameActionsOwner.ShowControllerMode => ControllerMode.ShowControllerSupport;

    bool IGameActionsOwner.ControllerModeBroken => ControllerMode.ControllerModeBroken;

    string? IGameActionsOwner.ControllerModeHiddenReason => ControllerMode.HiddenReason;

    void IGameActionsOwner.ShowPanProblem() => ControllerMode.ShowPanProblem();

    Task IGameActionsOwner.BeforeGameStartsAsync(CancellationToken cancellationToken) =>
        BeforeGameStarts?.Invoke(cancellationToken) ?? Task.CompletedTask;

    bool IGameActionsOwner.ShowGameMouseMode => ShowGameMouseMode;

    GameFolders IGameActionsOwner.GameFolders => Session.GameFolders;

    void IGameActionsOwner.ShowDetails(GameTileViewModel tile) => OpenDetail(tile.Game.Key);

    void IGameActionsOwner.Confirm(ConfirmationViewModel confirmation) => Confirmation = confirmation;

    /// <summary>"Arcadia jetzt beenden" in a dialog; the App closes the window, as its close button would.</summary>
    public event EventHandler? QuitRequested;

    /// <summary>The pointer went down on a held button (Pan's dialogs, the settings row): held as [A] would be.</summary>
    public void PressHold(HoldButtonViewModel button)
    {
        if (PanDialogs.PressHold(button))
        {
            return;
        }

        if (Settings?.Selected.Rows.OfType<HoldRowViewModel>().FirstOrDefault(row => row.Hold == button) is { } row)
        {
            row.PressByPointer();
        }
    }

    void IGameActionsOwner.StepGameMouseMode(Game game, int delta) => StepGameMouseModeOf(game, delta);

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
        else if (page == Settings && Settings.IsFirstRun)
        {
            FinishFirstRun();
        }
        else if (page == Settings)
        {
            LeaveSettings();
        }
    }

    void IPageHost.ShowViewOptions(IPage page, Func<ViewOptionsViewModel?> create) => ShowViewOptions(create, page);

    void IPageHost.ViewOptionsChanged() => RebuildViewOptions();

    void IPageHost.ShowMenu(GameTileViewModel tile, bool atCover) =>
        GameMenu = new GameMenuViewModel(tile, GameActionViewModel.For(tile, _actions)) { AtCover = atCover };

    void ILibraryHost.OpenLauncherSettings(string? launcherId) => ShowSettings(typeof(LauncherSettingsViewModel), launcherId);

    void ILibraryHost.Rescan(LauncherInfo launcher) => Session.RescanAsync(launcher).Forget(AppTexts.Rescan);

    void ILibraryHost.LibraryOutdated() => Session.RefreshLibraryAsync().Forget(AppTexts.ErrorContextRefreshLibrary);

    void ILibraryHost.PageRebuilt() => KeepHoldOnRebuiltPage();

    void ILibraryHost.ViewChanged()
    {
        UpdateDetail();
        RebuildViewOptions();
    }

    void ILibraryHost.ShowFilter(FilterModalViewModel modal)
    {
        // Over the library, or over a detail page opened from a tab, whose carousel turns through the tab's hits (#530).
        if (!IsDialogOpen && Search is null && Detail is null or { IsFromLibrary: true })
        {
            FilterModal = modal;
        }
    }

    void ILibraryHost.CloseFilter(FilterModalViewModel modal)
    {
        if (FilterModal == modal)
        {
            FilterModal = null;
        }
    }

    bool ILibraryHost.IsMetadataLoading => _metadata?.IsBusy == true;

    AgeRatingBoard ILibraryHost.FirstAgeRatingBoard => _metadata?.FirstAgeRatingBoard ?? AgeRatingRegion.System;

    FilterKeyboard ILibraryHost.FilterKeyboard => new(editor => SettingsKeyboard.Take(editor, Input.UsesController), SettingsKeyboard.GiveBack);

    LauncherRegistry ILauncherSettingsHost.Launchers => Session.Launchers;

    LauncherLibrary? ILauncherSettingsHost.LibraryOf(string launcherId) => LauncherTabs.Find(launcherId)?.Library;

    DateTimeOffset? ILauncherSettingsHost.ScannedAt(string launcherId) => Session.ScannedAt(launcherId);

    void ILauncherSettingsHost.LauncherChoiceChanged() => Session.ShowChosenLaunchers();

    void ILauncherSettingsHost.RescanLaunchers() => Session.RescanAsync(null).Forget(AppTexts.ErrorContextRescanLaunchers);

    LauncherLogos ILauncherSettingsHost.Logos => _logos;

    bool ILauncherSettingsHost.IsPanOn => ControllerMode.IsPanOn;

    bool ILauncherSettingsHost.IsPanSupported => ControllerMode.IsPanSupported;

    void ILauncherSettingsHost.LauncherLooksChanged() => ApplyLauncherLooks();

    void ILauncherSettingsHost.LibraryTypesChanged() => Session.ShowChosenTypes();

    void ILauncherSettingsHost.RescanLauncher(string launcherId) =>
        Session.RescanAsync(Session.Launchers.Info(launcherId)).Forget(AppTexts.ErrorContextRescanLaunchers);

    OnScreenKeyboardViewModel ILauncherSettingsHost.TakeKeyboard(IKeyboardTarget editor) => SettingsKeyboard.Take(editor, Input.UsesController);

    void ILauncherSettingsHost.GiveBackKeyboard(IKeyboardTarget editor) => SettingsKeyboard.GiveBack(editor);

    /// <summary>A keyboard that opens in the default layout and offers the quick selection (#553).</summary>
    private SharedKeyboard CreateSharedKeyboard() => new(
        () => SystemKeyboardLayout.Choice(_settings.Current.Localization),
        _time,
        action => Avalonia.Threading.Dispatcher.UIThread.Post(action));

    void ILauncherSettingsHost.Warn(string message) => Toast.Show(HealthSeverity.Warning, message);

    GameTileViewModel? IControllerModeHost.FocusedTile => Library.FocusedGame;

    void IControllerModeHost.ControllerModeChanged() => Detail?.RebuildActions();

    ControllerSettingsViewModel? IControllerModeHost.OpenControllerSettings()
    {
        ShowSettings(typeof(ControllerSettingsViewModel));
        return Settings?.Selected as ControllerSettingsViewModel;
    }
}
