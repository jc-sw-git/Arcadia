using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Core.Library;
using Arcadia.Core.Search;
using Arcadia.Input;
using Arcadia.Launchers.Abstractions;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Search;

/// <summary>A launcher chip; the first one stands for all launchers. The mouse switches them, the controller uses the filter (L3).</summary>
public sealed partial class SearchChipViewModel : ViewModelBase
{
    public SearchChipViewModel(LauncherInfo? launcher, LauncherBadge? badge, Action<LauncherInfo?> toggle)
    {
        Launcher = launcher;
        Badge = badge;
        Name = launcher?.DisplayName ?? "Alle Launcher";
        Keyword = launcher is null ? string.Empty : $"{SearchQuery.PrefixMarker}{launcher.SearchKeyword}";
        ToggleCommand = new RelayCommand(() => toggle(launcher));
    }

    /// <summary>Null for "Alle Launcher".</summary>
    public LauncherInfo? Launcher { get; }

    public bool IsLauncher => Launcher is not null;

    public string Name { get; }

    public string Keyword { get; }

    /// <summary>The launcher's logo (#138); null for "Alle Launcher".</summary>
    public LauncherBadge? Badge { get; }

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    public IRelayCommand ToggleCommand { get; }
}

/// <summary>What the hit area shows while there are no hits: waiting for a term, or none found.</summary>
/// <param name="IsNoHits">False while nothing is typed yet.</param>
public sealed record SearchEmptyState(bool IsNoHits, string Title, string Text);

/// <summary>
/// The search over all launchers (#12): a text that may start with a <c>/launcher</c> prefix, the
/// launchers it covers (chips, filter, prefix, kept in step), the hits and Arcadia's on-screen keyboard
/// (<see cref="OnScreenKeyboardViewModel"/>; without tab and the edge key, ↵ starts the focused hit, the arrows
/// move the caret). With the keyboard out the hits sit in one row above it: the D-pad (left stick) types, the right
/// stick picks a hit, LT/RT turn the hits. With the keyboard in (B, a real keyboard, the button
/// below the hits) they fill a grid that scrolls up and down, and the D-pad moves through them too;
/// X brings the keyboard back. Starting a hit, its details, its menu and the filter dialog are the
/// window's business (<see cref="MainViewModel"/>).
/// </summary>
public sealed partial class SearchViewModel : ViewModelBase, IPage, IKeyboardTarget, IDisposable
{
    /// <summary>Hits per row, in both layouts.</summary>
    public const int ResultColumns = 7;

    /// <summary>Rows of hits in a window with the on-screen keyboard in; beside it there is room for one.</summary>
    public const int ResultRowsWithoutKeyboard = 2;

    private readonly Func<Game, Action<GameTileViewModel>, GameTileViewModel> _createTile;
    private readonly LauncherTabsViewModel _tabs;
    private IReadOnlyList<LauncherLibrary> _libraries;
    private GameSearch _search;
    private IReadOnlyList<LauncherInfo> _launchers = [];
    private HashSet<string> _chosen = [];
    private LauncherScope _scope = new([], [], true);
    private HitWindow _window;
    private int _focus;
    private bool _hitsChanged;
    private List<GameTileViewModel> _results = [];
    private TextWithCaret _text = TextWithCaret.AtEnd(string.Empty);
    private readonly Func<LauncherInfo, LauncherBadge> _badgeOf;

