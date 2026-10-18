using System.Collections.ObjectModel;
using Arcadia.App.Input;
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
    public SearchChipViewModel(LauncherInfo? launcher, Action<LauncherInfo?> toggle)
    {
        Launcher = launcher;
        Name = launcher?.DisplayName ?? "Alle Launcher";
        Keyword = launcher is null ? string.Empty : $"{SearchQuery.PrefixMarker}{launcher.SearchKeyword}";
        Monogram = launcher?.DisplayName[..1] ?? string.Empty;
        TileBrush = launcher is null ? null : Formatting.LauncherTileBrush(launcher.Id);
        ToggleCommand = new RelayCommand(() => toggle(launcher));
    }

    /// <summary>Null for "Alle Launcher".</summary>
    public LauncherInfo? Launcher { get; }

    public bool IsLauncher => Launcher is not null;

    public string Name { get; }

    public string Keyword { get; }

    public string Monogram { get; }

    public IBrush? TileBrush { get; }

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    public IRelayCommand ToggleCommand { get; }
}

/// <summary>
/// The search over all launchers (#12): a text that may start with a <c>/launcher</c> prefix, the
/// launchers it covers (chips, filter, prefix, kept in step), the hits and an on-screen keyboard.
/// With the controller the keyboard shows and the hits sit in one row beside it: the D-pad (left
/// stick) types, the right stick picks a hit, LB/RB turn the hits. With a real keyboard the
/// on-screen one hides and the hits fill two rows. Starting a hit, its details, its menu and the
/// filter dialog are the window's business (<see cref="MainViewModel"/>).
/// </summary>
public sealed partial class SearchViewModel : ViewModelBase, IDisposable
{
    /// <summary>Hits per row, in both layouts.</summary>
    public const int ResultColumns = 7;

    /// <summary>Rows of hits without the on-screen keyboard; beside it there is room for one.</summary>
    public const int ResultRowsWithoutKeyboard = 2;

    // As a held Backspace on a typical keyboard.
    private static readonly TimeSpan EraseRepeatDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan EraseRepeatInterval = TimeSpan.FromMilliseconds(60);

    private readonly Func<Game, Action<GameTileViewModel>, GameTileViewModel> _createTile;
    private readonly RepeatGesture _erase;
    private IReadOnlyList<LauncherLibrary> _libraries;
    private IReadOnlyList<LauncherInfo> _launchers = [];
    private HashSet<string> _chosen = [];
    private LauncherScope _scope = new([], [], true);
    private HitWindow _window;
    private int _focus;

    public SearchViewModel(
        IReadOnlyList<LauncherLibrary> libraries,
        KeyboardLayout layout,
        Action<KeyboardLayout> layoutChosen,
        Func<Game, Action<GameTileViewModel>, GameTileViewModel> createTile,
        bool usesController,
        TimeProvider time,
        Action<Action> post)
    {
        _libraries = libraries;
        _createTile = createTile;
        UsesController = usesController;
        _window = HitWindow.Of(0, ResultRows, ResultColumns);
        Keyboard = new OnScreenKeyboardViewModel(layout, Press, layoutChosen, time);
        _erase = new RepeatGesture(EraseRepeatDelay, EraseRepeatInterval, time, post);
        _erase.Repeated += Erase;
        BuildChips();
        Recompute(keepFocusOn: null);
    }

    /// <summary>The filter key on the on-screen keyboard was clicked.</summary>
    public event Action? FilterRequested;

    /// <summary>What was typed, prefix included.</summary>
    [ObservableProperty]
    public partial string Text { get; private set; } = string.Empty;

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

    [ObservableProperty]
    public partial string ResultsTitle { get; private set; } = string.Empty;

    /// <summary>"8–14 von 23" when the hits don't all fit, else empty.</summary>
    [ObservableProperty]
    public partial string RangeText { get; private set; } = string.Empty;

    /// <summary>The library's sort orders, or null for relevance, the default of every new search.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SortText))]
    public partial SortOrder? Sort { get; private set; }

    public string SortText => $"Sortiert nach {Formatting.SortLabel(Sort)}";

    /// <summary>All hits in the chosen order; the detail page turns through these.</summary>
    public IReadOnlyList<Game> Hits { get; private set; } = [];

    /// <summary>The hits currently in view.</summary>
    public ObservableCollection<GameTileViewModel> Results { get; } = [];

    /// <summary>The hit before the window, shown faded at its left edge; a click turns back.</summary>
    [ObservableProperty]
    public partial GameTileViewModel? PeekBefore { get; private set; }

