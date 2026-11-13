using Arcadia.Launchers.Abstractions;
using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.Settings;
using Arcadia.Core.Artwork;
using Arcadia.Core.Metadata;
using Arcadia.Core.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// "Warten auf deine Auswahl" (#491, two levels since #506): the games SteamGridDB (artwork) or IGDB (metadata, #494) found
/// only by name with several matching games, left, one list per kind (#538), and the current game's hits, right
/// (<see cref="WaitingHitsViewModel"/>). In the games list
/// ▲ ▼ choose a game (round at the ends), A goes into its hits, B closes; in the hits the D-pad moves through the grid,
/// A takes the hit and comes back to the games list on the next waiting entry, B comes back without. Y skips the entry in
/// both. A click on a hit only marks it, a double click takes it (#539). The list follows the services: entries found by a later search come at the end of their kind, a game that left
/// the library goes.
/// </summary>
/// <remarks>
/// UI thread only. Moving through the games only marks the game and shows the hits already known; nothing is asked or
/// loaded until the focus rests on one (<see cref="WaitingHitsViewModel.Start"/>), so a held ▼ stays smooth.
/// </remarks>
public sealed partial class WaitingListViewModel : ViewModelBase, IDisposable
{
    private readonly WaitingKind _kind;
    private readonly ArtworkService _artwork;
    private readonly MetadataService? _metadata;
    private readonly Func<Game, string> _titleOf;
    private readonly WaitingKeyboard _keyboard;
    private readonly Action<WaitingListViewModel> _close;
    private bool _disposed;

    /// <param name="kind">Which list: the games waiting for their SteamGridDB game or for their IGDB game.</param>
    /// <param name="keyboard">The settings' on-screen keyboard, for "Eigener Suchbegriff".</param>
    /// <param name="close">B in the games list or the ✕: back to the row "Warten auf deine Auswahl".</param>
    /// <param name="titleOf">The title a game shows, its own one included (#496).</param>
    /// <param name="metadata">The metadata entries (#494); needed for <see cref="WaitingKind.Metadata"/>.</param>
    /// <param name="artworkShown">The artwork entries are on the list: SteamGridDB is switched on.</param>
    public WaitingListViewModel(
        WaitingKind kind, ArtworkService artwork, Func<Game, string> titleOf, WaitingKeyboard keyboard, Action<WaitingListViewModel> close, MetadataService? metadata = null,
        Func<bool>? artworkShown = null)
    {
        _kind = kind;
        _artwork = artwork;
        _metadata = metadata;
        _artworkShown = artworkShown ?? (() => true);
        _titleOf = titleOf;
        _keyboard = keyboard;
        _close = close;
        _artwork.WaitingChanged += OnWaitingChanged;
        _metadata?.WaitingChanged += OnWaitingChanged;
        Refresh();
    }

    private readonly Func<bool> _artworkShown;

    public void Dispose()
    {
        _disposed = true;
        _artwork.WaitingChanged -= OnWaitingChanged;
        _metadata?.WaitingChanged -= OnWaitingChanged;
        Hits?.Dispose();
    }

    public static string Title => AppTexts.WaitingTitle;

    public WaitingKind Kind => _kind;

    public static string Question => AppTexts.WaitingQuestion;

