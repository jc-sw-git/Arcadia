using Arcadia.App.Services;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>What <see cref="LibrarySession"/> hands to the window: the libraries read, and the covers whose states it keeps up to date.</summary>
public interface ILibrarySessionOwner
{
    /// <summary>Shows freshly read libraries on every page; tab, page and focused game stay where they were.</summary>
    void Show(IReadOnlyList<LauncherLibrary> libraries);

    /// <summary>The covers of every open page, also of one covered by another.</summary>
    IEnumerable<GameTileViewModel> ShownTiles { get; }

    /// <summary>A launcher was read again, maybe with nothing changed: its "Zuletzt eingelesen" changed anyway.</summary>
    void LaunchersRead();
}

/// <summary>
/// The library while Arcadia runs (#155): loading and rescanning the launchers, installs and running games. It hands what
/// it read to the window and keeps the covers' install and run states, but knows nothing of the pages or their navigation.
/// </summary>
/// <remarks>
/// One per window, created and owned by <see cref="MainViewModel"/>; UI thread only. Its delayed rescans (after an
/// uninstall, after a game ended) run on the UI thread through <see cref="AppErrors"/> and need no cancelling: a rescan
/// at any time is safe.
/// </remarks>
public sealed partial class LibrarySession : ObservableObject
{
    private static readonly TimeSpan UninstallSettleTime = TimeSpan.FromSeconds(5);

    /// <summary>Steam may write a game's playtime a while after it ended, so the library is read once more after this.</summary>
    private static readonly TimeSpan PlayStatsSettleTime = TimeSpan.FromSeconds(30);

    private readonly LibraryService _library;
    private readonly LibraryRefresher _refresher;
    private readonly InstallTracker _installs;
    private readonly RunningGamesTracker _running;
    private readonly LauncherTabsViewModel _tabs;
    private readonly ToastViewModel _toast;
    private readonly TimeProvider _time;
    private readonly ILibrarySessionOwner _owner;

    public LibrarySession(
        ILibrarySessionOwner owner,
        LibraryService library,
        InstallTracker installs,
        RunningGamesTracker running,
        LaunchedGames launchedGames,
        LauncherTabsViewModel tabs,
        ToastViewModel toast,
        TimeProvider time)
    {
        _owner = owner;
        _library = library;
        _refresher = new LibraryRefresher(library, time);
        _installs = installs;
        _running = running;
        LaunchedGames = launchedGames;
        _tabs = tabs;
        _toast = toast;
        _time = time;
    }

    [ObservableProperty]
    public partial bool IsLoading { get; private set; } = true;

    /// <summary>The install folders of the library's games: a process from one of them is a game in front.</summary>
    public GameFolders GameFolders { get; private set; } = GameFolders.Empty;

    /// <summary>Games Arcadia started, for a game in front whose path can't be read.</summary>
    public LaunchedGames LaunchedGames { get; }

    /// <summary>The launchers Arcadia knows and which of them are switched on.</summary>
    public LauncherRegistry Launchers => _library.Launchers;

    /// <summary>A game started from Arcadia ended, with its key: Arcadia may come back to the front (#94).</summary>
    public event EventHandler<string>? StartedGameEnded;

    public DateTimeOffset? ScannedAt(string launcherId) => _library.ScannedAt(launcherId);

    /// <summary>A new cover takes its game's install and run state.</summary>
    public void ShowStates(GameTileViewModel tile)
    {
        tile.InstallState = _installs.StateOf(tile.Game.Key);
        tile.RunState = _running.StateOf(tile.Game.Key);
    }

    /// <summary>A game is starting or its start failed: every shown cover takes its run state again.</summary>
    public void UpdateRunStates()
    {
        foreach (var tile in _owner.ShownTiles)
        {
            tile.RunState = _running.StateOf(tile.Game.Key);
        }
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var libraries = await _library.LoadAsync();
        var scanned = watch.ElapsedMilliseconds;
        IsLoading = false;
        Show(libraries);

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

        Show(libraries);
        _toast.Show(finished.Count == 1 ? $"„{finished[0].Title}“ ist jetzt installiert." : $"{finished.Count} Spiele sind jetzt installiert.");
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

        foreach (var tile in _owner.ShownTiles)
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
        RefreshLibraryLater(PlayStatsSettleTime);
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

        if (await _refresher.RefreshAsync(_tabs.Libraries, force) is { } libraries)
        {
            Show(libraries);
        }

        // "Zuletzt eingelesen" changed even if nothing else did.
        _owner.LaunchersRead();
    }

