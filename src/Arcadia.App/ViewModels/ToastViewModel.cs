using Arcadia.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>The short message at the bottom of the library; a newer message replaces the current one.</summary>
public partial class ToastViewModel(TimeProvider time) : ViewModelBase
{
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(4);

    private CancellationTokenSource? _expiry;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible))]
    public partial string? Text { get; private set; }

    public bool IsVisible => Text is not null;

    /// <param name="sticky">Stays until the next message, e.g. while a launcher starts.</param>
    public void Show(string message, bool sticky = false)
    {
        StopExpiry();
        Text = message;
        if (sticky)
        {
            return;
        }

        var expiry = new CancellationTokenSource();
        _expiry = expiry;
        HideLaterAsync(expiry).Forget("Meldung ausblenden");
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

    private void StopExpiry()
    {
        _expiry?.Cancel();
        _expiry?.Dispose();
        _expiry = null;
    }
}
