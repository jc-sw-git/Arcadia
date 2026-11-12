using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Navigation;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>Where the focus is in the hits of a game: the grid, the field "Eigener Suchbegriff" or "Zurücksetzen".</summary>
public enum WaitingHitsFocus
{
    Grid,
    Term,
    Reset,
}

/// <summary>The settings' one on-screen keyboard, for the field "Eigener Suchbegriff".</summary>
public sealed record WaitingKeyboard(Func<IKeyboardTarget, OnScreenKeyboardViewModel> Take, Action<IKeyboardTarget> GiveBack);

/// <summary>
/// The hits of the game shown on "Warten auf deine Auswahl" (#506), right of the games: what is sought, the field
/// "Eigener Suchbegriff" with "Zurücksetzen", and a grid of the hits that have an image of the sought kind. Hits known
/// to have one show at once; once the focus rests on the game, the others are checked (a few at a time, filling the
/// grid in SteamGridDB's order) and the tiles' images load. An own term searches SteamGridDB and shows the first
/// <see cref="SteamGridDbClient.MaxOwnTermHits"/> results with images; nothing of it is kept.
/// </summary>
/// <remarks>UI thread only. Every request runs off it and is cancelled when another game is shown or the list closes.</remarks>
public sealed partial class WaitingHitsViewModel : ViewModelBase, IDisposable
{
    /// <summary>Tiles a row: covers (2 : 3) and backgrounds (wide) fill the same width.</summary>
    public const int CoverColumns = 5;

    public const int HeroColumns = 3;

    /// <summary>How many hits are checked at once: enough to fill the grid quickly, few enough not to be slowed down.</summary>
    private const int ParallelChecks = 4;

    /// <summary>How long the focus rests on a game before anything is asked or loaded for it.</summary>
    internal static readonly TimeSpan RestDelay = TimeSpan.FromMilliseconds(150);

    private readonly ArtworkService _artwork;
    private readonly WaitingKeyboard _keyboard;
    private readonly Action<WaitingHitViewModel> _take;
    private CancellationTokenSource _work = new();
    private readonly List<Task> _tileLoads = [];
    private bool _tilesLoad;
    private int _focus;
    private int _column;

    // Until the player moves, the focus stays on the first hit in SteamGridDB's order, however the checks finish.
    private bool _playerMoved;
    private bool _disposed;

