namespace Arcadia.ControllerSupport.Copy;

/// <summary>
/// Tells Arcadia when Steam tries a firmware update through the copy (#295). Steam starts one with a firmware message
/// (<see cref="SteamMessageKind.Firmware"/>, e.g. <c>0x90</c>, which makes the real device restart into its
/// bootloader); the copy drops it, so nothing reaches the hardware, and Steam reports an error after a few tries. One
/// notice per attempt: messages less than <see cref="Quiet"/> apart belong to the same one. Times are monotonic.
/// </summary>
public sealed class FirmwareAttemptWatch(TimeProvider time, Action<string> log)
{
    /// <summary>Steam repeats the message about 5 times within seconds; a later one is a new attempt.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(30);

    private readonly Lock _lock = new();
    private long? _lastTraffic;

    /// <summary>The first firmware message of an attempt; on the copy's thread, a handler must not block.</summary>
    public event Action? Attempted;

    /// <summary>A firmware message the copy dropped; on the copy's thread, must not block.</summary>
    public void Traffic(string what)
    {
        bool first;
        lock (_lock)
        {
            var now = time.GetTimestamp();
            first = _lastTraffic is not { } last || time.GetElapsedTime(last, now) >= Quiet;
            _lastTraffic = now;
        }

        if (first)
        {
            log($"Pan: Steam versucht ein Firmware-Update über die Kopie ({what}); es kommt nicht beim Steam Controller an");
            Attempted?.Invoke();
        }
    }
}
