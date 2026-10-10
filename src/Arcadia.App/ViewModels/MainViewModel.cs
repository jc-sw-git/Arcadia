using System.Collections.ObjectModel;
using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Models;
using Arcadia.Launchers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>The Big Picture library: launcher tabs, a paginated cover grid and the preview panel.</summary>
public partial class MainViewModel : ViewModelBase
{
    /// <summary>Must match the column count of the grid in MainWindow.axaml.</summary>
    public const int GridColumns = 8;

    private readonly LibraryService _library;
    private readonly GameLaunchService _launcher;
    private readonly TimeProvider _time;
    private SortOrder _sort = SortOrder.Default;
    private int _preferredPageSize = PageSizes.Default;
    private int _requestedPage;
    private bool _launching;

    // Games handed to the install dialog of a launcher Arcadia can't watch; the library is
    // rescanned until they show up installed.
    private readonly HashSet<string> _pendingInstalls = new(StringComparer.Ordinal);

    // Watched launchers: running installations by game key, and install dialogs Arcadia opened
    // that the user hasn't confirmed yet (Epic gives up on its dialog after 10 minutes).
    private static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromMinutes(10);
    private readonly IReadOnlyList<IInstallMonitor> _installMonitors;
    private readonly Dictionary<string, DateTimeOffset> _awaitingConfirmation = new(StringComparer.Ordinal);
    private Dictionary<string, InstallProgress> _installs = new(StringComparer.Ordinal);
    private bool _pollingInstalls;

    private static readonly TimeSpan RefreshThrottle = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan UninstallSettleTime = TimeSpan.FromSeconds(5);
    private DateTimeOffset _lastLibraryRefresh;
    private bool _refreshingLibrary;
    private CancellationTokenSource? _toastTimer;

    private readonly ControllerSupportService _controllerSupport;

