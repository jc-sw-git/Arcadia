using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.Core.Navigation;
using Arcadia.Core.UserData;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>What the tag window and the tag list (#495) need from the window.</summary>
public interface IOwnTagsHost
{
    OwnTags Tags { get; }

    /// <summary>The games in the library now, by key, with their shown titles: counts and lists leave absent games out (D5).</summary>
    IReadOnlyDictionary<string, Game> GamesInLibrary();

    /// <summary>The name of the game's launcher, as its tab shows it.</summary>
    string LauncherNameOf(Game game);

    OnScreenKeyboardViewModel TakeKeyboard(IKeyboardTarget editor);

    void GiveBackKeyboard(IKeyboardTarget editor);

    /// <summary>A name is being written or no longer: the window's keys go to it.</summary>
    void EditorChanged();

    /// <summary>B or the ✕ on the dialog's first level.</summary>
    void Close(OwnTagsDialogViewModel dialog);

    /// <summary>A on a game of a tag: its detail page, turning through <paramref name="games"/> only (D8).</summary>
    void ShowGame(TagListViewModel list, IReadOnlyList<Game> games, int index);
}

/// <summary>
/// What the tag window and the tag list (#495) share: the list of tags with "Neuer Tag" first, a name written with the
/// settings' on-screen keyboard (at most <see cref="UserDataStore.MaxTagLength"/> characters), and the question before a
/// tag is deleted or merged (the confirmation dialog, focus on "Abbrechen", B = no). Changes hold at once; the window
/// calls <see cref="Refresh"/> when the tags changed, whoever changed them.
/// </summary>
public abstract partial class OwnTagsDialogViewModel : ViewModelBase, IDisposable
{
    protected OwnTagsDialogViewModel(IOwnTagsHost host)
    {
        Host = host;
    }

    protected IOwnTagsHost Host { get; }

    /// <summary>"Neuer Tag" first, then every tag alphabetically (D3).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<TagEntryViewModel> Entries { get; private set; } = [];

    /// <summary>The name being written, with the on-screen keyboard; null while none is.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    public partial TextEditorViewModel? Editor { get; private set; }

    public bool IsEditing => Editor is not null;

    /// <summary>"Neuer Tag" or "Tag umbenennen" above the field.</summary>
    [ObservableProperty]
    public partial string EditorHeading { get; private set; } = string.Empty;

    /// <summary>The question before a delete or a merge; null while none is asked.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAsking))]
    public partial ConfirmationViewModel? Question { get; private set; }

    public bool IsAsking => Question is not null;

    public static string NameNote => AppTexts.TagsNameNote;

    /// <summary>The focus moved in a list: the view keeps the focused row in sight.</summary>
    public event EventHandler? FocusMoved;

    protected int Focus { get; set; }

    protected TagEntryViewModel FocusedEntry => Entries[Focus];

    /// <summary>The focus is on "Neuer Tag": Y (and X in the list) do nothing there.</summary>
    public bool IsNewFocused => Focus == 0;

    /// <summary>A press: the name being written takes it, else the question, else the list.</summary>
    public void Handle(UiAction action)
    {
        if (Editor is { } editor)
        {
            editor.Handle(action);
            return;
        }

        if (Question is { } question)
        {
            var answer = question.Answer(action);
            if (answer != ConfirmationAnswer.Open)
            {
                Question = null;
                if (answer == ConfirmationAnswer.Confirmed)
                {
                    question.Confirm();
                }
            }

            return;
        }

        HandleList(action);
    }

    public void Release(UiAction action) => Editor?.Release(action);

    public void CancelHeldButtons() => Editor?.CancelHeldButtons();

    /// <summary>The tags or the library changed: the list shows them, the focus stays on its tag where it still is.</summary>
    public abstract void Refresh();

    public void Dispose() => CloseEditor();

    protected abstract void HandleList(UiAction action);

    /// <summary>
    /// The list's entries anew: in place while the same tags are listed, so ticking one of 200 rebuilds nothing; the focus
    /// on the tag it was on, else at the same place.
    /// </summary>
    protected void ShowEntries(IReadOnlyList<OwnTag> tags, Func<OwnTag, bool> isTicked, Func<OwnTag, string?> countText, string? focusName = null)
    {
        focusName ??= Focus > 0 && Focus < Entries.Count ? FocusedEntry.Name : null;
        if (Entries.Count == tags.Count + 1 && Entries.Skip(1).Zip(tags).All(pair => pair.First.Name == pair.Second.Name))
        {
            foreach (var (entry, tag) in Entries.Skip(1).Zip(tags))
            {
                entry.IsTicked = isTicked(tag);
                entry.CountText = countText(tag);
            }
        }
        else
        {
            Entries = [new TagEntryViewModel(null, false, null, Click), .. tags.Select(tag => new TagEntryViewModel(tag.Name, isTicked(tag), countText(tag), Click))];
        }

        var named = focusName is null ? -1 : Entries.ToList().FindIndex(entry => entry.Name is { } name && UserDataStore.TagNames.Equals(name, focusName));
        FocusAt(named >= 0 ? named : Math.Min(Focus, Entries.Count - 1));
    }

    protected void FocusAt(int index)
    {
        Focus = Math.Clamp(index, 0, Entries.Count - 1);
        for (var e = 0; e < Entries.Count; e++)
        {
            Entries[e].IsFocused = e == Focus;
        }

        OnFocusChanged();
        RaiseFocusMoved();
    }

    protected void RaiseFocusMoved() => FocusMoved?.Invoke(this, EventArgs.Empty);

    /// <summary>The hints follow the focus.</summary>
    protected virtual void OnFocusChanged() => OnPropertyChanged(nameof(IsNewFocused));

    /// <summary>▲ ▼ go round at the ends; held, they stop there (#476).</summary>
    protected void Step(UiAction action) => FocusAt(ListFocus.Cycle(Focus, action == UiAction.Up ? -1 : 1, Entries.Count, HeldPress.IsRepeat));

    /// <summary>A click on a row: the focus goes there and it does what A does.</summary>
    protected abstract void Activate();

    /// <summary>
    /// The settings' keyboard over the dialog for a name: ↵, Enter or ≡ hand it to <paramref name="take"/>, B or Esc
    /// drop it. Longer names can't be typed (D1); at the limit a notice below the field says so (#533).
    /// </summary>
    protected void Write(string heading, string text, Action<string> take)
    {
        CloseEditor();
        EditorHeading = heading;
        Editor = new TextEditorViewModel(
            text,
            // The editor types no more than the limit and says when it is reached; nothing typed is too long.
            _ => null,
            typed =>
            {
                take(typed);
                return true;
            },
            CloseEditor,
            Host.TakeKeyboard,
            Host.GiveBackKeyboard,
            maxLength: UserDataStore.MaxTagLength);
        Host.EditorChanged();
    }

    /// <summary>Y on a tag: asks with the number of games in the library that lose it (D6); yes deletes it from every game.</summary>
    protected void AskDelete(string name, Action deleted)
    {
        if (Host.Tags.Find(name) is not { } tag)
        {
            return;
        }

        var library = Host.GamesInLibrary();
        var losing = tag.Games.Count(library.ContainsKey);
        Ask(
            AppTexts.TagsDeleteQuestion(tag.Name),
            losing switch { 0 => AppTexts.TagsDeleteGamesNone, 1 => AppTexts.TagsDeleteGamesOne, _ => AppTexts.TagsDeleteGamesMany(Formatting.Count(losing)) },
            AppTexts.TagsDeleteConfirm,
            () =>
            {
                Host.Tags.Delete(tag.Name);
                deleted();
            });
    }

    protected void Ask(string title, string message, string confirmLabel, Action confirm) =>
        Question = new ConfirmationViewModel(title, message, confirmLabel, confirm, () => Question = null);

    /// <summary>B: one level back, or the dialog closes.</summary>
    protected abstract void Back();

    /// <summary>The ✕, B's hint or a click beside the dialog: as B.</summary>
    [RelayCommand]
    private void GoBack() => Back();

    /// <summary>A click on Y's hint.</summary>
    [RelayCommand]
    private void Delete() => HandleList(UiAction.Status);

    /// <summary>A click on "Übernehmen" beside the field.</summary>
    [RelayCommand]
    private void TakeName() => Editor?.Take();

    /// <summary>A click beside the field: the name is dropped, as with B.</summary>
    [RelayCommand]
    private void DropName() => Editor?.Drop();

    private void Click(TagEntryViewModel entry)
    {
        if (Editor is not null || Question is not null || Entries.ToList().IndexOf(entry) is not (var index and >= 0))
        {
            return;
        }

        FocusAt(index);
        Activate();
    }

    private void CloseEditor()
    {
        if (Editor is not { } editor)
        {
            return;
        }

        Editor = null;
        editor.Dispose();
        Host.EditorChanged();
    }
}

/// <summary>One row of the tag window or the tag list: "Neuer Tag" (no name) or a tag, ticked for the game or with its count.</summary>
public sealed partial class TagEntryViewModel : ViewModelBase
{
    private readonly Action<TagEntryViewModel> _click;

    public TagEntryViewModel(string? name, bool isTicked, string? countText, Action<TagEntryViewModel> click)
    {
        Name = name;
        IsTicked = isTicked;
        CountText = countText;
        _click = click;
    }

    /// <summary>The tag's name; null for "Neuer Tag".</summary>
    public string? Name { get; }

    public bool IsNew => Name is null;

    public bool IsTag => Name is not null;

    public string Text => Name ?? AppTexts.TagsNew;

    [ObservableProperty]
    public partial bool IsTicked { get; set; }

    /// <summary>"8 Spiele" in the tag list; null in the tag window.</summary>
    [ObservableProperty]
    public partial string? CountText { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    [RelayCommand]
    private void Click() => _click(this);
}
