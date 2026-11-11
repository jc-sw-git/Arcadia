using Arcadia.App.Input;
using Arcadia.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// "Steam will die Firmware aktualisieren" (#295): Steam tried an update through the copy, which drops it, so nothing
/// reached the real Steam Controller and Steam reports an error. A notice, no wait: "Controller freigeben" and
/// "Arcadia beenden" (held 1 s) act at once, "Schließen" (focused first, also [B]) keeps them taken over. After the
/// release it says how the update goes on, and closes with [A] or [B].
/// </summary>
public sealed partial class FirmwareAttemptViewModel : ViewModelBase, IDisposable
{
    private readonly Action<Action> _post;
    private readonly HoldButtonViewModel[] _order;

    public FirmwareAttemptViewModel(Func<Task> release, Action quit, TimeProvider time, Action<Action> post)
    {
        _post = post;
        ReleaseButton = new HoldButtonViewModel(AppTexts.FirmwareRelease, TimeSpan.FromSeconds(1), () => ReleaseAsync(release).Forget(AppTexts.ErrorContextReleaseForFirmware), time, post);
        QuitButton = new HoldButtonViewModel(AppTexts.QuitArcadia, TimeSpan.FromSeconds(1), quit, time, post);
        CloseButton = new HoldButtonViewModel(AppTexts.Close, TimeSpan.Zero, () => Closed?.Invoke(), time, post) { IsFocused = true };
        _order = [ReleaseButton, QuitButton, CloseButton];
        UpdateTexts();
    }

    public string Title => AppTexts.FirmwareAttemptTitle;

    public string Message => AppTexts.FirmwareAttemptMessage;

    /// <summary>"Schließen" or [B]: the notice goes, the controllers stay taken over.</summary>
    public event Action? Closed;

    public HoldButtonViewModel ReleaseButton { get; }

    public HoldButtonViewModel QuitButton { get; }

    public HoldButtonViewModel CloseButton { get; }

    /// <summary>Released: the notice says how the update goes on.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAsking))]
    public partial bool IsReleased { get; private set; }

    public bool IsAsking => !IsReleased;

    [ObservableProperty]
    public partial string FocusText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Legend { get; private set; } = string.Empty;

    public string ReleasedTitle => AppTexts.FirmwareReleasedTitle;

    public string ReleasedText => AppTexts.FirmwareReleasedText;

    public string ReleasedNext => AppTexts.FirmwareReleasedNext;

    /// <summary>True while the notice stays; false once it is closed.</summary>
    public bool Handle(UiAction action)
    {
        if (IsReleased)
        {
            return action is not (UiAction.Accept or UiAction.Back);
        }

        switch (action)
        {
            case UiAction.Left or UiAction.Up:
                Move(-1);
                break;
            case UiAction.Right or UiAction.Down:
                Move(1);
                break;
            case UiAction.Accept:
                Focused.Press();
                break;
            case UiAction.Back:
                return false;
        }

        return true;
    }

    public void Release(UiAction action)
    {
        if (action == UiAction.Accept)
        {
            foreach (var button in _order)
            {
                button.Release();
            }
        }
    }

    public void Press(HoldButtonViewModel button)
    {
        Focus(button);
        button.Press();
    }

    public void CancelHold()
    {
        foreach (var button in _order)
        {
            button.Cancel();
        }
    }

    private HoldButtonViewModel Focused => _order.First(button => button.IsFocused);

    private void Move(int step)
    {
        var at = Array.IndexOf(_order, Focused);
        Focus(_order[Math.Clamp(at + step, 0, _order.Length - 1)]);
    }

    private void Focus(HoldButtonViewModel button)
    {
        if (!button.IsFocused)
        {
            CancelHold();
        }

        foreach (var other in _order)
        {
            other.IsFocused = other == button;
        }

        UpdateTexts();
    }

    private async Task ReleaseAsync(Func<Task> release)
    {
        await release();
        _post(() => IsReleased = true);
    }

    private void UpdateTexts()
    {
        var focused = Focused;
        FocusText = focused == ReleaseButton ? AppTexts.FirmwareHelpRelease
            : focused == QuitButton ? AppTexts.FirmwareHelpQuit
            : AppTexts.FirmwareHelpClose;
        Legend = focused == CloseButton ? AppTexts.Close : AppTexts.HoldToSelect;
    }

    public void Dispose()
    {
        foreach (var button in _order)
        {
            button.Dispose();
        }
    }
}
