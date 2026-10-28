using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.Services;

/// <summary>
/// The library while Arcadia runs (#155): loading and rescanning the launchers, installs and running games. It holds what
/// it read and the games' install and run states, and says when they changed; it knows no pages, covers, tabs or toasts
/// (#228), so another view could follow it the same way.
/// </summary>
/// <remarks>
/// One per window, created and owned by <c>MainViewModel</c>; UI thread only, and its events are raised there. Its delayed
/// rescans (after an uninstall, after a game ended) run on the UI thread through <see cref="AppErrors"/>; disposing the
/// session cancels them.
/// </remarks>
public sealed partial class LibrarySession : ObservableObject, IDisposable
{
    private static readonly TimeSpan UninstallSettleTime = TimeSpan.FromSeconds(5);

    /// <summary>Steam may write a game's playtime a while after it ended, so the library is read once more after this.</summary>
    private static readonly TimeSpan PlayStatsSettleTime = TimeSpan.FromSeconds(30);

    private readonly LibraryService _library;
    private readonly LibraryRefresher _refresher;
    private readonly InstallTracker _installs;
    private readonly RunningGamesTracker _running;
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _shutdown = new();

    // Launchers whose account changed their games while the library was loading: read again once it is loaded (#83).
    private readonly HashSet<string> _changedWhileLoading = new(StringComparer.Ordinal);
    private readonly List<(ILauncherAccount Account, EventHandler Changed, EventHandler<Exception> Failed)> _accounts = [];

    public LibrarySession(LibraryService library, InstallTracker installs, RunningGamesTracker running, LaunchedGames launchedGames, TimeProvider time)
    {
        _library = library;
        _refresher = new LibraryRefresher(library, time);
        _installs = installs;
        _running = running;
        LaunchedGames = launchedGames;
        _time = time;
        foreach (var integration in library.Launchers.All)
        {
            if (integration.Account is { } account)
            {
                var launcherId = integration.Launcher.Id;
                EventHandler changed = (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => OnAccountLibraryChanged(launcherId));
                EventHandler<Exception> failed = (_, e) => Avalonia.Threading.Dispatcher.UIThread.Post(() => AppErrors.Report(e, $"Konto von {integration.Launcher.DisplayName}"));
                account.LibraryChanged += changed;
                account.Failed += failed;
                _accounts.Add((account, changed, failed));
            }
        }
    }

    [ObservableProperty]
    public partial bool IsLoading { get; private set; } = true;

    // What the scanners delivered, every type (#141): a switch on a launcher's page then needs no rescan.
    private IReadOnlyList<LauncherLibrary> _scanned = [];

    /// <summary>
    /// The libraries read last, one per active launcher in the user's order, with the entries their tabs show: software,
    /// tools and soundtracks only where the launcher's switch for them is on (#141).
    /// </summary>
    public IReadOnlyList<LauncherLibrary> Libraries { get; private set; } = [];

    /// <summary>The install folders of the library's games: a process from one of them is a game in front.</summary>
    public GameFolders GameFolders { get; private set; } = GameFolders.Empty;

    /// <summary>Games Arcadia started, for a game in front whose path can't be read.</summary>
    public LaunchedGames LaunchedGames { get; }

    /// <summary>The launchers Arcadia knows and which of them are switched on.</summary>
    public LauncherRegistry Launchers => _library.Launchers;

    /// <summary>Freshly read <see cref="Libraries"/>, maybe with nothing changed in a launcher's games.</summary>
    public event EventHandler? LibrariesChanged;

    /// <summary>A launcher was read again, maybe with nothing changed: its "Zuletzt eingelesen" changed anyway.</summary>
    public event EventHandler? LaunchersRead;

    /// <summary>A game's install state changed (<see cref="InstallStateOf"/>).</summary>
    public event EventHandler? InstallStatesChanged;

    /// <summary>A game started, runs or ended (<see cref="RunStateOf"/>).</summary>
    public event EventHandler? RunStatesChanged;

    /// <summary>Something the player should hear of: a rescan started or ended, an install finished, a game was uninstalled.</summary>
    public event EventHandler<LibraryNotice>? Noticed;

    /// <summary>A game started from Arcadia ended, with its key: Arcadia may come back to the front (#94).</summary>
    public event EventHandler<string>? StartedGameEnded;

    /// <summary>The launcher aborted the start of a game from Arcadia, with its key: Arcadia comes to the front as after a game (#277).</summary>
    public event EventHandler<string>? StartRefused;

    /// <summary>A game Arcadia started waited for an update, and its launcher didn't start it after it: start it once more.</summary>
    public event EventHandler<string>? StartAgainRequested;

    public DateTimeOffset? ScannedAt(string launcherId) => _library.ScannedAt(launcherId);

    public InstallProgress? InstallStateOf(string gameKey) => _installs.StateOf(gameKey);

    public RunState? RunStateOf(string gameKey) => _running.StateOf(gameKey);

