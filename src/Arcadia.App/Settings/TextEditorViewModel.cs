using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.ViewModels;
using Arcadia.Core.Search;
using Arcadia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.Settings;

/// <summary>
/// A text of the settings being written, e.g. a launcher's name (#137), with the shared on-screen keyboard (without ↹ and
/// the edge key). It takes every input until it closes: ≡, Enter or ↵ take the text, B, Esc or "Ausblenden" drop it. The
/// text is checked while it is typed; taken while it has a problem, the old value stays and the problem is said.
/// </summary>
public sealed partial class TextEditorViewModel : ViewModelBase, IKeyboardTarget, IDisposable
{
    private readonly Func<string, string?> _problemOf;
    private readonly Func<string, bool> _take;
    private readonly Action _close;
    private TextWithCaret _text;

    /// <param name="problemOf">Why a text can't be taken, or null if it can.</param>
    /// <param name="take">Takes a text without problem; false if it couldn't, and the editor stays open.</param>
    /// <param name="close">The editor is done, the text taken or dropped.</param>
    public TextEditorViewModel(
        string text,
        Func<string, string?> problemOf,
        Func<string, bool> take,
        Action close,
        Func<IKeyboardTarget, OnScreenKeyboardViewModel> createKeyboard)
    {
        _text = TextWithCaret.AtEnd(text);
        _problemOf = problemOf;
        _take = take;
        _close = close;
        Keyboard = createKeyboard(this);
        Keyboard.Configure(key => key.Kind is BoardKeyKind.Tab or BoardKeyKind.Dock ? BoardKeyState.Hidden : BoardKeyState.Shown);
        Problem = problemOf(text);
    }

    public OnScreenKeyboardViewModel Keyboard { get; }

    public string Text => _text.Text;

    public string TextBeforeCaret => _text.BeforeCaret;

    public string TextAfterCaret => _text.AfterCaret;

    /// <summary>Why the text as it stands can't be taken; null if it can.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; private set; }

    public bool HasProblem => Problem is not null;

    /// <summary>Set when it was taken with a problem: the old value stayed, this says why.</summary>
    public string? Rejection { get; private set; }

    /// <summary>A controller button: ≡ takes the text, the others act on the keyboard as in the search.</summary>
    public void Handle(UiAction action)
    {
        if (action == UiAction.Settings && !Keyboard.LayoutMenu.IsOpen)
        {
            Take();
        }
        else if (KeyboardButtonOf(action) is { } button)
        {
            Keyboard.ButtonDown(button);
        }
    }

    /// <summary>A button was let go: ends a held erase, shift or AltGr.</summary>
    public void Release(UiAction action)
    {
        if (KeyboardButtonOf(action) is { } button)
        {
            Keyboard.ButtonUp(button);
        }
    }

    /// <summary>
    /// A real keyboard's key that types nothing: Enter takes the text, Esc drops it (closing the layout menu first),
    /// backspace erases, the arrows, Home and End move the caret. False for every other key; its text comes by
    /// <see cref="Type(string)"/>.
    /// </summary>
    public bool HandleKey(Avalonia.Input.Key key)
    {
        switch (key)
        {
            case Avalonia.Input.Key.Enter:
                Take();
                return true;
            case Avalonia.Input.Key.Escape:
                Keyboard.ButtonDown(KeyboardButton.Hide);
                return true;
            case Avalonia.Input.Key.Back:
                Erase();
                return true;
            case Avalonia.Input.Key.Left:
                MoveCaret(_text.Left());
                return true;
            case Avalonia.Input.Key.Right:
                MoveCaret(_text.Right());
                return true;
            case Avalonia.Input.Key.Home or Avalonia.Input.Key.Up:
                MoveCaret(_text.Home());
                return true;
            case Avalonia.Input.Key.End or Avalonia.Input.Key.Down:
                MoveCaret(_text.End());
                return true;
            default:
                return false;
        }
    }

    /// <summary>Text from a real keyboard or the on-screen one, at the caret; control characters are dropped.</summary>
    public void Type(string text)
    {
        var typed = new string(text.Where(c => !char.IsControl(c)).ToArray());
        if (typed.Length > 0)
        {
            SetText(_text.Insert(typed));
        }
    }

    /// <summary>Drops held buttons, e.g. when the window loses focus and their release may never arrive.</summary>
    public void CancelHeldButtons() => Keyboard.CancelPresses();

    /// <summary>≡, Enter or ↵: a text without problem is taken; one with a problem leaves the old value and says why.</summary>
    public void Take()
    {
        Rejection = Problem;
        if (Problem is null && !_take(Text))
        {
            return;
        }

        Close();
    }

    /// <summary>B, Esc or "Ausblenden": the old value stays.</summary>
    public void Drop() => Close();

    public void Dispose() => Keyboard.Dispose();

    void IKeyboardTarget.Type(char character) => Type(character.ToString());

    void IKeyboardTarget.Press(BoardKeyKind key, bool withShift)
    {
        switch (key)
        {
            case BoardKeyKind.Backspace:
                Erase();
                break;
            case BoardKeyKind.Enter:
                Take();
                break;
            case BoardKeyKind.Left:
                MoveCaret(_text.Left());
                break;
            case BoardKeyKind.Right:
                MoveCaret(_text.Right());
                break;
            case BoardKeyKind.Up:
                MoveCaret(_text.Home());
                break;
            case BoardKeyKind.Down:
                MoveCaret(_text.End());
                break;
        }
    }

    void IKeyboardTarget.Hide() => Drop();

    void IKeyboardTarget.Dock(bool atTop)
    {
        // The editor's keyboard has no edge key (Configure in the constructor).
    }

    /// <summary>The keyboard's meaning of Arcadia's buttons, as in the search: B puts it away, here dropping the text.</summary>
    private static KeyboardButton? KeyboardButtonOf(UiAction action) => action switch
    {
        UiAction.Up => KeyboardButton.Up,
        UiAction.Down => KeyboardButton.Down,
        UiAction.Left => KeyboardButton.Left,
        UiAction.Right => KeyboardButton.Right,
        UiAction.Accept => KeyboardButton.Type,
        UiAction.Search => KeyboardButton.Erase,
        UiAction.ViewOptions => KeyboardButton.Space,
        UiAction.PreviousTab => KeyboardButton.Shift,
        UiAction.NextTab => KeyboardButton.AltGr,
        UiAction.KeyboardLayout => KeyboardButton.Layout,
        UiAction.Back => KeyboardButton.Hide,
        _ => null,
    };

    private void Erase()
    {
        if (_text.Caret > 0)
        {
            SetText(_text.EraseBefore());
        }
    }

    private void SetText(TextWithCaret text)
    {
        _text = text;
        Problem = _problemOf(text.Text);
        ShowCaret();
    }

    private void MoveCaret(TextWithCaret text)
    {
        _text = text;
        ShowCaret();
    }

    private void ShowCaret()
    {
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(TextBeforeCaret));
        OnPropertyChanged(nameof(TextAfterCaret));
    }

    private void Close()
    {
        Keyboard.Closed();
        _close();
    }
}
