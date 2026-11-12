using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>One game on the waiting list, left: its initial, name, launcher and how many hits it has.</summary>
public sealed partial class WaitingGameViewModel(WaitingGame waiting, Action<WaitingGameViewModel> pick) : ViewModelBase
{
    public WaitingGame Waiting { get; } = waiting;

    public string Key => Waiting.Game.Key;

    public string Title => Waiting.Game.Title;

    public string Monogram => Formatting.Monogram(Waiting.Game.Title);

    public string LauncherName => Waiting.LauncherName;

    public string HitsText => AppTexts.WaitingHitCount(Waiting.Hits.Count);

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    /// <summary>A click makes it the current game.</summary>
    [RelayCommand]
    private void Pick() => pick(this);
}

/// <summary>One hit of the current game, right: its cover (a placeholder with its name until it is there), name and year.</summary>
public sealed partial class WaitingHitViewModel(SteamGridDbHit hit, Action<WaitingHitViewModel> pick) : ViewModelBase
{
    public SteamGridDbHit Hit { get; } = hit;

    public string Name => Hit.Name;

    /// <summary>The year it came out; empty when SteamGridDB doesn't know it.</summary>
    public string YearText => Hit.Year?.ToString(Formatting.Culture) ?? string.Empty;

    public bool HasYear => Hit.Year is not null;

    [ObservableProperty]
    public partial ArtworkFile? Cover { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>A click focuses it; A takes it.</summary>
    [RelayCommand]
    private void Pick() => pick(this);
}

/// <summary>
/// "Warten auf deine Auswahl" (#491, prototype in #13): the games SteamGridDB found only by name with several matching
/// games, left, and the current game's hits, right. Left/right move through the hits, up/down change the game (D4), both
/// round at the ends; A takes the focused hit and moves on to the next game, Y skips the game, B closes. The list follows
/// the service: games found by a later search come at the end, a game that left the library goes.
/// </summary>
/// <remarks>UI thread only. Hit covers load in the background for the game shown, a few at a time.</remarks>
public sealed partial class WaitingListViewModel : ViewModelBase, IDisposable
{
    private readonly ArtworkService _artwork;
    private readonly Action<WaitingListViewModel> _close;
    private CancellationTokenSource? _coverLoad;
    private int _focus;
    private bool _disposed;

    /// <param name="close">B or the ✕: back to the row "Warten auf deine Auswahl".</param>
    public WaitingListViewModel(ArtworkService artwork, Action<WaitingListViewModel> close)
    {
        _artwork = artwork;
        _close = close;
        _artwork.WaitingChanged += OnWaitingChanged;
        Refresh();
    }

    public void Dispose()
    {
        _disposed = true;
        _artwork.WaitingChanged -= OnWaitingChanged;
        _coverLoad?.Cancel();
    }

    public static string Title => AppTexts.WaitingTitle;

    public static string Question => AppTexts.WaitingQuestion;

    public ObservableCollection<WaitingGameViewModel> Games { get; } = [];

