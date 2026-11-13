using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.Core.Artwork;
using Arcadia.Core.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>Where the focus is in the hits of an entry: the grid, the field "Eigener Suchbegriff" or "Zurücksetzen".</summary>
public enum WaitingHitsFocus
{
    Grid,
    Term,
    Reset,
}

/// <summary>The settings' one on-screen keyboard, for the field "Eigener Suchbegriff".</summary>
public sealed record WaitingKeyboard(Func<IKeyboardTarget, OnScreenKeyboardViewModel> Take, Action<IKeyboardTarget> GiveBack);

/// <summary>
/// The hits of the entry shown on "Warten auf deine Auswahl", right of the games (#506): what is sought, the field
/// "Eigener Suchbegriff" with "Zurücksetzen", and a grid of hits. The same for both kinds of entries; what is sought, where
/// the hits come from and their images is the kind's own (<see cref="ArtworkHitsViewModel"/> for SteamGridDB,
/// <see cref="MetadataHitsViewModel"/> for IGDB, #494). In the grid ◀ ▶ ▲ ▼ go round in rows and columns, the field's row
/// counting as each column's top; A acts on what is focused.
/// </summary>
/// <remarks>UI thread only. Every request runs off it and is cancelled when another entry is shown or the list closes.</remarks>
public abstract partial class WaitingHitsViewModel : ViewModelBase, IDisposable
{
    /// <summary>Tiles a row: covers (2 : 3) and backgrounds (wide) fill the same width.</summary>
    public const int CoverColumns = 5;

    public const int HeroColumns = 3;

    /// <summary>How long the focus rests on an entry before anything is asked or loaded for it.</summary>
    internal static readonly TimeSpan RestDelay = TimeSpan.FromMilliseconds(150);

    private readonly WaitingKeyboard _keyboard;
    private readonly Action<WaitingHitViewModel> _take;
    private readonly Action<WaitingHitViewModel> _mark;
    private CancellationTokenSource _work = new();
    private readonly List<Task> _tileLoads = [];
    private int _focus;
    private int _column;

    /// <param name="take">A or a double click chose a hit: the list takes it.</param>
    /// <param name="mark">A click marked a hit: the list brings the focus into the hits, onto it (#539).</param>
    protected WaitingHitsViewModel(string title, string originalTerm, WaitingKeyboard keyboard, Action<WaitingHitViewModel> take, Action<WaitingHitViewModel> mark)
    {
        Title = title;
        _keyboard = keyboard;
        _take = take;
        _mark = mark;
        Hits.CollectionChanged += (_, _) => MarkedHit = Hits.Count == 0 ? null : Hits[Math.Min(_focus, Hits.Count - 1)];
        OriginalTerm = originalTerm;
        Term = originalTerm;
    }

    public void Dispose()
    {
        IsDisposed = true;
        _work.Cancel();
        CloseEditor();
    }

    public string Title { get; }

    public ObservableCollection<WaitingHitViewModel> Hits { get; } = [];

    /// <summary>"Gesucht: …": what the choice gives the game.</summary>
    public abstract string SoughtText { get; }

    /// <summary>How many hits there are and what A on one does.</summary>
    public abstract string Intro { get; }

    /// <summary>Below the grid: what Y does to the entry.</summary>
    public abstract string Note { get; }

    /// <summary>
    /// The intro's count says what the source found; with the grid empty and a reason shown (no key, no connection, no
    /// hits) the reason says it, and a count of 0 would claim an answer that may never have come.
    /// </summary>
    public bool HasIntro => Hits.Count > 0 || !HasMessage;

    /// <summary>The tiles are backgrounds, three a row; covers otherwise.</summary>
    public virtual bool IsHero => false;

    /// <summary>
    /// The hits are a list beside an info box with the marked hit's facts, not a grid of images (metadata, #539): games
    /// of the same name tell apart by their facts, not by their covers.
    /// </summary>
    public virtual bool ShowsFacts => false;

    /// <summary>The hit the focus is on, or would be on once in the hits; the info box shows its facts. Null without hits.</summary>
    [ObservableProperty]
    public partial WaitingHitViewModel? MarkedHit { get; private set; }

