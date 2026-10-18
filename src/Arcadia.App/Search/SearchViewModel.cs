using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Core.Library;
using Arcadia.Core.Search;
using Arcadia.Launchers.Abstractions;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Search;

/// <summary>Where the controller focus is on the search page, top to bottom.</summary>
public enum SearchZone
{
    Chips,
    Results,
    Keyboard,
}

/// <summary>A launcher chip; the first one stands for all launchers.</summary>
public sealed partial class SearchChipViewModel : ViewModelBase
{
    public SearchChipViewModel(LauncherInfo? launcher, Action<SearchChipViewModel> toggle)
    {
        Launcher = launcher;
        Name = launcher?.DisplayName ?? "Alle Launcher";
        Keyword = launcher is null ? string.Empty : $"{SearchQuery.PrefixMarker}{launcher.SearchKeyword}";
        Monogram = launcher?.DisplayName[..1] ?? string.Empty;
        TileBrush = launcher is null ? null : Formatting.LauncherTileBrush(launcher.Id);
        ToggleCommand = new RelayCommand(() => toggle(this));
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

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    public IRelayCommand ToggleCommand { get; }
}

/// <summary>
/// The search over all launchers (#12): a text that may start with a <c>/launcher</c> prefix,
/// launcher chips kept in step with it, the hits by relevance and an on-screen keyboard. Starting
/// a hit, its details and its menu are the window's business (<see cref="MainViewModel"/>).
/// </summary>
public sealed partial class SearchViewModel : ViewModelBase
{
    /// <summary>Hits shown at once; the row scrolls on with the focus (design: 7 and "+N weitere").</summary>
    public const int VisibleResults = 7;

    private readonly Func<Game, Action<GameTileViewModel>, GameTileViewModel> _createTile;
    private IReadOnlyList<LauncherLibrary> _libraries;
    private IReadOnlyList<LauncherInfo> _launchers = [];
    private HashSet<string> _chosen = [];
    private LauncherScope _scope = new([], [], true);
    private int _chip;
    private int _result;
    private int _windowStart;

    public SearchViewModel(
        IReadOnlyList<LauncherLibrary> libraries,
        KeyboardLayout layout,
        Action<KeyboardLayout> layoutChosen,
        Func<Game, Action<GameTileViewModel>, GameTileViewModel> createTile)
    {
        _libraries = libraries;
        _createTile = createTile;
        Keyboard = new OnScreenKeyboardViewModel(layout, Press, layoutChosen);
        BuildChips();
        Recompute(keepFocusOn: null);
        FocusZone(SearchZone.Keyboard);
    }

    /// <summary>What was typed, prefix included.</summary>
    [ObservableProperty]
    public partial string Text { get; private set; } = string.Empty;

    public ObservableCollection<SearchChipViewModel> Chips { get; } = [];

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

    /// <summary>All hits by relevance; the detail page turns through these.</summary>
    public IReadOnlyList<Game> Hits { get; private set; } = [];