    public ObservableCollection<WaitingHitViewModel> Hits { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGames), nameof(IsDone), nameof(Position), nameof(GamesHeading), nameof(CurrentTitle), nameof(Intro))]
    public partial WaitingGameViewModel? CurrentGame { get; private set; }

    public bool HasGames => CurrentGame is not null;

    /// <summary>The last game was taken or skipped: "Alles erledigt."</summary>
    public bool IsDone => CurrentGame is null;

    public string Position => CurrentGame is null ? string.Empty : AppTexts.WaitingPosition(number: Games.IndexOf(CurrentGame) + 1, count: Games.Count);

    public string GamesHeading => Games.Count == 1 ? AppTexts.WaitingGamesOne : AppTexts.WaitingGames(Games.Count);

    public string CurrentTitle => CurrentGame?.Title ?? string.Empty;

    public string Intro => CurrentGame is null ? string.Empty : AppTexts.WaitingIntro(CurrentGame.Waiting.Hits.Count);

    /// <summary>The last choice being made (its images loading); the tests wait for it.</summary>
    public Task Loading { get; private set; } = Task.CompletedTask;

    /// <summary>The covers of the game shown being loaded; the tests wait for it.</summary>
    public Task Covers { get; private set; } = Task.CompletedTask;

    /// <summary>The focus moved: the view brings the current game into sight.</summary>
    public event EventHandler? FocusMoved;

    /// <summary>A press while the list is open; it takes every one.</summary>
    public void Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Left when HasGames:
                FocusHit(GridFocus.Step(_focus, Hits.Count, Hits.Count, Hits.Count, -1, 0, HeldPress.IsRepeat));
                break;
            case UiAction.Right when HasGames:
                FocusHit(GridFocus.Step(_focus, Hits.Count, Hits.Count, Hits.Count, 1, 0, HeldPress.IsRepeat));
                break;
            case UiAction.Up when HasGames:
                ShowGame(Games[GridFocus.Step(Games.IndexOf(CurrentGame!), Games.Count, Games.Count, 1, 0, -1, HeldPress.IsRepeat)]);
                break;
            case UiAction.Down when HasGames:
                ShowGame(Games[GridFocus.Step(Games.IndexOf(CurrentGame!), Games.Count, Games.Count, 1, 0, 1, HeldPress.IsRepeat)]);
                break;
            case UiAction.Accept:
                Take();
                break;
            case UiAction.Status:
                Skip();
                break;
            case UiAction.Back:
                _close(this);
                break;
        }
    }

    [RelayCommand]
    private void Close() => _close(this);

    [RelayCommand]
    private void Take()
    {
        if (CurrentGame is not { } game || _focus >= Hits.Count)
        {
            return;
        }

        // The choice is kept at once and the next game becomes current; the images come off the UI thread meanwhile.
        var choosing = _artwork.ChooseAsync(game.Waiting.Game, Hits[_focus].Hit);
        Refresh();
        Loading = choosing;
        choosing.Forget(AppTexts.ErrorContextWaitingList);
    }

    [RelayCommand]
    private void Skip()
    {
        if (CurrentGame is { } game)
        {
            _artwork.Skip(game.Waiting.Game);
            Refresh();
        }
    }

    private void OnWaitingChanged(object? sender, EventArgs e) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed)
            {
                Refresh();
            }
        });

    /// <summary>The games as the service has them now; the current one stays, or the one after it when it left.</summary>
    private void Refresh()
    {
        var waiting = _artwork.Waiting;
        var currentKey = CurrentGame?.Key;
        var currentIndex = CurrentGame is null ? 0 : Games.IndexOf(CurrentGame);
        var known = Games.ToDictionary(g => g.Key, StringComparer.Ordinal);
        Games.Clear();
        foreach (var game in waiting)
        {
            Games.Add(known.TryGetValue(game.Game.Key, out var same) && ReferenceEquals(same.Waiting.Hits, game.Hits) ? same : new WaitingGameViewModel(game, ShowGame));
        }

        var next = Games.FirstOrDefault(g => g.Key == currentKey) ?? (Games.Count == 0 ? null : Games[Math.Min(currentIndex, Games.Count - 1)]);
        if (next is null || next.Key != currentKey)
        {
            ShowGame(next);
        }
        else
        {
            MarkCurrent(next);
        }

        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(GamesHeading));
    }

    private void ShowGame(WaitingGameViewModel? game)
    {
        if (game is not null && game == CurrentGame)
        {
            return;
        }

        MarkCurrent(game);
        _coverLoad?.Cancel();
        Hits.Clear();
        if (game is null)
        {
            return;
        }

        foreach (var hit in game.Waiting.Hits)
        {
            Hits.Add(new WaitingHitViewModel(hit, h => FocusHit(Hits.IndexOf(h))));
        }

        FocusHit(0);
        _coverLoad = new CancellationTokenSource();
        var covers = LoadCoversAsync(game, [.. Hits], _coverLoad.Token);
        Covers = covers;
        covers.Forget(AppTexts.ErrorContextWaitingList);
    }

    private void MarkCurrent(WaitingGameViewModel? game)
    {
        foreach (var each in Games)
        {
            each.IsCurrent = each == game;
        }

        CurrentGame = game;
        FocusMoved?.Invoke(this, EventArgs.Empty);
    }

    private void FocusHit(int index)
    {
        _focus = Math.Clamp(index, 0, Math.Max(Hits.Count - 1, 0));
        for (var i = 0; i < Hits.Count; i++)
        {
            Hits[i].IsFocused = i == _focus;
        }
    }

    /// <summary>The covers of the game shown, off the UI thread; a game shown meanwhile cancels the rest.</summary>
    private async Task LoadCoversAsync(WaitingGameViewModel game, IReadOnlyList<WaitingHitViewModel> hits, CancellationToken cancel)
    {
        try
        {
            await Task.WhenAll(hits.Select(async hit =>
            {
                var cover = await Task.Run(() => _artwork.HitCoverAsync(game.Waiting.Game, hit.Hit, cancel), cancel);
                if (!cancel.IsCancellationRequested)
                {
                    hit.Cover = cover;
                }
            }));
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // Another game is shown, or the list closed.
        }
    }
}