    /// <param name="tabs">The launcher tabs; the search covers the ones with games.</param>
    public SearchViewModel(
        LauncherTabsViewModel tabs,
        KeyboardLayout layout,
        Action<KeyboardLayout> layoutChosen,
        Func<Game, Action<GameTileViewModel>, GameTileViewModel> createTile,
        Func<LauncherInfo, LauncherBadge> badgeOf,
        bool usesController,
        TimeProvider time,
        Action<Action> post)
    {
        _tabs = tabs;
        _libraries = tabs.SearchedLibraries();
        _search = new GameSearch(_libraries.SelectMany(l => l.Games), Formatting.Culture);
        _createTile = createTile;
        _badgeOf = badgeOf;
        Keyboard = new OnScreenKeyboardViewModel(layout, this, time, post);
        Keyboard.LayoutChosen += layoutChosen;
        // A search field has no tab order to go through, and the keyboard is part of the page, with no edge to move to.
        Keyboard.Configure(key => key.Kind is BoardKeyKind.Tab or BoardKeyKind.Dock ? BoardKeyState.Hidden : BoardKeyState.Shown);
        UsesController = usesController;
        Keyboard.UsesController = usesController;
        IsKeyboardOut = usesController;
        _window = HitWindow.Of(0, ResultRows, ResultColumns);
        BuildChips();
        Recompute(keepFocusOn: null);
    }

    /// <summary>↵ on the on-screen keyboard: start the focused hit, as ≡ does.</summary>
    public event Action? StartRequested;

    /// <summary>The on-screen keyboard went in or the search closed; the mouse mode lets go of it (#110).</summary>
    public event Action? KeyboardClosed;

    /// <summary>
    /// The hits on screen moved. The argument is where the window started before, so the view can
    /// slide from there; null when it should just jump (a new search, the other layout).
    /// </summary>
    public event Action<int?>? WindowMoved;

    /// <summary>What was typed, prefix included.</summary>
    public string Text => _text.Text;

    /// <summary>The field shows the caret between these two; the on-screen keyboard's arrows move it.</summary>
    public string TextBeforeCaret => _text.BeforeCaret;

    public string TextAfterCaret => _text.AfterCaret;

    public ObservableCollection<SearchChipViewModel> Chips { get; } = [];

    /// <summary>The launchers the search can cover, in tab order.</summary>
    public IReadOnlyList<LauncherInfo> Launchers => _launchers;

    /// <summary>The launchers the search covers now.</summary>
    public LauncherScope Scope => _scope;

    /// <summary>"Suche in allen Launchern", "Per Kürzel gefiltert: Steam, EA app".</summary>
    [ObservableProperty]
    public partial string ScopeText { get; private set; } = string.Empty;

    /// <summary>"Unbekanntes Kürzel: /xyz", or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnknownKeywords))]
    public partial string? UnknownKeywordsText { get; private set; }

    public bool HasUnknownKeywords => UnknownKeywordsText is not null;

    /// <summary>"23 Treffer in 2 Launchern".</summary>
    [ObservableProperty]
    public partial string ResultsTitle { get; private set; } = string.Empty;

    /// <summary>"8–14 von 23" when the hits don't all fit, else empty.</summary>
    [ObservableProperty]
    public partial string RangeText { get; private set; } = string.Empty;

    /// <summary>Shown instead of the hits while there are none; null while there are.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHits))]
    public partial SearchEmptyState? Empty { get; private set; }

    public bool HasHits => Empty is null;

    /// <summary>The library's sort orders, or null for relevance, the default of every new search.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortText))]
    public partial SortOrder? Sort { get; private set; }

    public string SortText => $"Sortiert nach {Formatting.SortLabel(Sort)}";

    /// <summary>All hits in the chosen order; the detail page turns through these.</summary>
    public IReadOnlyList<Game> Hits { get; private set; } = [];

    /// <summary>
    /// The hits around the window: the window itself and one window's worth before and after it,
    /// so a step or a turn can slide them in. The first one is hit <see cref="SliceStart"/>.
    /// </summary>
    public IReadOnlyList<GameTileViewModel> Results => _results;

    /// <summary>
    /// The places the view shows <see cref="Results"/> in, in the same order; places beyond them are hidden.
    /// A place keeps its controls when it gets another tile: building a tile's controls takes milliseconds,
    /// and building them for every key press or step at the window's edge broke the budget for a focus move.
    /// </summary>
    public ObservableCollection<HitSlotViewModel> Slots { get; } = [];

    /// <summary>The index in <see cref="Hits"/> of the first of <see cref="Results"/>.</summary>
    public int SliceStart { get; private set; }

    /// <summary>The first hit of the window, the one at the leading edge of the hit area.</summary>
    public int WindowStart => _window.Start;

