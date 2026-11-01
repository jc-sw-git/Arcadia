namespace Arcadia.Input;

/// <summary>
/// The handlers of one event, each called on its own: a handler that throws is reported and the other handlers, and
/// the thread that raises the event, go on (DoD: robust). An event raised every millisecond would repeat the same
/// error a thousand times a second, so each handler is reported once. Adding and removing copy the list, so raising
/// allocates nothing (hot path: <see cref="SdlGamepadSource"/>'s poll thread).
/// </summary>
internal sealed class GuardedHandlers<T>(string context, Action<Exception, string> unexpected, Action<string> log)
{
    private readonly Lock _lock = new();
    private Action<T>[] _handlers = [];

    // The raising thread only: every event of a source is raised on its one poll thread.
    private readonly HashSet<Action<T>> _reported = [];

    public void Add(Action<T>? handler)
    {
        if (handler is null)
        {
            return;
        }

        lock (_lock)
        {
            _handlers = [.. _handlers, handler];
        }
    }

    public void Remove(Action<T>? handler)
    {
        if (handler is null)
        {
            return;
        }

        lock (_lock)
        {
            var index = Array.LastIndexOf(_handlers, handler);
            if (index >= 0)
            {
                _handlers = [.. _handlers[..index], .. _handlers[(index + 1)..]];
            }
        }
    }

    public void Raise(T value)
    {
        foreach (var handler in Volatile.Read(ref _handlers))
        {
            try
            {
                handler(value);
            }
            catch (Exception ex)
            {
                Report(handler, ex);
            }
        }
    }

    private void Report(Action<T> handler, Exception error)
    {
        if (!_reported.Add(handler))
        {
            return;
        }

        try
        {
            unexpected(error, context);
            log($"{context}: weitere Fehler dieses Empfängers werden nicht gemeldet");
        }
        catch (Exception)
        {
            // Reporting must not end the poll thread in turn; the next handler still gets the event.
        }
    }
}
