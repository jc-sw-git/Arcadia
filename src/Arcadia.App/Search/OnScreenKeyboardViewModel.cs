using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Search;

/// <summary>The keys of the bottom row; each also has its own gamepad button.</summary>
public enum SpecialKey
{
    /// <summary>R3: the layout menu.</summary>
    Layout,

    /// <summary>L3: the launcher filter and sort order.</summary>
    Filter,

    /// <summary>LB: upper case, tapped or held.</summary>
    Shift,

    /// <summary>Y.</summary>
    Space,

    /// <summary>X: deletes the last character, held: one after another.</summary>
    Erase,

    /// <summary>RB: between the letters and the symbols, tapped or held.</summary>
    Symbols,
}

/// <summary>One key of the on-screen keyboard.</summary>
public sealed partial class KeyViewModel : ViewModelBase
{
    public KeyViewModel(string label, double width, double centre, Action<KeyViewModel> press, char? character = null, SpecialKey? special = null, string? glyph = null)
    {
        Label = label;
        Width = width;
        Centre = centre;
        Character = character;
        Special = special;
        Glyph = glyph;
        PressCommand = new RelayCommand(() => press(this));
    }

    /// <summary>The character in upper case while shift is on, the name of a special key.</summary>
    [ObservableProperty]
    public partial string Label { get; set; }

    public double Width { get; }

    /// <summary>Horizontal centre relative to the keyboard's middle; up and down go to the nearest key.</summary>
    public double Centre { get; }

    /// <summary>The character in lower case; <see cref="Label"/> shows what a press types.</summary>
    public char? Character { get; }

    public SpecialKey? Special { get; }

    /// <summary>The gamepad button that does the same, shown on the key.</summary>
    public string? Glyph { get; }

    public bool HasGlyph => Glyph is not null;

    /// <summary>Single-letter glyphs (X, Y) are round, like on the pad.</summary>
    public bool IsRoundGlyph => Glyph is { Length: 1 };

    public bool IsLayoutKey => Special == SpecialKey.Layout;

    public bool IsShiftKey => Special == SpecialKey.Shift;

    public bool IsWide => Special is not null;

    /// <summary>A switch that is on: shift, or the symbols page.</summary>
    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    public IRelayCommand PressCommand { get; }
}

public sealed class KeyRowViewModel(IReadOnlyList<KeyViewModel> keys)
{
    public IReadOnlyList<KeyViewModel> Keys { get; } = keys;
}

/// <summary>An entry of the layout menu (R3).</summary>
public sealed partial class LayoutItemViewModel(KeyboardLayout layout, Action<KeyboardLayout> pick) : ViewModelBase
{
    public KeyboardLayout Layout { get; } = layout;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    public IRelayCommand PickCommand { get; } = new RelayCommand(() => pick(layout));
}

/// <summary>
/// The on-screen keyboard of the search: four rows of characters from the chosen layout (or the
/// symbols page) and a row of special keys. The D-pad moves the focus; up and down go to the key
/// nearest in position. Shift (LB) and the symbols page (RB) are on while held, or switched on and
/// off by a tap.
/// </summary>
public sealed partial class OnScreenKeyboardViewModel : ViewModelBase
{
    private const double KeyWidth = 96;
    private const double KeyGap = 8;

    /// <summary>Held at least this long without typing, shift or the symbols go off on release instead of staying on.</summary>
    private static readonly TimeSpan HoldTime = TimeSpan.FromMilliseconds(500);

    // About as wide as the character rows: R3, L3, LB, Y, X, RB.
    private static readonly (SpecialKey Key, double Width, string Glyph, string Label)[] SpecialKeys =
    [
        (SpecialKey.Layout, 160, "R3", string.Empty),
        (SpecialKey.Filter, 180, "L3", "Filter"),
        (SpecialKey.Shift, 160, "LB", "⇧ Groß"),
        (SpecialKey.Space, 240, "Y", "Leertaste"),
        (SpecialKey.Erase, 180, "X", "Löschen"),
        (SpecialKey.Symbols, 160, "RB", string.Empty),
    ];

    private readonly Action<KeyViewModel> _press;
    private readonly Action<KeyboardLayout> _layoutChosen;
    private readonly LatchGesture _shift;
    private readonly LatchGesture _symbols;
    private int _row = 1;
    private int _column;
    private int _menuIndex;

