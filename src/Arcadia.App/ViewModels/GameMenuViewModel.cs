using Arcadia.App.Input;

namespace Arcadia.App.ViewModels;

/// <summary>The context menu of one game (⧉, right click). Up/down skip actions that aren't available.</summary>
public sealed class GameMenuViewModel : ViewModelBase
{
    private int _focus = -1;

    public GameMenuViewModel(GameTileViewModel tile, IReadOnlyList<GameActionViewModel> actions)
    {
        Title = tile.Title.ToUpperInvariant();
        Actions = actions;
        Move(1);
    }

    public string Title { get; }

    /// <summary>
    /// Opened at a cover (a right click, or a hit in the search): the menu stands beside it. Else, opened with ⧉, R or
    /// "Optionen", it stands over the preview's "Optionen" button (owner, 2026-10-21).
    /// </summary>
    public bool AtCover { get; init; }

    public IReadOnlyList<GameActionViewModel> Actions { get; }

    /// <returns>False if the action closes the menu.</returns>
    public bool Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Up:
                Move(-1);
                return true;
            case UiAction.Down:
                Move(1);
                return true;
            case UiAction.Accept when _focus >= 0:
                var chosen = Actions[_focus];
                chosen.Invoke();
                return chosen.KeepsMenuOpen;
            case UiAction.Back:
            case UiAction.Options:
                return false;
            default:
                return true;
        }
    }

    private void Move(int delta)
    {
        for (var i = _focus + delta; i >= 0 && i < Actions.Count; i += delta)
        {
            if (Actions[i].IsEnabled)
            {
                if (_focus >= 0)
                {
                    Actions[_focus].IsFocused = false;
                }

                _focus = i;
                Actions[i].IsFocused = true;
                return;
            }
        }
    }
}
