using Arcadia.Core.Net;

namespace Arcadia.Core.Tests.Net;

/// <summary>The connection check for the tests (#683): online or not as the test says; records the addresses each check named.</summary>
internal sealed class FakeConnectionCheck : IConnectionCheck
{
    private readonly List<IReadOnlyCollection<Uri>> _checks = [];

    public bool Online { get; set; } = true;

    /// <summary>The answers of the next checks, before <see cref="Online"/> counts again: e.g. online before a run, offline in it.</summary>
    public Queue<bool> Next { get; } = new();

    /// <summary>Set: the next checks wait for it, e.g. to look at the rows while a check goes.</summary>
    public TaskCompletionSource<bool>? Held { get; set; }

    /// <summary>Each check's addresses, in order.</summary>
    public IReadOnlyList<IReadOnlyCollection<Uri>> Checks
    {
        get
        {
            lock (_checks)
            {
                return [.. _checks];
            }
        }
    }

    public Task<bool> ReachesAnyAsync(Func<IReadOnlyCollection<Uri>> addresses, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        lock (_checks)
        {
            _checks.Add(addresses());
            if (Held is { } held)
            {
                return held.Task.WaitAsync(cancel);
            }

            return Task.FromResult(Next.TryDequeue(out var next) ? next : Online);
        }
    }
}
