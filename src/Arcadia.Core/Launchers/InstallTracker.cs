using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Launchers;

/// <summary>An installation that ended, by <see cref="Game.Key"/>.</summary>
public sealed record InstallResult(string GameKey, InstallOutcome Outcome);

/// <summary>
/// Follows the installations Arcadia handed to a launcher and the ones started in the launcher
/// itself. Enabled launchers with an <see cref="IInstallMonitor"/> report progress; for the others,
/// the game stays pending until a rescan shows it installed.
/// </summary>
public sealed class InstallTracker(LauncherRegistry launchers, TimeProvider time)
{
    /// <summary>Epic gives up on its install dialog after 10 minutes.</summary>
    public static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromMinutes(10);

    /// <summary>How long a poll waits for a monitor; a launcher folder on a share that went away can hang one.</summary>
    public static readonly TimeSpan PollDeadline = TimeSpan.FromSeconds(10);

    private readonly MonitorPolls<InstallSnapshot> _monitors = new(time, PollDeadline, (launcherId, ex) => $"Installationen von {launcherId} nicht lesbar: {ex}");

    // What each launcher reported last; a monitor that fails or is late keeps showing it.
    private readonly Dictionary<string, IReadOnlyDictionary<string, InstallProgress>> _lastActive = new(StringComparer.Ordinal);

    // Install dialogs Arcadia opened in a watched launcher that the user hasn't confirmed yet.
    private readonly Dictionary<string, DateTimeOffset> _awaitingConfirmation = new(StringComparer.Ordinal);

    // Games handed to a launcher Arcadia can't watch.
    private readonly HashSet<string> _pending = new(StringComparer.Ordinal);

    private Dictionary<string, InstallProgress> _installs = new(StringComparer.Ordinal);
    private bool _polling;

    /// <summary>Where a monitor that failed or didn't answer in time is written, once per failure.</summary>
    public Action<string>? Log
    {
        get => _monitors.Log;
        set => _monitors.Log = value;
    }

    /// <summary>True while a game handed to an unwatched launcher hasn't shown up installed yet.</summary>
    public bool HasPending => _pending.Count > 0;

    /// <summary>What the installation of a game is doing, or null if none runs.</summary>
    public InstallProgress? StateOf(string gameKey) => _installs.GetValueOrDefault(gameKey);

    /// <summary>
    /// Records that the launcher's install dialog was opened for <paramref name="game"/>.
    /// </summary>
    /// <returns>The state to show until the next poll, or null if the launcher isn't watched.</returns>
    public InstallProgress? Started(Game game)
    {
        if (launchers.Find(game.LauncherId)?.InstallMonitor is null)
        {
            _pending.Add(game.Key);
            return null;
        }

        var awaiting = new InstallProgress(InstallPhase.AwaitingConfirmation);
        _awaitingConfirmation[game.Key] = time.GetUtcNow();
        _installs[game.Key] = awaiting;
        return awaiting;
    }

    /// <summary>
    /// Takes the pending games that <paramref name="scanned"/> shows installed off the list.
    /// </summary>
    /// <returns>The games that finished, empty if none did.</returns>
    public IReadOnlyList<Game> CompletePending(IEnumerable<Game> scanned)
    {
        var finished = scanned.Where(g => g.IsInstalled && _pending.Contains(g.Key)).ToList();
        foreach (var game in finished)
        {
            _pending.Remove(game.Key);
        }

        return finished;
    }

    /// <summary>
    /// Asks the watched launchers what their installations are doing. The monitors are read off
    /// the calling thread; a poll that is still running or has no monitors to ask returns null.
    /// A monitor that throws or misses <see cref="PollDeadline"/> keeps its last progress; the others apply.
    /// </summary>
    /// <returns>The installations that ended since the previous poll, in the order reported.</returns>
    public async Task<IReadOnlyList<InstallResult>?> PollAsync()
    {
        var monitors = launchers.Enabled.Select(i => i.InstallMonitor).OfType<IInstallMonitor>().ToList();
        if (_polling || monitors.Count == 0)
        {
            return null;
        }

        _polling = true;
        try
        {
            var answers = await _monitors.AskAsync(monitors.Select(m => (m.LauncherId, (Func<CancellationToken, InstallSnapshot>)m.Poll)));
            var snapshots = monitors.Select(m => (m.LauncherId, Snapshot: answers.GetValueOrDefault(m.LauncherId) ?? LastOf(m.LauncherId))).ToList();
            return Apply(snapshots);
        }
        finally
        {
            _polling = false;
        }
    }

    private InstallSnapshot LastOf(string launcherId) =>
        new(_lastActive.GetValueOrDefault(launcherId) ?? InstallSnapshot.Empty.Active, InstallSnapshot.Empty.Finished);

    private List<InstallResult> Apply(List<(string LauncherId, InstallSnapshot Snapshot)> snapshots)
    {
        var installs = new Dictionary<string, InstallProgress>(StringComparer.Ordinal);
        var results = new List<InstallResult>();
        foreach (var (launcherId, snapshot) in snapshots)
        {
            _lastActive[launcherId] = snapshot.Active;
            foreach (var (id, progress) in snapshot.Active)
            {
                var key = Game.KeyOf(launcherId, id);
                installs[key] = progress;
                _awaitingConfirmation.Remove(key);
            }

            foreach (var (id, outcome) in snapshot.Finished)
            {
                var key = Game.KeyOf(launcherId, id);
                _awaitingConfirmation.Remove(key);
                results.Add(new InstallResult(key, outcome));
            }
        }

        var now = time.GetUtcNow();
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
        return results;
    }
}
