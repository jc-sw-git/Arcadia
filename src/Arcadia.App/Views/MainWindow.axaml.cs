using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Arcadia.App.Views;

public partial class MainWindow : Window
{
    private const double TabScrollStep = 320;
    private const double TabEdgePadding = 96;

    /// <summary>Space between a cover and its context menu, and between the menu and the window's edge.</summary>
    private const double MenuGap = 24;

    /// <summary>Height of the fades at the grid's edges (Border.listFade in Theme.axaml).</summary>
    private const double ListFadeHeight = 96;

    private readonly HashSet<Key> _keysDown = [];
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        // Tunnel so arrow keys and Enter reach us before Avalonia's own focus navigation.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
        GameGrid.AddHandler(DoubleTappedEvent, OnGridDoubleTapped);
        GameGrid.AddHandler(PointerPressedEvent, OnGridPointerPressed, RoutingStrategies.Tunnel);

        // A key released in another window never reports its release here.
        Deactivated += (_, _) =>
        {
            _keysDown.Clear();
            _viewModel?.CancelHold();
        };
        GridScroller.ScrollChanged += (_, _) => UpdateGridFades();
        TabScroller.ScrollChanged += (_, _) => UpdateTabEdges();
        TabScroller.SizeChanged += (_, _) => UpdateTabEdges();
        TabScrollLeft.Click += (_, _) => ScrollTabs(-TabScrollStep);
        TabScrollRight.Click += (_, _) => ScrollTabs(TabScrollStep);

        // Coming back from a launcher: games may have been installed or uninstalled there.
        Activated += async (_, _) =>
        {
            if (_viewModel is not null)
            {
                await _viewModel.RefreshLibraryAsync();
            }
        };
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
            // A held key repeats its KeyDown; A acts on the release or after a hold, like the gamepad button.
            if (!_keysDown.Add(e.Key) && action == UiAction.Accept)
            {
                e.Handled = true;
                return;
            }

            Services.AppLog.Write($"Taste: {e.Key} → {action}");
            _viewModel.Handle(action);
            e.Handled = true;
            if (_viewModel.IsSettingsOpen)
            {
                Dispatcher.UIThread.Post(BringFocusedSettingIntoView, DispatcherPriority.Loaded);
            }
        }
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        _keysDown.Remove(e.Key);
        if (_viewModel is not null && UiActionMap.FromKey(e.Key, e.KeyModifiers) is { } action)
        {
            _viewModel.Release(action);
        }
    }

    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed
            && (e.Source as Control)?.DataContext is GameTileViewModel tile
            && _viewModel is not null)
        {
            _viewModel.OpenMenuFor(tile);
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
            case nameof(MainViewModel.GameMenu) when _viewModel?.GameMenu is not null:
                // Hidden until placed, so it doesn't flash at its default position.
                GameMenuPanel.Opacity = 0;
                Dispatcher.UIThread.Post(PlaceGameMenu, DispatcherPriority.Loaded);
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
        else if (index > 0 && GameGrid.ContainerFromIndex(index) is { } cover)
        {
            // Keeps the focused cover clear of the edge fades; at the ends the scroll range stops it.
            cover.BringIntoView(new Rect(cover.Bounds.Size).Inflate(new Thickness(0, ListFadeHeight)));
        }
    }

    /// <summary>A fade shows only at an edge the grid can still scroll towards (#102).</summary>
    private void UpdateGridFades()
    {
        var offset = GridScroller.Offset.Y;
        var max = GridScroller.Extent.Height - GridScroller.Viewport.Height;
        GridFadeTop.Classes.Set("shown", offset > 1);
        GridFadeBottom.Classes.Set("shown", offset < max - 1);
    }

    /// <summary>
    /// Scrolls the settings to the item the controller focus is on. A whole section is shown when
    /// it fits, so e.g. the changelog below "Jetzt aktualisieren" comes into view with it.
    /// </summary>
    private void BringFocusedSettingIntoView()
    {
        // Settings items carry the "focused" class; the dialog's scroll viewer is their nearest one.
        var focused = this.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Classes.Contains("focused") && b.IsEffectivelyVisible && b.DataContext is SettingsViewModel or SettingsItemViewModel);
        if (focused?.FindAncestorOfType<ScrollViewer>() is not { } scroller)
        {
            return;
        }

        var section = focused.FindAncestorOfType<Border>() is { } border && border.Classes.Contains("settingsSection") ? border : null;
        if (section is not null && section.Bounds.Height <= scroller.Viewport.Height)
        {
            section.BringIntoView();
        }
        else
        {
            focused.BringIntoView();
        }
    }

    /// <summary>
    /// Puts the context menu beside the focused cover: to its right, or to its left where the
    /// right side has no room, top-aligned with the cover and kept inside the window.
    /// </summary>
    private void PlaceGameMenu()
    {
        GameMenuPanel.Opacity = 1;
        if (_viewModel?.FocusedGame is not { } tile
            || GameGrid.ContainerFromIndex(_viewModel.PageGames.IndexOf(tile)) is not { } cover
            || cover.TranslatePoint(default, GameMenuLayer) is not { } topLeft)
        {
            GameMenuPanel.Margin = new Thickness((GameMenuLayer.Bounds.Width - GameMenuPanel.Bounds.Width) / 2, (GameMenuLayer.Bounds.Height - GameMenuPanel.Bounds.Height) / 2, 0, 0);
            return;
        }

        var width = GameMenuPanel.Bounds.Width;
        var height = GameMenuPanel.Bounds.Height;
        var right = topLeft.X + cover.Bounds.Width + MenuGap;
        var x = right + width <= GameMenuLayer.Bounds.Width - MenuGap ? right : topLeft.X - MenuGap - width;
        var y = Math.Clamp(topLeft.Y, MenuGap, Math.Max(MenuGap, GameMenuLayer.Bounds.Height - height - MenuGap));
        GameMenuPanel.Margin = new Thickness(Math.Max(MenuGap, x), y, 0, 0);
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
