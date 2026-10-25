using System.Collections.ObjectModel;
using Arcadia.App.ViewModels;
using Arcadia.Core.Search;
using Arcadia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.Keyboard;

/// <summary>
/// Arcadia's on-screen keyboard, one design everywhere (owner, 2026-10-22): laid out like the real keyboard of the
/// chosen layout (<see cref="FullKeyboard"/>), the keys showing what they type on the shown level
/// (<see cref="KeyboardLevels"/>), a block of its own keys and the arrows on the right. What it types into and
/// what its own keys do is the <see cref="IKeyboardTarget"/>'s business: the search's field, or the window in front
/// in the mouse mode (#110). Whoever shows it may grey out or leave out keys (<see cref="Configure"/>).
/// </summary>
/// <remarks>
/// Lives on the UI thread. The buttons come as <see cref="KeyboardButton"/>s: from Arcadia's navigation, whose
/// D-pad repeats by itself (<see cref="ButtonDown"/>, <see cref="ButtonUp"/>), or from the mouse mode, which only
/// reports presses and releases (<see cref="Handle"/>).
/// </remarks>
public sealed partial class OnScreenKeyboardViewModel : ViewModelBase, IDisposable
{
    /// <summary>A held D-pad direction moves on like on Arcadia's pages; a held erase deletes like a held Backspace.</summary>
    private static readonly TimeSpan MoveRepeatDelay = TimeSpan.FromMilliseconds(400);

    private static readonly TimeSpan MoveRepeatInterval = TimeSpan.FromMilliseconds(90);
    private static readonly TimeSpan EraseRepeatDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan EraseRepeatInterval = TimeSpan.FromMilliseconds(60);

    private readonly IKeyboardTarget _target;
    private readonly Func<bool>? _controllerInUse;
    private readonly KeyboardLevels _levels;
    private readonly RepeatGesture _move;
    private readonly RepeatGesture _erase;
    private readonly Dictionary<BoardKey, BoardKeyViewModel> _keyViews = [];
    private Func<BoardKey, BoardKeyState> _stateOf = _ => BoardKeyState.Shown;
    private Func<char, bool> _takes = _ => true;
    private FullKeyboard _board;
    private KeyboardButton _moveDirection;
    private int _row = 2;
    private int _column = 1;

    /// <param name="controllerInUse">
    /// For a keyboard outside Arcadia: whether a click came from the controller's pointer rather than a real mouse,
    /// so the legend follows. Without it, <see cref="UsesController"/> is the user's to set.
    /// </param>
    /// <param name="post">Reaches the UI thread from the repeat timers.</param>
    public OnScreenKeyboardViewModel(KeyboardLayout layout, IKeyboardTarget target, TimeProvider time, Action<Action> post, Func<bool>? controllerInUse = null)
    {
        _target = target;
        _controllerInUse = controllerInUse;
        _board = FullKeyboard.For(layout);
        _levels = new KeyboardLevels(() => _board.Layout.HasAltGr, time);
        _levels.Changed += ShowLevel;
        _move = new RepeatGesture(MoveRepeatDelay, MoveRepeatInterval, time, post);
        _move.Repeated += () => Move(_moveDirection);
        _erase = new RepeatGesture(EraseRepeatDelay, EraseRepeatInterval, time, post);
        _erase.Repeated += Erase;
        LayoutMenu = new LayoutMenuViewModel(layout, ChooseLayout);
        BuildKeys();
    }

    /// <summary>A layout was picked; it counts everywhere the keyboard shows.</summary>
    public event Action<KeyboardLayout>? LayoutChosen;

    public ObservableCollection<BoardKeyViewModel> Keys { get; } = [];

    public LayoutMenuViewModel LayoutMenu { get; }

    public KeyboardLayout Layout => _board.Layout;

    /// <summary>The level the keys show and type.</summary>
    public KeyLevel Level => _levels.Level;

    /// <summary>The keyboard sits at the top edge of the screen; the dock key offers the other one.</summary>
    [ObservableProperty]
    public partial bool AtTop { get; private set; }

    /// <summary>The buttons on the keys follow the input in use (#127): shown for the controller, not for the mouse.</summary>
    [ObservableProperty]
    public partial bool UsesController { get; set; } = true;

    public BoardKeyViewModel FocusedKey => KeyAt(_row, _column);