    public MainViewModel(
        LibraryService library,
        GameLaunchService launcher,
        ControllerSupportService controllerSupport,
        IReadOnlyList<IInstallMonitor> installMonitors,
        TimeProvider time)
    {
        _library = library;
        _launcher = launcher;
        _controllerSupport = controllerSupport;
        _installMonitors = installMonitors;
        _time = time;
        _controllerSupport.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateControllerSupportState);
        UpdateClock();
        UpdateControllerSupportState();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsOpen))]
    public partial SettingsViewModel? Settings { get; private set; }

    public bool IsSettingsOpen => Settings is not null;

    /// <summary>Steam Controller Support is on but something is wrong; shown as a warning in the header.</summary>
    [ObservableProperty]
    public partial bool HasControllerWarning { get; private set; }

    [ObservableProperty]
    public partial string ControllerWarningText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSisrUpdate { get; private set; }

    /// <summary>SISR is starting and the controller isn't emulated yet; shown as a spinner in the header.</summary>
    [ObservableProperty]
    public partial bool IsControllerStarting { get; private set; }

    [ObservableProperty]
    public partial bool ShowControllerSupport { get; private set; }

    /// <summary>The Steam Controller Support mode of the focused game, e.g. "Standard (an)".</summary>
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
    public partial bool IsLoading { get; private set; } = true;

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    public partial string CountText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ViewSummary { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string RangeText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Clock { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Date { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToast))]
    public partial string? Toast { get; private set; }

    public bool HasToast => Toast is not null;

    public async Task LoadAsync()
    {
        IsLoading = true;
        var libraries = await _library.LoadAsync();

        Tabs.Clear();
        foreach (var library in libraries)
        {
            Tabs.Add(new LauncherTabViewModel(library, SelectTab));
        }

        IsLoading = false;
        IsEmpty = Tabs.Count == 0;
        if (Tabs.Count > 0)
        {
            SelectTab(Tabs[0]);
        }
    }

    /// <summary>
    /// Rescans the library while installations are pending and reports the games that finished.
    /// Called periodically; does nothing when no installation was started from Arcadia.
    /// </summary>
    public async Task CheckPendingInstallsAsync()
    {
        if (_pendingInstalls.Count == 0 || IsLoading)
        {
            return;
        }

        var libraries = await _library.LoadAsync();
        var finished = libraries
            .SelectMany(l => l.Games)
            .Where(g => g.IsInstalled && _pendingInstalls.Contains(g.Key))
            .ToList();
        if (finished.Count == 0)
        {
            return;
        }

        foreach (var game in finished)
        {
            _pendingInstalls.Remove(game.Key);
        }

        ApplyLibraries(libraries);
        ShowToast(finished.Count == 1 ? $"„{finished[0].Title}“ ist jetzt installiert." : $"{finished.Count} Spiele sind jetzt installiert.");
    }

    /// <summary>
    /// Asks the watched launchers what their installations are doing — also the ones started in
    /// the launcher itself — and updates the covers. Rescans the library when one completed.
    /// </summary>
    public async Task PollInstallsAsync()
    {
        if (_pollingInstalls || IsLoading || _installMonitors.Count == 0)
        {
            return;
        }

        _pollingInstalls = true;
        try
        {
            var snapshots = await Task.Run(() => _installMonitors.Select(m => (m.LauncherId, Snapshot: m.Poll())).ToList());

            var installs = new Dictionary<string, InstallProgress>(StringComparer.Ordinal);
            var outcomes = new List<(string Key, InstallOutcome Outcome)>();
            foreach (var (launcherId, snapshot) in snapshots)
            {
                foreach (var (id, progress) in snapshot.Active)
                {
                    var key = $"{launcherId}:{id}";
                    installs[key] = progress;
                    _awaitingConfirmation.Remove(key);
                }

                foreach (var (id, outcome) in snapshot.Finished)
                {
                    var key = $"{launcherId}:{id}";
                    _awaitingConfirmation.Remove(key);
                    outcomes.Add((key, outcome));
                }
            }

            var now = _time.GetUtcNow();
            foreach (var (key, since) in _awaitingConfirmation.ToList())
            {
                if (now - since > ConfirmationTimeout)
                {
                    _awaitingConfirmation.Remove(key);
                }
                else
                {
                    installs.TryAdd(key, new InstallProgress(InstallPhase.AwaitingConfirmation));
                }
            }

            _installs = installs;
            ApplyInstallStates();

            if (outcomes.Count > 0)
            {
                await ReportInstallOutcomesAsync(outcomes);
            }
        }
        finally
        {
            _pollingInstalls = false;
        }
    }

    private async Task ReportInstallOutcomesAsync(List<(string Key, InstallOutcome Outcome)> outcomes)
    {
        string TitleOf(string key) =>
            Tabs.SelectMany(t => t.Games).FirstOrDefault(g => g.Key == key)?.Title ?? "Spiel";

        if (outcomes.Any(o => o.Outcome == InstallOutcome.Completed))
        {
            await RefreshLibraryAsync(force: true);
        }

        if (outcomes.Any(o => o.Outcome == InstallOutcome.Uninstalled))
        {
            // Epic logs the uninstall when it starts; the manifest disappears a moment later.
            await RefreshLibraryAsync(force: true);
            _ = Task.Delay(UninstallSettleTime, _time).ContinueWith(
                _ => RefreshLibraryAsync(force: true),
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        var (key, outcome) = outcomes[^1];
        ShowToast(outcome switch
        {
            InstallOutcome.Uninstalled => $"„{TitleOf(key)}“ wurde deinstalliert.",
            InstallOutcome.Completed when outcomes.Count(o => o.Outcome == InstallOutcome.Completed) > 1
                => $"{outcomes.Count(o => o.Outcome == InstallOutcome.Completed)} Spiele sind jetzt installiert.",
            InstallOutcome.Completed => $"„{TitleOf(key)}“ ist jetzt installiert.",
            InstallOutcome.Cancelled => $"Installation von „{TitleOf(key)}“ abgebrochen.",
            _ => $"Installation von „{TitleOf(key)}“ ist fehlgeschlagen – bitte im Launcher nachsehen.",
        });
    }

    private void ApplyInstallStates()
    {
        foreach (var tile in PageGames)
        {
            tile.InstallState = _installs.GetValueOrDefault(tile.Game.Key);
        }
    }

    private bool IsWatched(Game game) => _installMonitors.Any(m => m.LauncherId == game.LauncherId);

    /// <summary>
    /// Rescans all launchers and takes over what changed — games installed or uninstalled outside
    /// Arcadia. Called when the window becomes active again, when a selected game's install
    /// directory is gone, and after install or uninstall events. Without <paramref name="force"/>
    /// it skips if the last rescan was only seconds ago.
    /// </summary>
    public async Task RefreshLibraryAsync(bool force = false)
    {
        var now = _time.GetUtcNow();
        if (IsLoading || _refreshingLibrary || (!force && now - _lastLibraryRefresh < RefreshThrottle))
        {
            return;
        }

        _refreshingLibrary = true;
        _lastLibraryRefresh = now;
        try
        {
            var libraries = await _library.LoadAsync();
            if (HasChanged(libraries))
            {
                ApplyLibraries(libraries);
            }
        }
        finally
        {
            _refreshingLibrary = false;
        }
    }

    private bool HasChanged(IReadOnlyList<LauncherLibrary> libraries)
    {
        static string Signature(IEnumerable<Game> games) => string.Join('|', games
            .Select(g => $"{g.Key}:{g.IsInstalled}:{g.UpdateAvailable}:{g.InstallDirectory}")
            .Order(StringComparer.Ordinal));

        return libraries.Count != Tabs.Count
            || Signature(libraries.SelectMany(l => l.Games)) != Signature(Tabs.SelectMany(t => t.Games));
    }

    /// <summary>Swaps in rescanned games and keeps tab, page and focused game where they were.</summary>
    private void ApplyLibraries(IReadOnlyList<LauncherLibrary> libraries)
    {
        foreach (var library in libraries)
        {
            if (Tabs.FirstOrDefault(t => t.Launcher.Id == library.Launcher.Id) is { } tab)
            {
                tab.Games = library.Games;
            }
            else
            {
                Tabs.Add(new LauncherTabViewModel(library, SelectTab));
            }
        }

        var focusedKey = FocusedGame?.Game.Key;
        var focusIndex = FocusedGame is { } focused ? PageGames.IndexOf(focused) : 0;
        Refresh(focusIndex);
        if (focusedKey is not null && PageGames.FirstOrDefault(t => t.Game.Key == focusedKey) is { } tile)
        {
            Focus(tile);
        }
    }

    public void UpdateClock()
    {
        var now = _time.GetLocalNow();
        Clock = Formatting.Clock(now);
        Date = Formatting.ShortDate(now);
    }

    public void Handle(UiAction action)
    {
        if (Settings is { } settings)
        {
            if (!settings.Handle(action))
            {
                Settings = null;
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
                if (FocusedGame is { } game)
                {
                    Activate(game);
                }

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
        }
    }

    [RelayCommand]
    private void OpenSettings() => Settings = new SettingsViewModel(_controllerSupport);

    [RelayCommand]
    private void CloseSettings() => Settings = null;

    partial void OnSettingsChanged(SettingsViewModel? oldValue, SettingsViewModel? newValue) => oldValue?.Dispose();

    /// <summary>Cycles the focused game through: default → always on → always off → default.</summary>
    [RelayCommand]
    private void CycleControllerSupport()
    {
        if (FocusedGame?.Game is not { } game)
        {
            return;
        }

        var next = _controllerSupport.Settings.ControllerSupportOverrideFor(game) switch
        {
            null => true,
            true => false,
            false => (bool?)null,
        };
        _controllerSupport.SetOverride(game, next);
    }

    private void UpdateControllerSupportState()
    {
        var support = _controllerSupport;
        ShowControllerSupport = support.Enabled;
        HasSisrUpdate = support.Enabled && support.AvailableUpdate is not null;
        IsControllerStarting = support.Enabled && support.IsStarting;
        HasControllerWarning = support.Enabled && support.Issues.Count > 0;
        ControllerWarningText = support.Issues.Count switch
        {
            0 => string.Empty,
            1 => $"Steam Controller Support: {support.Issues[0].Title}",
            var n => $"Steam Controller Support: {n} Probleme",
        };

        if (FocusedGame?.Game is { } game)
        {
            var effective = support.Settings.ControllerSupportFor(game) ? "an" : "aus";
            FocusedControllerSupportText = support.Settings.ControllerSupportOverrideFor(game) is null
                ? $"Standard ({effective})"
                : (effective == "an" ? "Immer an" : "Immer aus");
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
        SelectTab(Tabs[index]);
    }

    private void GoToPage(int page)
    {
        if (SelectedTab is null)
        {
            return;
        }

        var window = PageWindow.For(SelectedTab.Count, PageSizes.Effective(_preferredPageSize, SelectedTab.Count), page);
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
            _ = RefreshLibraryAsync();
        }
    }

    private async void Activate(GameTileViewModel tile)
    {
        Focus(tile);
        if (tile.InstallState is { } install)
        {
            ShowToast(install.Phase == InstallPhase.AwaitingConfirmation
                ? $"Die Installation von „{tile.Title}“ wartet auf deine Bestätigung in {tile.LauncherName}."
                : install.Phase == InstallPhase.Uninstalling
                    ? $"„{tile.Title}“ wird gerade deinstalliert."
                    : $"„{tile.Title}“ wird bereits installiert – Pausieren und Abbrechen in {tile.LauncherName}.");
            return;
        }

        if (!tile.IsInstalled)
        {
            Install(tile);
            return;
        }

        if (_launching)
        {
            return;
        }

        _launching = true;
        var progress = new Progress<LaunchStage>(stage => OnLaunchStage(tile, stage));
        try
        {
            // Games that use Steam Input themselves would get every input twice while SISR runs.
            await _controllerSupport.BeginGameAsync(tile.Game);
            await _launcher.StartAsync(tile.Game, progress);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            ShowToast($"„{tile.Title}“ konnte nicht gestartet werden: {ex.Message}");
        }
        finally
        {
            _launching = false;
        }
    }

    private void Install(GameTileViewModel tile)
    {
        try
        {
            if (GameLaunchService.Install(tile.Game))
            {
                if (IsWatched(tile.Game))
                {
                    _awaitingConfirmation[tile.Game.Key] = _time.GetUtcNow();
                    tile.InstallState = new InstallProgress(InstallPhase.AwaitingConfirmation);
                }
                else
                {
                    _pendingInstalls.Add(tile.Game.Key);
                }

                ShowToast($"Installation von „{tile.Title}“ wird in {tile.LauncherName} geöffnet …");
            }
            else
            {
                ShowToast(tile.Game.Install is LaunchTarget.None
                    ? $"Demo-Spiel: Hier würde jetzt „{tile.Title}“ installiert."
                    : $"„{tile.Title}“ kann nur direkt in {tile.LauncherName} installiert werden.");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            ShowToast($"Installation von „{tile.Title}“ konnte nicht geöffnet werden: {ex.Message}");
        }
    }

    private void OnLaunchStage(GameTileViewModel tile, LaunchStage stage)
    {
        switch (stage)
        {
            case LaunchStage.StartingLauncher:
                ShowToast($"{tile.LauncherName} wird im Hintergrund gestartet …", sticky: true);
                _ = Task.Delay(TimeSpan.FromSeconds(2), _time).ContinueWith(
                    _ => ForegroundRequested?.Invoke(this, EventArgs.Empty),
                    TaskScheduler.FromCurrentSynchronizationContext());
                break;
            case LaunchStage.LauncherReady:
                // Starting a launcher can steal the foreground; take it back before the game opens.
                ForegroundRequested?.Invoke(this, EventArgs.Empty);
                break;
            case LaunchStage.Starting:
                ShowToast(tile.UpdateAvailable ? $"Update für „{tile.Title}“ wird in {tile.LauncherName} gestartet …" : $"„{tile.Title}“ wird gestartet …");
                break;
            case LaunchStage.NotStartable:
                ShowToast($"Demo-Spiel: Hier würde jetzt „{tile.Title}“ {(tile.UpdateAvailable ? "aktualisiert" : "gestartet")}.");
                break;
            case LaunchStage.NotConfirmed:
                ShowToast($"„{tile.Title}“ ist nicht gestartet – bitte in {tile.LauncherName} nachsehen.");
                break;
        }
    }

    private void Refresh(int focusIndex)
    {
        if (SelectedTab is not { } tab)
        {
            return;
        }

        var pageSize = PageSizes.Effective(_preferredPageSize, tab.Count);
        var sorted = GameSorter.Sort(tab.Games, _sort, Formatting.Culture);
        var window = PageWindow.For(sorted.Count, pageSize, _requestedPage);
        _requestedPage = window.PageIndex;

        var now = _time.GetLocalNow();
        PageGames.Clear();
        foreach (var game in sorted.Skip(window.Start).Take(window.Length))
        {
            PageGames.Add(new GameTileViewModel(game, tab.Launcher, now, Focus, Activate) { InstallState = _installs.GetValueOrDefault(game.Key) });
        }

        FocusedGame = null;
        if (PageGames.Count > 0)
        {
            Focus(PageGames[Math.Clamp(focusIndex, 0, PageGames.Count - 1)]);
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
        ViewSummary = $"{SortLabel(_sort)}  ·  {pageSize} pro Seite";
    }

    private ViewOptionsViewModel CreateViewOptions() => new(
        SelectedTab!.Name,
        SelectedTab.Count,
        _sort,
        _preferredPageSize,
        sort =>
        {
            _sort = sort;
            _requestedPage = 0;
            Refresh(focusIndex: 0);
            RebuildViewOptions();
        },
        size =>
        {
            _preferredPageSize = size;
            _requestedPage = 0;
            Refresh(focusIndex: 0);
            RebuildViewOptions();
        });

    private void RebuildViewOptions()
    {
        if (ViewOptions is { } current)
        {
            var position = current.FocusPosition;
            ViewOptions = CreateViewOptions();
            ViewOptions.FocusPosition = position;
        }
    }

    private void ShowToast(string message, bool sticky = false)
    {
        _toastTimer?.Cancel();
        _toastTimer = new CancellationTokenSource();
        var token = _toastTimer.Token;
        Toast = message;
        if (sticky)
        {
            return;
        }

        _ = Task.Delay(TimeSpan.FromSeconds(4), _time, token).ContinueWith(
            _ => Toast = null,
            token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.FromCurrentSynchronizationContext());
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