    /// <summary>More hits than fit: LT/RT turn them.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRowScrollBar), nameof(ShowsGridScrollBar))]
    public partial bool CanTurn { get; private set; }

    /// <summary>The scroll bar runs across below the row of hits beside the keyboard, and down beside the grid without it.</summary>
    public bool ShowsRowScrollBar => CanTurn && IsKeyboardOut;

    public bool ShowsGridScrollBar => CanTurn && !IsKeyboardOut;

    // The scroll bar beside the hits: where the window starts, the last place it can start, and how many hits it shows.
    [ObservableProperty]
    public partial int ScrollStart { get; private set; }

    [ObservableProperty]
    public partial int ScrollEnd { get; private set; }

    [ObservableProperty]
    public partial int ScrollViewport { get; private set; }

    public OnScreenKeyboardViewModel Keyboard { get; }

    /// <summary>The controller is in use: the button legend and the buttons' glyphs show.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsStickHint))]
    public partial bool UsesController { get; set; }

    /// <summary>The mouse mode types on the keyboard (Y, #110): its buttons are the keyboard's, the sticks and triggers the mouse's.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsStickHint))]
    public partial bool IsInMouseMode { get; private set; }

    /// <summary>The on-screen keyboard is out, and the hits get one row above it; in, they fill a grid.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsStickHint), nameof(ShowsRowScrollBar), nameof(ShowsGridScrollBar))]
    public partial bool IsKeyboardOut { get; set; }

    /// <summary>Beside the keyboard only the right stick picks a hit, so it's said above the hits too, not only in the legend.</summary>
    public bool ShowsStickHint => UsesController && IsKeyboardOut && !IsInMouseMode;

    private int ResultRows => IsKeyboardOut ? 1 : ResultRowsWithoutKeyboard;

    /// <summary>The hit that ≡, ⧉, Enter and the right stick act on; null without hits.</summary>
    [ObservableProperty]
    public partial GameTileViewModel? FocusedResult { get; private set; }

    /// <summary>
    /// Everything the search does itself. With the keyboard out: the D-pad and A on the keys, and
    /// the buttons that stand for keys (X deletes, held: one after another; Y types a space; LB
    /// shift; RB AltGr, both tapped or held; R3 the layout menu). With it in, the D-pad moves
    /// through the hits and X brings the keyboard back. The right stick or a real keyboard's arrows
    /// move through the hits, LT/RT or Page Up/Down turn them.
    /// </summary>
    public bool Handle(UiAction action)
    {
        if (Keyboard.LayoutMenu.IsOpen)
        {
            if (KeyboardButtonOf(action) is { } button)
            {
                Keyboard.ButtonDown(button);
            }

            return true;
        }

        switch (action)
        {
            // Backspace repeats by itself; X repeats while held.
            case UiAction.Search when UsesController && !IsKeyboardOut:
                IsKeyboardOut = true;
                break;
            case UiAction.Search when UsesController:
                Keyboard.ButtonDown(KeyboardButton.Erase);
                break;
            case UiAction.Search:
                Erase();
                break;
            // LT/RT turn the hits, as they turn pages in the library and games on the detail page.
            case UiAction.PreviousPage:
                Turn(-1);
                break;
            case UiAction.NextPage:
                Turn(1);
                break;
            case UiAction.SecondaryUp:
                MoveHit(0, -1);
                break;
            case UiAction.SecondaryDown:
                MoveHit(0, 1);
                break;
            case UiAction.SecondaryLeft:
                MoveHit(-1, 0);
                break;
            case UiAction.SecondaryRight:
                MoveHit(1, 0);
                break;
            default:
                if (IsKeyboardOut)
                {
                    HandleKeyboard(action);
                }
                else
                {
                    HandleHits(action);
                }

                break;
        }

        return true;
    }

    /// <summary>B: closes the layout menu, else puts the keyboard away. False if there was neither, so B leaves the search.</summary>
    public bool Back()
    {
        if (Keyboard.LayoutMenu.IsOpen)
        {
            Keyboard.LayoutMenu.Close();
            return true;
        }

        if (IsKeyboardOut)
        {
            IsKeyboardOut = false;
            return true;
        }

        return false;
    }

