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
    private readonly Action<IKeyboardTarget> _giveBackKeyboard;
    private readonly Func<char, bool>? _takes;
    private readonly int? _maxLength;
    private TextWithCaret _text;

    /// <param name="problemOf">Why a text can't be taken, or null if it can.</param>
    /// <param name="take">Takes a text without problem; false if it couldn't, and the editor stays open.</param>
    /// <param name="close">The editor is done, the text taken or dropped.</param>
    /// <param name="takeKeyboard">The settings' one on-screen keyboard, now typing into this editor; given back on dispose.</param>
    /// <param name="takes">The characters the text may hold; the on-screen keyboard greys out the others. Null: all.</param>
    /// <param name="maxLength">The most characters the text may hold: typing beyond is refused and the keyboard greys out its characters once it is full. Null: no limit.</param>
    public TextEditorViewModel(
        string text,
        Func<string, string?> problemOf,
        Func<string, bool> take,
        Action close,
        Func<IKeyboardTarget, OnScreenKeyboardViewModel> takeKeyboard,
        Action<IKeyboardTarget> giveBackKeyboard,
        Func<char, bool>? takes = null,
        int? maxLength = null)
    {
        _text = TextWithCaret.AtEnd(text);
        _takes = takes;
        _maxLength = maxLength;
        _problemOf = problemOf;
        _take = take;
        _close = close;
        _giveBackKeyboard = giveBackKeyboard;
        Keyboard = takeKeyboard(this);
        ConfigureKeyboard();
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

    /// <summary>
    /// Says the text is at its most characters, so a key that types nothing more isn't a mystery (#533); null below it.
    /// Unlike <see cref="Problem"/> it doesn't stop the text being taken.
    /// </summary>
    public string? LimitNotice => IsFull ? AppTexts.TextAtMaxLength(_maxLength!.Value) : null;

    public bool HasLimitNotice => IsFull;

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
        if (_maxLength is { } max)
        {
            typed = typed[..Math.Min(typed.Length, Math.Max(0, max - Text.Length))];
        }

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

    public void Dispose() => _giveBackKeyboard(this);

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
        UiAction.Status => KeyboardButton.Space,
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
        var wasFull = IsFull;
        _text = text;
        Problem = _problemOf(text.Text);
        ShowCaret();
        if (IsFull != wasFull)
        {
            ConfigureKeyboard();
            OnPropertyChanged(nameof(LimitNotice));
            OnPropertyChanged(nameof(HasLimitNotice));
        }
    }

    private bool IsFull => _maxLength is { } max && Text.Length >= max;

    // Without ↹ and the edge key; full, every character key is greyed out and only erasing, moving and taking are left.
    private void ConfigureKeyboard() => Keyboard.Configure(
        key => key.Kind is BoardKeyKind.Tab or BoardKeyKind.Dock ? BoardKeyState.Hidden : BoardKeyState.Shown,
        _maxLength is null ? _takes : c => !IsFull && (_takes?.Invoke(c) ?? true));

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
