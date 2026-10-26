namespace Arcadia.Pan;

/// <summary>
/// Milliseconds that don't advance while the PC sleeps (<c>QueryUnbiasedInterruptTime</c> on Windows), so a night in
/// standby never counts as three seconds of silence from Arcadia (#243, review point 6).
/// </summary>
public interface IPanClock
{
    long UnbiasedMilliseconds { get; }
}
