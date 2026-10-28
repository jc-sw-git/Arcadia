using Arcadia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// A button that acts only when held, for answers that must not happen by a stray press (#242, the firmware dialogs):
/// it fills over its hold time and acts once it is full; letting go earlier does nothing. Only a press made while it
/// is shown counts, as a press arrives only when a button goes down.
/// </summary>
public sealed partial class HoldButtonViewModel : ViewModelBase, IDisposable
{
    private readonly HoldGesture _hold;

    public HoldButtonViewModel(string label, TimeSpan holdTime, Action held, TimeProvider time, Action<Action> post)
    {
        Label = label;
        HoldTime = holdTime;
        _hold = new HoldGesture(TimeSpan.Zero, holdTime, time, post);
        _hold.FeedbackChanged += holding => IsHolding = holding;
        _hold.Held += () =>
        {
            if (IsEnabled)
            {
                held();
            }
        };
    }

    public string Label { get; }

    public TimeSpan HoldTime { get; }

    /// <summary>Picks the fill's duration in <c>Theme.axaml</c>.</summary>
    public bool HoldsThreeSeconds => HoldTime >= TimeSpan.FromSeconds(3);

    public bool HoldsOneSecond => !HoldsThreeSeconds;

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>Shown greyed while it can't act yet (e.g. Steam still tries the update).</summary>
    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    /// <summary>False hides it, e.g. "Steam hat das Update abgebrochen" once Steam gave up.</summary>
    [ObservableProperty]
    public partial bool IsShown { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Shown))]
    public partial bool IsHolding { get; private set; }

    public string Shown => IsHolding ? "Halten …" : Label;

    public void Press()
    {
        if (IsEnabled)
        {
            _hold.Press();
        }
    }

    public void Release() => _hold.Release();

    public void Cancel() => _hold.Cancel();

    public void Dispose() => _hold.Dispose();
}