    /// <param name="take">A or a click on a hit: the list takes it.</param>
    /// <param name="title">The title the game shows, its own one included (#496); the search term starts from the launcher's.</param>
    public WaitingHitsViewModel(ArtworkService artwork, WaitingGame waiting, string title, WaitingKeyboard keyboard, Action<WaitingHitViewModel> take)
    {
        Title = title;
        _artwork = artwork;
        _keyboard = keyboard;
        _take = take;
        Waiting = waiting;
        Sought = waiting.Match.Sought;
        OriginalTerm = SteamGridDbNames.SearchTermOf(waiting.Game.Title);
        Term = OriginalTerm;

        // The tiles after the frame that shows the focus move: building them doesn't delay it (#506, scenario 20).
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () =>
            {
                if (!_disposed && !_tilesLoad && Hits.Count == 0)
                {
                    ShowKnownHits(waiting.Match);
                }
            },
            Avalonia.Threading.DispatcherPriority.Background);
    }

    public void Dispose()
    {
        _disposed = true;
        _work.Cancel();
        CloseEditor();
    }

    public WaitingGame Waiting { get; }

    public string Title { get; }

    public ObservableCollection<WaitingHitViewModel> Hits { get; } = [];

    /// <summary>The kinds SteamGridDB is to give the game after the choice; decided again once the focus rests on it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SoughtText), nameof(Intro), nameof(TileKind), nameof(IsHero), nameof(Columns))]
    public partial IReadOnlyList<ArtworkKind> Sought { get; private set; }

    public string SoughtText => Sought.Count > 1 ? AppTexts.WaitingSoughtBoth
        : Sought[0] == ArtworkKind.Cover ? AppTexts.WaitingSoughtCover
        : AppTexts.WaitingSoughtHero;

    public string Intro
    {
        get
        {
            var loads = Sought.Count > 1 ? AppTexts.WaitingLoadsBoth : Sought[0] == ArtworkKind.Cover ? AppTexts.WaitingLoadsCover : AppTexts.WaitingLoadsHero;
            return IsOwnTerm ? AppTexts.WaitingIntroOwnTerm(count: Hits.Count, loads: loads) : AppTexts.WaitingIntro(count: Hits.Count, loads: loads);
        }
    }

    /// <summary>
    /// The intro's count says what SteamGridDB found; with the grid empty and a reason shown (no key, no connection, no
    /// hits) the reason says it, and a count of 0 would claim an answer that may never have come.
    /// </summary>
    public bool HasIntro => Hits.Count > 0 || !HasMessage;

    /// <summary>What the tiles show and the hits are checked for: the cover when both are sought.</summary>
    public ArtworkKind TileKind => NameMatch.TileKindOf(Sought);

    public bool IsHero => TileKind == ArtworkKind.Hero;

    public int Columns => IsHero ? HeroColumns : CoverColumns;

    /// <summary>The game's search name; "Zurücksetzen" brings it back.</summary>
    public string OriginalTerm { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOwnTerm), nameof(CanReset), nameof(Intro))]
    public partial string Term { get; private set; }

    public bool IsOwnTerm => Term != OriginalTerm;

    public bool CanReset => IsOwnTerm;

    /// <summary>"Treffer werden geprüft …": hits are being checked or searched.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial bool IsChecking { get; private set; }

    /// <summary>Why the grid is empty: no hits, SteamGridDB not reachable or the key turned down; null while it isn't.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage), nameof(HasIntro))]
    public partial string? Message { get; private set; }

    public bool HasMessage => Message is not null && !IsChecking;

    /// <summary>The focus is in the hits (the second level); false while it is in the games list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTermFocused), nameof(IsResetFocused))]
    public partial bool IsActive { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTermFocused), nameof(IsResetFocused))]
    public partial WaitingHitsFocus Zone { get; private set; }

    public bool IsTermFocused => IsActive && Zone == WaitingHitsFocus.Term;

    public bool IsResetFocused => IsActive && Zone == WaitingHitsFocus.Reset;

    /// <summary>The field being written with the on-screen keyboard; null while it isn't.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    public partial TextEditorViewModel? Editor { get; private set; }

    public bool IsEditing => Editor is not null;

    /// <summary>The last checks or search started; the tests wait for it.</summary>
    public Task Ready { get; private set; } = Task.CompletedTask;

    /// <summary>The tile images being loaded; the tests wait for it.</summary>
    public Task Images => Task.WhenAll(_tileLoads);

    /// <summary>The focused hit changed: the view brings it into sight.</summary>
    public event EventHandler? FocusMoved;

    /// <summary>The focus rests on the game: what it seeks is decided again, its unchecked hits checked, the tiles loaded.</summary>
    public void Start() => Run(c => ShowOriginalAsync(RestDelay, c));

    /// <summary>A in the games list: the focus comes to the first hit, or to the field while there is none.</summary>
    public void Enter()
    {
        IsActive = true;
        _playerMoved = false;
        Zone = Hits.Count > 0 ? WaitingHitsFocus.Grid : WaitingHitsFocus.Term;
        _column = 0;
        FocusHit(0);
    }

    /// <summary>B in the hits, or a hit taken: the focus goes back to the games list.</summary>
    public void Leave()
    {
        IsActive = false;
        FocusHit(_focus);
    }

    /// <summary>A press in the hits: the D-pad moves through the grid and the field's row, A acts on what is focused.</summary>
    public void Handle(UiAction action)
    {
        var held = HeldPress.IsRepeat;
        _playerMoved |= action is UiAction.Left or UiAction.Right or UiAction.Up or UiAction.Down;
        switch (action)
        {
            case UiAction.Left:
                Sideways(-1, held);
                break;
            case UiAction.Right:
                Sideways(1, held);
                break;
            case UiAction.Up:
                Vertical(-1, held);
                break;
            case UiAction.Down:
                Vertical(1, held);
                break;
            case UiAction.Accept:
                Accept();
                break;
        }
    }

    [RelayCommand]
    private void EditTerm()
    {
        if (Editor is not null)
        {
            return;
        }

        IsActive = true;
        Zone = WaitingHitsFocus.Term;
        FocusHit(_focus);
        TextEditorViewModel? editor = null;
        editor = new TextEditorViewModel(
            Term,
            text => text.Trim().Length > SteamGridDbClient.MaxNameLength ? AppTexts.WaitingTermTooLong(SteamGridDbClient.MaxNameLength) : null,
            text =>
            {
                Search(text);
                return true;
            },
            () => CloseEditor(editor),
            _keyboard.Take,
            _keyboard.GiveBack);
        Editor = editor;
    }

    [RelayCommand]
    private void Reset()
    {
        if (!CanReset)
        {
            return;
        }

        Term = OriginalTerm;
        Hits.Clear();
        _playerMoved = false;
        Zone = WaitingHitsFocus.Grid;
        Run(c => ShowOriginalAsync(TimeSpan.Zero, c));
        FocusHit(0);
    }

    private void Accept()
    {
        switch (Zone)
        {
            case WaitingHitsFocus.Grid when _focus < Hits.Count:
                _take(Hits[_focus]);
                break;
            case WaitingHitsFocus.Term:
                EditTerm();
                break;
            case WaitingHitsFocus.Reset:
                Reset();
                break;
        }
    }

    private void CloseEditor(TextEditorViewModel? editor = null)
    {
        if (Editor is { } open && (editor is null || editor == open))
        {
            Editor = null;
            open.Dispose();
        }
    }

    /// <summary>↵ on the keyboard: the term's results replace the hits; the game's own search name brings back its hits.</summary>
    private void Search(string text)
    {
        var term = text.Trim();
        if (term == OriginalTerm)
        {
            Reset();
            return;
        }

        Term = term;
        Hits.Clear();
        _playerMoved = false;

        // The first result to arrive takes the focus, so A takes it; with none the focus goes back to the field.
        Zone = WaitingHitsFocus.Grid;
        Run(c => SearchAsync(term, c));
    }

    /// <summary>◀ ▶: through a row of the grid, round at its ends; in the field's row between field and "Zurücksetzen".</summary>
    private void Sideways(int step, bool held)
    {
        if (Zone != WaitingHitsFocus.Grid)
        {
            // Only two places, and "Zurücksetzen" only while an own term is in the field.
            Zone = Zone == WaitingHitsFocus.Term && CanReset ? WaitingHitsFocus.Reset : WaitingHitsFocus.Term;
            return;
        }

        if (Hits.Count == 0)
        {
            return;
        }

        var rowStart = _focus - (_focus % Columns);
        var rowLength = Math.Min(Columns, Hits.Count - rowStart);
        var column = (_focus - rowStart) + step;
        if (column < 0 || column >= rowLength)
        {
            if (held)
            {
                return;
            }

            column = column < 0 ? rowLength - 1 : 0;
        }

        _column = column;
        FocusHit(rowStart + column);
    }

    /// <summary>
    /// ▲ ▼: through a column, the field's row counting as its top; past the bottom round to the field, from the field
    /// round to the column's last hit. Held, it stops at the ends.
    /// </summary>
    private void Vertical(int step, bool held)
    {
        if (Zone != WaitingHitsFocus.Grid)
        {
            var rows = ColumnIndices(_column);
            if (rows.Count == 0 || (step < 0 && held))
            {
                return;
            }

            Zone = WaitingHitsFocus.Grid;
            FocusHit(step > 0 ? rows[0] : rows[^1]);
            return;
        }

        var next = _focus + (step * Columns);
        if (next >= 0 && next < Hits.Count)
        {
            FocusHit(next);
        }
        else if (step < 0 || !held)
        {
            Zone = WaitingHitsFocus.Term;
            FocusHit(_focus);
        }
    }

    private List<int> ColumnIndices(int column)
    {
        var indices = new List<int>();
        for (var i = Math.Min(column, Columns - 1); i < Hits.Count; i += Columns)
        {
            indices.Add(i);
        }

        return indices.Count > 0 || Hits.Count == 0 ? indices : [Hits.Count - 1];
    }

    private void FocusHit(int index)
    {
        _focus = Math.Clamp(index, 0, Math.Max(Hits.Count - 1, 0));
        if (Zone == WaitingHitsFocus.Grid && Hits.Count > 0)
        {
            _column = _focus % Columns;
        }

        var inGrid = IsActive && Zone == WaitingHitsFocus.Grid;
        for (var i = 0; i < Hits.Count; i++)
        {
            Hits[i].IsFocused = inGrid && i == _focus;
        }

        OnPropertyChanged(nameof(IsTermFocused));
        OnPropertyChanged(nameof(IsResetFocused));
        FocusMoved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The hits known to have an image of the tile kind, at once, without a request.</summary>
    private void ShowKnownHits(NameMatch match)
    {
        Hits.Clear();
        foreach (var hit in match.Hits.Where(h => match.Checked.Contains(h.Id)))
        {
            Hits.Add(NewHit(hit));
        }

        OnPropertyChanged(nameof(Intro));
        OnPropertyChanged(nameof(HasIntro));
    }

    private WaitingHitViewModel NewHit(SteamGridDbHit hit)
    {
        var tile = new WaitingHitViewModel(hit, h => _take(h));
        if (_tilesLoad)
        {
            LoadTileAsync(tile, _work.Token).Forget(AppTexts.ErrorContextWaitingList);
        }

        return tile;
    }

    /// <summary>Cancels what runs for the hits shown and starts <paramref name="work"/>.</summary>
    private void Run(Func<CancellationToken, Task> work)
    {
        _work.Cancel();
        _work = new CancellationTokenSource();
        var cancel = _work.Token;
        var running = RunGuardedAsync(work, cancel);
        Ready = running;
        running.Forget(AppTexts.ErrorContextWaitingList);
    }

    private async Task RunGuardedAsync(Func<CancellationToken, Task> work, CancellationToken cancel)
    {
        try
        {
            await work(cancel);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // Another game, another term, or the list closed.
        }
    }

    /// <summary>The game's own hits: what it seeks now, the known ones with their tiles, the unchecked ones checked.</summary>
    private async Task ShowOriginalAsync(TimeSpan rest, CancellationToken cancel)
    {
        await Task.Delay(rest, cancel);
        var game = Waiting.Game;
        if (await Task.Run(() => _artwork.WaitingMatchNow(game), cancel) is not { } match || _disposed)
        {
            return;
        }

        cancel.ThrowIfCancellationRequested();
        if (!match.Sought.SequenceEqual(Sought))
        {
            var kindBefore = TileKind;
            Sought = match.Sought;
            if (TileKind != kindBefore)
            {
                // Other tiles: the hits shown were checked for the other kind.
                ShowKnownHits(match);
                FocusHit(0);
            }
        }

        // Hits dropped meanwhile (no image, found on another screen) leave; the others stay where the focus is.
        var current = match.Hits.Select(h => h.Id).ToHashSet();
        for (var i = Hits.Count - 1; i >= 0; i--)
        {
            if (!current.Contains(Hits[i].Hit.Id))
            {
                Hits.RemoveAt(i);
            }
        }

        _tilesLoad = true;
        foreach (var tile in Hits)
        {
            LoadTileAsync(tile, cancel).Forget(AppTexts.ErrorContextWaitingList);
        }

        await CheckInOrderAsync(match.Hits, int.MaxValue, hit => _artwork.CheckWaitingHitAsync(game, hit, cancel), cancel);
    }

    /// <summary>An own term: SteamGridDB's results, the first with images up to <see cref="SteamGridDbClient.MaxOwnTermHits"/>.</summary>
    private async Task SearchAsync(string term, CancellationToken cancel)
    {
        IsChecking = true;
        Message = null;
        _tilesLoad = true;
        var search = await Task.Run(() => _artwork.SearchByTermAsync(term, cancel), cancel);
        cancel.ThrowIfCancellationRequested();
        switch (search.Outcome)
        {
            case SteamGridDbOutcome.KeyRejected:
                Done(AppTexts.DataSourcesKeyRejected);
                return;
            case SteamGridDbOutcome.NoConnection:
                Done(AppTexts.WaitingUnreachable);
                return;
        }

        var kind = TileKind;
        await CheckInOrderAsync(search.Hits, SteamGridDbClient.MaxOwnTermHits, hit => _artwork.CheckHitAsync(hit, kind, cancel), cancel);
    }

    /// <summary>
    /// Checks the hits not shown yet, <see cref="ParallelChecks"/> at a time, and shows each with an image at its place in
    /// <paramref name="hits"/>' order without moving the focus off the hit it is on. Stops at the first failure (a key
    /// turned down or SteamGridDB not reachable): one request, not one per hit; the rest is checked the next time.
    /// </summary>
    private async Task CheckInOrderAsync(IReadOnlyList<SteamGridDbHit> hits, int most, Func<SteamGridDbHit, Task<HitCheck>> check, CancellationToken cancel)
    {
        var order = hits.Select((hit, index) => (hit.Id, index)).ToDictionary(e => e.Id, e => e.index);
        var shown = Hits.Select(h => h.Hit.Id).ToHashSet();
        var running = new List<Task<(SteamGridDbHit Hit, HitCheck Check)>>();
        string? failure = null;
        IsChecking = hits.Any(h => !shown.Contains(h.Id));
        foreach (var hit in hits.Where(h => !shown.Contains(h.Id)))
        {
            while (running.Count > 0 && (running.Count >= ParallelChecks || Hits.Count + running.Count >= most))
            {
                var finished = Apply(await TakeFinishedAsync(running), order);
                failure ??= finished;
            }

            if (failure is not null || Hits.Count >= most)
            {
                break;
            }

            running.Add(CheckOffThreadAsync(hit, check, cancel));
        }

        while (running.Count > 0)
        {
            var finished = Apply(await TakeFinishedAsync(running), order);
            failure ??= finished;
        }

        cancel.ThrowIfCancellationRequested();
        while (Hits.Count > most)
        {
            Hits.RemoveAt(Hits.Count - 1);
        }

        Done(Hits.Count > 0 ? null : failure ?? AppTexts.WaitingNoHits);
    }

    private static async Task<(SteamGridDbHit Hit, HitCheck Check)> CheckOffThreadAsync(
        SteamGridDbHit hit, Func<SteamGridDbHit, Task<HitCheck>> check, CancellationToken cancel) =>
        (hit, await Task.Run(() => check(hit), cancel));

    private static async Task<(SteamGridDbHit Hit, HitCheck Check)> TakeFinishedAsync(List<Task<(SteamGridDbHit Hit, HitCheck Check)>> running)
    {
        var finished = await Task.WhenAny(running);
        running.Remove(finished);
        return await finished;
    }

    /// <summary>A hit's check shown: with an image it joins the grid; the failure's message, or null.</summary>
    private string? Apply((SteamGridDbHit Hit, HitCheck Check) result, Dictionary<int, int> order)
    {
        switch (result.Check)
        {
            case HitCheck.HasImage:
                Insert(result.Hit, order);
                return null;
            case HitCheck.KeyRejected:
                return AppTexts.DataSourcesKeyRejected;
            case HitCheck.NoConnection:
                return AppTexts.WaitingUnreachable;
            default:
                return null;
        }
    }

    private void Insert(SteamGridDbHit hit, Dictionary<int, int> order)
    {
        var at = Hits.Count(h => order.GetValueOrDefault(h.Hit.Id, int.MaxValue) < order[hit.Id]);
        var wasEmpty = Hits.Count == 0;
        Hits.Insert(at, NewHit(hit));
        OnPropertyChanged(nameof(Intro));
        OnPropertyChanged(nameof(HasIntro));

        // The focus stays on the hit it is on once the player moved it; before that on the first hit.
        if (!_playerMoved || wasEmpty)
        {
            _focus = 0;
        }
        else if (at <= _focus)
        {
            _focus++;
        }

        FocusHit(_focus);
    }

    private void Done(string? message)
    {
        IsChecking = false;
        Message = message;
        OnPropertyChanged(nameof(Intro));
        OnPropertyChanged(nameof(HasIntro));
        if (Hits.Count == 0 && IsActive && Zone == WaitingHitsFocus.Grid)
        {
            Zone = WaitingHitsFocus.Term;
        }
    }

    /// <summary>A tile's image off the UI thread; a game shown meanwhile cancels it.</summary>
    private Task LoadTileAsync(WaitingHitViewModel tile, CancellationToken cancel)
    {
        var loading = LoadTileImageAsync(tile, cancel);
        _tileLoads.Add(loading);
        return loading;
    }

    private async Task LoadTileImageAsync(WaitingHitViewModel tile, CancellationToken cancel)
    {
        var kind = TileKind;
        try
        {
            var image = await Task.Run(() => _artwork.HitImageAsync(Waiting.Game, tile.Hit, kind, cancel), cancel);
            if (!cancel.IsCancellationRequested)
            {
                tile.Image = image;
            }
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // Another game is shown, or the list closed.
        }
    }
}
