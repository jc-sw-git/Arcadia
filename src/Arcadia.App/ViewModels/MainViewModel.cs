using System.Collections.ObjectModel;
using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>The Big Picture library: launcher tabs, a paginated cover grid and the preview panel.</summary>
public partial class MainViewModel : ViewModelBase
{
    /// <summary>Must match the column count of the grid in MainWindow.axaml.</summary>
    public const int GridColumns = 8;

    private static readonly TimeSpan UninstallSettleTime = TimeSpan.FromSeconds(5);

    private readonly LibraryService _library;
    private readonly LibraryRefresher _refresher;
    private readonly InstallTracker _installs;
    private readonly GameLaunchService _launcher;
    private readonly ControllerSupportService _controllerSupport;
    private readonly ArcadiaUpdateService _updates;
    private readonly TimeProvider _time;
    private SortOrder _sort = SortOrder.Default;
    private int _preferredPageSize = PageSizes.Default;
    private int _requestedPage;
    private bool _launching;

    public MainViewModel(
        LibraryService library,
        GameLaunchService launcher,
        ControllerSupportService controllerSupport,
        ArcadiaUpdateService updates,
        InstallTracker installs,
        TimeProvider time)
    {
        _library = library;
        _refresher = new LibraryRefresher(library, time);
        _launcher = launcher;
        _controllerSupport = controllerSupport;
        _updates = updates;
        _installs = installs;
        _time = time;
        Status = new StatusBarViewModel(controllerSupport, updates, time);
        Toast = new ToastViewModel(time);
        _controllerSupport.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateControllerSupportState);
        UpdateControllerSupportState();
    }

    public StatusBarViewModel Status { get; }

    public ToastViewModel Toast { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsOpen))]
    public partial SettingsViewModel? Settings { get; private set; }

    public bool IsSettingsOpen => Settings is not null;

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

        foreach (var tile in PageGames)
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
            _ = Task.Delay(UninstallSettleTime, _time).ContinueWith(
                _ => RefreshLibraryAsync(force: true),
                TaskScheduler.FromCurrentSynchronizationContext());
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
                Tabs.Insert(i, new LauncherTabViewModel(library, SelectTab));
                continue;
            }

            Tabs[index].Games = library.Games;
            if (index != i)
            {
                Tabs.Move(index, i);
            }
        }

        IsEmpty = Tabs.Count == 0;
        if (SelectedTab is null || !Tabs.Contains(SelectedTab))
        {
            SelectedTab = null;
            FocusedGame = null;
            ViewOptions = null;
            PageGames.Clear();
            PageButtons.Clear();
            CountText = RangeText = ViewSummary = string.Empty;
            if (Tabs.Count > 0)
            {
                SelectTab(Tabs[0]);
            }

            return;
        }

        var focusedKey = FocusedGame?.Game.Key;
        var focusIndex = FocusedGame is { } focused ? PageGames.IndexOf(focused) : 0;
        Refresh(focusIndex);
        if (focusedKey is not null && PageGames.FirstOrDefault(t => t.Game.Key == focusedKey) is { } tile)
        {
            Focus(tile);
        }
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
    private void OpenSettings() => Settings = new SettingsViewModel(_controllerSupport, _updates, _updates.InstallAsync);

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
            Toast.Show($"„{tile.Title}“ konnte nicht gestartet werden: {ex.Message}");
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
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
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
                _ = Task.Delay(TimeSpan.FromSeconds(2), _time).ContinueWith(
                    _ => ForegroundRequested?.Invoke(this, EventArgs.Empty),
                    TaskScheduler.FromCurrentSynchronizationContext());
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
            PageGames.Add(new GameTileViewModel(game, tab.Launcher, now, Focus, Activate) { InstallState = _installs.StateOf(game.Key) });
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
