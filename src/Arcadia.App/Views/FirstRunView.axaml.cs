using Arcadia.App.Setup;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Arcadia.App.Views;

public partial class FirstRunView : UserControl
{
    /// <summary>Room around a row for its focus ring when it is brought into view.</summary>
    private const double FocusRingRoom = 8;

    /// <summary>Room above a group's first row for the group's heading.</summary>
    private const double GroupHeadingRoom = 48;

    /// <summary>
    /// Windows also reports a pointer "move" when only the content under a resting pointer changes, e.g. the rows scrolled
    /// under it; a few pixels don't count as the mouse moving.
    /// </summary>
    private const double PointerMoveThreshold = 6;

    private FirstRunViewModel? _viewModel;
    private Point? _rowPointer;

    /// <summary>The mouse focused the row: it is under the pointer already, so the rows don't scroll to it.</summary>
    private bool _focusedByPointer;

    public FirstRunView()
    {
        InitializeComponent();
        RowsScroller.AddHandler(PointerMovedEvent, OnRowPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.FocusMoved -= OnFocusMoved;
        }

        _viewModel = DataContext as FirstRunViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusMoved += OnFocusMoved;
        }
    }

    /// <summary>The mouse over a row focuses it, so the info panel explains it before a click changes it.</summary>
    private void OnRowPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(this);
        if (_rowPointer is { } last && Math.Abs(position.X - last.X) + Math.Abs(position.Y - last.Y) <= PointerMoveThreshold)
        {
            return;
        }

        _rowPointer = position;
        if ((e.Source as StyledElement)?.DataContext is FirstRunLauncherViewModel { IsFocused: false } row)
        {
            _focusedByPointer = true;
            row.Point();
        }
    }

    /// <summary>A launcher further down (more than fit, e.g. with plugins) comes into view with the controller or the keys.</summary>
    private void OnFocusMoved(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        var byPointer = _focusedByPointer;
        _focusedByPointer = false;
        if (byPointer || _viewModel?.FocusedRow is not { } row)
        {
            return;
        }

        if ((FoundRows.ContainerFromItem(row) ?? MissingRows.ContainerFromItem(row)) is { } container)
        {
            var bounds = new Rect(container.Bounds.Size).Inflate(FocusRingRoom);
            container.BringIntoView(row.StartsGroup ? bounds.Inflate(new Thickness(0, GroupHeadingRoom, 0, 0)) : bounds);
        }
    }, DispatcherPriority.Loaded);
}
