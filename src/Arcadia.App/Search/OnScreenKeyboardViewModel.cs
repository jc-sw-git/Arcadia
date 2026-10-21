using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Search;
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

    /// <summary>LB: the shift level, tapped or held.</summary>
    Shift,

    /// <summary>Y.</summary>
    Space,

    /// <summary>X: deletes the last character, held: one after another.</summary>
    Erase,

    /// <summary>RB: the AltGr level, tapped or held; greyed out for a layout without one.</summary>
    AltGr,
}

/// <summary>One key of the on-screen keyboard.</summary>
public sealed partial class KeyViewModel : ViewModelBase
{
    public KeyViewModel(string label, double width, double centre, Action<KeyViewModel> press, KeyCap? cap = null, SpecialKey? special = null, string? glyph = null)
    {
        Label = label;
        Width = width;
        Centre = centre;
        Cap = cap;
        Special = special;
        Glyph = glyph;
        PressCommand = new RelayCommand(() => press(this));
    }

    /// <summary>What a press types on the shown level, the name of a special key; empty for a key that types nothing.</summary>
    [ObservableProperty]
    public partial string Label { get; set; }

    /// <summary>Drawn small above <see cref="Label"/> on the normal level: the shift character, as printed on the key.</summary>
    [ObservableProperty]
    public partial string? ShiftHint { get; set; }

    /// <summary>Drawn small at the bottom right on the normal level: the AltGr character.</summary>
    [ObservableProperty]
    public partial string? AltGrHint { get; set; }

    public double Width { get; }

    /// <summary>Horizontal centre relative to the keyboard's middle; up and down go to the nearest key.</summary>
    public double Centre { get; }

    /// <summary>What a character key types on each level; null for a special key.</summary>
    public KeyCap? Cap { get; }

    public SpecialKey? Special { get; }

    /// <summary>The gamepad button that does the same, shown on the key.</summary>
    public string? Glyph { get; }

    public bool HasGlyph => Glyph is not null;

    /// <summary>Single-letter glyphs (X, Y) are round, like on the pad.</summary>
    public bool IsRoundGlyph => Glyph is { Length: 1 };

    public bool IsLayoutKey => Special == SpecialKey.Layout;

    public bool IsShiftKey => Special == SpecialKey.Shift;

    public bool IsWide => Special is not null;

    /// <summary>A switch that is on: shift or AltGr.</summary>
    [ObservableProperty]
    public partial bool IsOn { get; set; }

    /// <summary>Greyed out: AltGr for a layout without that level.</summary>
    [ObservableProperty]
    public partial bool IsUnavailable { get; set; }

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
/// The on-screen keyboard of the search: the character rows of the chosen real layout and a row of
/// special keys. The keys show what they type on the shown level: on the normal one also the shift
/// and AltGr characters small, as printed on a real key; with shift (LB) or AltGr (RB) only that
/// level's characters. Both are on while held, or switched on and off by a tap, and only one at a
/// time. The D-pad moves the focus over the keys that type something (<see cref="KeyGrid"/>).
/// </summary>
public sealed partial class OnScreenKeyboardViewModel : ViewModelBase
{
    /// <summary>13 keys, the longest row, fit the width the keyboard had before (owner, 2026-10-21).</summary>
    private const double KeyWidth = 80;
    private const double KeyGap = 8;

    /// <summary>Held at least this long without typing, shift or AltGr go off on release instead of staying on.</summary>
    private static readonly TimeSpan HoldTime = TimeSpan.FromMilliseconds(500);

    // About as wide as the character rows: R3, L3, LB, Y, X, RB.
    private static readonly (SpecialKey Key, double Width, string Glyph, string Label)[] SpecialKeys =
    [
        (SpecialKey.Layout, 160, "R3", string.Empty),
        (SpecialKey.Filter, 180, "L3", "Filter"),
        (SpecialKey.Shift, 160, "LB", "⇧ Groß"),
        (SpecialKey.Space, 240, "Y", "Leertaste"),
        (SpecialKey.Erase, 180, "X", "Löschen"),
        (SpecialKey.AltGr, 160, "RB", "AltGr"),
    ];

    private readonly Action<KeyViewModel> _press;
    private readonly Action<KeyboardLayout> _layoutChosen;
    private readonly LatchGesture _shift;
    private readonly LatchGesture _altGr;
    private int _row = 1;
    private int _column;
    private int _menuIndex;

    public OnScreenKeyboardViewModel(KeyboardLayout layout, Action<KeyViewModel> press, Action<KeyboardLayout> layoutChosen, TimeProvider time)
    {
        _press = press;
        _layoutChosen = layoutChosen;
        _shift = new LatchGesture(HoldTime, time);
        _shift.Changed += _ => ShowLevel();
        _altGr = new LatchGesture(HoldTime, time);
        _altGr.Changed += _ => ShowLevel();
        LayoutItems = KeyboardLayout.All.Select(l => new LayoutItemViewModel(l, Choose)).ToList();
        Layout = layout;
        BuildRows();
        SetFocus(_row, _column);
    }