    /// <summary>A button was let go: ends a held erase, shift or AltGr.</summary>
    public void Release(UiAction action)
    {
        if (KeyboardButtonOf(action) is { } button)
        {
            Keyboard.ButtonUp(button);
        }
    }

    /// <summary>Drops held buttons, e.g. when the window loses focus and their release may never arrive.</summary>
    public void CancelHeldButtons() => Keyboard.CancelPresses();

    /// <summary>The hits; also while a detail page opened from them covers the search.</summary>
    public IEnumerable<GameTileViewModel> ShownTiles => _results;

    /// <summary>The controller shows the button legend; a real keyboard puts the on-screen one away; the mouse leaves it.</summary>
    public void InputKindChanged(InputKind kind)
    {
        UsesController = kind == InputKind.Gamepad;
        if (kind == InputKind.Keyboard)
        {
            IsKeyboardOut = false;
        }
    }

    /// <summary>Y in the mouse mode with the search in front (#110): the keyboard comes out and takes the mouse mode's buttons.</summary>
    public void StartMouseModeTyping()
    {
        IsKeyboardOut = true;
        IsInMouseMode = true;
        Keyboard.UsesController = true;
    }

    /// <summary>The mouse mode let go of the keyboard; it stays out for the controller or the mouse.</summary>
    public void EndMouseModeTyping()
    {
        IsInMouseMode = false;
        Keyboard.CancelPresses();
    }

    /// <summary>The button below the hits: the keyboard out or back in.</summary>
    [RelayCommand]
    private void ToggleKeyboard() => IsKeyboardOut = !IsKeyboardOut;

    /// <summary>The LT button beside the hits.</summary>
    [RelayCommand]
    private void TurnBack() => Turn(-1);

    /// <summary>The RT button beside the hits.</summary>
    [RelayCommand]
    private void TurnOn() => Turn(1);

    /// <summary>Text from a real keyboard or the on-screen one, at the caret; control characters are dropped.</summary>
    public void Type(string text)
    {
        var typed = new string(text.Where(c => !char.IsControl(c)).ToArray());
        if (typed.Length > 0)
        {
            SetText(_text.Insert(typed));
        }
    }

    /// <summary>Backspace: the character before the caret.</summary>
    public void Erase()
    {
        if (_text.Caret > 0)
        {
            SetText(_text.EraseBefore());
        }
    }

    void IKeyboardTarget.Type(char character) => Type(character.ToString());

    /// <summary>↵ starts the focused hit; the arrows move the caret, up and down to the start and the end of the text.</summary>
    void IKeyboardTarget.Press(BoardKeyKind key, bool withShift)
    {
        switch (key)
        {
            case BoardKeyKind.Backspace:
                Erase();
                break;
            case BoardKeyKind.Enter:
                StartRequested?.Invoke();
                break;
            case BoardKeyKind.Left:
                MoveCaret(_text.Left());
                break;
            case BoardKeyKind.Right:
                MoveCaret(_text.Right());
                break;
            case BoardKeyKind.Up:
                MoveCaret(_text.Home());
                break;
            case BoardKeyKind.Down:
                MoveCaret(_text.End());
                break;
        }
    }

    void IKeyboardTarget.Hide() => IsKeyboardOut = false;

    void IKeyboardTarget.Dock(bool atTop)
    {
        // The search's keyboard has no edge key (Configure in the constructor).
    }

    /// <summary>
    /// A chip or a filter entry switched <paramref name="launcher"/> (null: all launchers). A typed
    /// prefix is rewritten to match, so it never contradicts the chips; without one the text stays.
    /// </summary>
    public void ToggleLauncher(LauncherInfo? launcher)
    {
        var all = _launchers.Select(l => l.Id).ToList();
        IReadOnlyList<string>? next = launcher is null ? all : _scope.Toggle(launcher.Id, _launchers);
        if (next is null)
        {
            return;
        }

        // As in the design: with a prefix only the prefix changes, so deleting it brings the chips back.
        if (next.Count == all.Count || !SearchQuery.Parse(Text).HasLauncherFilter)
        {
            _chosen = [.. next];
        }

        SetText(TextWithCaret.AtEnd(LauncherScope.Rewrite(Text, next, _launchers)));
    }

