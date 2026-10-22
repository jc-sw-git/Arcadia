using Arcadia.App.ViewModels;
using Arcadia.Core.Search;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Keyboard;

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
/// The layout menu (R3) of the on-screen keyboard (<see cref="OnScreenKeyboardViewModel"/>): all layouts,
/// the chosen one ticked. Up and down move through it, A picks; a pick closes it.
/// </summary>
public sealed partial class LayoutMenuViewModel : ViewModelBase
{
    private readonly Action<KeyboardLayout> _pick;
    private KeyboardLayout _selected;
    private int _focus;

    /// <param name="pick">A layout was picked, by the controller or a click; the menu is closed by then.</param>
    public LayoutMenuViewModel(KeyboardLayout selected, Action<KeyboardLayout> pick)
    {
        _pick = pick;
        _selected = selected;
        Items = KeyboardLayout.All.Select(l => new LayoutItemViewModel(l, Pick)).ToList();
        UpdateItems();
    }

    public IReadOnlyList<LayoutItemViewModel> Items { get; }

    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>The ticked layout, e.g. after another keyboard changed it.</summary>
    public KeyboardLayout Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            UpdateItems();
        }
    }

    /// <summary>Opens it with the focus on the chosen layout, or closes it.</summary>
    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        _focus = KeyboardLayout.All.ToList().IndexOf(_selected);
        IsOpen = true;
        UpdateItems();
    }

    public void Close()
    {
        IsOpen = false;
        UpdateItems();
    }

    public void Up() => MoveFocus(-1);

    public void Down() => MoveFocus(1);

    /// <summary>Picks the focused layout.</summary>
    public void Accept() => Pick(Items[_focus].Layout);

    private void MoveFocus(int step)
    {
        _focus = Math.Clamp(_focus + step, 0, Items.Count - 1);
        UpdateItems();
    }

    private void Pick(KeyboardLayout layout)
    {
        Close();
        _pick(layout);
    }

    private void UpdateItems()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            Items[i].IsFocused = IsOpen && i == _focus;
            Items[i].IsSelected = Items[i].Layout == _selected;
        }
    }
}
