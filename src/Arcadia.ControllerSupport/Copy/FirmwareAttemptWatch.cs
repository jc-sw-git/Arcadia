namespace Arcadia.ControllerSupport.Copy;

/// <summary>Where a firmware update Steam tries through the copy stands (owner's design, 2026-10-28, PR #286).</summary>
public enum FirmwareAttemptState
{
    /// <summary>No update traffic since the last attempt ended.</summary>
    None,

    /// <summary>Steam sent update traffic within <see cref="FirmwareAttemptWatch.Quiet"/>: it is still trying.</summary>
    Trying,

    /// <summary>Quiet for a while: the countdown of <see cref="FirmwareAttemptWatch.Countdown"/> runs.</summary>
    CountingDown,

    /// <summary>Quiet through the countdown: Steam gave up, the controller may be released or Arcadia ended.</summary>
    GaveUp,
}

/// <summary>
/// Tells whether Steam still tries an update through the copy (#242). Steam's real update path for this controller is
/// unverified, so every message the allow list drops counts as update traffic, but for those Steam sends routinely
/// (<see cref="SteamControllerMessages.IsRoutineDrop"/>). Releasing the real puck while Steam still sends could hand it
/// the rest of an update, so the release waits until Steam has been quiet for <see cref="Quiet"/> plus
/// <see cref="Countdown"/>. Every message that counted is logged by name and count, so a misread can be found later.
/// Times are monotonic.
/// </summary>
public sealed class FirmwareAttemptWatch(TimeProvider time, Action<string> log)
{
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan Countdown = TimeSpan.FromSeconds(25);

    private readonly object _lock = new();
    private readonly Dictionary<string, int> _counted = [];
    private long _lastTraffic;
    private bool _active;

    /// <summary>The first update traffic of an attempt: Arcadia comes to the front with the dialog. Any thread.</summary>
    public event Action? Started;

    public bool IsActive
    {
        get { lock (_lock) return _active; }
    }

    /// <summary>A message the allow list dropped and that isn't routine; on the copy's thread, must not block.</summary>
    public void Traffic(string what)
    {
        bool started;
        lock (_lock)
        {
            _lastTraffic = time.GetTimestamp();
            started = !_active;
            _active = true;
            _counted[what] = _counted.GetValueOrDefault(what) + 1;
        }

        if (started)
        {
            log($"Pan: Firmware-Update über die Kopie vermutet ({what}), der echte Steam Controller bleibt versteckt");
            Started?.Invoke();
        }
    }

    public FirmwareAttemptState State => Now().State;

    /// <summary>The state and, while counting down, the time left.</summary>
    public (FirmwareAttemptState State, TimeSpan Left) Now()
    {
        lock (_lock)
        {
            if (!_active)
            {
                return (FirmwareAttemptState.None, TimeSpan.Zero);
            }

            var quiet = time.GetElapsedTime(_lastTraffic);
            return quiet < Quiet ? (FirmwareAttemptState.Trying, Countdown)
                : quiet < Quiet + Countdown ? (FirmwareAttemptState.CountingDown, Quiet + Countdown - quiet)
                : (FirmwareAttemptState.GaveUp, TimeSpan.Zero);
        }
    }

    /// <summary>The player says Steam aborted the update, before it was quiet long enough: logged with what counted so far.</summary>
    public void PlayerSaysAborted()
    {
        string counted;
        lock (_lock)
        {
            counted = Counted();
        }

        log($"Pan: Spieler sagt, Steam hat das Update abgebrochen (vor Ablauf der Wartezeit); als Update gewertet: {counted}");
    }

    /// <summary>
    /// The attempt ends: <paramref name="how"/> says whether Steam gave up or the player said it did (their call, so they
    /// are never trapped by a misread message). Logs what counted as update traffic.
    /// </summary>
    public void End(string how)
    {
        string counted;
        lock (_lock)
        {
            if (!_active)
            {
                return;
            }

            counted = Counted();
            _counted.Clear();
            _active = false;
        }

        log($"Pan: Firmware-Versuch beendet ({how}); als Update gewertet: {counted}");
    }

    private string Counted() => string.Join(", ", _counted.Select(entry => $"{entry.Key} ×{entry.Value}"));
}