    /// <summary>A new order for the hits; as in the library, the focus goes to the first one.</summary>
    public void SetSort(SortOrder? sort)
    {
        Sort = sort;
        Recompute(keepFocusOn: null);
    }

    /// <summary>The library was rescanned: same input, fresh hits, the focus stays on its game where it can.</summary>
    public void Update()
    {
        _libraries = _tabs.SearchedLibraries();
        _search = new GameSearch(_libraries.SelectMany(l => l.Games), Formatting.Culture);
        BuildChips();
        Recompute(FocusedResult?.Game.Key);
    }

    /// <summary>Back from the detail page: the focus goes to the hit shown last.</summary>
    public void FocusHit(string key)
    {
        var index = IndexOf(key);
        if (index >= 0)
        {
            _focus = index;
            _window = _window.Showing(index);
            ShowWindow(previousStart: null);
        }
    }

    /// <summary>The mouse wheel over the hits.</summary>
    public void Scroll(int delta) => MoveWindow(_window.Scroll(delta));

    /// <summary>The scroll bar beside the hits was dragged or clicked.</summary>
    public void ScrollTo(double start) => MoveWindow(_window.ScrolledTo((int)Math.Round(start)));

    private void MoveWindow(HitWindow scrolled)
    {
        if (scrolled != _window)
        {
            var previous = _window.Start;
            _window = scrolled;
            _focus = Math.Clamp(_focus, _window.Start, Math.Min(Hits.Count, _window.Start + _window.Size) - 1);
            ShowWindow(previous);
        }
    }

    public void Dispose()
    {
        if (IsKeyboardOut)
        {
            KeyboardClosed?.Invoke();
        }

        Keyboard.Dispose();
    }

    partial void OnUsesControllerChanged(bool value) => Keyboard.UsesController = value;

    partial void OnIsKeyboardOutChanged(bool value)
    {
        if (!value)
        {
            Keyboard.Closed();
            KeyboardClosed?.Invoke();
        }

        _window = _window.WithLayout(ResultRows, ResultColumns, _focus);
        ShowWindow(previousStart: null);
    }

    /// <summary>The D-pad and the buttons that stand for keys, while the keyboard is out.</summary>
    private void HandleKeyboard(UiAction action)
    {
        if (KeyboardButtonOf(action) is { } button)
        {
            Keyboard.ButtonDown(button);
        }
    }

    /// <summary>
    /// The keyboard's meaning of Arcadia's buttons: the D-pad and A on the keys, X erases, Y space, LB shift, RB
    /// AltGr, R3 the layout menu, B puts it away (closing the layout menu first).
    /// </summary>
    private static KeyboardButton? KeyboardButtonOf(UiAction action) => action switch
    {
        UiAction.Up => KeyboardButton.Up,
        UiAction.Down => KeyboardButton.Down,
        UiAction.Left => KeyboardButton.Left,
        UiAction.Right => KeyboardButton.Right,
        UiAction.Accept => KeyboardButton.Type,
        UiAction.Search => KeyboardButton.Erase,
        UiAction.ViewOptions => KeyboardButton.Space,
        UiAction.PreviousTab => KeyboardButton.Shift,
        UiAction.NextTab => KeyboardButton.AltGr,
        UiAction.KeyboardLayout => KeyboardButton.Layout,
        UiAction.Back => KeyboardButton.Hide,
        _ => null,
    };

    /// <summary>The D-pad, or a real keyboard's arrows, while the keyboard is in.</summary>
    private void HandleHits(UiAction action)
    {
        switch (action)
        {
            case UiAction.Up:
                MoveHit(0, -1);
                break;
            case UiAction.Down:
                MoveHit(0, 1);
                break;
            case UiAction.Left:
                MoveHit(-1, 0);
                break;
            case UiAction.Right:
                MoveHit(1, 0);
                break;
        }
    }