    /// <summary>
    /// "Neu einlesen" on an empty tab or a launcher's page reads <paramref name="launcher"/> again; X and "Alle neu einlesen"
    /// in the launcher settings read all (null). Says what came of it.
    /// </summary>
    public async Task RescanAsync(LauncherInfo? launcher)
    {
        if (IsLoading)
        {
            return;
        }

        _toast.Show(launcher is null ? "Alle Launcher werden neu eingelesen …" : $"{launcher.DisplayName} wird neu eingelesen …", sticky: true);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        await (launcher is null ? RefreshLibraryAsync(force: true) : RefreshLauncherAsync(launcher.Id));
        AppLog.Write($"Neu eingelesen{(launcher is null ? string.Empty : $" für {launcher.Id}")}: {watch.ElapsedMilliseconds} ms");
        if (launcher is null)
        {
            _toast.Show(_tabs.Items.Count == 0 ? "Weiterhin ist kein Launcher aktiv." : "Alle Launcher sind neu eingelesen.");
            return;
        }

        var name = launcher.DisplayName;
        _toast.Show(_tabs.Find(launcher.Id) switch
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
    public void ShowChosenLaunchers()
    {
        if (IsLoading)
        {
            return;
        }

        var active = _library.Launchers.ActiveIds;
        var shown = _tabs.Libraries();
        var kept = active.Select(id => shown.FirstOrDefault(l => l.Launcher.Id == id)).OfType<LauncherLibrary>().ToList();
        Show(kept);
        if (kept.Count < active.Count)
        {
            RefreshLibraryAsync(force: true).Forget("Bibliothek aktualisieren");
        }
    }

    /// <summary>Reads one launcher again ("Neu einlesen", #137); the other tabs keep their games.</summary>
    private async Task RefreshLauncherAsync(string launcherId)
    {
        if (IsLoading)
        {
            return;
        }

        if (await _refresher.RefreshAsync(_tabs.Libraries, launcherId) is { } libraries)
        {
            Show(libraries);
        }

        _owner.LaunchersRead();
    }

    private async Task ReportInstallResultsAsync(IReadOnlyList<InstallResult> results)
    {
        string? KnownTitleOf(string key) => _tabs.Items.SelectMany(t => t.Games).FirstOrDefault(g => g.Key == key)?.Title;

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
            RefreshLibraryLater(UninstallSettleTime);
        }

        var completed = results.Count(r => r.Outcome == InstallOutcome.Completed);
        var title = titleBefore ?? KnownTitleOf(key) ?? "Spiel";
        _toast.Show(outcome switch
        {
            InstallOutcome.Uninstalled => $"„{title}“ wurde deinstalliert.",
            InstallOutcome.Completed when completed > 1 => $"{completed} Spiele sind jetzt installiert.",
            InstallOutcome.Completed => $"„{title}“ ist jetzt installiert.",
            InstallOutcome.Cancelled => $"Installation von „{title}“ abgebrochen.",
            _ => $"Installation von „{title}“ ist fehlgeschlagen – bitte im Launcher nachsehen.",
        });
    }

    private void RefreshLibraryLater(TimeSpan delay) =>
        Task.Delay(delay, _time).ContinueWith(
            _ => RefreshLibraryAsync(force: true),
            TaskScheduler.FromCurrentSynchronizationContext()).Unwrap().Forget("Bibliothek aktualisieren");

    private void Show(IReadOnlyList<LauncherLibrary> libraries)
    {
        GameFolders = GameFolders.Of(libraries.SelectMany(l => l.Games));
        _running.SetLibrary(libraries.SelectMany(l => l.Games));
        _owner.Show(libraries);
    }
}
