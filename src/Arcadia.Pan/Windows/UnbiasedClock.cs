using System.Runtime.Versioning;

namespace Arcadia.Pan.Windows;

/// <summary><c>QueryUnbiasedInterruptTime</c>: 100 ns units that stand still while the PC sleeps.</summary>
[SupportedOSPlatform("windows")]
public sealed class UnbiasedClock : IPanClock
{
    public long UnbiasedMilliseconds => Native.QueryUnbiasedInterruptTime(out var time) ? (long)(time / 10_000) : Environment.TickCount64;
}