    /// <summary>Greys out or leaves out keys; asked again for every key whenever the layout changes.</summary>
    /// <param name="takes">
    /// The characters the text may hold, e.g. a launcher's keyword (#137); null: all. A key whose character on the shown
    /// level isn't one is greyed out, the space bar too, and shift or AltGr when their level offers none.
    /// </param>
    public void Configure(Func<BoardKey, BoardKeyState> stateOf, Func<char, bool>? takes = null)
    {
        _stateOf = stateOf;
        _takes = takes ?? (_ => true);
        ShowStates();
        ShowLevel();
    }

    /// <summary>Shown again: the layout may have changed elsewhere meanwhile.</summary>
    public void Open(KeyboardLayout layout, bool atTop)
    {
        Closed();
        AtTop = atTop;
        UpdateDockKey();
        UsesController = true;
        if (layout != _board.Layout)
        {
            SetLayout(layout);
        }
    }

    /// <summary>Put away, by any means: nothing may repeat on, shift and AltGr start off next time.</summary>
    public void Closed()
    {
        StopRepeats();
        _levels.Reset();
        LayoutMenu.Close();
    }

    /// <summary>The releases of held buttons may never arrive, e.g. when the window loses the focus.</summary>
    public void CancelPresses()
    {
        StopRepeats();
        _levels.CancelPresses();
    }

    /// <summary>A button from the mouse mode (#110), which reports presses and releases only: a held D-pad repeats here.</summary>
    public void Handle(KeyboardButton button, bool down)
    {
        UsesController = true;
        if (button is KeyboardButton.Up or KeyboardButton.Down or KeyboardButton.Left or KeyboardButton.Right && !LayoutMenu.IsOpen)
        {
            if (down)
            {
                _moveDirection = button;
                _move.Release();
                _move.Press();
            }
            else if (button == _moveDirection)
            {
                _move.Release();
            }

            return;
        }

        if (down)
        {
            ButtonDown(button);
        }
        else
        {
            ButtonUp(button);
        }
    }

    /// <summary>
    /// A button went down. In the layout menu the D-pad, A and B act on it; otherwise the D-pad moves the focus
    /// over the keys a step, A presses the focused key (held on ⌫: erases on), and the others stand for their key.
    /// </summary>
    public void ButtonDown(KeyboardButton button)
    {
        if (LayoutMenu.IsOpen && HandleLayoutMenu(button))
        {
            return;
        }

        switch (button)
        {
            case KeyboardButton.Up or KeyboardButton.Down or KeyboardButton.Left or KeyboardButton.Right:
                Move(button);
                break;
            case KeyboardButton.Type when FocusedKey.Key.Kind == BoardKeyKind.Backspace:
                _erase.Press();
                break;
            case KeyboardButton.Type:
                Press(FocusedKey);
                break;
            case KeyboardButton.Erase when IsAvailable(BoardKeyKind.Backspace):
                _erase.Press();
                break;
            case KeyboardButton.Space when IsAvailable(BoardKeyKind.Space):
                Type(' ');
                break;
            case KeyboardButton.Shift when IsAvailable(BoardKeyKind.Shift):
                _levels.PressShift();
                break;
            case KeyboardButton.AltGr when IsAvailable(BoardKeyKind.AltGr):
                _levels.PressAltGr();
                break;
            case KeyboardButton.Enter when IsAvailable(BoardKeyKind.Enter):
                Tap(BoardKeyKind.Enter);
                break;
            case KeyboardButton.Layout when IsAvailable(BoardKeyKind.Layout):
                LayoutMenu.Toggle();
                break;
            case KeyboardButton.Dock when IsAvailable(BoardKeyKind.Dock):
                Dock();
                break;
            case KeyboardButton.Hide when IsAvailable(BoardKeyKind.Hide):
                Hide();
                break;
        }
    }

    /// <summary>A button was let go: ends a held erase, shift or AltGr.</summary>
    public void ButtonUp(KeyboardButton button)
    {
        switch (button)
        {
            case KeyboardButton.Type or KeyboardButton.Erase:
                _erase.Release();
                break;
            case KeyboardButton.Shift:
                _levels.ReleaseShift();
                break;
            case KeyboardButton.AltGr:
                _levels.ReleaseAltGr();
                break;
        }
    }

