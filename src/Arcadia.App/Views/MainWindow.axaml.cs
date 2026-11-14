using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.Search;
using Arcadia.App.Services;
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

    /// <summary>Closer than this, the launcher chips and the sort button get a divider between them.</summary>
    private const double SortDividerGap = 48;

    /// <summary>From one search hit to the next: tile width or height (SearchHitTile) plus the spacing between them.</summary>
    private const double HitPitchX = 190 + 20;

    private const double HitPitchY = 315 + 20;

    /// <summary>Seven hits side by side, the width of the window of hits in both layouts.</summary>
    private const double HitWindowWidth = (7 * HitPitchX) - 20;

    /// <summary>Room above the row of hits for the focused one's lift.</summary>
    private const double HitRowTop = 8;

    // Two rows of 2 : 3 hits (#24) take 650 of the grid's 674 px: they sit in the middle, whole and clear of its fades,
    // and no row before or after peeks in (HitsGridFade, MainWindow.axaml).
    private const double HitGridTop = 12;

    /// <summary>A step of RS (or Shift+W/S) through the settings' info panel.</summary>
    private const double InfoScrollStep = 160;

    /// <summary>Room kept above a focused setting, so its group's heading comes into view with the group's first row.</summary>
    private const double SettingsGroupRoom = 48;

    /// <summary>The pointer must move this far (window pixels) from where it rested to count as the mouse being used.</summary>
    private const double PointerMoveThreshold = 6;

    private readonly HashSet<Key> _keysDown = [];

    // Keys whose press ended "Bild ansehen" (#565): their repeats and release do nothing either.
    private readonly HashSet<Key> _keysSwallowedByImageShow = [];

    // Where the mouse was when it last counted as input for "Bild ansehen".
    private Point? _imageShowPointer;
    private MainViewModel? _viewModel;
    private Point? _restingPointer;

    /// <summary>Where the pointer last focused a settings row; rows scrolling under a resting pointer don't count.</summary>
    private Point? _rowPointer;

    /// <summary>The mouse focused the row: it is under the pointer already, so the rows don't scroll to it.</summary>
    private bool _focusedByPointer;

    /// <summary>A row the left button went down on that can be moved (#136, #506); moved far enough, it is dragged.</summary>
    private SettingsRowViewModel? _pressedRow;
    private Point _rowPressPoint;
    private bool _draggingRow;
    private SearchViewModel? _search;
    private SettingsPageViewModel? _settingsPage;
    private SettingsRowViewModel? _infoRow;
    private readonly TabStrip _tabs;
    private readonly TabStrip _categories;

    /// <summary>
    /// Puts the web page (#280) on its place in the scaled layout, in window pixels: a native view keeps its own size
    /// inside a Viewbox. While the page is hidden its place has no position, and the view keeps the last one.
    /// </summary>
    private void PlaceWebPage()
    {
        if (WebPageArea.TransformToVisual(this) is not { } toWindow)
        {
            return;
        }

        var place = new Rect(WebPageArea.Bounds.Size).TransformToAABB(toWindow);
        var margin = new Thickness(place.X, place.Y, 0, 0);
        if (WebPageView.Margin != margin || WebPageView.Width != place.Width || WebPageView.Height != place.Height)
        {
            WebPageView.Margin = margin;
            WebPageView.Width = place.Width;
            WebPageView.Height = place.Height;
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        Latency = InputLatency.For(this);

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
        LayoutUpdated += (_, _) => PlaceWebPage();

        // Typing on a real keyboard while the search is open. Nothing has Avalonia focus, so the text arrives at the window.
        AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);

        // Any use of the mouse ends "Bild ansehen" (#565), and a click or turn of the wheel that ends it does nothing else.
        AddHandler(PointerPressedEvent, OnPointerInputForImageShow, RoutingStrategies.Tunnel);
        AddHandler(PointerWheelChangedEvent, OnPointerInputForImageShow, RoutingStrategies.Tunnel);

        // Which input is in use decides whether the search shows its on-screen keyboard.
        AddHandler(PointerPressedEvent, (_, _) => _viewModel?.NotePointer(), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMovedAnywhere, RoutingStrategies.Tunnel, handledEventsToo: true);
        SettingsRows.AddHandler(PointerMovedEvent, OnSettingsRowPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);

        // Launchers and artwork sources are moved by dragging them (#136, #506). Tunnel: the row's button captures the pointer on its press.
        SettingsRows.AddHandler(PointerPressedEvent, OnRowPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        SettingsRows.AddHandler(PointerMovedEvent, OnRowDragged, RoutingStrategies.Tunnel, handledEventsToo: true);
        SettingsRows.AddHandler(PointerReleasedEvent, OnRowReleased, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Ends a held click on a cover, wherever the mouse is let go.
        AddHandler(PointerReleasedEvent, (_, _) => _viewModel?.ReleasePointer(), RoutingStrategies.Tunnel, handledEventsToo: true);

        // Buttons that act only when held (#242): the press starts the hold, letting go anywhere ends it.
        AddHandler(PointerPressedEvent, OnHoldPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, (_, _) => ReleaseHold(), RoutingStrategies.Tunnel, handledEventsToo: true);

        // A key released in another window never reports its release here.
        Deactivated += (_, _) =>
        {
            _keysDown.Clear();
            _keysSwallowedByImageShow.Clear();
            _viewModel?.CancelHold();
            _viewModel?.SetInFront(false);
        };
        _tabs = new TabStrip(TabScroller, TabList, TabDividerLeft, TabDividerRight, () => _viewModel?.IsTabStripShown != false);
        _categories = new TabStrip(CategoryScroller, CategoryList, CategoryDividerLeft, CategoryDividerRight, () => true);
        InfoScroller.ScrollChanged += (_, _) => UpdateInfoOverflow();

        // Coming back from a launcher: games may have been installed or uninstalled there.
        Activated += (_, _) =>
        {
            _viewModel?.SetInFront(true);
            _viewModel?.Session.RefreshLibraryAsync().Forget(AppTexts.ErrorContextRefreshLibrary);
        };

        // The key hints name the letters of the player's real keyboard layout (#588).
        if (OperatingSystem.IsWindows())
        {
            WindowsKeyboardLayout.Follow(this);
        }
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
            _viewModel.LauncherTabs.PropertyChanged -= OnTabsPropertyChanged;
            _viewModel.Library.PropertyChanged -= OnLibraryPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.LauncherTabs.PropertyChanged += OnTabsPropertyChanged;
            _viewModel.Library.PropertyChanged += OnLibraryPropertyChanged;
            BuildKeyboardsOnceLibraryShows(_viewModel.Session);
        }

        // A settings page open before the window knew its view model (the first start resumed on "Datenquellen", #497):
        // its rows scroll with the focus too.
        FollowSettingsPage(_viewModel?.Settings);
    }

    /// <summary>
    /// Builds the on-screen keyboards of the search and of the settings' texts while nobody waits for them (#477): right
    /// after the first frame of the library (or the first start's page), so Arcadia still shows up as fast and the first X
    /// finds its keyboard built. Building one on demand took about half a second, nearly all of it its keys' controls. One
    /// keyboard per job, so a press in between is handled; one right after the start may wait for a keyboard being built.
    /// </summary>
    private void BuildKeyboardsOnceLibraryShows(LibrarySession session)
    {
        if (!session.IsLoading)
        {
            RequestAnimationFrame(_ =>
            {
                Dispatcher.UIThread.Post(() => BuildKeyboard(SearchKeyboard, AppTexts.KeyboardOfSearch), DispatcherPriority.Background);
                Dispatcher.UIThread.Post(() => BuildKeyboard(SettingsKeyboard, AppTexts.KeyboardOfSettings), DispatcherPriority.Background);
            });
            return;
        }

        void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LibrarySession.IsLoading) && !session.IsLoading)
            {
                session.PropertyChanged -= OnSessionChanged;
                BuildKeyboardsOnceLibraryShows(session);
            }
        }

        session.PropertyChanged += OnSessionChanged;
    }

    /// <summary>
    /// Measuring a keyboard builds its keys' controls although the page hiding it isn't shown: the layout skips what is
    /// hidden, a measure called on a visible control doesn't.
    /// </summary>
    private static void BuildKeyboard(OnScreenKeyboardView keyboard, string whose)
    {
        try
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            keyboard.Measure(Size.Infinity);
            AppLog.Write($"Tastatur {whose} in {watch.ElapsedMilliseconds} ms vorbereitet");
        }
        catch (Exception ex)
        {
            // Only a head start: the page builds the keyboard itself when it shows it.
            AppErrors.Report(ex, AppTexts.ErrorContextPrepareKeyboard(whose));
        }
    }

    /// <summary>Measures every press in this window, key or button, to the frame that shows it (#229).</summary>
    public InputLatency Latency { get; }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        var pressedAt = InputLatency.Now();
        if (SwallowedByImageShow(e.Key))
        {
            e.Handled = true;
            return;
        }

        _viewModel?.NoteKeyboard();
        if (e.Key == Key.F11)
        {
            WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
            e.Handled = true;
            return;
        }

        // A text being written in the settings (#137) or the waiting list (#506): keys that type nothing act on it, the
        // rest arrive as text.
        if (_viewModel?.OpenTextEditor is { } editor)
        {
            if (editor.HandleKey(e.Key))
            {
                Services.AppLog.Write($"Taste: {e.Key} → Texteingabe");
                e.Handled = true;
            }

            return;
        }

        // By position, so the cluster around WASD stays in place on AZERTY too (#588).
        var key = KeyPosition.Of(e);
        if (_viewModel is { IsSearchShown: false } && UiActionMap.TabNumber(key, e.KeyModifiers) is { } tab)
        {
            Services.AppLog.Write($"Taste: {KeyForLog(key, e.Key)} → Launcher {tab + 1}");
            Latency.Measure($"Launcher {tab + 1}", pressedAt, () => _viewModel.ChooseTabNumber(tab));
            e.Handled = true;
            return;
        }

        if (_viewModel is not null && ActionFor(e, key) is { } action)
        {
            // A held key repeats its KeyDown. Moving and turning may repeat, and stop at a list's end like a held button
            // (#476); A acts on the release or after a hold, like the gamepad button, and a dialog must not open and close
            // again with every repeat.
            var repeat = !_keysDown.Add(key);
            if (repeat && action is UiAction.Accept or UiAction.Settings or UiAction.Options or UiAction.ViewOptions or UiAction.Status
                or UiAction.KeyboardLayout)
            {
                e.Handled = true;
                return;
            }

            Services.AppLog.Write($"Taste: {KeyForLog(key, e.Key)} → {action}{(repeat ? " (gehalten)" : string.Empty)}");
            var viewModel = _viewModel;
            Latency.Measure(action.ToString(), pressedAt, () => viewModel.Handle(action, repeat));
            e.Handled = true;
        }
    }

    /// <summary>The key's place and, where the layout prints another letter there, that letter too: "W (Z)" on AZERTY (#588).</summary>
    private static string KeyForLog(Key place, Key printed) => place == printed ? place.ToString() : $"{place} ({printed})";

    /// <summary>A key that ends "Bild ansehen" (#565) does nothing else, nor do its repeats.</summary>
    private bool SwallowedByImageShow(Key key)
    {
        if (_keysSwallowedByImageShow.Contains(key))
        {
            return true;
        }

        if (_viewModel?.TakeInputForImageShow() == true)
        {
            _keysSwallowedByImageShow.Add(key);
            return true;
        }

        return false;
    }

    private void OnPointerInputForImageShow(object? sender, PointerEventArgs e)
    {
        if (_viewModel?.TakePointerInputForImageShow() == true)
        {
            e.Handled = true;
        }
    }

    private void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        var key = KeyPosition.Of(e);
        _keysDown.Remove(key);
        if (_keysSwallowedByImageShow.Remove(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (_viewModel is not null && (ActionFor(e, key) ?? UiActionMap.FromKey(key, e.KeyModifiers)) is { } action)
        {
            _viewModel.Release(action);
        }
    }

    /// <summary>
    /// While the search takes typed text, only keys that type nothing are actions there; the rest
    /// arrive as text (<see cref="OnTextInput"/>). Elsewhere the usual key map applies.
    /// </summary>
    private UiAction? ActionFor(KeyEventArgs e, Key key)
    {
        if (_viewModel is { IsSettingsOpen: true })
        {
            return SettingsActionFor(key, e.KeyModifiers) ?? UiActionMap.FromKey(key, e.KeyModifiers);
        }

        if (_viewModel is not { IsSearchShown: true, IsDialogOpen: false })
        {
            return UiActionMap.FromKey(key, e.KeyModifiers);
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
    /// launcher's page to the next launcher (#137), as in the library (owner, 2026-10-23). Page Up/Down scroll the info panel
    /// as RS does (Shift+W/S is RS everywhere, UiActionMap).
    /// </summary>
    private static UiAction? SettingsActionFor(Key key, KeyModifiers modifiers) => (key, modifiers) switch
    {
        (Key.Q, KeyModifiers.Shift) => UiAction.PreviousTab,
        (Key.E, KeyModifiers.Shift) => UiAction.NextTab,
        (Key.Q, KeyModifiers.None) => UiAction.PreviousPage,
        (Key.E, KeyModifiers.None) => UiAction.NextPage,
        (Key.PageUp, _) => UiAction.SecondaryUp,
        (Key.PageDown, _) => UiAction.SecondaryDown,
        _ => null,
    };

    /// <summary>
    /// A real mouse movement shows the dialogs' ✕. Windows also reports a pointer "move" when only the content under a
    /// resting pointer changes, so a few pixels from where it rested don't count.
    /// </summary>
    private HoldButtonViewModel? _pointerHold;

    private void OnHoldPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<StyledElement>()
                .FirstOrDefault(element => element.Classes.Contains("hold"))?.DataContext is not HoldButtonViewModel button)
        {
            return;
        }

        ReleaseHold();
        _pointerHold = button;
        _viewModel?.PressHold(button);
    }

    private void ReleaseHold()
    {
        _pointerHold?.Release();
        _pointerHold = null;
    }

    private void OnPointerMovedAnywhere(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(this);
        if (_imageShowPointer is not { } last || Math.Abs(position.X - last.X) + Math.Abs(position.Y - last.Y) > PointerMoveThreshold)
        {
            // The first move only marks where the mouse rests: a pointer that didn't move isn't input.
            if (_imageShowPointer is not null)
            {
                _viewModel?.TakeInputForImageShow();
            }

            _imageShowPointer = position;
        }

        if (_viewModel is null || _viewModel.Input.ShowsCloseButtons)
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
        if (_rowPointer is not { } last)
        {
            // The first event only says where the pointer rests: a window that opens under it (Arcadia restarted on
            // "Sprache", #554) must keep the focus it opened with.
            _rowPointer = position;
            return;
        }

        if (Math.Abs(position.X - last.X) + Math.Abs(position.Y - last.Y) <= PointerMoveThreshold)
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

    private void OnRowPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressedRow = null;
        _draggingRow = false;
        var source = e.Source as Control;
        if (!e.GetCurrentPoint(SettingsRows).Properties.IsLeftButtonPressed
            || _settingsPage?.Selected is not IDraggableRows rows
            || source?.DataContext is not SettingsRowViewModel row || !rows.CanDrag(row)
            || source.FindAncestorOfType<Button>(includeSelf: true) is { } button && (button.Classes.Contains("valueArrow") || button.Classes.Contains("valueToggle")))
        {
            return;
        }

        _pressedRow = row;
        _rowPressPoint = e.GetPosition(SettingsRows);
    }

    /// <summary>The dragged row stands below the rows of its list whose middle is above the pointer.</summary>
    private void OnRowDragged(object? sender, PointerEventArgs e)
    {
        if (_pressedRow is not { } row || _settingsPage?.Selected is not (SettingsCategoryViewModel category and IDraggableRows rows))
        {
            return;
        }

        var position = e.GetPosition(SettingsRows);
        if (!_draggingRow)
        {
            if (Math.Abs(position.Y - _rowPressPoint.Y) + Math.Abs(position.X - _rowPressPoint.X) <= PointerMoveThreshold)
            {
                return;
            }

            _draggingRow = true;
            rows.BeginDrag(row);
        }

        var above = 0;
        foreach (var other in category.Rows)
        {
            // The row's button, not its container: the first row of a group carries the group's heading above it.
            if (other != row && rows.IsInDragList(other) && SettingsRows.ContainerFromItem(other) is { } container
                && (container.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Classes.Contains("settingRow")) ?? (Visual)container) is { } button
                && button.TranslatePoint(new Point(0, button.Bounds.Height / 2), SettingsRows) is { } middle && middle.Y < position.Y)
            {
                above++;
            }
        }

        rows.DragTo(above);
    }

    private void OnRowReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingRow && _settingsPage?.Selected is IDraggableRows rows)
        {
            rows.Drop();

            // A drag ends where it began often enough; the row's click would then switch it.
            e.Handled = true;
        }

        _pressedRow = null;
        _draggingRow = false;
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_viewModel?.OpenTextEditor is { } editor && !string.IsNullOrEmpty(e.Text))
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

    private void OnTabsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LauncherTabsViewModel.SelectedTab))
        {
            Dispatcher.UIThread.Post(() => _tabs.Center(_viewModel?.LauncherTabs.SelectedTab), DispatcherPriority.Loaded);
        }
    }

    private void OnLibraryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.FocusedGame))
        {
            // Wait for the grid to realize the new page before scrolling to the tile.
            Dispatcher.UIThread.Post(BringFocusedTileIntoView, DispatcherPriority.Loaded);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.GameMenu) when _viewModel?.GameMenu is not null:
                // Hidden until placed, so it doesn't flash at its default position.
                GameMenuPanel.Opacity = 0;
                Dispatcher.UIThread.Post(PlaceGameMenu, DispatcherPriority.Loaded);
                break;
            case nameof(MainViewModel.IsTabStripShown):
                Dispatcher.UIThread.Post(_tabs.UpdateDividers, DispatcherPriority.Loaded);
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
        if (_viewModel?.Library.FocusedGame is not { } tile)
        {
            return;
        }

        var index = _viewModel.Library.PageGames.IndexOf(tile);
        if (index == 0)
        {
            GridScroller.Offset = default;
        }
        else if (index > 0 && GameGrid.ContainerFromIndex(index) is { } cover)
        {
            GridFade.ScrollIntoView(cover);
        }
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
        FollowInfoRow(page?.FocusedRow);
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
        FollowInfoRow(page.FocusedRow);
        InfoScroller.Offset = default;
        UpdateInfoOverflow();
        var byPointer = _focusedByPointer;
        _focusedByPointer = false;
        if (!byPointer && page.FocusedRow is { } row && SettingsRows.ContainerFromItem(row) is { } container)
        {
            // Clear of the fades with its focus ring, and at a group's start with the group's heading above it.
            SettingsRowsFade.ScrollIntoView(container, row.StartsGroup ? new Thickness(0, SettingsGroupRoom, 0, 0) : default);
        }
    }, DispatcherPriority.Loaded);

    /// <summary>The row the info panel explains: a change of its value or of its greying brings the panel to its top (#506).</summary>
    private void FollowInfoRow(SettingsRowViewModel? row)
    {
        if (_infoRow is not null)
        {
            _infoRow.PropertyChanged -= OnInfoRowPropertyChanged;
        }

        _infoRow = row;
        if (row is not null)
        {
            row.PropertyChanged += OnInfoRowPropertyChanged;
        }
    }

    private void OnInfoRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is "ValueText" or nameof(SettingsRowViewModel.IsDimmed))
        {
            Dispatcher.UIThread.Post(() =>
            {
                InfoScroller.Offset = default;
                UpdateInfoOverflow();
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnInfoScrollRequested(object? sender, int direction)
    {
        var max = Math.Max(0, InfoScroller.Extent.Height - InfoScroller.Viewport.Height);
        InfoScroller.Offset = new Vector(0, Math.Clamp(InfoScroller.Offset.Y + (direction * InfoScrollStep), 0, max));
    }

    /// <summary>While the info panel can scroll, the footer names RS.</summary>
    private void UpdateInfoOverflow()
    {
        var max = InfoScroller.Extent.Height - InfoScroller.Viewport.Height;
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
        // Where the button stands at rest: a click leaves it shrinking back from Fluent's pressed scale for a moment.
        if (!PreviewOptionsButton.IsEffectivelyVisible
            || PreviewOptionsButton.GetVisualParent() is not { } parent
            || parent.TranslatePoint(PreviewOptionsButton.Bounds.Position, GameMenuLayer) is not { } topLeft)
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

        return _viewModel?.Library.FocusedGame is { } tile ? GameGrid.ContainerFromIndex(_viewModel.Library.PageGames.IndexOf(tile)) : null;
    }
}
