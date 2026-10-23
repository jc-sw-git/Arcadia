using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.Search;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Arcadia.App.Views;

public partial class MainWindow : Window
{
    /// <summary>Space between a cover and its context menu, and between the menu and the window's edge.</summary>
    private const double MenuGap = 24;

    /// <summary>Height of the fades at the grid's edges (Border.listFade in Theme.axaml).</summary>
    private const double ListFadeHeight = 96;

    /// <summary>Closer than this, the launcher chips and the sort button get a divider between them.</summary>
    private const double SortDividerGap = 48;

    /// <summary>From one search hit to the next: tile width or height (SearchHitTile) plus the spacing between them.</summary>
    private const double HitPitchX = 190 + 20;

    private const double HitPitchY = 270 + 20;

    /// <summary>Seven hits side by side, the width of the window of hits in both layouts.</summary>
    private const double HitWindowWidth = (7 * HitPitchX) - 20;

    /// <summary>Room above the row of hits for the focused one's lift, and above the grid for the row before it, faded.</summary>
    private const double HitRowTop = 8;

    private const double HitGridTop = 40;

    /// <summary>The focus ring around a setting reaches this far beyond it (FocusRing in Theme.axaml).</summary>
    private const double FocusRingRoom = 8;

    /// <summary>A step of RS (or Shift+W/S) through the settings' info panel.</summary>
    private const double InfoScrollStep = 160;

    /// <summary>Room kept above a focused setting, so its group's heading comes into view with the group's first row.</summary>
    private const double SettingsGroupRoom = 48;

    /// <summary>The pointer must move this far (window pixels) from where it rested to count as the mouse being used.</summary>
    private const double PointerMoveThreshold = 6;

    private readonly HashSet<Key> _keysDown = [];
    private MainViewModel? _viewModel;
    private Point? _restingPointer;

    /// <summary>Where the pointer last focused a settings row; rows scrolling under a resting pointer don't count.</summary>
    private Point? _rowPointer;

    /// <summary>The mouse focused the row: it is under the pointer already, so the rows don't scroll to it.</summary>
    private bool _focusedByPointer;

    /// <summary>A launcher row the left button went down on (#136); moved far enough, it is dragged.</summary>
    private LauncherRowViewModel? _pressedLauncher;
    private Point _launcherPressPoint;
    private bool _draggingLauncher;
    private SearchViewModel? _search;
    private SettingsPageViewModel? _settingsPage;
    private readonly TabStrip _tabs;
    private readonly TabStrip _categories;

    public MainWindow()
    {
        InitializeComponent();

        // Tunnel so arrow keys and Enter reach us before Avalonia's own focus navigation.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
        GameGrid.AddHandler(DoubleTappedEvent, OnGridDoubleTapped);
        GameGrid.AddHandler(PointerPressedEvent, OnGridPointerPressed, RoutingStrategies.Tunnel);
        SearchResults.AddHandler(DoubleTappedEvent, OnGridDoubleTapped);
        SearchResults.AddHandler(PointerPressedEvent, OnSearchResultPointerPressed, RoutingStrategies.Tunnel);
        SearchViewport.PointerWheelChanged += OnSearchResultsWheel;
        HitsRowScrollBar.Scroll += OnHitsScrollBarScroll;
        HitsGridScrollBar.Scroll += OnHitsScrollBarScroll;
        SearchViewport.SizeChanged += (_, _) => PlaceHits(previousStart: null);
        SearchChips.SizeChanged += (_, _) => UpdateSortDivider();
        SearchChipRow.SizeChanged += (_, _) => UpdateSortDivider();
        SearchSortButton.SizeChanged += (_, _) => UpdateSortDivider();

        // Typing on a real keyboard while the search is open. Nothing has Avalonia focus, so the text arrives at the window.
        AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);

        // Which input is in use decides whether the search shows its on-screen keyboard.
        AddHandler(PointerPressedEvent, (_, _) => _viewModel?.NotePointer(), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMovedAnywhere, RoutingStrategies.Tunnel, handledEventsToo: true);
        SettingsRows.AddHandler(PointerMovedEvent, OnSettingsRowPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);

