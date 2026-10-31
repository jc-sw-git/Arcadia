using Arcadia.App.Input;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Settings;

/// <summary>
/// A question of a category over the settings page, e.g. "Alle Spiele auf die Vorauswahl setzen?" (#178): [A] answers
/// yes, [B] (or a click beside it, ✕) no. Both buttons name their pad button, so there is no focus to move.
/// </summary>
public sealed partial class SettingsQuestionViewModel(string title, string message, string confirmText, Action confirm, Action cancel)
{
    public string Title => title;

    public string Message => message;

    public string ConfirmText => confirmText;

    public void Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Accept:
                Confirm();
                break;
            case UiAction.Back:
                Cancel();
                break;
        }
    }

    [RelayCommand]
    private void Confirm() => confirm();

    [RelayCommand]
    private void Cancel() => cancel();
}
