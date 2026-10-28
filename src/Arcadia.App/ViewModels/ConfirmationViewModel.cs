using Arcadia.App.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// Asks before something that can't be undone from Arcadia. The focus starts on "Abbrechen", so
/// pressing A twice by accident does nothing.
/// </summary>
public sealed partial class ConfirmationViewModel : ViewModelBase
{
    private readonly Action _confirm;

    public ConfirmationViewModel(string title, string message, string confirmLabel, Action confirm)
    {
        Title = title;
        Message = message;
        ConfirmLabel = confirmLabel;
        _confirm = confirm;
    }

    public string Title { get; }

    public string Message { get; }

    public string ConfirmLabel { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCancelFocused))]
    public partial bool IsConfirmFocused { get; private set; }

    public bool IsCancelFocused => !IsConfirmFocused;

    /// <returns>False if the action closes the dialog.</returns>
    public bool Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Left:
                IsConfirmFocused = true;
                return true;
            case UiAction.Right:
                IsConfirmFocused = false;
                return true;
            case UiAction.Accept:
                if (IsConfirmFocused)
                {
                    _confirm();
                }

                return false;
            case UiAction.Back:
                return false;
            default:
                return true;
        }
    }

    /// <summary>Runs the action, e.g. for a mouse click; the caller closes the dialog.</summary>
    public void Confirm() => _confirm();
}