        // Launchers are moved by dragging them (#136). Tunnel: the row's button captures the pointer on its press.
        SettingsRows.AddHandler(PointerPressedEvent, OnLauncherRowPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        SettingsRows.AddHandler(PointerMovedEvent, OnLauncherRowDragged, RoutingStrategies.Tunnel, handledEventsToo: true);
        SettingsRows.AddHandler(PointerReleasedEvent, OnLauncherRowReleased, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Ends a held click on a cover, wherever the mouse is let go.
        AddHandler(PointerReleasedEvent, (_, _) => _viewModel?.ReleasePointer(), RoutingStrategies.Tunnel, handledEventsToo: true);

        // A key released in another window never reports its release here.
        Deactivated += (_, _) =>
        {
            _keysDown.Clear();
            _viewModel?.CancelHold();
        };
        GridScroller.ScrollChanged += (_, _) => UpdateGridFades();
        _tabs = new TabStrip(TabScroller, TabList, TabEdgeLeft, TabEdgeRight, TabDividerLeft, TabDividerRight, () => _viewModel?.IsTabStripShown != false);
        _categories = new TabStrip(CategoryScroller, CategoryList, CategoryEdgeLeft, CategoryEdgeRight, CategoryDividerLeft, CategoryDividerRight, () => true);
        InfoScroller.ScrollChanged += (_, _) => UpdateInfoFades();
        InfoScroller.SizeChanged += (_, _) => UpdateInfoFades();

        // Coming back from a launcher: games may have been installed or uninstalled there.
        Activated += async (_, _) =>
        {
            if (_viewModel is not null)
            {
                await _viewModel.RefreshLibraryAsync();
            }
        };
    }

    private void UpdateSortDivider()
    {
        var gap = (SortDivider.Bounds.X - SortDivider.Margin.Left) - (SearchChips.Bounds.Right + SearchChips.Margin.Right);
        SortDivider.Classes.Set("unneeded", gap >= SortDividerGap);
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
        _viewModel?.NoteKeyboard();
        if (e.Key == Key.F11)
        {
            WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
            e.Handled = true;
            return;
        }

        // A text being written in the settings (#137): keys that type nothing act on it, the rest arrive as text.
        if (_viewModel?.Settings?.Selected.Editor is { } editor)
        {
            if (editor.HandleKey(e.Key))
            {
                Services.AppLog.Write($"Taste: {e.Key} → Texteingabe");
                e.Handled = true;
            }

            return;
        }

        if (_viewModel is { IsSearchShown: false } && UiActionMap.TabNumber(e.Key, e.KeyModifiers) is { } tab)
        {
            Services.AppLog.Write($"Taste: {e.Key} → Launcher {tab + 1}");
            _viewModel.ChooseTabNumber(tab);
            e.Handled = true;
            return;
        }

        if (_viewModel is not null && ActionFor(e) is { } action)
        {
            // A held key repeats its KeyDown. Moving and turning may repeat; A acts on the release or after a hold, like the
            // gamepad button, and a dialog must not open and close again with every repeat.
            if (!_keysDown.Add(e.Key) && action is UiAction.Accept or UiAction.Settings or UiAction.Options or UiAction.ViewOptions)
            {
                e.Handled = true;
                return;
            }

            Services.AppLog.Write($"Taste: {e.Key} → {action}");
            _viewModel.Handle(action);
            e.Handled = true;
        }
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        _keysDown.Remove(e.Key);
        if (_viewModel is not null && (ActionFor(e) ?? UiActionMap.FromKey(e.Key, e.KeyModifiers)) is { } action)
        {
            _viewModel.Release(action);
        }
    }

    /// <summary>
    /// While the search takes typed text, only keys that type nothing are actions there; the rest
    /// arrive as text (<see cref="OnTextInput"/>). Elsewhere the usual key map applies.
    /// </summary>
    private UiAction? ActionFor(KeyEventArgs e)
    {
        if (_viewModel is { IsSettingsOpen: true })
        {
            return SettingsActionFor(e) ?? UiActionMap.FromKey(e.Key, e.KeyModifiers);
        }

        if (_viewModel is not { IsSearchShown: true, IsDialogOpen: false })
        {
            return UiActionMap.FromKey(e.Key, e.KeyModifiers);
        }

        // The arrows move through the hits, Page Up/Down turn them, Enter starts the focused one (held: its details).
        return e.Key switch
        {
            Key.Up => UiAction.Up,
            Key.Down => UiAction.Down,
            Key.Left => UiAction.Left,
            Key.Right => UiAction.Right,
            Key.Enter => UiAction.Accept,
            Key.Escape => UiAction.Back,
            Key.Back => UiAction.Search,
            Key.PageUp => UiAction.PreviousPage,
            Key.PageDown => UiAction.NextPage,
            Key.Apps => UiAction.Options,
            Key.F10 when e.KeyModifiers == KeyModifiers.Shift => UiAction.Options,
            _ => null,
        };
    }

    /// <summary>
    /// On the settings page Shift+Q/E switch the category as LB/RB do and Q/E the tabs inside one as LT/RT do, e.g. a
    /// launcher's page to the next launcher (#137), as in the library (owner, 2026-10-23). Shift+W/S (and Page Up/Down)
    /// scroll the info panel as RS does.
    /// </summary>
    private static UiAction? SettingsActionFor(KeyEventArgs e) => (e.Key, e.KeyModifiers) switch
    {
        (Key.Q, KeyModifiers.Shift) => UiAction.PreviousTab,
        (Key.E, KeyModifiers.Shift) => UiAction.NextTab,
        (Key.Q, KeyModifiers.None) => UiAction.PreviousPage,
        (Key.E, KeyModifiers.None) => UiAction.NextPage,
        (Key.W, KeyModifiers.Shift) or (Key.PageUp, _) => UiAction.SecondaryUp,
        (Key.S, KeyModifiers.Shift) or (Key.PageDown, _) => UiAction.SecondaryDown,
        _ => null,
    };

    /// <summary>
    /// A real mouse movement shows the dialogs' ✕. Windows also reports a pointer "move" when only the content under a
    /// resting pointer changes, so a few pixels from where it rested don't count.
    /// </summary>
    private void OnPointerMovedAnywhere(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(this);
        if (_viewModel is null || _viewModel.ShowsCloseButtons)
        {
            _restingPointer = position;
            return;
        }

        if (_restingPointer is not { } resting)
        {
            _restingPointer = position;
            return;
        }

        if (Math.Abs(position.X - resting.X) + Math.Abs(position.Y - resting.Y) > PointerMoveThreshold)
        {
            _restingPointer = position;
            _viewModel.NotePointerMoved();
        }
    }

    /// <summary>The mouse over a settings row focuses it, so its info shows before a click changes its value (owner, 2026-10-22).</summary>
    private void OnSettingsRowPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(this);
        if (_rowPointer is { } last && Math.Abs(position.X - last.X) + Math.Abs(position.Y - last.Y) <= PointerMoveThreshold)
        {
            return;
        }

        _rowPointer = position;
        if ((e.Source as StyledElement)?.DataContext is SettingsRowViewModel { IsFocused: false } row)
        {
            _focusedByPointer = true;
            row.Point();
        }
    }

