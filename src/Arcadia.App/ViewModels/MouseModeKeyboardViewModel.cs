using System.Collections.ObjectModel;
using Arcadia.App.Search;
using Arcadia.Core.Search;
using Arcadia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>One key of the mouse mode's on-screen keyboard, placed on its canvas in the design's pixels.</summary>
public sealed partial class BoardKeyViewModel : ViewModelBase
{
    /// <summary>A key and the gap to its neighbour; a key is <see cref="KeyGap"/> narrower.</summary>
    public const double Pitch = 88;

    public const double KeyGap = 8;

    public const double RowPitch = 60;

    public const double KeyHeight = RowPitch - KeyGap;

    public BoardKeyViewModel(BoardKey key, int index, Action<BoardKeyViewModel> press)
    {
        Key = key;
        Index = index;
        Left = key.Left * Pitch;
        Top = key.Row * RowPitch;
        Width = (key.Width * Pitch) - KeyGap;
        Height = ((key.LastRow - key.Row) * RowPitch) + KeyHeight;
        (Symbol, Text, Glyph) = Face(key);
        PressCommand = new RelayCommand(() => press(this));
    }

    public BoardKey Key { get; }

    /// <summary>Its place in <see cref="FullKeyboard.Keys"/>; names it to the focus (<see cref="KeySpot.Key"/>).</summary>
    public int Index { get; }

    public double Left { get; }

    public double Top { get; }

    public double Width { get; }

    public double Height { get; }

    public bool IsCharacter => Key.Kind == BoardKeyKind.Character;

    /// <summary>The tall ISO Enter, drawn as an upside-down L over two rows.</summary>
    public bool IsTallEnter => Key.LowerLeft is not null;

    /// <summary>Caps lock: empty and darker, as nothing is behind it.</summary>
    public bool IsBlank => Key.Kind == BoardKeyKind.CapsLock;

    /// <summary>Every other key: a symbol and a name, like the printing on a real one.</summary>
    public bool IsLabelled => !IsCharacter && !IsTallEnter && !IsBlank;

    public bool IsLayoutKey => Key.Kind == BoardKeyKind.Layout;

    public bool IsDockKey => Key.Kind == BoardKeyKind.Dock;

    public bool IsHideKey => Key.Kind == BoardKeyKind.Hide;

    public bool HasSymbol => Symbol.Length > 0;

    /// <summary>The symbol printed on a real key, e.g. ⌫; empty for the right block's own keys, which have icons.</summary>
    public string Symbol { get; }

    /// <summary>The key's name beside its symbol; the layout and dock keys change theirs.</summary>
    [ObservableProperty]
    public partial string Text { get; set; }

    /// <summary>The controller button that does the same, shown on the key while the controller is in use.</summary>
    public string? Glyph { get; }

    public bool HasGlyph => Glyph is not null;

    public bool IsRoundGlyph => Glyph is { Length: 1 };

    /// <summary>What a press types on the shown level; empty where it types nothing.</summary>
    [ObservableProperty]
    public partial string Label { get; set; } = string.Empty;

    /// <summary>On the normal level: the shift character small above, as printed on the key.</summary>
    [ObservableProperty]
    public partial string? ShiftHint { get; set; }

    /// <summary>On the normal level: the AltGr character small at the bottom right.</summary>
    [ObservableProperty]
    public partial string? AltGrHint { get; set; }

    /// <summary>A switch that is on: shift or AltGr.</summary>
    [ObservableProperty]
    public partial bool IsOn { get; set; }

