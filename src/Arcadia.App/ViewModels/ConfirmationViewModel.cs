using Arcadia.App.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// Asks before something that can't be undone from Arcadia. The focus starts on "Abbrechen", so
/// pressing A twice by accident does nothing.
/// </summary>
public sealed partial class ConfirmationViewModel : ViewModelBase
{
    private readonly Action _confirm;
    private readonly Action? _close;

    /// <param name="close">Closes the dialog after a click on one of its buttons; the main window's own dialog closes itself.</param>
    public ConfirmationViewModel(string title, string message, string confirmLabel, Action confirm, Action? close = null)
    {
        Title = title;
        Message = message;
        ConfirmLabel = confirmLabel;
        _confirm = confirm;
        _close = close;
    }

    public string Title { get; }

    public string Message { get; }

    public string ConfirmLabel { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCancelFocused))]
    public partial bool IsConfirmFocused { get; private set; }

    public bool IsCancelFocused => !IsConfirmFocused;

    /// <summary>
    /// Moves the focus, or says how the press answered the question without running the action, so the caller can close
    /// the dialog before <see cref="Confirm"/>: the confirmed action may open the next one.
    /// </summary>
    public ConfirmationAnswer Answer(UiAction action)
    {
        switch (action)
        {
            case UiAction.Left:
                IsConfirmFocused = true;
                return ConfirmationAnswer.Open;
            case UiAction.Right:
                IsConfirmFocused = false;
                return ConfirmationAnswer.Open;
            case UiAction.Accept:
                return IsConfirmFocused ? ConfirmationAnswer.Confirmed : ConfirmationAnswer.Cancelled;
            case UiAction.Back:
                return ConfirmationAnswer.Cancelled;
            default:
                return ConfirmationAnswer.Open;
        }
    }

    /// <summary>Runs the action, after A (<see cref="Answer"/>) or a click; the caller has closed the dialog first.</summary>
    public void Confirm() => _confirm();

    /// <summary>
    /// A click on the confirming button of a dialog that closes itself (a settings category's). Closed first, as after A:
    /// the action may open the next dialog.
    /// </summary>
    [RelayCommand]
    private void ConfirmAndClose()
    {
        _close?.Invoke();
        _confirm();
    }

    /// <summary>A click on "Abbrechen" or beside the dialog.</summary>
    [RelayCommand]
    private void Cancel() => _close?.Invoke();
}

/// <summary>How a press answered a <see cref="ConfirmationViewModel"/>.</summary>
public enum ConfirmationAnswer
{
    /// <summary>Not answered: the dialog stays open.</summary>
    Open,

    /// <summary>A on the confirming button.</summary>
    Confirmed,

    /// <summary>B, or A on "Abbrechen".</summary>
    Cancelled,
}
