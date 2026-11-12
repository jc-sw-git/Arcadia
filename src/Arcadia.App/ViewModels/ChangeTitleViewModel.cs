using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Settings;
using Arcadia.Core.Library;
using Arcadia.Core.UserData;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// "Titel ändern" (#496): the shown title in a field with the on-screen keyboard over the detail page. ↵, Enter or ≡ take
/// it, B, Esc or "Ausblenden" keep the old one; an empty title or the automatic one clears the own title.
/// </summary>
public sealed partial class ChangeTitleViewModel : ViewModelBase, IDisposable
{
    /// <param name="shownTitle">What the field starts with: the own title, else the automatic one.</param>
    /// <param name="automaticTitle">The title the game shows without an own one.</param>
    /// <param name="take">Keeps the typed title; false when it couldn't, and the dialog stays open.</param>
    /// <param name="close">The dialog is done.</param>
    public ChangeTitleViewModel(
        string shownTitle,
        string automaticTitle,
        Func<string, bool> take,
        Action<ChangeTitleViewModel> close,
        Func<IKeyboardTarget, OnScreenKeyboardViewModel> takeKeyboard,
        Action<IKeyboardTarget> giveBackKeyboard)
    {
        Note = AppTexts.ChangeTitleNote(automaticTitle);
        Editor = new TextEditorViewModel(
            shownTitle,
            text => GameTitles.IsTooLong(text) ? AppTexts.ChangeTitleTooLong(UserDataStore.MaxTitleLength) : null,
            take,
            () => close(this),
            takeKeyboard,
            giveBackKeyboard);
    }

    public static string Heading => AppTexts.ActionChangeTitle;

    public TextEditorViewModel Editor { get; }

    /// <summary>How the automatic title comes back.</summary>
    public string Note { get; }

    /// <summary>Every button acts on the field and its keyboard.</summary>
    public void Handle(UiAction action) => Editor.Handle(action);

    public void Release(UiAction action) => Editor.Release(action);

    public void CancelHeldButtons() => Editor.CancelHeldButtons();

    /// <summary>A click on "Übernehmen".</summary>
    [RelayCommand]
    private void Take() => Editor.Take();

    /// <summary>A click beside the dialog or on its ✕: the old title stays.</summary>
    [RelayCommand]
    private void Close() => Editor.Drop();

    public void Dispose() => Editor.Dispose();
}
