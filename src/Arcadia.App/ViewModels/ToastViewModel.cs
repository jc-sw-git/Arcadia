using Arcadia.App.Messages;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The short message at the bottom of the library; a newer message replaces the current one. Each one is also kept in
/// <see cref="RecentMessages"/> for "Letzte Meldungen" (#433).
/// </summary>
public partial class ToastViewModel(TimeProvider time, RecentMessages history) : ViewModelBase, IDisposable
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(4);

    private CancellationTokenSource? _expiry;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    public partial string? Text { get; private set; }

    public bool IsVisible => Text is not null;

    /// <summary>Where every message is kept, also one shown elsewhere because Arcadia is behind a game.</summary>
    public RecentMessages History => history;

    /// <param name="level">No default (#433): every caller says whether it is information, a warning or an error.</param>
    /// <param name="sticky">Stays until the next message, e.g. while a launcher starts.</param>
    /// <param name="repeats">How often an unexpected error came again since this toast (#399), shown as "×N" in its entry.</param>
    public void Show(HealthSeverity level, string message, bool sticky = false, Arcadia.Input.ErrorStreak? repeats = null)
    {
        history.Add(level, MessageKind.Toast, message, repeats);
        StopExpiry();
        Text = message;
        if (sticky)
        {
            return;
        }

        var expiry = new CancellationTokenSource();
        _expiry = expiry;
        HideLaterAsync(expiry).Forget(AppTexts.ErrorContextHideToast);
    }

    /// <summary>Hides this message after <see cref="Duration"/>, unless a newer one replaced it first.</summary>
    private async Task HideLaterAsync(CancellationTokenSource expiry)
    {
        try
        {
            // Back on the caller's (UI) thread afterwards: Text is bound.
            await Task.Delay(Duration, time, expiry.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ReferenceEquals(_expiry, expiry))
        {
            StopExpiry();
            Text = null;
        }
    }

    /// <summary>The window closes: a pending hide is cancelled.</summary>
    public void Dispose()
    {
        StopExpiry();
        GC.SuppressFinalize(this);
    }

    private void StopExpiry()
    {
        _expiry?.Cancel();
        _expiry?.Dispose();
        _expiry = null;
    }
}
