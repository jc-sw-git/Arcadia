using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Arcadia.App.Views;

public partial class MainWindow : Window
{
    private const double TabScrollStep = 320;
    private const double TabEdgePadding = 96;

    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Tunnel so arrow keys and Enter reach us before Avalonia's own focus navigation.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        GameGrid.AddHandler(DoubleTappedEvent, OnGridDoubleTapped);
        TabScroller.ScrollChanged += (_, _) => UpdateTabEdges();
        TabScroller.SizeChanged += (_, _) => UpdateTabEdges();
        TabScrollLeft.Click += (_, _) => ScrollTabs(-TabScrollStep);
        TabScrollRight.Click += (_, _) => ScrollTabs(TabScrollStep);
    }

    /// <summary>
    /// Takes the foreground back, e.g. after a launcher started in the background grabbed it.
    /// Briefly toggling Topmost gets past Windows' focus-stealing protection more reliably than Activate alone.
    /// </summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Topmost = true;
        Activate();
        Topmost = false;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
            e.Handled = true;
            return;
        }

        if (_viewModel is not null && UiActionMap.FromKey(e.Key, e.KeyModifiers) is { } action)
        {
            Services.AppLog.Write($"Taste: {e.Key} → {action}");
            _viewModel.Handle(action);
            e.Handled = true;
        }
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is GameTileViewModel tile)
        {
            tile.ActivateCommand.Execute(null);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.FocusedGame):
                // Wait for the grid to realize the new page before scrolling to the tile.
                Dispatcher.UIThread.Post(BringFocusedTileIntoView, DispatcherPriority.Loaded);
                break;
            case nameof(MainViewModel.SelectedTab):
                Dispatcher.UIThread.Post(BringSelectedTabIntoView, DispatcherPriority.Loaded);
                break;
        }
    }

    private void BringFocusedTileIntoView()
    {
        if (_viewModel?.FocusedGame is not { } tile)
        {
            return;
        }

        var index = _viewModel.PageGames.IndexOf(tile);
        if (index == 0)
        {
            GridScroller.Offset = default;
        }
        else if (index > 0)
        {
            GameGrid.ContainerFromIndex(index)?.BringIntoView();
        }
    }

    private void BringSelectedTabIntoView()
    {
        if (_viewModel?.SelectedTab is not { } tab || TabList.ContainerFromItem(tab) is not { } container)
        {
            return;
        }

        var left = container.TranslatePoint(default, TabList)?.X ?? 0;
        var right = left + container.Bounds.Width;
        var offset = TabScroller.Offset.X;
        var viewport = TabScroller.Viewport.Width;

        if (left < offset + TabEdgePadding)
        {
            offset = left - TabEdgePadding;
        }
        else if (right > offset + viewport - TabEdgePadding)
        {
            offset = right - viewport + TabEdgePadding;
        }

        SetTabOffset(offset);
    }

    private void ScrollTabs(double delta) => SetTabOffset(TabScroller.Offset.X + delta);

    private void SetTabOffset(double offset)
    {
        var max = Math.Max(0, TabScroller.Extent.Width - TabScroller.Viewport.Width);
        TabScroller.Offset = new Vector(Math.Clamp(offset, 0, max), 0);
        Dispatcher.UIThread.Post(UpdateTabEdges, DispatcherPriority.Background);
    }

    private void UpdateTabEdges()
    {
        var offset = TabScroller.Offset.X;
        var max = Math.Max(0, TabScroller.Extent.Width - TabScroller.Viewport.Width);
        TabEdgeLeft.IsVisible = offset > 1;
        TabEdgeRight.IsVisible = offset < max - 1;
    }
}