    public OnScreenKeyboardViewModel(KeyboardLayout layout, Action<KeyViewModel> press, Action<KeyboardLayout> layoutChosen, TimeProvider time)
    {
        _press = press;
        _layoutChosen = layoutChosen;
        _shift = new LatchGesture(HoldTime, time);
        _shift.Changed += _ => ShowCase();
        _symbols = new LatchGesture(HoldTime, time);
        _symbols.Changed += _ => ShowPage();
        LayoutItems = KeyboardLayout.All.Select(l => new LayoutItemViewModel(l, Choose)).ToList();
        Layout = layout;
        BuildRows();
        SetFocus(_row, _column);
    }

    [ObservableProperty]
    public partial KeyboardLayout Layout { get; private set; }

    /// <summary>RB switched to the symbols page.</summary>
    public bool IsSymbols => _symbols.IsOn;

    public bool IsShifted => _shift.IsOn;

    public ObservableCollection<KeyRowViewModel> Rows { get; } = [];

    public IReadOnlyList<LayoutItemViewModel> LayoutItems { get; }

    [ObservableProperty]
    public partial bool IsLayoutMenuOpen { get; private set; }

    public KeyViewModel FocusedKey => Rows[_row].Keys[_column];

    /// <summary>Moves the focus; it stays at the keyboard's edges.</summary>
    public void Move(UiAction direction)
    {
        switch (direction)
        {
            case UiAction.Left:
                SetFocus(_row, Math.Max(0, _column - 1));
                break;
            case UiAction.Right:
                SetFocus(_row, Math.Min(Rows[_row].Keys.Count - 1, _column + 1));
                break;
            case UiAction.Up when _row > 0:
                SetFocus(_row - 1, Nearest(Rows[_row - 1], FocusedKey.Centre));
                break;
            case UiAction.Down when _row < Rows.Count - 1:
                SetFocus(_row + 1, Nearest(Rows[_row + 1], FocusedKey.Centre));
                break;
        }
    }

    /// <summary>What a press of <paramref name="key"/> types: its character, in upper case while shift is on.</summary>
    public string Typed(KeyViewModel key)
    {
        if (key.Character is not { } character)
        {
            return string.Empty;
        }

        _shift.Use();
        _symbols.Use();
        return (IsShifted ? char.ToUpper(character, Formatting.Culture) : character).ToString();
    }

    /// <summary>LB down: shift is on at once, so a letter typed while holding it is upper case.</summary>
    public void PressShift() => _shift.Press();

    /// <summary>LB up: off after a hold, switched after a tap.</summary>
    public void ReleaseShift() => _shift.Release();

    /// <summary>LB's release may never arrive, e.g. when the window loses focus.</summary>
    public void CancelShift() => _shift.Cancel();

    /// <summary>The shift key pressed with A or clicked.</summary>
    public void ToggleShift() => _shift.Toggle();

    /// <summary>RB down: the symbols show at once, so one typed while holding it is a symbol.</summary>
    public void PressSymbols() => _symbols.Press();

    /// <summary>RB up: back to the letters after a hold, switched after a tap.</summary>
    public void ReleaseSymbols() => _symbols.Release();

    /// <summary>RB's release may never arrive, e.g. when the window loses focus.</summary>
    public void CancelSymbols() => _symbols.Cancel();

    /// <summary>The symbols key pressed with A or clicked.</summary>
    public void ToggleSymbols() => _symbols.Toggle();

    public void ToggleLayoutMenu()
    {
        if (IsLayoutMenuOpen)
        {
            CloseLayoutMenu();
            return;
        }

        _menuIndex = KeyboardLayout.All.ToList().IndexOf(Layout);
        UpdateMenuFocus();
        IsLayoutMenuOpen = true;
    }

    public void CloseLayoutMenu()
    {
        IsLayoutMenuOpen = false;
        UpdateMenuFocus();
    }

