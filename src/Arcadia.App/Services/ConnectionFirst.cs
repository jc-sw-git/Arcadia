using Arcadia.ControllerSupport;

namespace Arcadia.App.Services;

/// <summary>
/// A search or load the player starts checks the connection first (#683): without one a warning toast says so and nothing
/// starts, so no row runs into one timeout after another. The check takes at most a few seconds, off the UI thread. One
/// instance serves a tab's rows: while one row's check goes, that row shows that it works (A on it again stops the check)
/// and the tab's other rows wait, as they wait for its search.
/// </summary>
/// <param name="isOnline">The service's check of the hosts it would ask.</param>
/// <param name="toast">Shows the warning.</param>
public sealed class ConnectionFirst(Func<CancellationToken, Task<bool>> isOnline, Action<HealthSeverity, string> toast)
{
    private CancellationTokenSource? _checking;

    /// <summary>The key of the row whose check goes on; null while none goes.</summary>
    public string? Checking { get; private set; }

    /// <summary>A check began or ended: the rows show their state anew. UI thread.</summary>
    public event EventHandler? Changed;

    /// <summary>Checks, then runs <paramref name="start"/> with a connection; UI thread. Nothing while a check goes.</summary>
    /// <param name="row">The row that starts it, for <see cref="Checking"/>.</param>
    public void Start(string row, Action start)
    {
        if (_checking is not null)
        {
            return;
        }

        var checking = _checking = new CancellationTokenSource();
        Checking = row;
        Changed?.Invoke(this, EventArgs.Empty);
        CheckAsync(start, checking).Forget(AppTexts.ErrorContextConnectionCheck);
    }

    /// <summary>A again on <paramref name="row"/> while its check goes: nothing starts. False when its check doesn't go.</summary>
    public bool Cancel(string row)
    {
        if (_checking is not { } checking || Checking != row)
        {
            return false;
        }

        End();
        checking.Cancel();
        return true;
    }

    private async Task CheckAsync(Action start, CancellationTokenSource checking)
    {
        try
        {
            var online = await isOnline(checking.Token);
            if (!ReferenceEquals(_checking, checking))
            {
                return;
            }

            End();
            if (online)
            {
                start();
            }
            else
            {
                toast(HealthSeverity.Warning, AppTexts.NoInternetConnection);
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (checking.IsCancellationRequested)
        {
            // Stopped by the player: nothing starts.
        }
        finally
        {
            if (ReferenceEquals(_checking, checking))
            {
                End();
            }

            checking.Dispose();
        }
    }

    private void End()
    {
        _checking = null;
        Checking = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
