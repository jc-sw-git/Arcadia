namespace Arcadia.ControllerSupport.Copy;

/// <summary>
/// Whether a window of Steam itself is in front, and when it last was (#242): while the real puck is visible, Steam in
/// front may start a firmware update on it, so no hide (a puck restart) may follow. Asked on every gamepad poll and by
/// the emulation's tick, so the last time is recorded wherever it is seen, not only every 2 s. Times are monotonic
/// (<see cref="TimeProvider.GetTimestamp"/>): a clock change can't shorten a wait.
/// </summary>
public sealed class SteamFrontWatch(Func<bool> steamInFront, TimeProvider time)
{
    private long _lastSeen = long.MinValue;

    /// <summary>Steam is in front now; records it. Never throws (an error counts as in front, the safe side).</summary>
    public bool Now()
    {
        bool inFront;
        try
        {
            inFront = steamInFront();
        }
        catch (Exception)
        {
            inFront = true;
        }

        if (inFront)
        {
            Interlocked.Exchange(ref _lastSeen, time.GetTimestamp());
        }

        return inFront;
    }

    /// <summary>The timestamp Steam was last seen in front, or null if never since Arcadia started.</summary>
    public long? LastSeen => Interlocked.Read(ref _lastSeen) is var seen && seen != long.MinValue ? seen : null;
}
