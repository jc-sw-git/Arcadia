namespace Arcadia.App.Services;

/// <summary>
/// Asks off the UI thread whether a focused game's install folder is still there (#227): on a network share that can't be
/// reached or a sleeping drive the answer can take seconds, and the focus move must not wait for it.
/// </summary>
/// <remarks>
/// Called on the UI thread, and answers there. One check runs at a time and at most one waits behind it, the last one
/// asked for: rapid focus moves don't queue a check per cover, and a check that hangs holds one thread, never more. Only
/// the answer to the last check asked for counts; a check overtaken by a newer one is dropped when it ends.
/// </remarks>
public sealed class InstallFolderCheck
{
    private readonly Func<string, bool> _exists;
    private readonly Action<Action> _post;
    private Request? _waiting;
    private Request? _running;
    private int _lastId;

    /// <param name="exists">Whether the folder exists; may block.</param>
    /// <param name="post">Runs an action on the UI thread.</param>
    public InstallFolderCheck(Func<string, bool> exists, Action<Action> post)
    {
        _exists = exists;
        _post = post;
    }

    /// <summary>A check runs or waits; its answer is still to come.</summary>
    public bool IsChecking => _running is not null;

    /// <summary>Checks <paramref name="folder"/> and calls <paramref name="gone"/> on the UI thread if it isn't there.</summary>
    public void Check(string folder, Action gone)
    {
        _waiting = new Request(folder, gone, ++_lastId);
        if (_running is null)
        {
            StartWaiting();
        }
    }

    private void StartWaiting()
    {
        if (_waiting is not { } request)
        {
            return;
        }

        _waiting = null;
        _running = request;
        Task.Run(() => _exists(request.Folder))
            .ContinueWith(check => _post(() => Finish(request, check)), TaskScheduler.Default)
            .Forget("Installordner prüfen");
    }

    private void Finish(Request request, Task<bool> check)
    {
        _running = null;
        if (check.Exception is { } error)
        {
            AppErrors.Report(error.GetBaseException(), "Installordner prüfen");
        }
        else if (!check.Result && request.Id == _lastId)
        {
            request.Gone();
        }

        StartWaiting();
    }

    private sealed record Request(string Folder, Action Gone, int Id);
}
