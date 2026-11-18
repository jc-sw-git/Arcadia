using Arcadia.ControllerSupport;

namespace Arcadia.App.Services;

/// <summary>
/// A search or load the player starts checks the connection first (#683): without one a warning toast says so and nothing
/// starts, so no row runs into one timeout after another. The check takes at most a few seconds, off the UI thread; while
/// it goes the row shows that it works, and A on it again stops the check, as it would stop the search.
/// </summary>
/// <param name="isOnline">The service's check of the hosts it would ask.</param>
/// <param name="toast">Shows the warning.</param>
/// <param name="changed">The check began or ended: the row shows its state anew. UI thread.</param>
public sealed class ConnectionFirst(Func<CancellationToken, Task<bool>> isOnline, Action<HealthSeverity, string> toast, Action changed)
{
    private CancellationTokenSource? _checking;

    /// <summary>The check goes on; the row reads as working.</summary>
    public bool IsChecking => _checking is not null;

    /// <summary>Checks, then runs <paramref name="start"/> with a connection; UI thread. Nothing while a check goes.</summary>
    public void Start(Action start)
    {
        if (_checking is not null)
        {
            return;
        }

        var checking = _checking = new CancellationTokenSource();
        changed();
        CheckAsync(start, checking).Forget(AppTexts.ErrorContextConnectionCheck);
    }

    /// <summary>A again while the check goes: nothing starts. False when none goes.</summary>
    public bool Cancel()
    {
        if (_checking is not { } checking)
        {
            return false;
        }

        _checking = null;
        checking.Cancel();
        changed();
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

            _checking = null;
            if (online)
            {
                start();
            }
            else
            {
                toast(HealthSeverity.Warning, AppTexts.NoInternetConnection);
            }

            changed();
        }
        catch (OperationCanceledException) when (checking.IsCancellationRequested)
        {
            // Stopped by the player: nothing starts.
        }
        finally
        {
            if (ReferenceEquals(_checking, checking))
            {
                _checking = null;
                changed();
            }

            checking.Dispose();
        }
    }
}