    /// <summary>LT/RT: the next or previous window of hits; the row wraps round, the grid stops at its ends.</summary>
    private void Turn(int direction)
    {
        if (_window.CanTurn)
        {
            var previous = _window.Start;
            (_window, _focus) = _window.Turn(direction, _focus);
            ShowWindow(previous);
        }
    }

    private void MoveHit(int columns, int rows)
    {
        // A single row has nowhere to go up or down.
        if (Hits.Count == 0 || (rows != 0 && ResultRows == 1))
        {
            return;
        }

        var previous = _window.Start;
        (_window, _focus) = _window.Move(_focus, columns, rows);
        if (_window.Start != previous)
        {
            ShowWindow(previous);
        }
        else
        {
            ShowFocus();
        }
    }

    private void SetText(TextWithCaret text)
    {
        _text = text;
        OnPropertyChanged(nameof(Text));
        ShowCaret();
        Recompute(keepFocusOn: null);
    }

    private void MoveCaret(TextWithCaret text)
    {
        _text = text;
        ShowCaret();
    }

    private void ShowCaret()
    {
        OnPropertyChanged(nameof(TextBeforeCaret));
        OnPropertyChanged(nameof(TextAfterCaret));
    }

    private void BuildChips()
    {
        _launchers = _libraries.Select(l => l.Launcher).ToList();

        // Launchers that are new to this search start switched on; ones that are gone drop out.
        var known = Chips.Where(c => c.Launcher is not null).Select(c => c.Launcher!.Id).ToHashSet();
        _chosen = _launchers.Select(l => l.Id).Where(id => _chosen.Contains(id) || !known.Contains(id)).ToHashSet();
        if (_chosen.Count == 0)
        {
            _chosen = _launchers.Select(l => l.Id).ToHashSet();
        }

        Chips.Clear();
        Chips.Add(new SearchChipViewModel(null, null, ToggleLauncher));
        foreach (var launcher in _launchers)
        {
            Chips.Add(new SearchChipViewModel(launcher, _badgeOf(launcher), ToggleLauncher));
        }
    }

    private void Recompute(string? keepFocusOn)
    {
        var query = SearchQuery.Parse(Text);
        _scope = LauncherScope.Of(query, _launchers, _chosen);
        foreach (var chip in Chips)
        {
            chip.IsOn = chip.Launcher is { } launcher ? _scope.LauncherIds.Contains(launcher.Id) : _scope.IsAll;
        }

        var names = _launchers.Where(l => _scope.LauncherIds.Contains(l.Id)).Select(l => l.DisplayName).ToList();
        ScopeText = _scope.IsAll ? "Suche in allen Launchern"
            : (query.HasLauncherFilter ? "Per Kürzel gefiltert: " : "Suche in: ") + (names.Count > 0 ? string.Join(", ", names) : "–");
        UnknownKeywordsText = _scope.UnknownKeywords.Count switch
        {
            0 => null,
            1 => $"Unbekanntes Kürzel: {SearchQuery.PrefixMarker}{_scope.UnknownKeywords[0]}",
            _ => "Unbekannte Kürzel: " + string.Join(", ", _scope.UnknownKeywords.Select(k => $"{SearchQuery.PrefixMarker}{k}")),
        };

        Hits = _search.Find(query.Term, _scope.LauncherIds.ToHashSet(), Sort);
        _hitsChanged = true;
        var launchersHit = Hits.Select(g => g.LauncherId).Distinct().Count();
        ResultsTitle = $"{Hits.Count} Treffer in {launchersHit} {(launchersHit == 1 ? "Launcher" : "Launchern")}";
        Empty = query.Term.Trim().Length == 0
            ? new SearchEmptyState(false, "Tippe einen Suchbegriff ein", "Arcadia sucht in den Titeln der Spiele aller gewählten Launcher.")
            : Hits.Count == 0
                ? new SearchEmptyState(true, "Keine Treffer", $"Kein Spiel passt zu „{query.Term.Trim()}“. Prüfe die Schreibweise oder nimm weitere Launcher dazu.")
                : null;

        // A rescan keeps the focus on its game; new input starts again at the first hit.
        var kept = keepFocusOn is null ? -1 : IndexOf(keepFocusOn);
        if (kept >= 0)
        {
            _focus = kept;
            _window = _window.WithCount(Hits.Count).Showing(kept);
        }
        else
        {
            _focus = keepFocusOn is null ? 0 : Math.Clamp(_focus, 0, Math.Max(0, Hits.Count - 1));
            _window = (keepFocusOn is null ? HitWindow.Of(Hits.Count, ResultRows, ResultColumns) : _window.WithCount(Hits.Count)).Showing(_focus);
        }

        ShowWindow(previousStart: null);
    }