    /// <summary>B closes the layout menu first; up, down and A act in it. The other buttons work as usual.</summary>
    private bool HandleLayoutMenu(KeyboardButton button)
    {
        switch (button)
        {
            case KeyboardButton.Up:
                LayoutMenu.Up();
                return true;
            case KeyboardButton.Down:
                LayoutMenu.Down();
                return true;
            case KeyboardButton.Type:
                LayoutMenu.Accept();
                return true;
            case KeyboardButton.Hide:
                LayoutMenu.Close();
                return true;
            default:
                return button is KeyboardButton.Left or KeyboardButton.Right;
        }
    }

    /// <summary>A key clicked, with the controller's pointer or a real mouse; the focus follows it.</summary>
    private void Click(BoardKeyViewModel key)
    {
        if (_controllerInUse is not null)
        {
            UsesController = _controllerInUse();
        }

        if (key.IsSkipped)
        {
            return;
        }

        var row = Enumerable.Range(0, _board.Rows.Count).First(r => _board.Rows[r].Contains(key.Key));
        SetFocus(row, IndexInRow(row, key));
        Press(key);
    }

    private void Press(BoardKeyViewModel key)
    {
        switch (key.Key.Kind)
        {
            case BoardKeyKind.Character when key.Key.Cap!.On(_levels.Level) is { } character:
                Type(character);
                break;
            case BoardKeyKind.Backspace:
                Erase();
                break;
            case BoardKeyKind.Tab or BoardKeyKind.Enter or BoardKeyKind.Up or BoardKeyKind.Down or BoardKeyKind.Left or BoardKeyKind.Right:
                Tap(key.Key.Kind);
                break;
            case BoardKeyKind.Space:
                Type(' ');
                break;
            case BoardKeyKind.Shift:
                _levels.ToggleShift();
                break;
            case BoardKeyKind.AltGr:
                _levels.ToggleAltGr();
                break;
            case BoardKeyKind.Layout:
                LayoutMenu.Toggle();
                break;
            case BoardKeyKind.Dock:
                Dock();
                break;
            case BoardKeyKind.Hide:
                Hide();
                break;
        }
    }

    private void Type(char character)
    {
        _target.Type(character);
        _levels.Use();
    }

    private void Erase() => _target.Press(BoardKeyKind.Backspace, withShift: false);

    /// <summary>
    /// With shift on, tab, enter and the arrows go with Shift, as on a real keyboard: Shift+Tab goes back, Shift
    /// and the arrows select (owner, 2026-10-22).
    /// </summary>
    private void Tap(BoardKeyKind key)
    {
        _target.Press(key, withShift: _levels.Level == KeyLevel.Shift);
        _levels.Use();
    }

    private void Dock()
    {
        AtTop = !AtTop;
        UpdateDockKey();
        _target.Dock(AtTop);
    }

    private void Hide()
    {
        Closed();
        _target.Hide();
    }

    private void ChooseLayout(KeyboardLayout layout)
    {
        if (layout != _board.Layout)
        {
            SetLayout(layout);
            LayoutChosen?.Invoke(layout);
        }
    }

    private void SetLayout(KeyboardLayout layout)
    {
        var centre = FocusedKey.Key.CentreIn(_row);

        // Before the board changes: switching AltGr off redraws the keys, and the new board's aren't built yet.
        if (!layout.HasAltGr)
        {
            _levels.ResetAltGr();
        }

        _board = FullKeyboard.For(layout);
        LayoutMenu.Selected = layout;
        BuildKeys();
        FocusClosest(centre);
    }

    /// <summary>Moves the focus over the keys that do something; it stays at the keyboard's edges.</summary>
    private void Move(KeyboardButton direction)
    {
        (int Row, int Column)? target = direction switch
        {
            KeyboardButton.Left => KeyGrid.Sideways(AllSpots(), _row, _column, -1),
            KeyboardButton.Right => KeyGrid.Sideways(AllSpots(), _row, _column, 1),
            KeyboardButton.Up => KeyGrid.Vertical(AllSpots(), _row, _column, -1),
            KeyboardButton.Down => KeyGrid.Vertical(AllSpots(), _row, _column, 1),
            _ => null,
        };
        if (target is { } to)
        {
            SetFocus(to.Row, to.Column);
        }
    }

    private void BuildKeys()
    {
        Keys.Clear();
        _keyViews.Clear();
        for (var i = 0; i < _board.Keys.Count; i++)
        {
            var key = new BoardKeyViewModel(_board.Keys[i], i, Click);
            Keys.Add(key);
            _keyViews[key.Key] = key;
        }

        UpdateDockKey();
        ShowStates();
        ShowLevel();
    }

