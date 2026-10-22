using Arcadia.Core.Search;
using Arcadia.Input;

namespace Arcadia.App.Keyboard;

/// <summary>
/// The field that has the focus in the window in front, for the mouse mode's keyboard (#110): characters go out as
/// Unicode, so any layout and dead key types right whatever the Windows layout; the other keys go out as keys.
/// </summary>
public sealed class WindowInFrontTarget(IMouseKeyboardOutput output, Action hide, Action<bool> dock) : IKeyboardTarget
{
    public void Type(char character) => output.Type(character);

    public void Press(BoardKeyKind key, bool withShift)
    {
        OutputKey? sent = key switch
        {
            BoardKeyKind.Backspace => OutputKey.Backspace,
            BoardKeyKind.Tab => OutputKey.Tab,
            BoardKeyKind.Enter => OutputKey.Enter,
            BoardKeyKind.Up => OutputKey.Up,
            BoardKeyKind.Down => OutputKey.Down,
            BoardKeyKind.Left => OutputKey.Left,
            BoardKeyKind.Right => OutputKey.Right,
            _ => null,
        };
        if (sent is not { } pressed)
        {
            return;
        }

        if (withShift)
        {
            output.SetKey(OutputKey.Shift, true);
        }

        output.SetKey(pressed, true);
        output.SetKey(pressed, false);
        if (withShift)
        {
            output.SetKey(OutputKey.Shift, false);
        }
    }

    public void Hide() => hide();

    public void Dock(bool atTop) => dock(atTop);
}
