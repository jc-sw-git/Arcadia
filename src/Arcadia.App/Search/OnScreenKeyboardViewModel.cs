using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Search;

/// <summary>The keys of the bottom row; each also has its own gamepad button.</summary>
public enum SpecialKey
{
    /// <summary>R3: the layout menu.</summary>
    Layout,

    /// <summary>LT: starts a <c>/launcher</c> filter.</summary>
    Slash,

    /// <summary>Y.</summary>
    Space,

    /// <summary>X: deletes the last character.</summary>
    Erase,

    /// <summary>≡: on to the results.</summary>
    Submit,
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

    public string Label { get; }

    public double Width { get; }

    /// <summary>Horizontal centre relative to the keyboard's middle; up and down go to the nearest key.</summary>
    public double Centre { get; }

    public char? Character { get; }

    public SpecialKey? Special { get; }

    /// <summary>The gamepad button that does the same, shown on the key.</summary>
    public string? Glyph { get; }

    public bool HasGlyph => Glyph is not null;

    /// <summary>Single-letter glyphs (X, Y) are round, like on the pad.</summary>
    public bool IsRoundGlyph => Glyph is { Length: 1 } and not "≡";

    public bool IsLayoutKey => Special == SpecialKey.Layout;

    public bool IsPrimary => Special == SpecialKey.Submit;

    public bool IsWide => Special is not null;

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
/// The on-screen keyboard of the search: four rows of characters from the chosen layout and a
/// row of special keys. The D-pad moves the focus; up and down go to the key nearest in position.
/// </summary>
public sealed partial class OnScreenKeyboardViewModel : ViewModelBase
{
    private const double KeyWidth = 96;
    private const double KeyGap = 8;

    // Widths from the design: R3, LT, Y, X, ≡.
    private static readonly (SpecialKey Key, double Width, string Glyph, string Label)[] SpecialKeys =
    [
        (SpecialKey.Layout, 160, "R3", string.Empty),
        (SpecialKey.Slash, 160, "LT", "/ Filter"),
        (SpecialKey.Space, 312, "Y", "Leertaste"),
        (SpecialKey.Erase, 200, "X", "Löschen"),
        (SpecialKey.Submit, 248, "≡", "Suchen"),
    ];

    private readonly Action<KeyViewModel> _press;
    private readonly Action<KeyboardLayout> _layoutChosen;
    private int _row = 1;
    private int _column;
    private int _menuIndex;

    public OnScreenKeyboardViewModel(KeyboardLayout layout, Action<KeyViewModel> press, Action<KeyboardLayout> layoutChosen)
    {
        _press = press;
        _layoutChosen = layoutChosen;
        LayoutItems = KeyboardLayout.All.Select(l => new LayoutItemViewModel(l, Choose)).ToList();
        Layout = layout;
        BuildRows();
    }

    [ObservableProperty]
    public partial KeyboardLayout Layout { get; private set; }

    public ObservableCollection<KeyRowViewModel> Rows { get; } = [];

    public IReadOnlyList<LayoutItemViewModel> LayoutItems { get; }

    [ObservableProperty]
    public partial bool IsLayoutMenuOpen { get; private set; }

    /// <summary>Null while the focus is outside the keyboard.</summary>
    public KeyViewModel? FocusedKey { get; private set; }

    /// <summary>Puts the focus on the keyboard, on the key it had last.</summary>
    public void Enter() => SetFocus(_row, _column);

    public void Leave() => SetFocus(-1, -1);

    /// <summary>Moves the focus; false when it would leave the keyboard upwards.</summary>
    public bool Move(UiAction direction)
    {
        switch (direction)
        {
            case UiAction.Left:
                SetFocus(_row, Math.Max(0, _column - 1));
                return true;
            case UiAction.Right:
                SetFocus(_row, Math.Min(Rows[_row].Keys.Count - 1, _column + 1));
                return true;
            case UiAction.Up when _row == 0:
                return false;
            case UiAction.Up:
                SetFocus(_row - 1, Nearest(Rows[_row - 1], Rows[_row].Keys[_column].Centre));
                return true;
            case UiAction.Down when _row < Rows.Count - 1:
                SetFocus(_row + 1, Nearest(Rows[_row + 1], Rows[_row].Keys[_column].Centre));
                return true;
            default:
                return true;
        }
    }

    public void PressFocused()
    {
        if (FocusedKey is { } key)
        {
            _press(key);
        }
    }

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
        if (layout == Layout)
        {
            return;
        }

        Layout = layout;
        var focused = FocusedKey is not null;
        BuildRows();
        if (focused)
        {
            Enter();
        }

        _layoutChosen(layout);
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

    private void BuildRows()
    {
        Leave();
        Rows.Clear();
        foreach (var characters in Layout.Rows)
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
            special.Add(new KeyViewModel(key == SpecialKey.Layout ? Layout.Code : label, width, x + (width / 2), _press, special: key, glyph: glyph));
            x += width + KeyGap;
        }

        Rows.Add(new KeyRowViewModel(special));
        _column = Math.Min(_column, Rows[_row].Keys.Count - 1);
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
        if (FocusedKey is { } previous)
        {
            previous.IsFocused = false;
        }

        FocusedKey = row >= 0 && row < Rows.Count && column >= 0 && column < Rows[row].Keys.Count ? Rows[row].Keys[column] : null;
        if (FocusedKey is { } key)
        {
            _row = row;
            _column = column;
            key.IsFocused = true;
        }
    }
}