    /// <summary>The library read last of <paramref name="launcherId"/>; null if the launcher isn't active.</summary>
    public LauncherLibrary? LibraryOf(string launcherId) => Libraries.FirstOrDefault(l => l.Launcher.Id == launcherId);

    public async Task LoadAsync()
    {
        IsLoading = true;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var libraries = await _library.LoadAsync();
        var scanned = watch.ElapsedMilliseconds;
        IsLoading = false;
        Show(libraries);
        foreach (var launcherId in _changedWhileLoading)
        {
            RefreshLauncherAsync(launcherId).Forget("Bibliothek aktualisieren");
        }

        _changedWhileLoading.Clear();

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
        Noticed?.Invoke(this, new LibraryNotice.PendingInstallsFinished(finished));
    }

    /// <summary>
    /// Asks the watched launchers what their installations are doing — also the ones started in
    /// the launcher itself — and says so. Rescans the library when one completed.
    /// </summary>
    public async Task PollInstallsAsync()
    {
        if (IsLoading || await _installs.PollAsync() is not { } results)
        {
            return;
        }

        InstallStatesChanged?.Invoke(this, EventArgs.Empty);
        if (results.Count > 0)
        {
            await ReportInstallResultsAsync(results);
        }
    }

    /// <summary>
    /// Asks the launchers which games run (#95) and says so. Called periodically, also in the background, and when Arcadia
    /// comes to the front; the end of a game started from Arcadia may bring it back (#94).
    /// </summary>
    public async Task PollRunningGamesAsync()
    {
        if (await _running.PollAsync() is not { } changes)
        {
            return;
        }

        if (changes.StatesChanged)
        {
            RunStatesChanged?.Invoke(this, EventArgs.Empty);
        }

        foreach (var unstarted in changes.NotStarted)
        {
            ReportNotStarted(unstarted);
        }

        foreach (var gameKey in changes.StartAgain)
        {
            StartAgainRequested?.Invoke(this, gameKey);
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

        if (await _refresher.RefreshAsync(() => _scanned, force) is { } libraries)
        {
            Show(libraries);
        }

        // "Zuletzt eingelesen" changed even if nothing else did.
        LaunchersRead?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// "Neu einlesen" on an empty tab or a launcher's page reads <paramref name="launcher"/> again; X and "Alle neu einlesen"
    /// in the launcher settings read all (null). Says when it starts and what came of it.
    /// </summary>
    public async Task RescanAsync(LauncherInfo? launcher)
    {
        if (IsLoading)
        {
            return;
        }

        Noticed?.Invoke(this, new LibraryNotice.RescanStarted(launcher));

        // Asked for by the user: an account fetches its games now (a game just bought, back online, #83).
        foreach (var integration in launcher is null ? _library.Launchers.Enabled : _library.Launchers.Enabled.Where(i => i.Launcher.Id == launcher.Id))
        {
            integration.Account?.RefreshNow();
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();
        await (launcher is null ? RefreshLibraryAsync(force: true) : RefreshLauncherAsync(launcher.Id));
        AppLog.Write($"Neu eingelesen{(launcher is null ? string.Empty : $" für {launcher.Id}")}: {watch.ElapsedMilliseconds} ms");
        Noticed?.Invoke(this, new LibraryNotice.RescanFinished(launcher));
    }

    /// <summary>
    /// The user switched launchers on or off or moved them (#136): the libraries follow at once from what is already read;
    /// a launcher switched on is read now.
    /// </summary>
    public void ShowChosenLaunchers()
    {
        if (IsLoading)
        {
            return;
        }

        var active = _library.Launchers.ActiveIds;
        var kept = active.Select(id => _scanned.FirstOrDefault(l => l.Launcher.Id == id)).OfType<LauncherLibrary>().ToList();
        Show(kept);
        if (kept.Count < active.Count)
        {
            RefreshLibraryAsync(force: true).Forget("Bibliothek aktualisieren");
        }
    }

    /// <summary>
    /// A switch for software, tools or soundtracks changed on a launcher's page (#141): the tabs show the entries it adds
    /// or hides at once, from what is already read.
    /// </summary>
    public void ShowChosenTypes()
    {
        if (!IsLoading)
        {
            Show(_scanned);
        }
    }

    /// <summary>Cancels the delayed rescans; Arcadia is closing. Once is enough, more calls do nothing.</summary>
    public void Dispose()
    {
        if (!_shutdown.IsCancellationRequested)
        {
            _shutdown.Cancel();
        }

        foreach (var (account, changed, failed) in _accounts)
        {
            account.LibraryChanged -= changed;
            account.Failed -= failed;
        }

        _accounts.Clear();
    }

    /// <summary>
    /// A launcher's account fetched other games, or was signed in or out (#83): the launcher is read again, so its tab and
    /// the search show the change at once.
    /// </summary>
    private void OnAccountLibraryChanged(string launcherId)
    {
        if (_shutdown.IsCancellationRequested || !_library.Launchers.ActiveIds.Contains(launcherId))
        {
            return;
        }

        if (IsLoading)
        {
            _changedWhileLoading.Add(launcherId);
            return;
        }

        RefreshLauncherAsync(launcherId).Forget("Bibliothek aktualisieren");
    }

    /// <summary>Reads one launcher again ("Neu einlesen", #137); the other launchers keep their games.</summary>
    private async Task RefreshLauncherAsync(string launcherId)
    {
        if (IsLoading)
        {
            return;
        }

        if (await _refresher.RefreshAsync(() => _scanned, launcherId) is { } libraries)
        {
            Show(libraries);
        }

        LaunchersRead?.Invoke(this, EventArgs.Empty);
    }

    private async Task ReportInstallResultsAsync(IReadOnlyList<InstallResult> results)
    {
        // An uninstalled game leaves the library with the rescan below, a newly installed one only comes with it.
        var (key, outcome) = results[^1];
        var titleBefore = GameOf(key)?.Title;

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
        Noticed?.Invoke(this, new LibraryNotice.InstallEnded(outcome, titleBefore ?? GameOf(key)?.Title, completed));
    }

    /// <summary>"Wird gestartet …" ended without the game running: says why (#277); an aborted start brings Arcadia back.</summary>
    private void ReportNotStarted(UnstartedGame unstarted)
    {
        var launcher = Launchers.Info(Game.LauncherIdOf(unstarted.GameKey));
        Noticed?.Invoke(this, new LibraryNotice.GameNotStarted(GameOf(unstarted.GameKey)?.Title, launcher, unstarted.Refusal));
        if (unstarted.Refusal is not null)
        {
            StartRefused?.Invoke(this, unstarted.GameKey);
        }
    }

    /// <summary>The game with this key in <see cref="Libraries"/>, or null if it left them.</summary>
    public Game? GameOf(string gameKey) => Libraries.SelectMany(l => l.Games).FirstOrDefault(g => g.Key == gameKey);

    private void RefreshLibraryLater(TimeSpan delay) => RefreshLibraryAfterAsync(delay, _shutdown.Token).Forget("Bibliothek aktualisieren");

    // Continues on the UI thread it was started on; a cancelled delay ends quietly (AppErrors ignores the cancellation).
    private async Task RefreshLibraryAfterAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, _time, cancellationToken);

        // The delay may have run out just before the session ended, with its continuation still queued.
        cancellationToken.ThrowIfCancellationRequested();
        await RefreshLibraryAsync(force: true);
    }

    private void Show(IReadOnlyList<LauncherLibrary> libraries)
    {
        // A scan still running when the session ended shows nothing: the window is closing.
        if (_shutdown.IsCancellationRequested)
        {
            return;
        }

        _scanned = libraries;
        var launchers = _library.Launchers;
        Libraries = libraries.Select(l => l with { Games = launchers.ShownEntries(l.Launcher.Id, l.Games) }).ToList();
        libraries = Libraries;
        GameFolders = GameFolders.Of(libraries.SelectMany(l => l.Games));
        _running.SetLibrary(libraries.SelectMany(l => l.Games));
        LibrariesChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>What <see cref="LibrarySession"/> tells the player through the window, which words it (#228).</summary>
public abstract record LibraryNotice
{
    private LibraryNotice()
    {
    }

    /// <summary>Games handed to a launcher Arcadia can't watch are installed now.</summary>
    public sealed record PendingInstallsFinished(IReadOnlyList<Game> Games) : LibraryNotice;

    /// <summary>
    /// The launchers' install monitors reported the end of an install or uninstall: <paramref name="Outcome"/> and
    /// <paramref name="Title"/> (null if unknown) are the last one's, <paramref name="Completed"/> counts the completed installs.
    /// </summary>
    public sealed record InstallEnded(InstallOutcome Outcome, string? Title, int Completed) : LibraryNotice;

    /// <summary>
    /// A game started from Arcadia didn't run (#277): <paramref name="Launcher"/> aborted the start
    /// (<paramref name="Refusal"/>), or nothing was heard of it until "Wird gestartet …" timed out (null).
    /// <paramref name="Title"/> is null if the game left the library meanwhile.
    /// </summary>
    public sealed record GameNotStarted(string? Title, LauncherInfo Launcher, RefusedStart? Refusal) : LibraryNotice;

    /// <summary>"Neu einlesen" started for <paramref name="Launcher"/>, or for all launchers (null).</summary>
    public sealed record RescanStarted(LauncherInfo? Launcher) : LibraryNotice;

    /// <summary>"Neu einlesen" of <paramref name="Launcher"/>, or of all launchers (null), is done: <see cref="LibrarySession.Libraries"/> hold the result.</summary>
    public sealed record RescanFinished(LauncherInfo? Launcher) : LibraryNotice;
}