    /// <summary>The hit after the window, shown faded at its right edge; a click turns on.</summary>
    [ObservableProperty]
    public partial GameTileViewModel? PeekAfter { get; private set; }

    public OnScreenKeyboardViewModel Keyboard { get; }

    /// <summary>The controller is in use: the on-screen keyboard and the button legend show, the hits get one row.</summary>
    [ObservableProperty]
    public partial bool UsesController { get; set; }

    private int ResultRows => UsesController ? 1 : ResultRowsWithoutKeyboard;

    /// <summary>The hit that ≡, ⧉, Enter and the right stick act on; null without hits.</summary>
    [ObservableProperty]
    public partial GameTileViewModel? FocusedResult { get; private set; }

    /// <summary>
    /// Everything the search does itself: the D-pad and A on the keys, and the buttons that stand
    /// for keys (X deletes, held: one after another; Y types a space; LT shift; RT symbols; R3 the
    /// layout menu); the right stick or a real keyboard's arrows move through the hits, LB/RB turn them.
    /// </summary>
    public void Handle(UiAction action)
    {
        if (Keyboard.IsLayoutMenuOpen)
        {
            if (action is UiAction.KeyboardLayout or UiAction.Back)
            {
                Keyboard.CloseLayoutMenu();
            }
            else
            {
                Keyboard.HandleLayoutMenu(action);
            }

            return;
        }

        switch (action)
        {
            // Backspace repeats by itself; X repeats while held.
            case UiAction.Search when UsesController:
                _erase.Press();
                break;
            case UiAction.Search:
                Erase();
                break;
            case UiAction.ViewOptions:
                Type(" ");
                break;
            case UiAction.PreviousPage:
                Keyboard.PressShift();
                break;
            case UiAction.NextPage:
                Keyboard.ToggleSymbols();
                break;
            case UiAction.KeyboardLayout:
                Keyboard.ToggleLayoutMenu();
                break;
            case UiAction.PreviousTab:
                Turn(-1);
                break;
            case UiAction.NextTab:
                Turn(1);
                break;
            case UiAction.Accept when UsesController:
                PressFocusedKey();
                break;
            case UiAction.Up or UiAction.Down or UiAction.Left or UiAction.Right when UsesController:
                Keyboard.Move(action);
                break;
            case UiAction.Up or UiAction.SecondaryUp:
                MoveHit(0, -1);
                break;
            case UiAction.Down or UiAction.SecondaryDown:
                MoveHit(0, 1);
                break;
            case UiAction.Left or UiAction.SecondaryLeft:
                MoveHit(-1, 0);
                break;
            case UiAction.Right or UiAction.SecondaryRight:
                MoveHit(1, 0);
                break;
        }
    }

    /// <summary>A button was let go: ends a held erase or shift.</summary>
    public void Release(UiAction action)
    {
        switch (action)
        {
            case UiAction.Search or UiAction.Accept:
                _erase.Release();
                break;
            case UiAction.PreviousPage:
                Keyboard.ReleaseShift();
                break;
        }
    }

    /// <summary>Drops held buttons, e.g. when the window loses focus and their release may never arrive.</summary>
    public void CancelHeldButtons()
    {
        _erase.Release();
        Keyboard.CancelShift();
    }

    /// <summary>Text from a real keyboard; control characters are dropped.</summary>
    public void Type(string text)
    {
        var typed = new string(text.Where(c => !char.IsControl(c)).ToArray());
        if (typed.Length > 0)
        {
            SetText(Text + typed);
        }
    }

    public void Erase()
    {
        if (Text.Length > 0)
        {
            // A surrogate pair is one character to the user.
            var cut = Text.Length > 1 && char.IsLowSurrogate(Text[^1]) ? 2 : 1;
            SetText(Text[..^cut]);
        }
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

        Text = LauncherScope.Rewrite(Text, next, _launchers);
        Recompute(keepFocusOn: null);
    }

    /// <summary>A new order for the hits; as in the library, the focus goes to the first one.</summary>
    public void SetSort(SortOrder? sort)
    {
        Sort = sort;
        Recompute(keepFocusOn: null);
    }