    /// <summary>The hits currently in view.</summary>
    public ObservableCollection<GameTileViewModel> Results { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMore))]
    public partial string? MoreText { get; private set; }

    public bool HasMore => MoreText is not null;

    public OnScreenKeyboardViewModel Keyboard { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResultsFocused))]
    public partial SearchZone Zone { get; private set; }

    public bool IsResultsFocused => Zone == SearchZone.Results;

    /// <summary>The hit with the controller focus; null while the focus is elsewhere.</summary>
    [ObservableProperty]
    public partial GameTileViewModel? FocusedResult { get; private set; }

    /// <summary>
    /// D-pad, A on chips and keys, and the buttons that stand for keyboard keys: X deletes, Y types a
    /// space, LT starts a filter, ≡ goes to the results, R3 opens the layout menu.
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
            case UiAction.Search:
                Erase();
                break;
            case UiAction.ViewOptions:
                Type(" ");
                break;
            case UiAction.PreviousPage:
                StartFilter();
                break;
            case UiAction.Settings:
                Submit();
                break;
            case UiAction.KeyboardLayout:
                Keyboard.ToggleLayoutMenu();
                break;
            case UiAction.Accept when Zone == SearchZone.Chips:
                Toggle(Chips[_chip]);
                break;
            case UiAction.Accept when Zone == SearchZone.Keyboard:
                Keyboard.PressFocused();
                break;
            case UiAction.Up or UiAction.Down or UiAction.Left or UiAction.Right:
                Move(action);
                break;
        }
    }

    /// <summary>Text from the on-screen keyboard or a real one; control characters are dropped.</summary>
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

    /// <summary>"/ Filter": the input starts with a launcher prefix, as in the design.</summary>
    public void StartFilter()
    {
        if (!Text.StartsWith(SearchQuery.PrefixMarker))
        {
            SetText(SearchQuery.PrefixMarker + Text);
        }
    }

    /// <summary>"Suchen": on to the hits, if there are any.</summary>
    public void Submit()
    {
        if (Hits.Count > 0)
        {
            FocusZone(SearchZone.Results);
        }
    }

    /// <summary>The library was rescanned: same input, fresh hits, the focus stays on its game where it can.</summary>
    public void Update(IReadOnlyList<LauncherLibrary> libraries)
    {
        _libraries = libraries;
        var focusedKey = FocusedResult?.Game.Key;
        var zone = Zone;
        BuildChips();
        Recompute(focusedKey);
        FocusZone(zone == SearchZone.Results && Hits.Count == 0 ? SearchZone.Keyboard : zone);
    }

    /// <summary>Back from the detail page: the focus goes to the hit shown last.</summary>
    public void FocusHit(string key)
    {
        var index = Hits.Select(g => g.Key).ToList().IndexOf(key);
        if (index < 0)
        {
            return;
        }

        _result = index;
        _windowStart = Math.Clamp(index - (VisibleResults - 1), 0, index);
        ShowWindow();
        FocusZone(SearchZone.Results);
    }

    /// <summary>The mouse wheel over the hits, or a click on "+N weitere".</summary>
    public void Scroll(int delta)
    {
        var start = Math.Clamp(_windowStart + delta, 0, Math.Max(0, Hits.Count - VisibleResults));
        if (start == _windowStart)
        {
            return;
        }

        _windowStart = start;
        _result = Math.Clamp(_result, start, start + VisibleResults - 1);
        ShowWindow();
        if (Zone == SearchZone.Results)
        {
            FocusZone(SearchZone.Results);
        }
    }

    [RelayCommand]
    private void ShowMore() => Scroll(VisibleResults);

    private void Press(KeyViewModel key)
    {
        if (key.Character is { } character)
        {
            Type(character.ToString());
            return;
        }

        switch (key.Special)
        {
            case SpecialKey.Layout:
                Keyboard.ToggleLayoutMenu();
                break;
            case SpecialKey.Slash:
                StartFilter();
                break;
            case SpecialKey.Space:
                Type(" ");
                break;
            case SpecialKey.Erase:
                Erase();
                break;
            case SpecialKey.Submit:
                Submit();
                break;
        }
    }

    private void SetText(string text)
    {
        Text = text;
        Recompute(keepFocusOn: null);
        if (Zone == SearchZone.Results && Hits.Count == 0)
        {
            FocusZone(SearchZone.Keyboard);
        }
        else if (Zone == SearchZone.Results)
        {
            FocusZone(SearchZone.Results);
        }
    }

    private void Toggle(SearchChipViewModel chip)
    {
        var all = _launchers.Select(l => l.Id).ToList();
        IReadOnlyList<string>? next = chip.Launcher is { } launcher ? _scope.Toggle(launcher.Id, _launchers) : all;
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

        var focused = _chip;
        Chips.Clear();
        Chips.Add(new SearchChipViewModel(null, Toggle));
        foreach (var launcher in _launchers)
        {
            Chips.Add(new SearchChipViewModel(launcher, Toggle));
        }

        _chip = Math.Min(focused, Chips.Count - 1);
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
        Hits = GameSearch.Find(games, query.Term, Formatting.Culture);
        var launchersHit = Hits.Select(g => g.LauncherId).Distinct().Count();
        ResultsTitle = query.Term.Length == 0 ? "Tippe einen Suchbegriff ein"
            : Hits.Count == 0 ? "Keine Treffer"
            : $"{Hits.Count} Treffer in {launchersHit} {(launchersHit == 1 ? "Launcher" : "Launchern")}";

        var index = keepFocusOn is null ? -1 : Hits.Select(g => g.Key).ToList().IndexOf(keepFocusOn);
        _result = index >= 0 ? index : Math.Clamp(keepFocusOn is null ? 0 : _result, 0, Math.Max(0, Hits.Count - 1));
        _windowStart = index >= 0 ? Math.Clamp(_windowStart, Math.Max(0, index - (VisibleResults - 1)), index) : 0;
        if (keepFocusOn is null)
        {
            _windowStart = 0;
        }

        ShowWindow();
    }

    private void ShowWindow()
    {
        FocusedResult = null;
        Results.Clear();
        foreach (var game in Hits.Skip(_windowStart).Take(VisibleResults))
        {
            Results.Add(_createTile(game, FocusByPointer));
        }

        var more = Hits.Count - _windowStart - Results.Count;
        MoreText = more > 0 ? $"+{more} weitere" : null;
    }

    /// <summary>A click on a hit focuses it.</summary>
    private void FocusByPointer(GameTileViewModel tile)
    {
        var index = Results.IndexOf(tile);
        if (index >= 0)
        {
            _result = _windowStart + index;
            FocusZone(SearchZone.Results);
        }
    }

    private void Move(UiAction direction)
    {
        switch (Zone, direction)
        {
            case (SearchZone.Keyboard, _):
                if (!Keyboard.Move(direction))
                {
                    FocusZone(Hits.Count > 0 ? SearchZone.Results : SearchZone.Chips);
                }

                break;
            case (SearchZone.Results, UiAction.Left):
                MoveResult(-1);
                break;
            case (SearchZone.Results, UiAction.Right):
                MoveResult(1);
                break;
            case (SearchZone.Results, UiAction.Up):
                FocusZone(SearchZone.Chips);
                break;
            case (SearchZone.Results, UiAction.Down):
                FocusZone(SearchZone.Keyboard);
                break;
            case (SearchZone.Chips, UiAction.Left):
                _chip = Math.Max(0, _chip - 1);
                FocusZone(SearchZone.Chips);
                break;
            case (SearchZone.Chips, UiAction.Right):
                _chip = Math.Min(Chips.Count - 1, _chip + 1);
                FocusZone(SearchZone.Chips);
                break;
            case (SearchZone.Chips, UiAction.Down):
                FocusZone(Hits.Count > 0 ? SearchZone.Results : SearchZone.Keyboard);
                break;
        }
    }

    /// <summary>Left/right through the hits; the row scrolls on at its edges and stops at the ends.</summary>
    private void MoveResult(int delta)
    {
        var next = Math.Clamp(_result + delta, 0, Hits.Count - 1);
        if (next == _result)
        {
            return;
        }

        _result = next;
        if (next < _windowStart || next >= _windowStart + VisibleResults)
        {
            _windowStart = Math.Clamp(_windowStart + delta, 0, Math.Max(0, Hits.Count - VisibleResults));
            ShowWindow();
        }

        FocusZone(SearchZone.Results);
    }

    private void FocusZone(SearchZone zone)
    {
        Zone = zone;
        for (var i = 0; i < Chips.Count; i++)
        {
            Chips[i].IsFocused = zone == SearchZone.Chips && i == _chip;
        }

        if (FocusedResult is { } previous)
        {
            previous.IsFocused = false;
        }

        FocusedResult = zone == SearchZone.Results && _result - _windowStart is var slot && slot >= 0 && slot < Results.Count ? Results[slot] : null;
        if (FocusedResult is { } focused)
        {
            focused.IsFocused = true;
        }

        if (zone == SearchZone.Keyboard)
        {
            Keyboard.Enter();
        }
        else
        {
            Keyboard.Leave();
        }
    }
}