    private void OnLauncherRowPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressedLauncher = null;
        _draggingLauncher = false;
        var source = e.Source as Control;
        if (!e.GetCurrentPoint(SettingsRows).Properties.IsLeftButtonPressed
            || source?.DataContext is not LauncherRowViewModel { CanMove: true } row
            || source.FindAncestorOfType<Button>(includeSelf: true) is { } button && (button.Classes.Contains("valueArrow") || button.Classes.Contains("valueToggle")))
        {
            return;
        }

        _pressedLauncher = row;
        _launcherPressPoint = e.GetPosition(SettingsRows);
    }

    /// <summary>The dragged launcher stands below the rows whose middle is above the pointer.</summary>
    private void OnLauncherRowDragged(object? sender, PointerEventArgs e)
    {
        if (_pressedLauncher is not { } row || _settingsPage?.Selected is not LauncherSettingsViewModel launchers)
        {
            return;
        }

        var position = e.GetPosition(SettingsRows);
        if (!_draggingLauncher)
        {
            if (Math.Abs(position.Y - _launcherPressPoint.Y) + Math.Abs(position.X - _launcherPressPoint.X) <= PointerMoveThreshold)
            {
                return;
            }

            _draggingLauncher = true;
            launchers.BeginDrag(row);
        }

        var above = 0;
        foreach (var other in launchers.Rows)
        {
            if (other != row && SettingsRows.ContainerFromItem(other) is { } container
                && container.TranslatePoint(new Point(0, container.Bounds.Height / 2), SettingsRows) is { } middle && middle.Y < position.Y)
            {
                above++;
            }
        }

        launchers.DragTo(above);
    }

    private void OnLauncherRowReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingLauncher && _settingsPage?.Selected is LauncherSettingsViewModel launchers)
        {
            launchers.Drop();

            // A drag ends where it began often enough; the row's click would then switch the launcher.
            e.Handled = true;
        }

        _pressedLauncher = null;
        _draggingLauncher = false;
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_viewModel?.Settings?.Selected.Editor is { } editor && !string.IsNullOrEmpty(e.Text))
        {
            editor.Type(e.Text);
            e.Handled = true;
            return;
        }

        if (_viewModel is { IsSearchShown: true } && !string.IsNullOrEmpty(e.Text))
        {
            _viewModel.TypeInSearch(e.Text);
            e.Handled = true;
        }
    }

    private void OnSearchResultPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is not GameTileViewModel tile || _viewModel is null)
        {
            return;
        }

        var button = e.GetCurrentPoint(this).Properties;
        if (button.IsRightButtonPressed)
        {
            _viewModel.OpenMenuForHit(tile);
            e.Handled = true;
        }
        else if (button.IsLeftButtonPressed)
        {
            _viewModel.PressTile(tile);
        }
    }

    private void OnSearchResultsWheel(object? sender, PointerWheelEventArgs e)
    {
        // Either wheel direction scrolls; most mice only have the vertical one.
        var delta = Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y) ? e.Delta.X : e.Delta.Y;
        if (_viewModel is not null && delta != 0)
        {
            _viewModel.ScrollSearch(delta < 0 ? 1 : -1);
            e.Handled = true;
        }
    }

    private void OnHitsScrollBarScroll(object? sender, ScrollEventArgs e) => _search?.ScrollTo(e.NewValue);

    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is not GameTileViewModel tile || _viewModel is null)
        {
            return;
        }

        var button = e.GetCurrentPoint(this).Properties;
        if (button.IsRightButtonPressed)
        {
            _viewModel.OpenMenuFor(tile);
            e.Handled = true;
        }
        else if (button.IsLeftButtonPressed)
        {
            _viewModel.PressTile(tile);
        }
    }

    private void OnGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is GameTileViewModel tile)
        {
            // The second press started a hold for the details; the game starts instead.
            _viewModel?.CancelHold();
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
                Dispatcher.UIThread.Post(() => _tabs.Center(_viewModel?.SelectedTab), DispatcherPriority.Loaded);
                break;
            case nameof(MainViewModel.IsTabStripShown):
                Dispatcher.UIThread.Post(_tabs.UpdateEdges, DispatcherPriority.Loaded);
                break;
            case nameof(MainViewModel.Settings):
                FollowSettingsPage(_viewModel?.Settings);
                break;
            case nameof(MainViewModel.Search):
                if (_search is not null)
                {
                    _search.WindowMoved -= PlaceHits;
                }

                _search = _viewModel?.Search;
                if (_search is not null)
                {
                    _search.WindowMoved += PlaceHits;
                    PlaceHits(previousStart: null);
                }

                break;
        }
    }

    /// <summary>
    /// Moves the hits so the window starts at the leading edge: in the row its first hit on the
    /// left, in the grid its first row at the top. After a step or a turn they slide there from
    /// where the window was, at most a window away (wrapping round from the end jumps further).
    /// </summary>
    private void PlaceHits(int? previousStart)
    {
        if (_search is not { } search)
        {
            return;
        }

        var target = HitsOffset(search, search.WindowStart);
        if (previousStart is not { } previous)
        {
            SetHitsOffsetAtOnce(target);
            return;
        }

        var size = search.IsKeyboardOut ? SearchViewModel.ResultColumns : SearchViewModel.ResultColumns * SearchViewModel.ResultRowsWithoutKeyboard;
        SetHitsOffsetAtOnce(HitsOffset(search, Math.Clamp(previous, search.WindowStart - size, search.WindowStart + size)));
        SearchResults.RenderTransform = target;
    }

    private TransformOperations HitsOffset(SearchViewModel search, int windowStart)
    {
        // The results hold the hits from SliceStart on; the window is centred in the hit area.
        var shift = windowStart - search.SliceStart;
        var left = (SearchViewport.Bounds.Width - HitWindowWidth) / 2;
        var builder = TransformOperations.CreateBuilder(1);
        if (search.IsKeyboardOut)
        {
            builder.AppendTranslate(left - (shift * HitPitchX), HitRowTop);
        }
        else
        {
            builder.AppendTranslate(left, HitGridTop - (shift / SearchViewModel.ResultColumns * HitPitchY));
        }

        return builder.Build();
    }

    /// <summary>Without the slide: a new search, the other layout, or the start of a slide.</summary>
    private void SetHitsOffsetAtOnce(TransformOperations offset)
    {
        var transitions = SearchResults.Transitions;
        SearchResults.Transitions = null;
        SearchResults.RenderTransform = offset;
        SearchResults.Transitions = transitions;
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

    /// <summary>The settings page opened or closed: its focus and its info panel are followed from here on.</summary>
    private void FollowSettingsPage(SettingsPageViewModel? page)
    {
        if (_settingsPage is not null)
        {
            _settingsPage.FocusMoved -= OnSettingsFocusMoved;
            _settingsPage.InfoScrollRequested -= OnInfoScrollRequested;
        }

        _settingsPage = page;
        if (page is not null)
        {
            page.FocusMoved += OnSettingsFocusMoved;
            page.InfoScrollRequested += OnInfoScrollRequested;
            OnSettingsFocusMoved(page, EventArgs.Empty);
        }
    }

    /// <summary>Another row or category: the row comes into view, the info panel starts at its top, the tab stands in the middle.</summary>
    private void OnSettingsFocusMoved(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (_settingsPage is not { } page)
        {
            return;
        }

        _categories.Center(page.Selected);
        InfoScroller.Offset = default;
        UpdateInfoFades();
        var byPointer = _focusedByPointer;
        _focusedByPointer = false;
        if (!byPointer && page.FocusedRow is { } row && SettingsRows.ContainerFromItem(row) is { } container)
        {
            // With room for the focus ring, and above it for the group's heading the row may stand under.
            var bounds = new Rect(container.Bounds.Size).Inflate(FocusRingRoom);
            container.BringIntoView(row.StartsGroup ? bounds.Inflate(new Thickness(0, SettingsGroupRoom, 0, 0)) : bounds);
        }
    }, DispatcherPriority.Loaded);

    private void OnInfoScrollRequested(object? sender, int direction)
    {
        var max = Math.Max(0, InfoScroller.Extent.Height - InfoScroller.Viewport.Height);
        InfoScroller.Offset = new Vector(0, Math.Clamp(InfoScroller.Offset.Y + (direction * InfoScrollStep), 0, max));
    }

    /// <summary>A fade shows only at an edge the info panel can still scroll towards; the footer then names RS.</summary>
    private void UpdateInfoFades()
    {
        var offset = InfoScroller.Offset.Y;
        var max = InfoScroller.Extent.Height - InfoScroller.Viewport.Height;
        InfoFadeTop.Classes.Set("shown", offset > 1);
        InfoFadeBottom.Classes.Set("shown", offset < max - 1);
        if (_settingsPage is { } page)
        {
            page.InfoOverflows = max > 1;
        }
    }

    /// <summary>
    /// Puts the context menu beside the focused cover: to its right, or to its left where the
    /// right side has no room, top-aligned with the cover and kept inside the window. Opened without
    /// a cover, it stands over the preview's "Optionen" button.
    /// </summary>
    private void PlaceGameMenu()
    {
        GameMenuPanel.Opacity = 1;
        if (_viewModel?.GameMenu is { AtCover: false } && PlaceOverOptionsButton())
        {
            return;
        }

        if (MenuAnchor() is not { } cover
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

    /// <summary>Covers the preview's "Optionen" button with the menu, left and bottom edges on the button's, kept inside the window.</summary>
    private bool PlaceOverOptionsButton()
    {
        if (!PreviewOptionsButton.IsEffectivelyVisible || PreviewOptionsButton.TranslatePoint(default, GameMenuLayer) is not { } topLeft)
        {
            return false;
        }

        var bottom = topLeft.Y + PreviewOptionsButton.Bounds.Height;
        var x = Math.Clamp(topLeft.X, MenuGap, Math.Max(MenuGap, GameMenuLayer.Bounds.Width - GameMenuPanel.Bounds.Width - MenuGap));
        GameMenuPanel.Margin = new Thickness(x, Math.Max(MenuGap, bottom - GameMenuPanel.Bounds.Height), 0, 0);
        return true;
    }

    /// <summary>The cover whose menu is open: the focused hit while the search is shown, else the focused cover.</summary>
    private Control? MenuAnchor()
    {
        if (_viewModel is { IsSearchShown: true, Search.FocusedResult: { } hit } searching)
        {
            return SearchResults.ContainerFromIndex(searching.Search!.SlotOf(hit));
        }

        return _viewModel?.FocusedGame is { } tile ? GameGrid.ContainerFromIndex(_viewModel.PageGames.IndexOf(tile)) : null;
    }
}
