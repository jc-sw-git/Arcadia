using Arcadia.ControllerSupport;

namespace Arcadia.App.Messages;

/// <summary>Whether an entry was a toast ("Meldung") or a status message ("Status"), #433.</summary>
public enum MessageKind
{
    Toast,
    Status,
}

/// <summary>One line of "Letzte Meldungen" (#433): what reached the screen, when, with its level.</summary>
/// <param name="Time">Local time, shown as hh:mm.</param>
public sealed record RecentMessage(DateTimeOffset Time, HealthSeverity Level, MessageKind Kind, string Text);

/// <summary>
/// The toasts and status changes since Arcadia started, newest first, at most <see cref="Capacity"/> (#433). In memory
/// only: arcadia.log keeps the whole history. Callable from any thread; <see cref="Changed"/> comes on the caller's.
/// </summary>
public sealed class RecentMessages(TimeProvider time)
{
    public const int Capacity = 50;

    private readonly object _gate = new();

    // Oldest first, so dropping the oldest and adding the newest are both cheap.
    private readonly List<RecentMessage> _entries = new(Capacity + 1);

    /// <summary>An entry came or the list was emptied; on the thread that changed it.</summary>
    public event EventHandler? Changed;

    /// <summary>A copy, newest first.</summary>
    public IReadOnlyList<RecentMessage> Entries
    {
        get
        {
            lock (_gate)
            {
                var newestFirst = new RecentMessage[_entries.Count];
                for (var i = 0; i < newestFirst.Length; i++)
                {
                    newestFirst[i] = _entries[_entries.Count - 1 - i];
                }

                return newestFirst;
            }
        }
    }

    public void Add(HealthSeverity level, MessageKind kind, string text)
    {
        lock (_gate)
        {
            _entries.Add(new RecentMessage(time.GetLocalNow(), level, kind, text));
            if (_entries.Count > Capacity)
            {
                // Counted before any filter: a hidden entry drops out like a shown one.
                _entries.RemoveAt(0);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>"Liste leeren": the filter stays, arcadia.log keeps everything.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            if (_entries.Count == 0)
            {
                return;
            }

            _entries.Clear();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
