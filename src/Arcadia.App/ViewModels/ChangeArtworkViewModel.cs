using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.Core.Artwork;
using Arcadia.Core.Navigation;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>What "Artwork ändern" asks of the window: closing it, the way to the data sources, the file dialog, a warning.</summary>
public interface IChangeArtworkHost
{
    void Close(ChangeArtworkViewModel modal);

    /// <summary>"Zu den Datenquellen ›": the modal closes, the settings open where the key is entered.</summary>
    void OpenDataSources(ChangeArtworkViewModel modal);

    /// <summary>The system's file dialog for an image, mouse mode on meanwhile; null if cancelled.</summary>
    Task<string?> PickImageAsync(string title);

    void Warn(string message);
}

/// <summary>One line below the preview: "Quelle", "Datei", "Format", "Gesetzt".</summary>
public sealed record ArtworkFactViewModel(string Label, string Value);

/// <summary>
/// "Artwork ändern" (#490, prototype in #13): the game's cover and background, chosen by hand. LB/RB switch between
/// "Cover" and "Hintergrund", the D-pad moves over the tiles ("Eigene Datei", the launcher's image, the suggestions) and
/// the notice's button, A uses the focused tile at once, X picks another own file, Y restores Arcadia's choice, B closes.
/// Every choice is applied at once; there is nothing to cancel.
/// </summary>
/// <remarks>UI thread only. The cache's work runs off it (<see cref="ArtworkService"/>); one choice at a time.</remarks>
public sealed partial class ChangeArtworkViewModel : ViewModelBase, IDisposable
{
    /// <summary>How long one loading text stays before another one comes.</summary>
    public static readonly TimeSpan LoadingTextTime = TimeSpan.FromSeconds(2.5);

    private const int CoverColumns = 5;
    private const int HeroColumns = 2;

    private readonly ArtworkService _artwork;
    private readonly IChangeArtworkHost _host;
    private readonly TimeProvider _time;
    private ITimer? _loadingTexts;
    private readonly Dictionary<ArtworkKind, ArtworkChoices> _choices = [];
    private int _focus;
    private bool _busy;
    private CachedImage? _current;

    public ChangeArtworkViewModel(GameTileViewModel tile, ArtworkService artwork, IChangeArtworkHost host, TimeProvider? time = null)
    {
        Tile = tile;
        _artwork = artwork;
        _host = host;
        _time = time ?? TimeProvider.System;
        GameLine = AppTexts.ArtworkGameLine(game: tile.Title, launcher: tile.LauncherName);
        Notice = new ArtworkNoticeViewModel(() => _host.OpenDataSources(this));
        (HeroBackdrop, _) = Formatting.HeroBrushes(tile.Title);
        Tile.PropertyChanged += OnTileChanged;
        ShowTab(ArtworkKind.Cover);
    }

    /// <summary>The modal closed: it no longer follows the game's images, and the loading texts stop.</summary>
    public void Dispose()
    {
        Tile.PropertyChanged -= OnTileChanged;
        StopLoadingTexts();
    }

