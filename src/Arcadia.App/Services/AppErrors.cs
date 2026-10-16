namespace Arcadia.App.Services;

/// <summary>
/// The last line of defence for errors no caller expected: they are logged with their stack
/// trace and shown as a toast instead of ending Arcadia. Background work that nobody awaits
/// (timers, fire-and-forget tasks) runs through <see cref="Guard"/> or <see cref="Forget"/>.
/// </summary>
public static class AppErrors
{
    /// <summary>Raised with a user-facing message after an unexpected error was logged.</summary>
    public static event Action<string>? Occurred;

    /// <param name="context">What Arcadia was doing, in German, for the log and the toast.</param>
    public static void Report(Exception exception, string context)
    {
        if (exception is OperationCanceledException)
        {
            return;
        }

        AppLog.Write($"Unerwarteter Fehler ({context}): {exception}");
        try
        {
            Occurred?.Invoke($"Unerwarteter Fehler ({context}) – Details stehen in arcadia.log.");
        }
        catch (Exception ex)
        {
            // Showing the error must not fail in turn; the log line above is what counts.
            AppLog.Write($"Fehler beim Anzeigen eines Fehlers: {ex}");
        }
    }

    /// <summary>Runs <paramref name="action"/> and reports instead of throwing whatever it throws.</summary>
    public static async Task Guard(Func<Task> action, string context)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Report(ex, context);
        }
    }

    /// <summary>For a task nobody awaits: reports its error instead of leaving it unobserved.</summary>
    public static void Forget(this Task task, string context) => _ = Guard(() => task, context);
}