    [ObservableProperty]
    public partial KeyboardLayout Layout { get; private set; }

    /// <summary>The level the keys show and type; AltGr wins should both be on for a moment.</summary>
    public KeyLevel Level => _altGr.IsOn ? KeyLevel.AltGr : _shift.IsOn ? KeyLevel.Shift : KeyLevel.Normal;

    public ObservableCollection<KeyRowViewModel> Rows { get; } = [];

    public IReadOnlyList<LayoutItemViewModel> LayoutItems { get; }

    [ObservableProperty]
    public partial bool IsLayoutMenuOpen { get; private set; }

    public KeyViewModel FocusedKey => Rows[_row].Keys[_column];

    /// <summary>Moves the focus over the keys that type something; it stays at the keyboard's edges.</summary>
    public void Move(UiAction direction)
    {
        switch (direction)
        {
            case UiAction.Left or UiAction.Right:
                SetFocus(_row, KeyGrid.Sideways(Spots(Rows[_row]), _column, direction == UiAction.Left ? -1 : 1));
                break;
            case UiAction.Up or UiAction.Down:
                var (row, column) = KeyGrid.Vertical(Rows.Select(Spots).ToList(), _row, _column, direction == UiAction.Up ? -1 : 1);
                SetFocus(row, column);
                break;
        }
    }

    /// <summary>What a press of <paramref name="key"/> types: its character on the shown level, nothing for an empty key.</summary>
    public string Typed(KeyViewModel key)
    {
        if (key.Cap?.On(Level) is not { } character)
        {
            return string.Empty;
        }

        _shift.Use();
        _altGr.Use();
        return character.ToString();
    }

    /// <summary>LB down: shift is on at once, so a key pressed while holding it types its shift character.</summary>
    public void PressShift()
    {
        _altGr.Reset();
        _shift.Press();
    }

    /// <summary>LB up: off after a hold, switched after a tap.</summary>
    public void ReleaseShift() => _shift.Release();

    /// <summary>LB's release may never arrive, e.g. when the window loses focus.</summary>
    public void CancelShift() => _shift.Cancel();

    /// <summary>The shift key pressed with A or clicked.</summary>
    public void ToggleShift()
    {
        _altGr.Reset();
        _shift.Toggle();
    }

    /// <summary>RB down: AltGr is on at once, so a key pressed while holding it types its AltGr character.</summary>
    public void PressAltGr()
    {
        if (Layout.HasAltGr)
        {
            _shift.Reset();
            _altGr.Press();
        }
    }

    /// <summary>RB up: off after a hold, switched after a tap.</summary>
    public void ReleaseAltGr() => _altGr.Release();

    /// <summary>RB's release may never arrive, e.g. when the window loses focus.</summary>
    public void CancelAltGr() => _altGr.Cancel();

    /// <summary>The AltGr key pressed with A or clicked.</summary>
    public void ToggleAltGr()
    {
        if (Layout.HasAltGr)
        {
            _shift.Reset();
            _altGr.Toggle();
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
        if (!layout.HasAltGr)
        {
            _altGr.Reset();
        }

        BuildRows();
        SetFocus(_row, _column);
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
        Rows.Clear();
        foreach (var caps in Layout.Rows)
        {
            var left = -RowWidth(caps.Select(_ => KeyWidth)) / 2;
            var keys = new List<KeyViewModel>();
            foreach (var cap in caps)
            {
                keys.Add(new KeyViewModel(string.Empty, KeyWidth, left + (KeyWidth / 2), _press, cap: cap));
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
        ShowLevel();
    }

    /// <summary>
    /// The labels follow the level like on Steam's keyboard: on the normal level the character with the
    /// shift and AltGr ones small beside it, on the others only that level's character, or nothing.
    /// </summary>
    private void ShowLevel()
    {
        OnPropertyChanged(nameof(Level));
        var level = Level;
        foreach (var key in Rows.SelectMany(r => r.Keys))
        {
            switch (key.Special)
            {
                case null:
                    key.Label = key.Cap!.On(level)?.ToString() ?? string.Empty;
                    key.ShiftHint = level == KeyLevel.Normal ? key.Cap.ShiftHint?.ToString() : null;
                    key.AltGrHint = level == KeyLevel.Normal ? key.Cap.AltGr?.ToString() : null;
                    break;
                case SpecialKey.Layout:
                    key.Label = Layout.Code;
                    break;
                case SpecialKey.Shift:
                    key.IsOn = level == KeyLevel.Shift;
                    break;
                case SpecialKey.AltGr:
                    key.IsOn = level == KeyLevel.AltGr;
                    key.IsUnavailable = !Layout.HasAltGr;
                    break;
            }
        }
    }

    private static List<KeySpot> Spots(KeyRowViewModel row) => row.Keys.Select(k => new KeySpot(k.Centre, k.Cap is not null && k.Label.Length == 0)).ToList();

    private static double RowWidth(IEnumerable<double> widths)
    {
        var list = widths.ToList();
        return list.Sum() + (KeyGap * (list.Count - 1));
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
