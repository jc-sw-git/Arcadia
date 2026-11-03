namespace Arcadia.Input;

/// <summary>
/// The handlers of one event, each called on its own: a handler that throws is reported and the other handlers, and
/// the thread that raises the event, go on (DoD: robust). An event raised every millisecond would repeat the same
/// error a thousand times a second, so each handler is a source of <see cref="RepeatedErrors{TKey}"/> (#399). Adding
/// and removing copy the list, so raising allocates nothing (hot path: <see cref="SdlGamepadSource"/>'s poll thread).
/// </summary>
internal sealed class GuardedHandlers<T>(string context, Action<Exception, string, ErrorStreak> unexpected, Action<string> log, TimeProvider time)
{
    private readonly Lock _lock = new();
    private Action<T>[] _handlers = [];
    private readonly RepeatedErrors<Action<T>> _errors = new(time, log, NameOf);

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

    private void Report(Action<T> handler, Exception error) => _errors.Report(handler, context, error, unexpected);

    /// <summary>Which handler repeats its error, for the log: two of one event share the context.</summary>
    private static string NameOf(Action<T> handler)
    {
        // A lambda's method lives in a compiler-made class ("<>c") inside the class that wrote it.
        var type = handler.Method.DeclaringType;
        while (type is { DeclaringType: not null } && type.Name.StartsWith('<'))
        {
            type = type.DeclaringType;
        }

        return $"{type?.Name}.{handler.Method.Name}";
    }
}
