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

    /// <summary>A click on the confirming button of a dialog that closes itself (a settings category's).</summary>
    [RelayCommand]
    private void ConfirmAndClose()
    {
        _confirm();
        _close?.Invoke();
    }

    /// <summary>A click on "Abbrechen" or beside the dialog.</summary>
    [RelayCommand]
    private void Cancel() => _close?.Invoke();
}