    public ObservableCollection<WaitingGameViewModel> Games { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGames), nameof(IsDone), nameof(Position), nameof(GamesHeading))]
    public partial WaitingGameViewModel? CurrentGame { get; private set; }

    /// <summary>The current game's hits; null after the last game.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArtworkTabs))]
    public partial WaitingHitsViewModel? Hits { get; private set; }

    /// <summary>The game seeks cover and background: the footer names LB/RB for their tabs (#539).</summary>
    public bool HasArtworkTabs => Hits is ArtworkHitsViewModel { SeeksBoth: true };

    /// <summary>The focus is in the hits (the second level), not in the games list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChooseHint), nameof(AcceptHint), nameof(BackHint))]
    public partial bool InHits { get; private set; }

    public bool HasGames => CurrentGame is not null;

    /// <summary>The last game was taken or skipped: "Alles erledigt."</summary>
    public bool IsDone => CurrentGame is null;

    public string Position => CurrentGame is null ? string.Empty : AppTexts.WaitingPosition(number: Games.IndexOf(CurrentGame) + 1, count: Games.Count);

    public string GamesHeading => Games.Count == 1 ? AppTexts.WaitingGamesOne : AppTexts.WaitingGames(Games.Count);

    public string ChooseHint => InHits ? AppTexts.WaitingChooseHitHint : AppTexts.WaitingChooseHint;

    public string AcceptHint => !InHits ? AppTexts.WaitingOpenHitsHint
        : Hits?.Zone == WaitingHitsFocus.Term ? AppTexts.WaitingSearchHint
        : Hits?.Zone == WaitingHitsFocus.Reset ? AppTexts.WaitingReset
        : Hits?.TakeHint ?? AppTexts.WaitingTakeHint;

    public string BackHint => InHits ? AppTexts.Back : AppTexts.Close;

    /// <summary>The last choice being made (its images loading); the tests wait for it.</summary>
    public Task Loading { get; private set; } = Task.CompletedTask;

    /// <summary>The focus moved in the games list: the view brings the current game into sight.</summary>
    public event EventHandler? FocusMoved;

    /// <summary>A press while the list is open; it takes every one. With the keyboard out, the keyboard takes it.</summary>
    public void Handle(UiAction action)
    {
        if (Hits?.Editor is { } editor)
        {
            editor.Handle(action);
            return;
        }

        switch (action)
        {
            case UiAction.Up or UiAction.Down when HasGames && !InHits:
                ShowGame(Games[GridFocus.Step(Games.IndexOf(CurrentGame!), Games.Count, Games.Count, 1, 0, action == UiAction.Up ? -1 : 1, HeldPress.IsRepeat)]);
                break;
            case UiAction.Up or UiAction.Down or UiAction.Left or UiAction.Right or UiAction.Accept when InHits:
                Hits!.Handle(action);
                HintsChanged();
                break;
            case UiAction.Accept when HasGames:
                EnterHits();
                break;
            case UiAction.Status:
                Skip();
                break;
            case UiAction.PreviousTab or UiAction.NextTab when Hits is ArtworkHitsViewModel artworkHits:
                // Cover and background sought: LB/RB turn their tabs (#539), as in "Artwork ändern".
                artworkHits.SwitchTab();
                break;
            case UiAction.Back when InHits:
                LeaveHits();
                break;
            case UiAction.Back:
                _close(this);
                break;
        }
    }

    /// <summary>A button let go: ends a held erase, shift or AltGr on the keyboard.</summary>
    public void Release(UiAction action) => Hits?.Editor?.Release(action);

    /// <summary>Drops held buttons, e.g. when the window loses focus.</summary>
    public void CancelHeldButtons() => Hits?.Editor?.CancelHeldButtons();

    [RelayCommand]
    private void Close() => _close(this);

    [RelayCommand]
    private void Skip()
    {
        if (CurrentGame is { } game)
        {
            if (game.IsMetadata)
            {
                _metadata?.Skip(game.Game);
            }
            else
            {
                _artwork.Skip(game.Game);
            }

            InHits = false;
            Refresh();
        }
    }

    /// <summary>The A hint in the footer: what A does where the focus is.</summary>
    [RelayCommand]
    private void Accept() => Handle(UiAction.Accept);

    /// <summary>A pick or another tab, also by a click or double click: the footer says what A does now.</summary>
    private void OnHitsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WaitingHitsViewModel.TakeHint))
        {
            HintsChanged();
        }
        else if (e.PropertyName is nameof(ArtworkHitsViewModel.SeeksBoth))
        {
            OnPropertyChanged(nameof(HasArtworkTabs));
        }
    }

    private void HintsChanged()
    {
        OnPropertyChanged(nameof(AcceptHint));
        OnPropertyChanged(nameof(BackHint));
    }

    /// <summary>A click on a hit: the focus comes into the hits, onto it; nothing is taken (#539).</summary>
    private void Mark(WaitingHitViewModel hit)
    {
        if (Hits is not { } hits || _disposed)
        {
            return;
        }

        InHits = true;
        hits.Mark(hit);
        HintsChanged();
    }

    private void EnterHits()
    {
        if (Hits is { } hits)
        {
            InHits = true;
            hits.Enter();
        }
    }

    private void LeaveHits()
    {
        InHits = false;
        Hits?.Leave();
        (Hits as ArtworkHitsViewModel)?.DropPicks();
    }

    /// <summary>A or a double click on a hit: the choice is kept at once and the next game becomes current; its images come off the UI thread.</summary>
    private void Take(WaitingHitViewModel hit)
    {
        if (CurrentGame is not { } game || _disposed)
        {
            return;
        }

        switch (hit)
        {
            case MetadataHitViewModel metadataHit:
                // In memory at once, so the entry leaves now; the file is written in the background.
                _metadata?.Choose(game.Game, metadataHit.Hit);
                InHits = false;
                Refresh();
                break;
            case ArtworkHitViewModel artworkHit:
                // A background chosen after the cover (#539) comes from its own game; the cover from the one picked first.
                var choosing = artworkHit.Cover is { } cover
                    ? _artwork.ChooseAsync(game.Game, cover, heroHit: artworkHit.Hit)
                    : _artwork.ChooseAsync(game.Game, artworkHit.Hit);
                InHits = false;
                Refresh();
                Loading = choosing;
                choosing.Forget(AppTexts.ErrorContextWaitingList);
                break;
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

    /// <summary>
    /// The entries as the services have them now; the current one stays, or the one after it when it left (the first after
    /// the last). Only what changed is removed, moved or added, so a press on a long list doesn't rebuild it.
    /// </summary>
    private void Refresh()
    {
        var waiting = Entries();
        var currentKey = CurrentGame?.Key;
        var currentIndex = CurrentGame is null ? 0 : Games.IndexOf(CurrentGame);
        var wanted = waiting.ToDictionary(w => w.Key, StringComparer.Ordinal);
        for (var i = Games.Count - 1; i >= 0; i--)
        {
            if (wanted.TryGetValue(Games[i].Key, out var now))
            {
                Games[i].Artwork = now.Artwork;
                Games[i].Metadata = now.Metadata;
            }
            else
            {
                Games.RemoveAt(i);
            }
        }

        var known = Games.ToDictionary(g => g.Key, StringComparer.Ordinal);
        for (var i = 0; i < waiting.Count; i++)
        {
            var key = waiting[i].Key;
            if (i < Games.Count && Games[i].Key == key)
            {
                continue;
            }

            if (known.TryGetValue(key, out var same))
            {
                Games.Move(Games.IndexOf(same), i);
            }
            else
            {
                Games.Insert(i, waiting[i].Artwork is { } artwork
                    ? new WaitingGameViewModel(artwork, _titleOf(artwork.Game), ShowGame)
                    : new WaitingGameViewModel(waiting[i].Metadata!, _titleOf(waiting[i].Metadata!.Game), ShowGame));
            }
        }

        var next = Games.FirstOrDefault(g => g.Key == currentKey) ?? (Games.Count == 0 ? null : Games[currentIndex < Games.Count ? currentIndex : 0]);
        if (next != CurrentGame)
        {
            ShowGame(next);
        }

        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(GamesHeading));
    }

    /// <summary>
    /// The game the focus moves to: marked at once with the hits already known; checks and images wait until the focus
    /// rests on it. A click on a game in the hits level brings the focus back to the games list.
    /// </summary>
    private void ShowGame(WaitingGameViewModel? game)
    {
        if (game is not null && game == CurrentGame)
        {
            LeaveHits();
            return;
        }

        CurrentGame?.IsCurrent = false;
        game?.IsCurrent = true;
        CurrentGame = game;
        InHits = false;
        if (Hits is { } before)
        {
            before.PropertyChanged -= OnHitsChanged;
            before.Dispose();
        }

        Hits = game switch
        {
            { Metadata: { } metadata } when _metadata is not null => new MetadataHitsViewModel(_metadata, metadata, game.Title, _keyboard, Take, Mark),
            { Artwork: { } artwork } => new ArtworkHitsViewModel(_artwork, artwork, game.Title, _keyboard, Take, Mark),
            _ => null,
        };
        Hits?.PropertyChanged += OnHitsChanged;
        Hits?.Start();
        FocusMoved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The games waiting for this list's kind (artwork only while SteamGridDB is on), in the order they came on it.</summary>
    private List<(string Key, WaitingGame? Artwork, WaitingMetadataGame? Metadata)> Entries() => _kind == WaitingKind.Artwork
        ? [.. (_artworkShown() ? _artwork.Waiting : []).Select(w => (WaitingGameViewModel.KeyOf(w.Game, metadata: false), (WaitingGame?)w, (WaitingMetadataGame?)null))]
        : [.. (_metadata?.Waiting ?? []).Select(w => (WaitingGameViewModel.KeyOf(w.Game, metadata: true), (WaitingGame?)null, (WaitingMetadataGame?)w))];
}
