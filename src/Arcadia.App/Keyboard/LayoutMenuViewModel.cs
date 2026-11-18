using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Navigation;
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
/// The layout menu (R3) of the on-screen keyboard (<see cref="OnScreenKeyboardViewModel"/>): the layouts of the quick
/// selection (#553) in the order of the settings, the one in use ticked. Up and down move through it, A picks; a pick closes it.
/// </summary>
public sealed partial class LayoutMenuViewModel : ViewModelBase
{
    private readonly Action<KeyboardLayout> _pick;
    private KeyboardLayout _selected;
    private int _focus;

    /// <param name="pick">A layout was picked, by the controller or a click; the menu is closed by then.</param>
    public LayoutMenuViewModel(KeyboardLayout selected, IReadOnlyList<KeyboardLayout> offered, Action<KeyboardLayout> pick)
    {
        _pick = pick;
        _selected = selected;
        Offer(offered);
    }

    [ObservableProperty]
    public partial IReadOnlyList<LayoutItemViewModel> Items { get; private set; } = [];

    /// <summary>The layouts it lists from now on; rebuilt only when they changed, so the menu's controls stay.</summary>
    public void Offer(IReadOnlyList<KeyboardLayout> offered)
    {
        if (!Items.Select(item => item.Layout).SequenceEqual(offered))
        {
            Items = [.. offered.Select(l => new LayoutItemViewModel(l, Pick))];
        }

        UpdateItems();
    }

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

        _focus = Math.Max(0, Items.Select(item => item.Layout).ToList().IndexOf(_selected));
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
        _focus = ListFocus.Cycle(_focus, step, Items.Count, HeldPress.IsRepeat);
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
