using Arcadia.App.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The window "Tags" of one game (#495), from its detail page or its context menu: "Neuer Tag" first, then every own tag
/// with a tick where the game has it. A ticks or unticks at once; A on "Neuer Tag" writes a name, made and ticked for the
/// game (an existing name in any case ticks that one, D2); Y deletes a tag from every game after asking; B closes. No
/// renaming here (#43).
/// </summary>
public sealed class TagWindowViewModel : OwnTagsDialogViewModel
{
    private readonly string _gameKey;

    public TagWindowViewModel(GameTileViewModel tile, IOwnTagsHost host)
        : base(host)
    {
        _gameKey = tile.Game.Key;
        Heading = AppTexts.TagsWindowHeading(tile.Title);
        Refresh();
    }

    public string Heading { get; }

    public static string AcceptHint => AppTexts.TagsHintToggle;

    public override void Refresh() => Show();

    protected override void HandleList(UiAction action)
    {
        switch (action)
        {
            case UiAction.Back:
                Back();
                break;
            case UiAction.Up or UiAction.Down:
                Step(action);
                break;
            case UiAction.Accept:
                Activate();
                break;
            case UiAction.Status when !IsNewFocused:
                // On the next tag after the delete, at the end the one before (as the tag list, T495-18).
                AskDelete(FocusedEntry.Name!, () => Show());
                break;
        }
    }

    protected override void Activate()
    {
        if (IsNewFocused)
        {
            Write(AppTexts.TagsNew, string.Empty, typed =>
            {
                if (Host.Tags.Create(typed, _gameKey) is { } name)
                {
                    Show(name);
                }
            });
            return;
        }

        var entry = FocusedEntry;
        Host.Tags.Set(entry.Name!, _gameKey, !entry.IsTicked);
    }

    protected override void Back() => Host.Close(this);

    private void Show(string? focusName = null) =>
        ShowEntries(Host.Tags.All, tag => tag.Games.Contains(_gameKey), _ => null, focusName);
}
