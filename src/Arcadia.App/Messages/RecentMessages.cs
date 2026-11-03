using Arcadia.ControllerSupport;
using Arcadia.Input;

namespace Arcadia.App.Messages;

/// <summary>Whether an entry was a toast ("Meldung") or a status message ("Status"), #433.</summary>
public enum MessageKind
{
    Toast,
    Status,
}

/// <summary>One line of "Letzte Meldungen" (#433): what reached the screen, when, with its level.</summary>
/// <param name="Id">Stays the same while the entry is listed, so a status message can mark its own entry when it ends.</param>
/// <param name="Time">Local time, shown as hh:mm.</param>
/// <param name="Repeats">How often the error came again without a toast of its own (#399); null: not a repeating error.</param>
public sealed record RecentMessage(long Id, DateTimeOffset Time, HealthSeverity Level, MessageKind Kind, string Text, ErrorStreak? Repeats = null)
{
    /// <summary>
    /// A status message that went (owner): its entry says "Behoben" with the time, instead of a line of its own, so
    /// every status entry without it is still pending. Null: still pending, or a toast.
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary>The level the status message changed to, when it changed instead of going: its newer entry stands above.</summary>
    public HealthSeverity? ChangedTo { get; init; }
}

/// <summary>
/// The toasts and status changes since Arcadia started, newest first, at most <see cref="Capacity"/> (#433). In memory
/// only; every entry is also written to arcadia.log, which keeps the whole history. Callable from any thread;
/// <see cref="Changed"/> comes on the caller's.
/// </summary>
/// <param name="log">arcadia.log; the list's text promises that it holds every message.</param>
public sealed class RecentMessages(TimeProvider time, Action<string> log)
{
    public const int Capacity = 50;

    private readonly object _gate = new();

    // Oldest first, so dropping the oldest and adding the newest are both cheap.
    private readonly List<RecentMessage> _entries = new(Capacity + 1);
    private long _nextId;

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

    /// <returns>The entry's <see cref="RecentMessage.Id"/>.</returns>
    public long Add(HealthSeverity level, MessageKind kind, string text, ErrorStreak? repeats = null)
    {
        log($"{(kind == MessageKind.Toast ? "Meldung" : "Status")} ({level}): {text}");
        long id;
        lock (_gate)
        {
            id = ++_nextId;
            _entries.Add(new RecentMessage(id, time.GetLocalNow(), level, kind, text, repeats));
            if (_entries.Count > Capacity)
            {
                // Counted before any filter: a hidden entry drops out like a shown one.
                _entries.RemoveAt(0);
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return id;
    }

    /// <summary>
    /// A status message went (<paramref name="changedTo"/> null) or changed its level: its entry is marked where it stands.
    /// An entry dropped from the list or cleared meanwhile stays gone; the log has the line either way.
    /// </summary>
    public void End(long id, HealthSeverity? changedTo = null)
    {
        var at = time.GetLocalNow();
        RecentMessage? ended = null;
        lock (_gate)
        {
            var index = _entries.FindIndex(entry => entry.Id == id);
            if (index >= 0)
            {
                ended = _entries[index] = _entries[index] with { ResolvedAt = changedTo is null ? at : null, ChangedTo = changedTo };
            }
        }

        if (ended is not null)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
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
