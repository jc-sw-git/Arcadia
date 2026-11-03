using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that also tells which waits have begun. Advancing the clock before code under test
/// has started its wait skips that wait's end; a test first waits for <see cref="HasTimer"/>, not for a fixed time.
/// </summary>
public sealed class TimerWatchingTimeProvider : FakeTimeProvider
{
    private readonly Lock _lock = new();
    private readonly List<TimeSpan> _dueTimes = [];

    /// <summary>Whether a timer was ever created to fire after <paramref name="dueTime"/>.</summary>
    public bool HasTimer(TimeSpan dueTime)
    {
        lock (_lock)
        {
            return _dueTimes.Contains(dueTime);
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_lock)
        {
            _dueTimes.Add(dueTime);
        }

        return base.CreateTimer(callback, state, dueTime, period);
    }
}
