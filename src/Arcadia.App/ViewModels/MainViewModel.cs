using System.Collections.ObjectModel;
using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
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
    private CancellationTokenSource? _toastTimer;

    private readonly ControllerSupportService _controllerSupport;

    public MainViewModel(LibraryService library, GameLaunchService launcher, ControllerSupportService controllerSupport, TimeProvider time)
    {
        _library = library;
        _launcher = launcher;
        _controllerSupport = controllerSupport;
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
    }

    private async void Activate(GameTileViewModel tile)
    {
        Focus(tile);
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
            PageGames.Add(new GameTileViewModel(game, tab.Launcher, now, Focus, Activate));
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

        CountText = $"{tab.Count} Spiele · Seite {window.PageIndex + 1} von {window.PageCount}";
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