    private void ShowStates()
    {
        foreach (var key in Keys)
        {
            var state = _stateOf(key.Key);
            key.IsHidden = state == BoardKeyState.Hidden;
            key.IsUnavailable = state == BoardKeyState.Disabled || !Offers(key.Key);
        }
    }

    /// <summary>
    /// Whether a key can type something the text takes on the shown level. A character key without a character there is
    /// skipped anyway, but not greyed out.
    /// </summary>
    private bool Offers(BoardKey key) => key.Kind switch
    {
        BoardKeyKind.Character => key.Cap!.On(_levels.Level) is not { } character || _takes(character),
        BoardKeyKind.Space => _takes(' '),
        BoardKeyKind.Shift => OffersLevel(KeyLevel.Shift),
        BoardKeyKind.AltGr => _board.Layout.HasAltGr && OffersLevel(KeyLevel.AltGr),
        _ => true,
    };

    private bool OffersLevel(KeyLevel level) =>
        _board.Keys.Any(k => k.Kind == BoardKeyKind.Character && k.Cap!.On(level) is { } character && _takes(character));

    /// <summary>
    /// The labels follow the level like on Steam's keyboard: on the normal level the character with the
    /// shift and AltGr ones small beside it, on the others only that level's character, or nothing.
    /// </summary>
    private void ShowLevel()
    {
        OnPropertyChanged(nameof(Level));
        ShowStates();
        var level = _levels.Level;
        foreach (var key in Keys)
        {
            switch (key.Key.Kind)
            {
                case BoardKeyKind.Character:
                    key.Label = key.Key.Cap!.On(level)?.ToString() ?? string.Empty;
                    key.ShiftHint = level == KeyLevel.Normal ? key.Key.Cap.ShiftHint?.ToString() : null;
                    key.AltGrHint = level == KeyLevel.Normal ? key.Key.Cap.AltGr?.ToString() : null;
                    break;
                case BoardKeyKind.Layout:
                    key.Text = $"Layout: {_board.Layout.Code}";
                    break;
                case BoardKeyKind.Shift:
                    key.IsOn = level == KeyLevel.Shift;
                    break;
                case BoardKeyKind.AltGr:
                    key.IsOn = level == KeyLevel.AltGr;
                    break;
            }
        }

        // AltGr empties most keys, a key may be left out; the focus mustn't stay on one that does nothing.
        if (FocusedKey.IsSkipped)
        {
            FocusClosest(FocusedKey.Key.CentreIn(_row));
        }
        else
        {
            SetFocus(_row, _column);
        }
    }

    private void UpdateDockKey()
    {
        foreach (var key in Keys.Where(k => k.IsDockKey))
        {
            key.Text = AtTop ? "Nach unten" : "Nach oben";
            key.PointsUp = !AtTop;
        }
    }

    /// <summary>A button stands for its key only while that key is there and not greyed out.</summary>
    private bool IsAvailable(BoardKeyKind kind) => Keys.Any(k => k.Key.Kind == kind && !k.IsHidden && !k.IsUnavailable);

    private BoardKeyViewModel KeyAt(int row, int column) => _keyViews[_board.Rows[row][column]];

    private int IndexInRow(int row, BoardKeyViewModel key) => _board.Rows[row].ToList().IndexOf(key.Key);

    private List<IReadOnlyList<KeySpot>> AllSpots() =>
        Enumerable.Range(0, _board.Rows.Count)
            .Select(row => (IReadOnlyList<KeySpot>)_board.Rows[row]
                .Select(k => _keyViews[k])
                .Select(k => new KeySpot(k.Key.CentreIn(row), k.IsSkipped, k.Index, k.Key.WidthIn(row)))
                .ToList())
            .ToList();

    /// <summary>Puts the focus on the key nearest to <paramref name="centre"/> in the focused row that it may rest on.</summary>
    private void FocusClosest(double centre)
    {
        var (row, column) = KeyGrid.Closest(AllSpots(), _row, centre) ?? (_row, _column);
        SetFocus(row, column);
    }

    private void SetFocus(int row, int column)
    {
        foreach (var key in Keys.Where(k => k.IsFocused))
        {
            key.IsFocused = false;
        }

        _row = row;
        _column = column;
        FocusedKey.IsFocused = true;
    }

    private void StopRepeats()
    {
        _move.Release();
        _erase.Release();
    }

    public void Dispose()
    {
        _move.Dispose();
        _erase.Dispose();
    }
}
