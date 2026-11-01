namespace Arcadia.Core.Launchers;

/// <summary>
/// Asks the monitors of several launchers at once, each on its own thread pool task with a deadline. Monitors may come
/// from plugins: one that throws or hangs gets no say this round and is logged once, and the others still answer.
/// </summary>
/// <remarks>
/// A poll that outlives the deadline isn't started again while it runs, so a hanging monitor holds one thread, not one
/// per round; its answer, once it comes, is used in the next round, so nothing it reports "since the last call" is lost.
/// Not thread-safe: callers ask one round at a time.
/// </remarks>
/// <param name="unreadable">The log line for a monitor that failed, from its launcher id and the exception.</param>
internal sealed class MonitorPolls<TAnswer>(TimeProvider time, TimeSpan deadline, Func<string, Exception, string> unreadable)
{
    private readonly Dictionary<string, Task<TAnswer>> _overdue = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failing = new(StringComparer.Ordinal);

    public Action<string>? Log { get; set; }

    /// <summary>The answers of the monitors that gave one in time, by launcher id. Never throws.</summary>
    /// <param name="monitors">Each launcher id with the call that asks its monitor; the token ends at the deadline.</param>
    public async Task<IReadOnlyDictionary<string, TAnswer>> AskAsync(IEnumerable<(string LauncherId, Func<CancellationToken, TAnswer> Ask)> monitors)
    {
        var asked = monitors.Select(m => (m.LauncherId, Poll: _overdue.Remove(m.LauncherId, out var overdue) ? overdue : Start(m.Ask))).ToList();

        // The deadline only bounds the wait; a monitor that ignores its token keeps running. Answered in time, the
        // deadline's timer is stopped rather than left to run out every round.
        using (var waiting = new CancellationTokenSource())
        {
            var all = Task.WhenAll(asked.Select(a => a.Poll));
            await Task.WhenAny(all, Task.Delay(deadline, time, waiting.Token)).ConfigureAwait(false);
            await waiting.CancelAsync().ConfigureAwait(false);
        }

        var answers = new Dictionary<string, TAnswer>(StringComparer.Ordinal);
        foreach (var (launcherId, poll) in asked)
        {
            if (!poll.IsCompleted)
            {
                _overdue[launcherId] = poll;
                Failed(launcherId, new TimeoutException($"keine Antwort nach {deadline.TotalSeconds:0} s"));
            }
            else if (poll.IsCompletedSuccessfully)
            {
                answers[launcherId] = poll.Result;
                if (_failing.Remove(launcherId))
                {
                    Log?.Invoke($"Monitor von {launcherId} antwortet wieder.");
                }
            }
            else
            {
                Failed(launcherId, poll.Exception?.InnerException ?? new OperationCanceledException());
            }
        }

        return answers;
    }

    private Task<TAnswer> Start(Func<CancellationToken, TAnswer> ask) => Task.Run(() =>
    {
        using var expiry = new CancellationTokenSource(deadline, time);
        return ask(expiry.Token);
    });

    // Logged once per failure streak: a broken monitor would otherwise write a stack trace every round.
    private void Failed(string launcherId, Exception error)
    {
        if (_failing.Add(launcherId))
        {
            Log?.Invoke(unreadable(launcherId, error));
        }
    }
}
