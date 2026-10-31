using Arcadia.App.Settings;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Arcadia.App.Views;

/// <summary>"Controller zuordnen" (#238 D3a): keeps the focused row in sight and fades the list where more rows follow.</summary>
public partial class ControllerLayoutsView : UserControl
{
    /// <summary>Room around a row for its focus ring, and past the fades, when it is brought into view.</summary>
    private const double FocusRoom = 8;

    /// <summary>As in the settings: a few pixels of "move" under a resting pointer don't count (the rows scrolled under it).</summary>
    private const double PointerMoveThreshold = 6;

    private ControllerLayoutsViewModel? _viewModel;
    private Point? _rowPointer;
    private bool _focusedByPointer;

    public ControllerLayoutsView()
    {
        InitializeComponent();
        RowsScroller.ScrollChanged += (_, _) => UpdateFades();
        RowsScroller.AddHandler(PointerMovedEvent, OnRowPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.FocusMoved -= OnFocusMoved;
        }

        _viewModel = DataContext as ControllerLayoutsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusMoved += OnFocusMoved;
            OnFocusMoved(_viewModel, EventArgs.Empty);
        }
    }

    /// <summary>The mouse over a row focuses it, as in the settings.</summary>
    private void OnRowPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(this);
        if (_rowPointer is { } last && Math.Abs(position.X - last.X) + Math.Abs(position.Y - last.Y) <= PointerMoveThreshold)
        {
            return;
        }

        _rowPointer = position;
        if ((e.Source as StyledElement)?.DataContext is ControllerLayoutRowViewModel { IsFocused: false } row)
        {
            _focusedByPointer = true;
            row.PointCommand.Execute(null);
        }
    }

    private void OnFocusMoved(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        var byPointer = _focusedByPointer;
        _focusedByPointer = false;
        if (!byPointer && _viewModel?.FocusedRow is { } row && LayoutRows.ContainerFromItem(row) is { } container)
        {
            container.BringIntoView(new Rect(container.Bounds.Size).Inflate(new Thickness(0, FocusRoom + FadeBottom.Height / 2)));
        }

        UpdateFades();
    }, DispatcherPriority.Loaded);

    private void UpdateFades()
    {
        var offset = RowsScroller.Offset.Y;
        var max = RowsScroller.Extent.Height - RowsScroller.Viewport.Height;
        FadeTop.Classes.Set("shown", offset > 1);
        FadeBottom.Classes.Set("shown", offset < max - 1);
    }
}