    /// <summary>Greyed out: AltGr for a layout without that level.</summary>
    [ObservableProperty]
    public partial bool IsUnavailable { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>The dock key: the keyboard is at the bottom, so the key moves it up (its icon's arrow points up).</summary>
    [ObservableProperty]
    public partial bool PointsUp { get; set; } = true;

    /// <summary>The focus passes over it: caps lock, a greyed-out AltGr, a character key empty on the shown level.</summary>
    public bool IsSkipped => IsBlank || IsUnavailable || (IsCharacter && Label.Length == 0);

    public IRelayCommand PressCommand { get; }

    /// <summary>The printing of a key (owner, 2026-10-22: symbol as on a real key and a name); narrow ones keep only the symbol.</summary>
    private static (string Symbol, string Text, string? Glyph) Face(BoardKey key) => key.Kind switch
    {
        BoardKeyKind.Backspace => ("⌫", "Löschen", "X"),
        BoardKeyKind.Tab => ("↹", "Tab", null),
        BoardKeyKind.Enter => ("↵", "Enter", "≡"),
        BoardKeyKind.Shift => ("⇧", key.Width >= 2 ? "Umschalt" : string.Empty, "LB"),
        BoardKeyKind.Space => ("␣", "Leertaste", "Y"),
        BoardKeyKind.AltGr => (string.Empty, "AltGr", "RB"),
        BoardKeyKind.Layout => (string.Empty, string.Empty, "R3"),
        BoardKeyKind.Dock => (string.Empty, string.Empty, "⧉"),
        BoardKeyKind.Hide => (string.Empty, "Ausblenden", "B"),
        BoardKeyKind.Up => ("↑", string.Empty, null),
        BoardKeyKind.Down => ("↓", string.Empty, null),
        BoardKeyKind.Left => ("←", string.Empty, null),
        BoardKeyKind.Right => ("→", string.Empty, null),
        _ => (string.Empty, string.Empty, null),
    };
}

/// <summary>
/// The mouse mode's on-screen keyboard (#110): Y opens it over any window, and it types into the field that
/// has the focus there, as a real keyboard of the chosen layout would (<see cref="FullKeyboard"/>). The
/// sticks and triggers stay the mouse meanwhile; the other buttons are the keyboard's
/// (<see cref="KeyboardButton"/>). Keys can also be clicked, with the pointer or a real mouse.
/// </summary>
/// <remarks>Lives on the UI thread; the controller's buttons reach it from the poll thread through <see cref="Handle"/>, posted.</remarks>
public sealed partial class MouseModeKeyboardViewModel : ViewModelBase, IDisposable
{
    /// <summary>A held D-pad direction moves on like on Arcadia's pages; a held erase deletes like in the search.</summary>
    private static readonly TimeSpan MoveRepeatDelay = TimeSpan.FromMilliseconds(400);

    private static readonly TimeSpan MoveRepeatInterval = TimeSpan.FromMilliseconds(90);
    private static readonly TimeSpan EraseRepeatDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan EraseRepeatInterval = TimeSpan.FromMilliseconds(60);

    private readonly IMouseKeyboardOutput _output;
    private readonly Func<bool> _controllerInUse;
    private readonly KeyboardLevels _levels;
    private readonly RepeatGesture _move;
    private readonly RepeatGesture _erase;
    private readonly Dictionary<BoardKey, BoardKeyViewModel> _keyViews = [];
    private FullKeyboard _board;
    private KeyboardButton _moveDirection;
    private int _row = 2;
    private int _column = 1;

    /// <param name="output">Where the keys go: the window in front.</param>
    /// <param name="controllerInUse">Whether a click came from the controller (RT) rather than a real mouse; the legend follows.</param>
    /// <param name="post">Reaches the UI thread from the repeat timers.</param>
    public MouseModeKeyboardViewModel(IMouseKeyboardOutput output, KeyboardLayout layout, bool atTop, Func<bool> controllerInUse, TimeProvider time, Action<Action> post)
    {
        _output = output;
        _controllerInUse = controllerInUse;
        _board = FullKeyboard.For(layout);
        _levels = new KeyboardLevels(() => _board.Layout.HasAltGr, time);
        _levels.Changed += ShowLevel;
        _move = new RepeatGesture(MoveRepeatDelay, MoveRepeatInterval, time, post);
        _move.Repeated += () => Move(_moveDirection);
        _erase = new RepeatGesture(EraseRepeatDelay, EraseRepeatInterval, time, post);
        _erase.Repeated += () => Tap(OutputKey.Backspace, withShift: false);
        LayoutMenu = new LayoutMenuViewModel(layout, ChooseLayout);
        AtTop = atTop;
        BuildKeys();
    }

    /// <summary>B or "Ausblenden": the keyboard closes, the mouse mode goes on.</summary>
    public event Action? Hidden;

    /// <summary>Moved to the other edge; the argument is the new one (true: the top). Kept for the next time.</summary>
    public event Action<bool>? Docked;

    /// <summary>A layout was picked; it counts for the search too.</summary>
    public event Action<KeyboardLayout>? LayoutChosen;

    public ObservableCollection<BoardKeyViewModel> Keys { get; } = [];

    public LayoutMenuViewModel LayoutMenu { get; }

    public KeyboardLayout Layout => _board.Layout;

    /// <summary>The keyboard sits at the top edge of the screen; the dock key offers the other one.</summary>
    [ObservableProperty]
    public partial bool AtTop { get; private set; }

    /// <summary>The legend and the buttons on the keys follow the input in use (#127): the controller's, or the mouse's.</summary>
    [ObservableProperty]
    public partial bool UsesController { get; private set; } = true;

    public BoardKeyViewModel FocusedKey => KeyAt(_row, _column);

    /// <summary>Opening again: the layout may have changed in the search meanwhile; shift and AltGr start off.</summary>
    public void Open(KeyboardLayout layout, bool atTop)
    {
        StopRepeats();
        _levels.Reset();
        LayoutMenu.Close();
        AtTop = atTop;
        UsesController = true;
        if (layout != _board.Layout)
        {
            SetLayout(layout);
        }
    }

    /// <summary>The keyboard closed by other means (the mouse mode ended, a game came to the front): nothing may repeat on.</summary>
    public void Closed()
    {
        StopRepeats();
        _levels.Reset();
        LayoutMenu.Close();
    }

    /// <summary>A controller button, from the mouse mode's poll thread (posted).</summary>
    public void Handle(KeyboardButton button, bool down)
    {
        UsesController = true;
        if (!down)
        {
            Release(button);
            return;
        }

        if (LayoutMenu.IsOpen && HandleLayoutMenu(button))
        {
            return;
        }

        switch (button)
        {
            case KeyboardButton.Up or KeyboardButton.Down or KeyboardButton.Left or KeyboardButton.Right:
                _moveDirection = button;
                _move.Release();
                _move.Press();
                break;
            case KeyboardButton.Type when FocusedKey.Key.Kind == BoardKeyKind.Backspace:
                _erase.Press();
                break;
            case KeyboardButton.Type:
                Press(FocusedKey);
                break;
            case KeyboardButton.Erase:
                _erase.Press();
                break;
            case KeyboardButton.Space:
                Type(' ');
                break;
            case KeyboardButton.Shift:
                _levels.PressShift();
                break;
            case KeyboardButton.AltGr:
                _levels.PressAltGr();
                break;
            case KeyboardButton.Enter:
                Tap(OutputKey.Enter, withShift: true);
                break;
            case KeyboardButton.Layout:
                LayoutMenu.Toggle();
                break;
            case KeyboardButton.Dock:
                Dock();
                break;
            case KeyboardButton.Hide:
                Hide();
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

    private void Release(KeyboardButton button)
    {
        switch (button)
        {
            case KeyboardButton.Up or KeyboardButton.Down or KeyboardButton.Left or KeyboardButton.Right when button == _moveDirection:
                _move.Release();
                break;
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

    /// <summary>A key clicked, with the controller's pointer or a real mouse; the focus follows it.</summary>
    private void Click(BoardKeyViewModel key)
    {
        UsesController = _controllerInUse();
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
                Tap(OutputKey.Backspace, withShift: false);
                break;
            case BoardKeyKind.Tab:
                Tap(OutputKey.Tab, withShift: true);
                break;
            case BoardKeyKind.Enter:
                Tap(OutputKey.Enter, withShift: true);
                break;
            case BoardKeyKind.Up:
                Tap(OutputKey.Up, withShift: true);
                break;
            case BoardKeyKind.Down:
                Tap(OutputKey.Down, withShift: true);
                break;
            case BoardKeyKind.Left:
                Tap(OutputKey.Left, withShift: true);
                break;
            case BoardKeyKind.Right:
                Tap(OutputKey.Right, withShift: true);
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
        _output.Type(character);
        _levels.Use();
    }

    /// <summary>
    /// A key down and up. With shift on, tab, enter and the arrows go with Shift, as on a real keyboard:
    /// Shift+Tab goes back, Shift and the arrows select (owner, 2026-10-22).
    /// </summary>
    private void Tap(OutputKey key, bool withShift)
    {
        var shift = withShift && _levels.Level == KeyLevel.Shift;
        if (shift)
        {
            _output.SetKey(OutputKey.Shift, true);
        }

        _output.SetKey(key, true);
        _output.SetKey(key, false);
        if (shift)
        {
            _output.SetKey(OutputKey.Shift, false);
        }

        _levels.Use();
    }

    private void Dock()
    {
        AtTop = !AtTop;
        UpdateDockKey();
        Docked?.Invoke(AtTop);
    }

    private void Hide()
    {
        Closed();
        Hidden?.Invoke();
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
        _board = FullKeyboard.For(layout);
        LayoutMenu.Selected = layout;
        if (!layout.HasAltGr)
        {
            _levels.ResetAltGr();
        }

        BuildKeys();
        SetFocus(_row, NearestInRow(_row, centre));
    }

    /// <summary>Moves the focus over the keys that type something; it stays at the keyboard's edges.</summary>
    private void Move(KeyboardButton direction)
    {
        switch (direction)
        {
            case KeyboardButton.Left or KeyboardButton.Right:
                SetFocus(_row, KeyGrid.Sideways(Spots(_row), _column, direction == KeyboardButton.Left ? -1 : 1));
                break;
            case KeyboardButton.Up or KeyboardButton.Down:
                var (row, column) = KeyGrid.Vertical(Enumerable.Range(0, _board.Rows.Count).Select(Spots).ToList(), _row, _column, direction == KeyboardButton.Up ? -1 : 1);
                SetFocus(row, column);
                break;
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
        ShowLevel();
    }

    /// <summary>
    /// The labels follow the level like on Steam's keyboard: on the normal level the character with the
    /// shift and AltGr ones small beside it, on the others only that level's character, or nothing.
    /// </summary>
    private void ShowLevel()
    {
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
                    key.IsUnavailable = !_board.Layout.HasAltGr;
                    break;
            }
        }

        // AltGr empties most keys; the focus mustn't stay on one that types nothing.
        if (Keys.Count > 0 && FocusedKey.IsSkipped)
        {
            SetFocus(_row, NearestInRow(_row, FocusedKey.Key.CentreIn(_row)));
        }
        else if (Keys.Count > 0)
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

    private BoardKeyViewModel KeyAt(int row, int column) => _keyViews[_board.Rows[row][column]];

    private int IndexInRow(int row, BoardKeyViewModel key) => _board.Rows[row].ToList().IndexOf(key.Key);

    private List<KeySpot> Spots(int row) =>
        _board.Rows[row].Select(k => _keyViews[k]).Select(k => new KeySpot(k.Key.CentreIn(row), k.IsSkipped, k.Index)).ToList();

    /// <summary>The key in <paramref name="row"/> nearest to <paramref name="centre"/> that the focus may rest on.</summary>
    private int NearestInRow(int row, double centre)
    {
        var spots = Spots(row);
        var best = 0;
        for (var i = 0; i < spots.Count; i++)
        {
            if (!spots[i].IsEmpty && (spots[best].IsEmpty || Math.Abs(spots[i].Centre - centre) < Math.Abs(spots[best].Centre - centre)))
            {
                best = i;
            }
        }

        return best;
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
