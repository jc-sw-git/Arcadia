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
        _expiry?.Cancel();
        _expiry = new CancellationTokenSource();
        var token = _expiry.Token;
        Text = message;
        if (sticky)
        {
            return;
        }

        _ = Task.Delay(Duration, time, token).ContinueWith(
            _ => Text = null,
            token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.FromCurrentSynchronizationContext());
    }
}
