using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Launchers;

public enum RunPhase
{
    /// <summary>Arcadia handed the game to its launcher and hasn't seen it run yet.</summary>
    Starting,

    Running,
}

/// <summary>Whether a game runs, as far as Arcadia can tell.</summary>
/// <param name="Since">When the game started, or null if that is unknown; while <see cref="RunPhase.Starting"/>, when Arcadia asked for it.</param>
public sealed record RunState(RunPhase Phase, DateTimeOffset? Since);

/// <summary>A game that stopped running.</summary>
/// <param name="StartedFromArcadia">Arcadia started it, so its end may bring Arcadia back to the front (#94).</param>
public sealed record EndedGame(string GameKey, bool StartedFromArcadia);

/// <param name="StatesChanged">A game started, ended or gave up starting; the covers show it.</param>
public sealed record RunningChanges(bool StatesChanged, IReadOnlyList<EndedGame> Ended);

/// <summary>
/// Which games run (#95), whether started from Arcadia or in their launcher: as the launcher reports it
/// (<see cref="IRunningGamesMonitor"/>), else by the games' processes (<see cref="ProcessGames"/>, only for launchers
/// without a monitor). It asks every launcher in every poll, also while Arcadia is in the background: Arcadia may be seen
/// on a second screen, and a game that ends must not go on showing "Läuft" (owner, 2026-10-21).
/// </summary>
public sealed class RunningGamesTracker(LauncherRegistry launchers, ProcessGames processes, TimeProvider time)
{
    /// <summary>
    /// "Wird gestartet …" lasts this long at most (owner, 2026-10-20), counted from the end of an installation of the game
    /// that runs meanwhile: a launcher may update a game first, however long that takes (owner, 2026-10-24, #167).
    /// </summary>
    public static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(2);

    // Games Arcadia handed to their launcher that weren't seen running yet.
    private readonly Dictionary<string, Start> _starting = new(StringComparer.Ordinal);

    // Games Arcadia started that were seen running and haven't ended.
    private readonly HashSet<string> _startedFromArcadia = new(StringComparer.Ordinal);

    private Dictionary<string, Running> _running = new(StringComparer.Ordinal);

    // A copy for other threads, renewed with every change of the two above.
    private volatile IReadOnlyCollection<string> _active = [];

    // Set on the UI thread, taken over by the next poll on the thread pool, where ProcessGames is used.
    private volatile GameFolders? _newFolders;
    private bool _polling;

    /// <summary>Optional diagnostics sink (Arcadia's log file).</summary>
    public Action<string>? Log { get; set; }

    /// <summary>Whether the launcher installs (e.g. updates) the game with this key now; asked on the calling thread.</summary>
    public Func<string, bool> IsInstalling { get; init; } = _ => false;

    /// <summary>Whether the game runs or is being started, or null.</summary>
    public RunState? StateOf(string gameKey) =>
        _running.TryGetValue(gameKey, out var running) ? new RunState(RunPhase.Running, running.StartedAt)
        : _starting.TryGetValue(gameKey, out var start) ? new RunState(RunPhase.Starting, start.AskedAt)
        : null;

    /// <summary>
    /// The keys of the games that run or are being started, from any thread: which Steam Controller mode applies (#213)
    /// is decided off the UI thread.
    /// </summary>
    public IReadOnlyCollection<string> ActiveGames => _active;

    /// <summary>The games started from Arcadia that haven't ended; an update restart hands them to the next Arcadia.</summary>
    public IReadOnlyCollection<string> StartedFromArcadia => [.. _starting.Keys.Union(_startedFromArcadia)];

    /// <summary>The library's games: those of launchers without a monitor are found by their processes.</summary>
    public void SetLibrary(IEnumerable<Game> games)
    {
        var byProcess = FallbackLaunchers();
        _newFolders = GameFolders.Of(games.Where(g => byProcess.Contains(g.LauncherId)));
    }

    /// <summary>Arcadia hands <paramref name="gameKey"/> to its launcher: "Wird gestartet …" until it runs, <see cref="StartTimeout"/> at most.</summary>
    public void Starting(string gameKey)
    {
        var now = time.GetUtcNow();
        _starting[gameKey] = new Start(now, now);
        UpdateActive();
    }

    /// <summary>The start failed or was refused; the game shows "Spielen" again.</summary>
    public void StartFailed(string gameKey)
    {
        _starting.Remove(gameKey);
        UpdateActive();
    }

    /// <summary>
    /// After an update restart: the Arcadia before started <paramref name="gameKey"/>. It counts as started from Arcadia
    /// once it is seen running, within <see cref="StartTimeout"/>.
    /// </summary>
    public void ResumeStartedFromArcadia(string gameKey) => Starting(gameKey);

