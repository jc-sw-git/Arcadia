using Arcadia.App.ViewModels;
using Arcadia.Core.Search;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Keyboard;

/// <summary>One key of an on-screen keyboard, placed on its canvas in the design's pixels.</summary>
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

    /// <summary>A key with a name shows its symbol at the left edge; one without (the arrows) in the middle, as its label.</summary>
    public bool HasSymbolBesideName => HasSymbol && Text.Length > 0;

    public bool HasSymbolAlone => HasSymbol && Text.Length == 0;

    /// <summary>The symbol printed on a real key, e.g. ⌫; empty for the right block's own keys, which have icons.</summary>
    public string Symbol { get; }

    /// <summary>The key's name beside its symbol; the layout and dock keys change theirs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSymbolBesideName), nameof(HasSymbolAlone))]
    public partial string Text { get; set; }

    /// <summary>The controller button that does the same, shown on the key while the controller is in use.</summary>
    public string? Glyph { get; }

    public bool HasGlyph => Glyph is not null;

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

    /// <summary>Greyed out: switched off by the keyboard's user (<see cref="BoardKeyState.Disabled"/>), or AltGr for a layout without that level.</summary>
    [ObservableProperty]
    public partial bool IsUnavailable { get; set; }

    /// <summary>Left out by the keyboard's user (<see cref="BoardKeyState.Hidden"/>); its place stays empty, so the others don't move.</summary>
    [ObservableProperty]
    public partial bool IsHidden { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>The dock key: the keyboard is at the bottom, so the key moves it up (its icon's arrow points up).</summary>
    [ObservableProperty]
    public partial bool PointsUp { get; set; } = true;

    /// <summary>The focus passes over it: caps lock, a key greyed out or left out, a character key empty on the shown level.</summary>
    public bool IsSkipped => IsBlank || IsUnavailable || IsHidden || (IsCharacter && Label.Length == 0);

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