    /// <summary>
    /// Shows the window and a window's worth of hits on either side, so a step or turn can slide
    /// them in. Tiles of games already shown are kept, with their state (focus, a hold in progress).
    /// </summary>
    private void ShowWindow(int? previousStart)
    {
        var sliceStart = Math.Max(0, _window.Start - _window.Size);
        var sliceEnd = Math.Min(Hits.Count, _window.Start + (2 * _window.Size));
        if (_hitsChanged || sliceStart != SliceStart || sliceEnd - sliceStart != Results.Count)
        {
            var shown = new Dictionary<string, GameTileViewModel>();
            foreach (var tile in Results)
            {
                shown.TryAdd(tile.Game.Key, tile);
            }

            _results = Hits.Take(sliceEnd).Skip(sliceStart)
                .Select(game => shown.TryGetValue(game.Key, out var tile) && tile.Game == game ? tile : _createTile(game, FocusByPointer))
                .ToList();
            FillSlots();

            SliceStart = sliceStart;
            _hitsChanged = false;
        }

        CanTurn = _window.CanTurn;
        ScrollStart = _window.Start;
        ScrollEnd = _window.LastStart;
        ScrollViewport = _window.Size;
        RangeText = _window.CanTurn ? $"{_window.Start + 1}–{Math.Min(Hits.Count, _window.Start + _window.Size)} von {Hits.Count}" : string.Empty;
        ShowFocus();
        WindowMoved?.Invoke(previousStart);
    }

    /// <summary>The place of <paramref name="tile"/> in <see cref="Results"/> and <see cref="Slots"/>; -1 if it isn't shown.</summary>
    public int SlotOf(GameTileViewModel tile) => _results.IndexOf(tile);

    private void FillSlots()
    {
        for (var i = 0; i < Math.Max(Slots.Count, Results.Count); i++)
        {
            if (i >= Slots.Count)
            {
                Slots.Add(new HitSlotViewModel(Results[i]));
            }
            else if (i < Results.Count)
            {
                Slots[i].Tile = Results[i];
                Slots[i].IsShown = true;
            }
            else
            {
                Slots[i].IsShown = false;
            }
        }
    }

    private void ShowFocus()
    {
        if (FocusedResult is { } previous)
        {
            previous.IsFocused = false;
        }

        var slot = _focus - SliceStart;
        FocusedResult = Hits.Count > 0 && slot >= 0 && slot < Results.Count ? Results[slot] : null;
        if (FocusedResult is { } focused)
        {
            focused.IsFocused = true;
        }
    }

    /// <summary>A click on a hit focuses it; one only partly on screen comes into view.</summary>
    private void FocusByPointer(GameTileViewModel tile)
    {
        var index = SlotOf(tile);
        if (index < 0)
        {
            return;
        }

        _focus = SliceStart + index;
        if (_window.Contains(_focus))
        {
            ShowFocus();
            return;
        }

        var previous = _window.Start;
        _window = _window.Showing(_focus);
        ShowWindow(previous);
    }

    private int IndexOf(string key)
    {
        for (var i = 0; i < Hits.Count; i++)
        {
            if (Hits[i].Key == key)
            {
                return i;
            }
        }

        return -1;
    }
}