    /// <summary>What B does in the hits: back to the games list, or a step back within the choice.</summary>
    public virtual string BackHint => AppTexts.Back;

    /// <summary>What A on a hit does.</summary>
    public virtual string TakeHint => AppTexts.WaitingTakeHint;

    public int Columns => ShowsFacts ? 1 : IsHero ? HeroColumns : CoverColumns;

    /// <summary>The entry's search name; "Zurücksetzen" brings it back.</summary>
    public string OriginalTerm { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOwnTerm), nameof(CanReset), nameof(Intro))]
    public partial string Term { get; private set; }

    public bool IsOwnTerm => Term != OriginalTerm;

    public bool CanReset => IsOwnTerm;

    /// <summary>"Treffer werden geprüft …": hits are being checked or searched.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial bool IsChecking { get; protected set; }

    /// <summary>Why the grid is empty: no hits, the source not reachable or its access turned down; null while it isn't.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage), nameof(HasIntro))]
    public partial string? Message { get; protected set; }

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

    protected bool IsDisposed { get; private set; }

    /// <summary>Until the player moves, the focus stays on the first hit in the source's order, however the checks finish.</summary>
    protected bool PlayerMoved { get; set; }

    /// <summary>The tiles' images load as tiles come; set once the focus rests on the entry.</summary>
    protected bool TilesLoad { get; set; }

    /// <summary>The token of what runs for the hits shown now.</summary>
    protected CancellationToken WorkToken => _work.Token;

    /// <summary>The longest own term the source takes.</summary>
    protected abstract int MaxTermLength { get; }

    /// <summary>The focus rests on the entry: the hits are brought up to date and the tiles loaded.</summary>
    public void Start() => Run(c => ShowOriginalAsync(RestDelay, c));

    /// <summary>A in the games list: the focus comes to the first hit, or to the field while there is none.</summary>
    public void Enter()
    {
        IsActive = true;
        PlayerMoved = false;
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
        PlayerMoved |= action is UiAction.Left or UiAction.Right or UiAction.Up or UiAction.Down;
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

    /// <summary>The entry's own hits, once <paramref name="rest"/> has passed: brought up to date, their tiles loaded.</summary>
    protected abstract Task ShowOriginalAsync(TimeSpan rest, CancellationToken cancel);

    /// <summary>An own term: the source's results replace the hits.</summary>
    protected abstract Task SearchAsync(string term, CancellationToken cancel);

    /// <summary>A hit's tile image off the UI thread; null keeps the placeholder.</summary>
    protected abstract Task<ArtworkFile?> TileImageAsync(WaitingHitViewModel tile, CancellationToken cancel);

    /// <summary>A new tile; its image loads when the tiles load.</summary>
    protected WaitingHitViewModel Track(WaitingHitViewModel tile)
    {
        if (TilesLoad)
        {
            LoadTileAsync(tile, _work.Token).Forget(AppTexts.ErrorContextWaitingList);
        }

        return tile;
    }

    /// <summary>The images of every tile shown, once they may load.</summary>
    protected void LoadShownTiles(CancellationToken cancel)
    {
        TilesLoad = true;
        foreach (var tile in Hits)
        {
            LoadTileAsync(tile, cancel).Forget(AppTexts.ErrorContextWaitingList);
        }
    }

    /// <summary>
    /// B in the hits: true when the choice went a step back within the hits (to the cover after the background, #539);
    /// false sends the focus back to the games list.
    /// </summary>
    public virtual bool StepBack() => false;

    /// <summary>A click on a hit (#539): marks it, the focus comes onto it; nothing is chosen.</summary>
    public void Mark(WaitingHitViewModel hit)
    {
        if (Hits.IndexOf(hit) is var index and >= 0)
        {
            IsActive = true;
            PlayerMoved = true;
            Zone = WaitingHitsFocus.Grid;
            FocusHit(index);
        }
    }

    /// <summary>A or a double click chose the hit; by default the list takes it.</summary>
    protected virtual void Choose(WaitingHitViewModel hit) => _take(hit);

    /// <summary>A new tile's double click: chosen as with A.</summary>
    protected void Take(WaitingHitViewModel hit)
    {
        if (!IsDisposed && Hits.Contains(hit))
        {
            Choose(hit);
        }
    }

    /// <summary>A new tile's click: the list marks it, bringing the focus into the hits first.</summary>
    protected void MarkFromClick(WaitingHitViewModel hit) => _mark(hit);

    /// <summary>The hits again from the start, with the term in the field: e.g. another step of the choice (#539).</summary>
    protected void Reload()
    {
        Hits.Clear();
        IntroChanged();
        PlayerMoved = false;
        Zone = WaitingHitsFocus.Grid;
        var term = Term;
        Run(c => IsOwnTerm ? SearchAsync(term, c) : ShowOriginalAsync(TimeSpan.Zero, c));
        FocusHit(0);
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
            text => text.Trim().Length > MaxTermLength ? AppTexts.WaitingTermTooLong(MaxTermLength) : null,
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
        PlayerMoved = false;
        Zone = WaitingHitsFocus.Grid;
        Run(c => ShowOriginalAsync(TimeSpan.Zero, c));
        FocusHit(0);
    }

    private void Accept()
    {
        switch (Zone)
        {
            case WaitingHitsFocus.Grid when _focus < Hits.Count:
                Choose(Hits[_focus]);
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

    /// <summary>↵ on the keyboard: the term's results replace the hits; the entry's own search name brings back its hits.</summary>
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
        PlayerMoved = false;

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

    protected void FocusHit(int index)
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

        MarkedHit = _focus < Hits.Count ? Hits[_focus] : null;

        OnPropertyChanged(nameof(IsTermFocused));
        OnPropertyChanged(nameof(IsResetFocused));
        FocusMoved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A hit joins the grid at <paramref name="at"/>; the focus stays on the hit it is on once the player moved it, before that on the first.</summary>
    protected void InsertHit(int at, WaitingHitViewModel tile)
    {
        var wasEmpty = Hits.Count == 0;
        Hits.Insert(at, tile);
        IntroChanged();
        if (!PlayerMoved || wasEmpty)
        {
            _focus = 0;
        }
        else if (at <= _focus)
        {
            _focus++;
        }

        FocusHit(_focus);
    }

    protected void IntroChanged()
    {
        OnPropertyChanged(nameof(Intro));
        OnPropertyChanged(nameof(HasIntro));
    }

    /// <summary>Cancels what runs for the hits shown and starts <paramref name="work"/>.</summary>
    protected void Run(Func<CancellationToken, Task> work)
    {
        _work.Cancel();
        _work = new CancellationTokenSource();
        var cancel = _work.Token;
        var running = RunGuardedAsync(work, cancel);
        Ready = running;
        running.Forget(AppTexts.ErrorContextWaitingList);
    }

    private static async Task RunGuardedAsync(Func<CancellationToken, Task> work, CancellationToken cancel)
    {
        try
        {
            await work(cancel);
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // Another entry, another term, or the list closed.
        }
    }

    /// <summary>Checking or searching ended: the grid's message, or null while it has hits.</summary>
    protected void Done(string? message)
    {
        IsChecking = false;
        Message = message;
        IntroChanged();
        if (Hits.Count == 0 && IsActive && Zone == WaitingHitsFocus.Grid)
        {
            Zone = WaitingHitsFocus.Term;
        }
    }

    /// <summary>A tile's image off the UI thread; an entry shown meanwhile cancels it.</summary>
    private Task LoadTileAsync(WaitingHitViewModel tile, CancellationToken cancel)
    {
        var loading = LoadTileImageAsync(tile, cancel);
        _tileLoads.Add(loading);
        return loading;
    }

    private async Task LoadTileImageAsync(WaitingHitViewModel tile, CancellationToken cancel)
    {
        try
        {
            var image = await TileImageAsync(tile, cancel);
            if (!cancel.IsCancellationRequested)
            {
                tile.Image = image;
            }
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // Another entry is shown, or the list closed.
        }
    }
}