    /// <summary>The library was rescanned: same input, fresh hits, the focus stays on its game where it can.</summary>
    public void Update(IReadOnlyList<LauncherLibrary> libraries)
    {
        _libraries = libraries;
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
            ShowWindow();
        }
    }

    /// <summary>The mouse wheel over the hits.</summary>
    public void Scroll(int delta)
    {
        var scrolled = _window.Scroll(delta);
        if (scrolled != _window)
        {
            _window = scrolled;
            _focus = Math.Clamp(_focus, _window.Start, Math.Min(Hits.Count, _window.Start + _window.Size) - 1);
            ShowWindow();
        }
    }

    public void Dispose() => _erase.Dispose();

    partial void OnUsesControllerChanged(bool value)
    {
        _window = _window.WithLayout(ResultRows, ResultColumns, _focus);
        ShowWindow();
    }

    /// <summary>LB/RB, or a click on a faded neighbour: the next or previous window of hits, round and round.</summary>
    private void Turn(int direction)
    {
        if (_window.CanTurn)
        {
            (_window, _focus) = _window.Turn(direction, _focus);
            ShowWindow();
        }
    }

    private void MoveHit(int columns, int rows)
    {
        // A single row has nowhere to go up or down.
        if (Hits.Count == 0 || (rows != 0 && ResultRows == 1))
        {
            return;
        }

        var (window, focus) = _window.Move(_focus, columns, rows);
        var scrolled = window != _window;
        (_window, _focus) = (window, focus);
        if (scrolled)
        {
            ShowWindow();
        }
        else
        {
            ShowFocus();
        }
    }

    /// <summary>A on the focused key. Erase repeats while A is held, like X.</summary>
    private void PressFocusedKey()
    {
        if (Keyboard.FocusedKey.Special == SpecialKey.Erase)
        {
            _erase.Press();
        }
        else
        {
            Press(Keyboard.FocusedKey);
        }
    }

    /// <summary>A key pressed with A or clicked.</summary>
    private void Press(KeyViewModel key)
    {
        if (key.Character is not null)
        {
            Type(Keyboard.Typed(key));
            return;
        }

        switch (key.Special)
        {
            case SpecialKey.Layout:
                Keyboard.ToggleLayoutMenu();
                break;
            case SpecialKey.Filter:
                FilterRequested?.Invoke();
                break;
            case SpecialKey.Shift:
                Keyboard.ToggleShift();
                break;
            case SpecialKey.Space:
                Type(" ");
                break;
            case SpecialKey.Erase:
                Erase();
                break;
            case SpecialKey.Symbols:
                Keyboard.ToggleSymbols();
                break;
        }
    }

    private void SetText(string text)
    {
        Text = text;
        Recompute(keepFocusOn: null);
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
        Chips.Add(new SearchChipViewModel(null, ToggleLauncher));
        foreach (var launcher in _launchers)
        {
            Chips.Add(new SearchChipViewModel(launcher, ToggleLauncher));
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

        var games = _libraries.Where(l => _scope.LauncherIds.Contains(l.Launcher.Id)).SelectMany(l => l.Games);
        Hits = GameSearch.Find(games, query.Term, Formatting.Culture, Sort);
        var launchersHit = Hits.Select(g => g.LauncherId).Distinct().Count();
        ResultsTitle = query.Term.Length == 0 ? "Tippe einen Suchbegriff ein"
            : Hits.Count == 0 ? "Keine Treffer"
            : $"{Hits.Count} Treffer in {launchersHit} {(launchersHit == 1 ? "Launcher" : "Launchern")}";

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

        ShowWindow();
    }

    private void ShowWindow()
    {
        FocusedResult = null;
        Results.Clear();
        foreach (var game in Hits.Skip(_window.Start).Take(_window.Size))
        {
            Results.Add(_createTile(game, FocusByPointer));
        }

        PeekBefore = _window.Before is { } before ? _createTile(Hits[before], _ => Turn(-1)) : null;
        PeekAfter = _window.After is { } after ? _createTile(Hits[after], _ => Turn(1)) : null;
        RangeText = _window.CanTurn ? $"{_window.Start + 1}–{_window.Start + Results.Count} von {Hits.Count}" : string.Empty;
        ShowFocus();
    }

    private void ShowFocus()
    {
        if (FocusedResult is { } previous)
        {
            previous.IsFocused = false;
        }

        var slot = _focus - _window.Start;
        FocusedResult = slot >= 0 && slot < Results.Count ? Results[slot] : null;
        if (FocusedResult is { } focused)
        {
            focused.IsFocused = true;
        }
    }

    /// <summary>A click on a hit focuses it.</summary>
    private void FocusByPointer(GameTileViewModel tile)
    {
        var index = Results.IndexOf(tile);
        if (index >= 0)
        {
            _focus = _window.Start + index;
            ShowFocus();
        }
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