    /// <summary>Up/down move through the open layout menu, A picks.</summary>
    public void HandleLayoutMenu(UiAction action)
    {
        switch (action)
        {
            case UiAction.Up:
                _menuIndex = Math.Max(0, _menuIndex - 1);
                UpdateMenuFocus();
                break;
            case UiAction.Down:
                _menuIndex = Math.Min(LayoutItems.Count - 1, _menuIndex + 1);
                UpdateMenuFocus();
                break;
            case UiAction.Accept:
                Choose(LayoutItems[_menuIndex].Layout);
                break;
        }
    }

    private void Choose(KeyboardLayout layout)
    {
        CloseLayoutMenu();
        if (layout == Layout && !IsSymbols)
        {
            return;
        }

        // Picking a layout on the symbols page goes back to the letters, so the choice shows.
        var changed = layout != Layout;
        Layout = layout;
        if (IsSymbols)
        {
            _symbols.Reset();
        }
        else
        {
            ShowPage();
        }

        if (changed)
        {
            _layoutChosen(layout);
        }
    }

    private void UpdateMenuFocus()
    {
        for (var i = 0; i < LayoutItems.Count; i++)
        {
            LayoutItems[i].IsFocused = IsLayoutMenuOpen && i == _menuIndex;
            LayoutItems[i].IsSelected = LayoutItems[i].Layout == Layout;
        }
    }

    partial void OnLayoutChanged(KeyboardLayout value)
    {
        foreach (var item in LayoutItems)
        {
            item.IsSelected = item.Layout == value;
        }
    }

    private void ShowPage()
    {
        OnPropertyChanged(nameof(IsSymbols));
        BuildRows();
        SetFocus(_row, _column);
    }

    private void BuildRows()
    {
        Rows.Clear();
        foreach (var characters in (IsSymbols ? KeyboardLayout.Symbols : Layout).Rows)
        {
            var left = -RowWidth(characters.Select(_ => KeyWidth)) / 2;
            var keys = new List<KeyViewModel>();
            foreach (var character in characters)
            {
                keys.Add(new KeyViewModel(character.ToString(), KeyWidth, left + (KeyWidth / 2), _press, character: character));
                left += KeyWidth + KeyGap;
            }

            Rows.Add(new KeyRowViewModel(keys));
        }

        var x = -RowWidth(SpecialKeys.Select(k => k.Width)) / 2;
        var special = new List<KeyViewModel>();
        foreach (var (key, width, glyph, label) in SpecialKeys)
        {
            special.Add(new KeyViewModel(label, width, x + (width / 2), _press, special: key, glyph: glyph));
            x += width + KeyGap;
        }

        Rows.Add(new KeyRowViewModel(special));
        _column = Math.Min(_column, Rows[_row].Keys.Count - 1);
        ShowCase();
    }

    /// <summary>The labels follow shift and the page: upper-case letters, "ABC" back from the symbols.</summary>
    private void ShowCase()
    {
        OnPropertyChanged(nameof(IsShifted));
        foreach (var key in Rows.SelectMany(r => r.Keys))
        {
            switch (key.Special)
            {
                case null:
                    key.Label = (IsShifted && !IsSymbols ? char.ToUpper(key.Character!.Value, Formatting.Culture) : key.Character!.Value).ToString();
                    break;
                case SpecialKey.Layout:
                    key.Label = Layout.Code;
                    break;
                case SpecialKey.Shift:
                    key.IsOn = IsShifted;
                    break;
                case SpecialKey.Symbols:
                    key.Label = IsSymbols ? "ABC" : KeyboardLayout.Symbols.Code;
                    key.IsOn = IsSymbols;
                    break;
            }
        }
    }

    private static double RowWidth(IEnumerable<double> widths)
    {
        var list = widths.ToList();
        return list.Sum() + (KeyGap * (list.Count - 1));
    }

    private static int Nearest(KeyRowViewModel row, double centre)
    {
        var best = 0;
        for (var i = 1; i < row.Keys.Count; i++)
        {
            if (Math.Abs(row.Keys[i].Centre - centre) < Math.Abs(row.Keys[best].Centre - centre))
            {
                best = i;
            }
        }

        return best;
    }

    private void SetFocus(int row, int column)
    {
        foreach (var key in Rows.SelectMany(r => r.Keys).Where(k => k.IsFocused))
        {
            key.IsFocused = false;
        }

        _row = row;
        _column = column;
        FocusedKey.IsFocused = true;
    }
}
