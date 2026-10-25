using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Search;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.App.Setup;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Search;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>The Big Picture library: launcher tabs, a paginated cover grid and the preview panel.</summary>
public partial class MainViewModel : ViewModelBase, IGameActionsOwner, IPageHost, ILauncherSettingsHost
{
    /// <summary>Must match the column count of the grid in MainWindow.axaml.</summary>
    public const int GridColumns = 8;

    private static readonly TimeSpan UninstallSettleTime = TimeSpan.FromSeconds(5);

    /// <summary>Steam may write a game's playtime a while after it ended, so the library is read once more after this.</summary>
    private static readonly TimeSpan PlayStatsSettleTime = TimeSpan.FromSeconds(30);

    private readonly LibraryService _library;
    private readonly LibraryRefresher _refresher;
    private readonly InstallTracker _installs;
    private readonly RunningGamesTracker _running;
    private readonly ControllerSupportService _controllerSupport;
    private readonly ArcadiaUpdateService _updates;
    private readonly SettingsService _settings;
    private readonly LauncherLogos _logos;
    private readonly FirstRunSetup _firstRun;
    private readonly TimeProvider _time;
    private readonly GameActions _actions;
    private Func<ViewOptionsViewModel?>? _createViewOptions;

    // The page whose options the dialog shows; null for the library's view.
    private IPage? _viewOptionsPage;
    private InputKind _input = InputKind.Gamepad;
    private int _requestedPage;

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
        TimeProvider time)
    {
        _library = library;
        _logos = logos;
        _refresher = new LibraryRefresher(library, time);
        _controllerSupport = controllerSupport;
        _updates = updates;
        _installs = installs;
        _running = running;
        LaunchedGames = launchedGames;
        _settings = settings;
        _firstRun = firstRun;
        _time = time;
        LauncherTabs = new LauncherTabsViewModel(logos.BadgeOf);
        LauncherTabs.Chosen += OnTabChosen;
        Status = new StatusBarViewModel(controllerSupport, updates, time);
        Toast = new ToastViewModel(time);
        _actions = new GameActions(this, launcher, controllerSupport, installs, running, launchedGames, settings, Toast, time);
        _actions.ForegroundRequested += (_, e) => ForegroundRequested?.Invoke(this, e);
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsOpen))]
    public partial SettingsPageViewModel? Settings { get; private set; }

    public bool IsSettingsOpen => Settings is not null;

    /// <summary>The first start's question which launchers Arcadia uses (#139), in place of the library until "Fertig".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFirstRunShown))]
    public partial FirstRunViewModel? FirstRun { get; private set; }

    public bool IsFirstRunShown => FirstRun is not null;

    /// <summary>The per-game mode (SISR or Steam Input) shows only with a Steam Controller connected and SISR switched on.</summary>
    [ObservableProperty]
    public partial bool ShowControllerSupport { get; private set; }

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

    /// <summary>The Steam Controller mode of the focused game: "Xbox Controller (SISR)" or "Steam Input".</summary>
    [ObservableProperty]
    public partial string FocusedControllerSupportText { get; private set; } = string.Empty;

    /// <summary>"Individueller Mausmodus" shows on games ("Von Hand" with "Individuell").</summary>
    [ObservableProperty]
    public partial bool ShowGameMouseMode { get; private set; }

    /// <summary>The focused game's "Individueller Mausmodus": "Standard", "Erlaubt" or "Deaktiviert".</summary>
    [ObservableProperty]
    public partial string FocusedGameMouseModeText { get; private set; } = string.Empty;

    partial void OnFocusedGameChanged(GameTileViewModel? value)
    {
        UpdateControllerSupportState();
        UpdateGameMouseModeState();
    }

    /// <summary>The install folders of the library's games: a process from one of them is a game in front.</summary>
    public GameFolders GameFolders { get; private set; } = GameFolders.Empty;

    /// <summary>Games Arcadia started, for a game in front whose path can't be read.</summary>
    public LaunchedGames LaunchedGames { get; }

    /// <summary>Raised when Arcadia should bring its window back to the front.</summary>
    public event EventHandler? ForegroundRequested;

    /// <summary>A game started from Arcadia ended, with its key: Arcadia may come back to the front (#94).</summary>
    public event EventHandler<string>? StartedGameEnded;

    /// <summary>The launcher tabs, shared by the library, the detail page and the search.</summary>
    public LauncherTabsViewModel LauncherTabs { get; }

    public ObservableCollection<GameTileViewModel> PageGames { get; } = [];

    public ObservableCollection<PageButtonViewModel> PageButtons { get; } = [];

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

    /// <summary>A dialog or menu over the page takes the input.</summary>
    public bool IsDialogOpen => IsFirstRunShown || IsSettingsOpen || IsConfirmationOpen || IsGameMenuOpen || IsViewOptionsOpen;

    /// <summary>The detail page; it takes the place of the cover grid while open, and of the search when opened from there.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailOpen), nameof(IsLibraryShown), nameof(IsSearchShown), nameof(IsTabStripShown), nameof(IsEmptyTabShown), nameof(AreGamesShown))]
    public partial GameDetailViewModel? Detail { get; private set; }

    public bool IsDetailOpen => Detail is not null;

    /// <summary>A detail page from the library that X left for the search; B in the search brings it back (#127).</summary>
    private GameDetailViewModel? _detailBehindSearch;

    public bool IsLibraryShown => Detail is null;

    /// <summary>The selected tab has no games, or its launcher is no longer found (#136): it says so instead of covers.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyTabShown), nameof(AreGamesShown))]
    public partial EmptyTabViewModel? EmptyTab { get; private set; }

    public bool IsEmptyTabShown => IsLibraryShown && EmptyTab is not null;

    /// <summary>The library shows covers: the preview, the grid, the pages and their buttons in the footer.</summary>
    public bool AreGamesShown => IsLibraryShown && EmptyTab is null;

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
    public partial string CountText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ViewSummary { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string RangeText { get; private set; } = string.Empty;

    public async Task LoadAsync()
    {
        IsLoading = true;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var libraries = await _library.LoadAsync();
        var scanned = watch.ElapsedMilliseconds;
        IsLoading = false;
        ApplyLibraries(libraries);

        // The scan runs off the UI thread; only showing it blocks the UI (#136 measures both). Time since the process
        // started and memory are what the budgets in CLAUDE.md name (start < 2 s with 1,000 games, < 400 MB with 5,000).
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var sinceStart = (long)(DateTime.Now - process.StartTime).TotalMilliseconds;
        AppLog.Write($"Bibliothek: {libraries.Sum(l => l.Games.Count)} Spiele aus {libraries.Count} Launchern in {scanned} ms eingelesen, "
            + $"in {watch.ElapsedMilliseconds - scanned} ms angezeigt; {sinceStart} ms nach dem Start, {Environment.WorkingSet / (1024 * 1024)} MB Arbeitsspeicher");
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

        foreach (var tile in ShownTiles())
        {
            tile.InstallState = _installs.StateOf(tile.Game.Key);
        }

        if (results.Count > 0)
        {
            await ReportInstallResultsAsync(results);
        }
    }

    /// <summary>
    /// Asks the launchers which games run (#95) and updates the covers. Called periodically, also in the background, and
    /// when Arcadia comes to the front; the end of a game started from Arcadia may bring it back (#94).
    /// </summary>
    public async Task PollRunningGamesAsync()
    {
        if (await _running.PollAsync() is not { } changes)
        {
            return;
        }

        if (changes.StatesChanged)
        {
            UpdateRunStates();
        }

        if (changes.Ended.Count == 0)
        {
            return;
        }

        foreach (var ended in changes.Ended)
        {
            AppLog.Write($"{ended.GameKey} läuft nicht mehr{(ended.StartedFromArcadia ? " (aus Arcadia gestartet)" : string.Empty)}");
            if (ended.StartedFromArcadia)
            {
                StartedGameEnded?.Invoke(this, ended.GameKey);
            }
        }

        // Playtime and "Zuletzt gespielt" changed, and the library may be sorted by them.
        await RefreshLibraryAsync(force: true);
        Task.Delay(PlayStatsSettleTime, _time).ContinueWith(
            _ => RefreshLibraryAsync(force: true),
            TaskScheduler.FromCurrentSynchronizationContext()).Unwrap().Forget("Bibliothek aktualisieren");
    }

    private void UpdateRunStates()
    {
        foreach (var tile in ShownTiles())
        {
            tile.RunState = _running.StateOf(tile.Game.Key);
        }
    }

    /// <summary>The covers of the library's page and of every open page, also one covered by another.</summary>
    private IEnumerable<GameTileViewModel> ShownTiles() => PageGames.Concat(OpenPages().SelectMany(p => p.ShownTiles));

    /// <summary>The pages open, on top first. The library underneath is not a page yet (#210).</summary>
    private IEnumerable<IPage> OpenPages() => new IPage?[] { FirstRun, Settings, Detail, Search }.OfType<IPage>();

    private async Task ReportInstallResultsAsync(IReadOnlyList<InstallResult> results)
    {
        string? KnownTitleOf(string key) => LauncherTabs.Items.SelectMany(t => t.Games).FirstOrDefault(g => g.Key == key)?.Title;

        // An uninstalled game leaves the tabs with the rescan below, a newly installed one only comes with it.
        var (key, outcome) = results[^1];
        var titleBefore = KnownTitleOf(key);

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
        var title = titleBefore ?? KnownTitleOf(key) ?? "Spiel";
        Toast.Show(outcome switch
        {
            InstallOutcome.Uninstalled => $"„{title}“ wurde deinstalliert.",
            InstallOutcome.Completed when completed > 1 => $"{completed} Spiele sind jetzt installiert.",
            InstallOutcome.Completed => $"„{title}“ ist jetzt installiert.",
            InstallOutcome.Cancelled => $"Installation von „{title}“ abgebrochen.",
            _ => $"Installation von „{title}“ ist fehlgeschlagen – bitte im Launcher nachsehen.",
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

        // "Zuletzt eingelesen" changed even if nothing else did.
        RefreshLauncherSettings();
    }

    /// <summary>Reads one launcher again ("Neu einlesen", #137); the other tabs keep their games.</summary>
    private async Task RefreshLauncherAsync(string launcherId)
    {
        if (IsLoading)
        {
            return;
        }

        if (await _refresher.RefreshAsync(CurrentLibraries, launcherId) is { } libraries)
        {
            ApplyLibraries(libraries);
        }

        RefreshLauncherSettings();
    }

    private IReadOnlyList<LauncherLibrary> CurrentLibraries() => LauncherTabs.Libraries();

    /// <summary>
    /// "Neu einlesen" on an empty tab or a launcher's page reads <paramref name="launcher"/> again; X and "Alle neu einlesen"
    /// in the launcher settings read all (null). Says what came of it.
    /// </summary>
    private async Task RescanAsync(LauncherInfo? launcher)
    {
        if (IsLoading)
        {
            return;
        }

        Toast.Show(launcher is null ? "Alle Launcher werden neu eingelesen …" : $"{launcher.DisplayName} wird neu eingelesen …", sticky: true);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await (launcher is null ? RefreshLibraryAsync(force: true) : RefreshLauncherAsync(launcher.Id));
        AppLog.Write($"Neu eingelesen{(launcher is null ? string.Empty : $" für {launcher.Id}")}: {watch.ElapsedMilliseconds} ms");
        if (launcher is null)
        {
            Toast.Show(LauncherTabs.Items.Count == 0 ? "Weiterhin ist kein Launcher aktiv." : "Alle Launcher sind neu eingelesen.");
            return;
        }

        var name = launcher.DisplayName;
        Toast.Show(LauncherTabs.Find(launcher.Id) switch
        {
            null => $"{name} ist nicht aktiv.",
            { IsFound: false } => $"{name} ist weiterhin nicht zu finden.",
            { Count: 0 } => $"Weiterhin keine Spiele in {name}.",
            { Count: var count } => count == 1 ? $"1 Spiel in {name} gefunden." : $"{count} Spiele in {name} gefunden.",
        });
    }

    /// <summary>
    /// The user switched launchers on or off or moved them (#136): tabs follow at once from what is already read; a
    /// launcher switched on is read now.
    /// </summary>
    private void ApplyLauncherChoice()
    {
        if (IsLoading)
        {
            return;
        }

        var active = _library.Launchers.ActiveIds;
        var shown = CurrentLibraries();
        var kept = active.Select(id => shown.FirstOrDefault(l => l.Launcher.Id == id)).OfType<LauncherLibrary>().ToList();
        ApplyLibraries(kept);
        if (kept.Count < active.Count)
        {
            RefreshLibraryAsync(force: true).Forget("Bibliothek aktualisieren");
        }
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
        ApplyLauncherChoice();
    }

    private void RefreshLauncherSettings() => Settings?.Categories.OfType<LauncherSettingsViewModel>().FirstOrDefault()?.Refresh();

    LauncherRegistry ILauncherSettingsHost.Launchers => _library.Launchers;


    LauncherLibrary? ILauncherSettingsHost.LibraryOf(string launcherId) => LauncherTabs.Find(launcherId)?.Library;

    DateTimeOffset? ILauncherSettingsHost.ScannedAt(string launcherId) => _library.ScannedAt(launcherId);

    void ILauncherSettingsHost.LauncherChoiceChanged() => ApplyLauncherChoice();

    void ILauncherSettingsHost.RescanLaunchers() => RescanAsync(null).Forget("Launcher neu einlesen");

    LauncherLogos ILauncherSettingsHost.Logos => _logos;

    /// <summary>The file and folder dialogs over the window (#138); set once the window is there.</summary>
    public ISystemDialogs? Dialogs { get; set; }

    void ILauncherSettingsHost.LauncherLooksChanged() => ApplyLauncherLooks();

    void ILauncherSettingsHost.RescanLauncher(string launcherId) =>
        RescanAsync(_library.Launchers.Info(launcherId)).Forget("Launcher neu einlesen");

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

    /// <summary>
    /// A launcher was renamed, got another keyword or logo (#137, #138): tabs, covers, the empty tab and the search show it
    /// at once; nothing needs to be read again.
    /// </summary>
    private void ApplyLauncherLooks()
    {
        LauncherTabs.Restyle(_library.Launchers.Info);
        EmptyTab = EmptyTab is { HasNoLauncher: true } shown ? shown : null;
        Refresh(FocusedGame is { } focused ? PageGames.IndexOf(focused) : 0, FocusedGame?.Game.Key);
        UpdateSearch();
    }

    /// <summary>Shows the scanned libraries in the tabs. Keeps tab, page and focused game where they were.</summary>
    private void ApplyLibraries(IReadOnlyList<LauncherLibrary> libraries)
    {
        FirstRun?.Show(libraries);
        GameFolders = GameFolders.Of(libraries.SelectMany(l => l.Games));
        _running.SetLibrary(libraries.SelectMany(l => l.Games));
        LauncherTabs.Show(libraries);
        UpdateSearch();
        if (LauncherTabs.SelectedTab is null)
        {
            FocusedGame = null;
            ViewOptions = null;
            if (Detail?.IsFromLibrary == true)
            {
                Detail = null;
            }

            PageGames.Clear();
            PageButtons.Clear();
            var tabs = LauncherTabs.Items;
            EmptyTab = EmptyTab is { HasNoLauncher: true } shown && tabs.Count == 0 ? shown : null;
            CountText = RangeText = ViewSummary = string.Empty;
            if (tabs.Count > 0)
            {
                // Selected, not chosen: a launcher missing from this scan must not overwrite the user's choice.
                SelectTab(LauncherTabs.Find(_settings.Current.LastTabLauncherId ?? string.Empty) ?? tabs[0]);
            }
            else
            {
                EmptyTab ??= new EmptyTabViewModel(null, () => ShowSettings(typeof(LauncherSettingsViewModel)), rescan: null);
            }

            RefreshLauncherSettings();
            return;
        }

        var focusIndex = FocusedGame is { } focused ? PageGames.IndexOf(focused) : 0;
        Refresh(focusIndex, FocusedGame?.Game.Key);
        UpdateDetail();
        RefreshLauncherSettings();
    }

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

    /// <summary>A button or key was pressed. A in the library and ≡ or Enter on a hit wait for the release (<see cref="Release"/>).</summary>
    public void Handle(UiAction action)
    {
        _actions.Pressed(action);
        if (FirstRun is { } firstRun)
        {
            firstRun.Handle(action);
            return;
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
            search.Handle(action);
            return;
        }

        if (EmptyTab is { } empty && action is UiAction.Left or UiAction.Right or UiAction.Accept)
        {
            empty.Handle(action);
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
                    _actions.Press(game, UiAction.Accept);
                }

                break;
            case UiAction.Options:
                OpenMenu();
                break;
            case UiAction.PreviousTab:
                LauncherTabs.Cycle(-1);
                break;
            case UiAction.NextTab:
                LauncherTabs.Cycle(1);
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
                // The hits belong to no tab and keep the search's order.
                break;
            case UiAction.PreviousPage:
                detail.Rotate(-1);
                break;
            case UiAction.NextPage:
                detail.Rotate(1);
                break;
            case UiAction.PreviousTab:
                LauncherTabs.Cycle(-1);
                break;
            case UiAction.NextTab:
                LauncherTabs.Cycle(1);
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
            case UiAction.Search:
                OpenSearch();
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
            Detail = hit < 0 ? null : new GameDetailViewModel(null, search.Hits, hit, null, CreateDetailTile, _actions);
            return;
        }

        if (LauncherTabs.SelectedTab is not { } tab)
        {
            return;
        }

        var games = SortedGames(tab);
        var index = Carousel.Find(games, key, 0);
        Detail = index < 0 ? null : new GameDetailViewModel(tab.Name, games, index, Formatting.SortLabel(_settings.Current.LibrarySort), CreateDetailTile, _actions);
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
            _detailBehindSearch = null;
            CloseSearch();
            ShowInLibrary(fromSearch.Current.Tile.Game);
            return;
        }

        if (Detail is not { } detail || LauncherTabs.SelectedTab is not { } tab)
        {
            Detail = null;
            return;
        }

        Detail = null;
        var pageSize = PageSizes.Effective(_settings.Current.PreferredPageSize, tab.Count);
        _requestedPage = detail.Index / pageSize;
        Refresh(focusIndex: detail.Index % pageSize);
    }

    /// <summary>The library on the tab of <paramref name="game"/>'s launcher, on its page and with the focus on it.</summary>
    private void ShowInLibrary(Game game)
    {
        if (LauncherTabs.Find(game.LauncherId) is not { } tab)
        {
            return;
        }

        if (tab != LauncherTabs.SelectedTab)
        {
            LauncherTabs.Choose(tab);
        }

        var games = SortedGames(tab);
        var index = Carousel.Find(games, game.Key, 0);
        if (index < 0)
        {
            return;
        }

        var pageSize = PageSizes.Effective(_settings.Current.PreferredPageSize, tab.Count);
        _requestedPage = index / pageSize;
        Refresh(focusIndex: index % pageSize);
    }

    /// <summary>The list changed (rescan, new sort): the detail page keeps its game in the middle.</summary>
    private void UpdateDetail()
    {
        if (Detail is { IsFromLibrary: true } detail && LauncherTabs.SelectedTab is { } tab && !detail.Update(SortedGames(tab), Formatting.SortLabel(_settings.Current.LibrarySort)))
        {
            Detail = null;
        }
    }

    private GameTileViewModel CreateDetailTile(Game game) =>
        new(game, LauncherTabs.LauncherOf(game), LauncherTabs.BadgeOf(game), _time.GetLocalNow(), _ => { }, _ => { }) { InstallState = _installs.StateOf(game.Key), RunState = _running.StateOf(game.Key) };

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
            _detailBehindSearch = detail;
            Detail = null;
        }

        // Opened with the gamepad, the search starts with the on-screen keyboard; by key or click without.
        Search = new SearchViewModel(
            LauncherTabs,
            _actions,
            this,
            KeyboardLayout.Find(_settings.Current.KeyboardLayout),
            layout => SaveView(settings => settings with { KeyboardLayout = layout.Id }),
            CreateSearchTile,
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
        if (_detailBehindSearch is { } detail)
        {
            _detailBehindSearch = null;
            if (LauncherTabs.SelectedTab is { } tab && detail.Update(SortedGames(tab), Formatting.SortLabel(_settings.Current.LibrarySort)))
            {
                Detail = detail;
            }
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

    private GameTileViewModel CreateSearchTile(Game game, Action<GameTileViewModel> focus, Action<GameTileViewModel> activate) =>
        new(game, LauncherTabs.LauncherOf(game), LauncherTabs.BadgeOf(game), _time.GetLocalNow(), focus, activate)
        { InstallState = _installs.StateOf(game.Key), RunState = _running.StateOf(game.Key) };

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

    private IReadOnlyList<Game> SortedGames(LauncherTabViewModel tab) =>
        GameSorter.Sort(tab.Games, _settings.Current.LibrarySort, Formatting.Culture);

    /// <summary>A button or key was let go.</summary>
    public void Release(UiAction action)
    {
        _actions.Release(action);
        foreach (var page in OpenPages())
        {
            page.Release(action);
        }
    }

    /// <summary>Drops held buttons, e.g. when the window loses focus and their release may never arrive.</summary>
    public void CancelHold()
    {
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

        if (!IsSettingsOpen && !IsViewOptionsOpen && !IsConfirmationOpen && !IsDetailOpen && Search is null && PageGames.Contains(tile))
        {
            tile.FocusCommand.Execute(null);
            _actions.Press(tile, button: null);
        }
    }

    /// <summary>A mouse button was let go anywhere.</summary>
    public void ReleasePointer() => _actions.ReleasePointer();

    /// <summary>A rescan rebuilt the page or the hits: a press held meanwhile moves to its game's new tile.</summary>
    private void KeepHoldOnRebuiltPage() => _actions.KeepHoldOn(IsSearchShown ? Search!.Results : PageGames);

    /// <summary>Opens the context menu of the focused game over the preview's "Optionen" button (⧉, R, a click on it).</summary>
    [RelayCommand]
    private void OpenMenu() => OpenMenu(atCover: false);

    private void OpenMenu(bool atCover)
    {
        if (FocusedGame is { } tile)
        {
            GameMenu = new GameMenuViewModel(tile, GameActionViewModel.For(tile, _actions)) { AtCover = atCover };
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
        OpenMenu(atCover: true);
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

    bool IGameActionsOwner.ShowControllerMode => ShowControllerSupport;

    bool IGameActionsOwner.ShowGameMouseMode => ShowGameMouseMode;

    void IGameActionsOwner.ShowDetails(GameTileViewModel tile) => OpenDetail(tile.Game.Key);

    void IGameActionsOwner.Confirm(ConfirmationViewModel confirmation) => Confirmation = confirmation;

    void IGameActionsOwner.CycleGameMouseMode(Game game) => CycleGameMouseModeOf(game);

    void IGameActionsOwner.RunStatesChanged() => UpdateRunStates();

    void IPageHost.Close(IPage page)
    {
        if (page == Search)
        {
            CloseSearch();
        }
    }

    void IPageHost.ShowViewOptions(IPage page, Func<ViewOptionsViewModel> create) => ShowViewOptions(create, page);

    void IPageHost.ViewOptionsChanged() => RebuildViewOptions();

    void IPageHost.ShowMenu(GameTileViewModel tile) => GameMenu = new GameMenuViewModel(tile, GameActionViewModel.For(tile, _actions)) { AtCover = true };

    [RelayCommand]
    private void OpenSettings() => ShowSettings(first: null);

    /// <summary>"Launcher-Einstellungen" on an empty tab: the category "Launcher" with that launcher focused (#136).</summary>
    private void OpenLauncherSettings(LauncherTabViewModel tab) => ShowSettings(typeof(LauncherSettingsViewModel), tab.Launcher.Id);

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
            new ControllerSettingsViewModel(_controllerSupport, changes),
            new AboutSettingsViewModel(_updates, _updates.InstallAsync),
        ];
        Settings = new SettingsPageViewModel(categories, changes, categories.FirstOrDefault(c => c.GetType() == first));
    }

    [RelayCommand]
    private void CloseSettings() => Settings = null;

    partial void OnSettingsChanged(SettingsPageViewModel? oldValue, SettingsPageViewModel? newValue)
    {
        OnTopPageChanged();
        oldValue?.Dispose();
        UpdateGameMouseModeState();
    }

    partial void OnFirstRunChanged(FirstRunViewModel? value) => OnTopPageChanged();

    partial void OnDetailChanged(GameDetailViewModel? value) => OnTopPageChanged();

    partial void OnSearchChanged(SearchViewModel? oldValue, SearchViewModel? newValue)
    {
        OnTopPageChanged();
        oldValue?.Dispose();
    }

    /// <summary>The page on top when the last page opened or closed; null for the library.</summary>
    private IPage? _topPage;

    /// <summary>
    /// The page that was on top lost it: its held buttons and the hold of A or ≡ are dropped, so their release can't type,
    /// start a game or open details on the page now shown (the contract on <see cref="IPage"/>).
    /// </summary>
    private void OnTopPageChanged()
    {
        var top = OpenPages().FirstOrDefault();
        if (top == _topPage)
        {
            return;
        }

        _topPage?.CancelHeldButtons();
        _actions.CancelHold();
        _topPage = top;
    }

    /// <summary>Switches the focused game's "Individueller Mausmodus": Standard → Erlaubt → Deaktiviert.</summary>
    [RelayCommand]
    private void CycleGameMouseMode()
    {
        if (FocusedGame?.Game is { } game)
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

    private bool _shownDefaultMouseModeAllowed = true;

    // Null until SDL has seen a Steam Controller.
    private bool? _steamControllerSeenBySdl;

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

        if (FocusedGame?.Game is { } game)
        {
            FocusedGameMouseModeText = GameMouseModes.Of(mouseMode.GameChoiceFor(game.Key), mouseMode.PerGameDefaultAllowed);
        }
    }

    /// <summary>Switches the focused game between "Xbox Controller (SISR)" and "Steam Input"; not while it runs (#95).</summary>
    [RelayCommand]
    private void CycleControllerSupport()
    {
        if (FocusedGame is not { IsRunningOrStarting: false, Game: var game })
        {
            return;
        }

        _controllerSupport.SetModeFor(game, !_controllerSupport.Settings.ControllerSupportFor(game));
    }

    /// <summary>
    /// SDL sees a Steam Controller or no longer does (#212). Once SDL has seen one, it alone says whether it is
    /// connected, also while SISR is paused for a game. Until then what SISR saw counts: Arcadia started through
    /// Steam doesn't see the controller in SDL, as Steam hides it.
    /// </summary>
    public void SetSteamControllerSeen(bool seen)
    {
        _steamControllerSeenBySdl = seen;
        UpdateControllerSupportState();
    }

    private void UpdateControllerSupportState()
    {
        var support = _controllerSupport;

        // The mode only matters with a Steam Controller; other pads never go through SISR (#72).
        var show = _steamControllerSeenBySdl is { } seen ? support.Enabled && seen : support.SteamControllerConnected;
        if (ShowControllerSupport != show)
        {
            ShowControllerSupport = show;
            Detail?.RebuildActions();
        }

        if (FocusedGame?.Game is { } game)
        {
            FocusedControllerSupportText = _actions.ControllerModeOf(game);
        }
    }

    [RelayCommand]
    private void PreviousPage() => GoToPage(_requestedPage - 1);

    [RelayCommand]
    private void NextPage() => GoToPage(_requestedPage + 1);

    [RelayCommand]
    private void OpenViewOptions()
    {
        if (LauncherTabs.SelectedTab is not null)
        {
            ShowViewOptions(CreateViewOptions);
        }
    }

    /// <summary>The "Ansicht" dialog for the library or the search; <paramref name="create"/> rebuilds it after each choice.</summary>
    private void ShowViewOptions(Func<ViewOptionsViewModel?> create, IPage? page = null)
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

    [RelayCommand]
    private void ActivateFocused()
    {
        if (FocusedGame is { } game)
        {
            Activate(game);
        }
    }

    /// <summary>The user picked <paramref name="tab"/>; Arcadia opens with it next time.</summary>
    private void OnTabChosen(LauncherTabViewModel tab)
    {
        ShowTab();
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
        LauncherTabs.Select(tab);
        ShowTab();
    }

    /// <summary>The library shows the selected tab from its first page.</summary>
    private void ShowTab()
    {
        _requestedPage = 0;
        Refresh(focusIndex: 0);
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

    private void GoToPage(int page)
    {
        if (LauncherTabs.SelectedTab is not { } tab)
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
            RefreshLibraryAsync().Forget("Bibliothek aktualisieren");
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
        if (LauncherTabs.SelectedTab is not { } tab)
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
            PageGames.Add(new GameTileViewModel(game, tab.Launcher, tab.Badge, now, Focus, Activate) { InstallState = _installs.StateOf(game.Key), RunState = _running.StateOf(game.Key) });
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

        EmptyTab = tab.Count > 0 ? null
            : EmptyTab is { } shown && shown.Tab == tab && shown.IsMissing == !tab.IsFound ? shown
            : new EmptyTabViewModel(tab, () => OpenLauncherSettings(tab), () => RescanAsync(tab.Launcher).Forget("Neu einlesen"));
        var installed = tab.Games.Count(g => g.IsInstalled);
        var installedText = installed == tab.Count ? string.Empty : $" · {installed} installiert";
        CountText = EmptyTab is { } emptyTab ? emptyTab.Summary : $"{tab.Count} Spiele{installedText} · Seite {window.PageIndex + 1} von {window.PageCount}";
        RangeText = window.Length == 0 ? string.Empty : $"{window.Start + 1}–{window.Start + window.Length} von {sorted.Count}";
        ViewSummary = $"{Formatting.SortLabel(view.LibrarySort)}  ·  {pageSize} pro Seite";
    }

    private ViewOptionsViewModel? CreateViewOptions() => LauncherTabs.SelectedTab is not { } tab ? null : ViewOptionsViewModel.ForLibrary(
        tab.Name,
        tab.Count,
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
        if (ViewOptions is { } current && _createViewOptions?.Invoke() is { } rebuilt)
        {
            rebuilt.FocusPosition = current.FocusPosition;
            ViewOptions = rebuilt;
        }
    }
}
