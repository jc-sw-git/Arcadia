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

    /// <summary>The other button; it closes the dialog, as B does.</summary>
    public string CancelLabel { get; init; } = "Abbrechen";

    /// <summary>The confirming button is red: it does something that can't be undone, e.g. uninstall.</summary>
    public bool IsConfirmDanger { get; init; } = true;

    /// <summary>The closing button stands first, e.g. "Verstanden" before "Arcadia jetzt beenden" (#242).</summary>
    public bool IsCancelFirst { get; init; }

    public bool IsConfirmFirst => !IsCancelFirst;

    /// <summary>Numbered steps below the message, under <see cref="StepsTitle"/>; empty shows none.</summary>
    public IReadOnlyList<string> Steps { get; init; } = [];

    public string StepsTitle { get; init; } = string.Empty;

    public bool HasSteps => Steps.Count > 0;

    /// <summary>The steps with their number, for the list.</summary>
    public IReadOnlyList<string> NumberedSteps => [.. Steps.Select((step, i) => $"{i + 1}.  {step}")];

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
                IsConfirmFocused = !IsCancelFirst;
                return true;
            case UiAction.Right:
                IsConfirmFocused = IsCancelFirst;
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