    /// <summary>Asks the launchers which games run, off the calling thread. Returns null without asking while a poll still runs.</summary>
    public async Task<RunningChanges?> PollAsync()
    {
        if (_polling)
        {
            return null;
        }

        var watched = _starting.Keys.Union(_startedFromArcadia).ToHashSet(StringComparer.Ordinal);
        var enabled = launchers.Enabled;
        var monitors = enabled.Where(i => i.RunningGames is not null).Select(i => i.RunningGames!).ToList();
        var byProcess = FallbackLaunchers(enabled);
        var scan = byProcess.Count > 0;

        // Only a game started from Arcadia may own a process whose path can't be read.
        var unreadableMayBe = watched.Where(key => byProcess.Contains(LauncherIdOf(key))).ToHashSet(StringComparer.Ordinal);

        _polling = true;
        try
        {
            var (reports, processGames) = await Task.Run(() =>
            {
                if (_newFolders is { } folders)
                {
                    _newFolders = null;
                    processes.Watch(folders);
                }

                return (monitors.Select(Ask).OfType<Report>().ToList(), scan ? processes.Poll(unreadableMayBe) : null);
            });

            var asked = reports.Select(r => r.LauncherId).ToHashSet(StringComparer.Ordinal);
            var found = reports.SelectMany(r => r.Games.Select(g => (Key: Game.KeyOf(r.LauncherId, g.GameId), r.LauncherId, g.StartedAt))).ToList();
            if (processGames is not null)
            {
                asked.UnionWith(byProcess);
                found.AddRange(processGames.Select(g => (g.Key, LauncherIdOf(g.Key), g.Value)));
            }

            return Apply(asked, found, enabled.Select(i => i.Launcher.Id).ToHashSet(StringComparer.Ordinal));
        }
        finally
        {
            _polling = false;
        }
    }

    private Report? Ask(IRunningGamesMonitor monitor)
    {
        try
        {
            return new Report(monitor.LauncherId, monitor.Poll());
        }
        catch (Exception ex)
        {
            // Monitors may come from plugins; one that fails keeps its games as they were.
            Log?.Invoke($"Laufende Spiele von {monitor.LauncherId} nicht lesbar: {ex}");
            return null;
        }
    }

    private RunningChanges Apply(HashSet<string> asked, List<(string Key, string LauncherId, DateTimeOffset? StartedAt)> found, HashSet<string> enabled)
    {
        var running = _running
            .Where(game => !asked.Contains(game.Value.LauncherId) && enabled.Contains(game.Value.LauncherId))
            .ToDictionary(StringComparer.Ordinal);
        foreach (var (key, launcherId, startedAt) in found)
        {
            running[key] = new Running(launcherId, startedAt ?? (_running.TryGetValue(key, out var known) ? known.StartedAt : null));
        }

        var changed = running.Count != _running.Count
            || running.Any(game => !_running.TryGetValue(game.Key, out var before) || before != game.Value);

        // A game whose launcher was switched off is only no longer watched; it didn't end.
        var ended = new List<EndedGame>();
        foreach (var key in _running.Where(game => !running.ContainsKey(game.Key) && enabled.Contains(game.Value.LauncherId)).Select(game => game.Key))
        {
            ended.Add(new EndedGame(key, _startedFromArcadia.Remove(key)));
        }

        var now = time.GetUtcNow();
        foreach (var (key, start) in _starting.ToList())
        {
            if (running.ContainsKey(key))
            {
                _starting.Remove(key);
                _startedFromArcadia.Add(key);
                changed = true;
            }
            else if (enabled.Contains(LauncherIdOf(key)) && IsInstalling(key))
            {
                _starting[key] = start with { WaitsSince = now };
            }
            else if (now - start.WaitsSince > StartTimeout || !enabled.Contains(LauncherIdOf(key)))
            {
                Log?.Invoke($"{key} lief nach dem Start aus Arcadia nicht; „Wird gestartet …“ endet.");
                _starting.Remove(key);
                changed = true;
            }
        }

        // A game whose launcher was switched off would be awaited for ever.
        _startedFromArcadia.RemoveWhere(key => !enabled.Contains(LauncherIdOf(key)));
        _running = running;
        UpdateActive();
        return new RunningChanges(changed, ended);
    }

    private void UpdateActive() => _active = _running.Keys.Union(_starting.Keys, StringComparer.Ordinal).ToList();

    private HashSet<string> FallbackLaunchers(IEnumerable<ILauncherIntegration>? enabled = null) =>
        (enabled ?? launchers.Enabled).Where(i => i.RunningGames is null).Select(i => i.Launcher.Id).ToHashSet(StringComparer.Ordinal);

    private static string LauncherIdOf(string gameKey) => Game.LauncherIdOf(gameKey);

    private sealed record Report(string LauncherId, IReadOnlyList<RunningGame> Games);

    private readonly record struct Running(string LauncherId, DateTimeOffset? StartedAt);

    /// <param name="AskedAt">When Arcadia handed the game to its launcher.</param>
    /// <param name="WaitsSince">Where <see cref="StartTimeout"/> counts from: the request, or the end of an installation since.</param>
    private readonly record struct Start(DateTimeOffset AskedAt, DateTimeOffset WaitsSince);
}
