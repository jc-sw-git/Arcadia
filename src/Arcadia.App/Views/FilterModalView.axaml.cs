using Arcadia.App.Filter;
using Avalonia.Controls;
using Avalonia.Input;

namespace Arcadia.App.Views;

/// <summary>"Filter" (#44, #466): everything is bound; focus and choice live in <see cref="FilterModalViewModel"/>.</summary>
public partial class FilterModalView : UserControl
{
    /// <summary>Rows a notch of the wheel moves the list.</summary>
    private const int RowsPerNotch = 3;

    public FilterModalView()
    {
        InitializeComponent();
    }

    // The list shows a window of its rows that the view model moves, so there is no ScrollViewer to take the wheel.
    private void OnListWheel(object? sender, PointerWheelEventArgs e)
    {
        if (sender is Control { DataContext: FilterListPane pane } && e.Delta.Y != 0)
        {
            pane.Scroll(e.Delta.Y > 0 ? -RowsPerNotch : RowsPerNotch);
            e.Handled = true;
        }
    }
}