    /// <summary>The cache's new image reaches the tile a moment after the choice: "Format" follows it.</summary>
    private void OnTileChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameTileViewModel.Artwork))
        {
            Facts = FactsOf(_current);
        }
    }

    public GameTileViewModel Tile { get; }

    /// <summary>The preview's background behind the hero, as the detail page draws it while the hero is missing.</summary>
    public Avalonia.Media.IBrush HeroBackdrop { get; }

    public static string Title => AppTexts.ActionChangeArtwork;

    public string GameLine { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCoverTab), nameof(IsHeroTab), nameof(Columns), nameof(GridHeading), nameof(IsCurrentCoverMarked), nameof(IsCurrentHeroMarked))]
    public partial ArtworkKind Kind { get; private set; }

    public bool IsCoverTab => Kind == ArtworkKind.Cover;

    public bool IsHeroTab => Kind == ArtworkKind.Hero;

    /// <summary>The preview marks the part being changed.</summary>
    public bool IsCurrentCoverMarked => IsCoverTab;

    public bool IsCurrentHeroMarked => IsHeroTab;

    public int Columns => IsCoverTab ? CoverColumns : HeroColumns;

    public ObservableCollection<ArtworkTileViewModel> Tiles { get; } = [];

    /// <summary>What the grid shows: the tiles, then the notice where it is shown, which flows on after them.</summary>
    public ObservableCollection<object> GridItems { get; } = [];

    /// <summary>The notice without a working key, with "Zu den Datenquellen ›".</summary>
    public ArtworkNoticeViewModel Notice { get; }

    /// <summary>The focus moved: the view brings the focused tile into sight.</summary>
    public event EventHandler<int>? FocusMoved;

    [ObservableProperty]
    public partial IReadOnlyList<ArtworkFactViewModel> Facts { get; private set; } = [];

    /// <summary>No key, or one that doesn't work: the notice with "Zu den Datenquellen ›" after the tiles.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridHeading))]
    public partial SuggestionKeyState KeyState { get; private set; } = SuggestionKeyState.Missing;

    public bool ShowsNotice => Notice.IsShown;

    public bool IsNoticeFocused => Notice.IsFocused;

    public string GridHeading => (IsCoverTab ? AppTexts.ArtworkCoverHeading : AppTexts.ArtworkHeroHeading)
        + (KeyState == SuggestionKeyState.Valid ? AppTexts.ArtworkSuggestionCount(Tiles.Count - 1) : string.Empty);

    /// <summary>Y can restore: the image in use was set by hand. Greyed out otherwise.</summary>
    [ObservableProperty]
    public partial bool CanRestore { get; private set; }

    /// <summary>X "Andere Datei": only while a filled "Eigene Datei" is focused.</summary>
    [ObservableProperty]
    public partial bool CanPickOtherFile { get; private set; }

    /// <summary>
    /// The tab's images are still being looked for (SteamGridDB's downloads can take a while): the grid shows the loading
    /// state with a text that changes at random every <see cref="LoadingTextTime"/>.
    /// </summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string LoadingText { get; private set; } = string.Empty;

    /// <summary>What the loading state says, one at a time.</summary>
    public static IReadOnlyList<string> LoadingTexts =>
    [
        AppTexts.ArtworkLoading1, AppTexts.ArtworkLoading2, AppTexts.ArtworkLoading3,
        AppTexts.ArtworkLoading4, AppTexts.ArtworkLoading5, AppTexts.ArtworkLoading6,
    ];

    /// <summary>The choices of a tab are being read; the tests wait for it.</summary>
    public Task Loading { get; private set; } = Task.CompletedTask;

    /// <summary>A press while the modal is open; it takes every one.</summary>
    public void Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.PreviousTab or UiAction.NextTab:
                // Two tabs: either button goes to the other, round at both ends.
                OtherTab();
                break;
            case UiAction.Left:
                Step(-1, 0);
                break;
            case UiAction.Right:
                Step(1, 0);
                break;
            case UiAction.Up:
                Step(0, -1);
                break;
            case UiAction.Down:
                Step(0, 1);
                break;
            case UiAction.Accept:
                AcceptFocused();
                break;
            case UiAction.Search when CanPickOtherFile:
                Run(PickFileAsync);
                break;
            case UiAction.Status when CanRestore:
                Run(RestoreAsync);
                break;
            case UiAction.Back:
                _host.Close(this);
                break;
        }
    }

    [RelayCommand]
    private void Close() => _host.Close(this);

    /// <summary>A click on LB or RB beside the tabs: the other tab, as the buttons do.</summary>
    [RelayCommand]
    private void OtherTab() => ShowTab(IsCoverTab ? ArtworkKind.Hero : ArtworkKind.Cover);

    [RelayCommand]
    private void ShowCover() => ShowTab(ArtworkKind.Cover);

    [RelayCommand]
    private void ShowHero() => ShowTab(ArtworkKind.Hero);

    [RelayCommand]
    private void PickOtherFile()
    {
        if (CanPickOtherFile)
        {
            Run(PickFileAsync);
        }
    }

    [RelayCommand]
    private void Restore()
    {
        if (CanRestore)
        {
            Run(RestoreAsync);
        }
    }

    private void ShowTab(ArtworkKind kind)
    {
        Kind = kind;
        if (_choices.TryGetValue(kind, out var known))
        {
            StopLoadingTexts();
            Show(known, focusInUse: true);
            return;
        }

        Tiles.Clear();
        GridItems.Clear();
        Facts = [];
        CanRestore = false;
        CanPickOtherFile = false;
        StartLoadingTexts();
        Loading = LoadAsync(kind);
        Loading.Forget(AppTexts.ErrorContextChangeArtwork);
    }

    private async Task LoadAsync(ArtworkKind kind)
    {
        try
        {
            var choices = await ChoicesOffUiThreadAsync(kind);
            _choices[kind] = choices;
            if (Kind == kind)
            {
                Show(choices, focusInUse: true);
            }
        }
        finally
        {
            // A failed load ends the loading state too: its error is a toast, and LB/RB read the tab again.
            if (Kind == kind)
            {
                StopLoadingTexts();
            }
        }
    }

    // The launcher's image is picked and copied synchronously inside the service: off the UI thread, the focus stays smooth.
    private Task<ArtworkChoices> ChoicesOffUiThreadAsync(ArtworkKind kind) => Task.Run(() => _artwork.ChoicesAsync(Tile.Game, kind));

    private void StartLoadingTexts()
    {
        NextLoadingText();
        IsLoading = true;
        _loadingTexts ??= _time.CreateTimer(_ => Avalonia.Threading.Dispatcher.UIThread.Post(NextLoadingText), null, LoadingTextTime, LoadingTextTime);
    }

    private void StopLoadingTexts()
    {
        IsLoading = false;
        _loadingTexts?.Dispose();
        _loadingTexts = null;
    }

    /// <summary>Another text than the one shown, at random.</summary>
    private void NextLoadingText()
    {
        var texts = LoadingTexts;
        var others = texts.Where(text => text != LoadingText).ToList();
        LoadingText = others[Random.Shared.Next(others.Count)];
    }

    /// <summary>Builds the tab's tiles and facts; the focus goes to the tile in use, or stays where it was.</summary>
    private void Show(ArtworkChoices choices, bool focusInUse)
    {
        KeyState = choices.KeyState;
        var keep = _focus;
        Tiles.Clear();
        Tiles.Add(ArtworkTileViewModel.OwnFile(choices.Own, choices.OwnName, OnPicked));
        foreach (var offer in choices.Offers)
        {
            Tiles.Add(ArtworkTileViewModel.Offered(offer, Tile.LauncherName, OnPicked));
        }

        Notice.Show(KeyState, choices.Off ?? []);
        GridItems.Clear();
        foreach (var tile in Tiles)
        {
            GridItems.Add(tile);
        }

        if (ShowsNotice)
        {
            GridItems.Add(Notice);
        }

        OnPropertyChanged(nameof(GridHeading));
        OnPropertyChanged(nameof(ShowsNotice));
        MarkInUse(choices.CurrentImage);
        var inUse = Tiles.ToList().FindIndex(t => t.IsInUse);
        SetFocus(focusInUse ? Math.Max(inUse, 0) : Math.Min(keep, FocusCount - 1));
    }

    private void MarkInUse(CachedImage? current)
    {
        foreach (var tile in Tiles)
        {
            tile.IsInUse = tile.Offer is { } offer ? offer.Is(current) : current?.Source == ArtworkSource.File && tile.File is not null;
        }

        _current = current;
        CanRestore = current is { SetByHand: true };
        Facts = FactsOf(current);
    }

    private List<ArtworkFactViewModel> FactsOf(CachedImage? current)
    {
        var facts = new List<ArtworkFactViewModel>
        {
            new(AppTexts.ArtworkSourceRow, current?.Source switch
            {
                null => AppTexts.ArtworkSourcePlaceholder,
                ArtworkSource.Launcher => AppTexts.ArtworkSourceLauncher(Tile.LauncherName),
                ArtworkSource.File => AppTexts.ArtworkSourceOwnFile,
                _ => ArtworkTileViewModel.SteamGridDbName,
            }),
        };

        if (current?.Source == ArtworkSource.File)
        {
            facts.Add(new(AppTexts.ArtworkFileRow, current.Stamp));
        }

        if ((IsCoverTab ? Tile.Cover : Tile.Hero) is { } shown && current is not null)
        {
            facts.Add(new(AppTexts.ArtworkFormatRow, AppTexts.ArtworkImageSize(width: shown.Size.Width, height: shown.Size.Height)));
        }

        if (current is not null)
        {
            facts.Add(new(AppTexts.ArtworkSetRow, current.SetByHand ? AppTexts.ArtworkSetByHand : AppTexts.ArtworkSetAutomatically));
        }

        return facts;
    }

    /// <summary>The tiles and, while it is shown, the notice's button after them.</summary>
    private int FocusCount => Tiles.Count + (ShowsNotice ? 1 : 0);

    private void Step(int columnStep, int rowStep)
    {
        var next = GridFocus.Step(_focus, FocusCount, FocusCount, Columns, columnStep, rowStep, HeldPress.IsRepeat);
        SetFocus(next);
    }

    private void SetFocus(int index)
    {
        _focus = Math.Clamp(index, 0, Math.Max(FocusCount - 1, 0));
        for (var i = 0; i < Tiles.Count; i++)
        {
            Tiles[i].IsFocused = i == _focus;
        }

        Notice.IsFocused = ShowsNotice && _focus == Tiles.Count;
        CanPickOtherFile = _focus < Tiles.Count && Tiles[_focus] is { IsOwnFile: true, IsEmptyOwnFile: false };
        FocusMoved?.Invoke(this, _focus);
    }

    private void AcceptFocused()
    {
        if (IsNoticeFocused)
        {
            _host.OpenDataSources(this);
        }
        else if (_focus < Tiles.Count)
        {
            OnPicked(Tiles[_focus]);
        }
    }

    /// <summary>A on a tile, or a click on it: it takes the focus and is used.</summary>
    private void OnPicked(ArtworkTileViewModel tile)
    {
        SetFocus(Tiles.IndexOf(tile));
        if (tile.IsEmptyOwnFile)
        {
            Run(PickFileAsync);
        }
        else if (tile.Offer is { } offer)
        {
            var kind = Kind;
            Run(() => UseOfferAsync(kind, offer));
        }
        else
        {
            var kind = Kind;
            Run(() => UseOwnFileAsync(kind));
        }
    }

    // The tab is taken when the choice is made: LB/RB while it waits on the artwork run must not move it to the other tab.
    private async Task UseOfferAsync(ArtworkKind kind, ArtworkOffer offer)
    {
        if (!await _artwork.UseAsync(Tile.Game, kind, offer))
        {
            _host.Warn(AppTexts.ArtworkOfferFailed);
        }

        await RefreshInUseAsync(kind);
    }

    private async Task UseOwnFileAsync(ArtworkKind kind)
    {
        await _artwork.UseOwnFileAsync(Tile.Game, kind);
        await RefreshInUseAsync(kind);
    }

    private async Task PickFileAsync()
    {
        var kind = Kind;
        var path = await _host.PickImageAsync(kind == ArtworkKind.Cover ? AppTexts.ArtworkPickCoverTitle : AppTexts.ArtworkPickHeroTitle);
        if (path is null)
        {
            return;
        }

        var result = await _artwork.UseFileAsync(Tile.Game, kind, path);
        if (result != OwnFileResult.Taken)
        {
            _host.Warn(result switch
            {
                OwnFileResult.TooLarge => AppTexts.ArtworkFileTooLarge,
                OwnFileResult.NotAnImage => AppTexts.ArtworkFileNoImage,
                _ => AppTexts.ArtworkFileUnreadable,
            });
            return;
        }

        // The own tile shows the new file: the tab is read again, its offers stay as they were.
        await ReloadAsync(kind);
    }

    private async Task RestoreAsync()
    {
        var kind = Kind;
        await _artwork.RestoreAsync(Tile.Game, kind);
        await ReloadAsync(kind);
    }

    /// <summary>The tab's choices again, after the own file or the launcher's image changed; the focus stays.</summary>
    private async Task ReloadAsync(ArtworkKind kind)
    {
        var choices = await ChoicesOffUiThreadAsync(kind);
        _choices[kind] = choices;
        if (Kind == kind)
        {
            Show(choices, focusInUse: false);
        }
    }

    /// <summary>After a choice: the tick, the facts and Y follow the image now in use.</summary>
    private async Task RefreshInUseAsync(ArtworkKind kind)
    {
        var current = await _artwork.InUseAsync(Tile.Game, kind);
        if (_choices.TryGetValue(kind, out var known))
        {
            _choices[kind] = known with { CurrentImage = current };
        }

        if (Kind == kind)
        {
            MarkInUse(current);
        }
    }

    /// <summary>One choice at a time: a press while one runs is dropped, so two quick A can't cross.</summary>
    private void Run(Func<Task> work)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        Loading = RunAsync(work);
        Loading.Forget(AppTexts.ErrorContextChangeArtwork);
    }

    private async Task RunAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        finally
        {
            _busy = false;
        }
    }
}
