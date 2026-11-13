using System.ComponentModel;
using Arcadia.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Arcadia.App.Views;

/// <summary>
/// "Warten auf deine Auswahl" (#491, #506): a double click takes a hit (#539); keeps the current game and the focused hit in sight and out of the edge
/// fades when the list or the grid is taller than its place.
/// </summary>
public partial class WaitingListView : UserControl
{
    private WaitingListViewModel? _viewModel;
    private WaitingHitsViewModel? _hits;

    public WaitingListView()
    {
        InitializeComponent();

        // The tile's Button handles the press; the double tap comes after its two clicks marked the hit (#539).
        HitGrid.AddHandler(DoubleTappedEvent, OnHitDoubleTapped, handledEventsToo: true);
    }

    private void OnHitDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is WaitingHitViewModel hit)
        {
            hit.TakeCommand.Execute(null);
            e.Handled = true;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.FocusMoved -= OnGameFocusMoved;
            _viewModel.PropertyChanged -= OnListChanged;
        }

        _viewModel = DataContext as WaitingListViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusMoved += OnGameFocusMoved;
            _viewModel.PropertyChanged += OnListChanged;
        }

        FollowHits();
    }

    private void OnListChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WaitingListViewModel.Hits))
        {
            FollowHits();
        }
    }

    private void FollowHits()
    {
        _hits?.FocusMoved -= OnHitFocusMoved;
        _hits = _viewModel?.Hits;
        _hits?.FocusMoved += OnHitFocusMoved;
    }

    private void OnGameFocusMoved(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (_viewModel?.CurrentGame is not { } current || _viewModel.Games.IndexOf(current) is not (var index and >= 0))
        {
            return;
        }

        // By index first: with the virtualised list the current game may have no row yet; then out of the fade.
        GameList.ScrollIntoView(index);
        if (GameList.ContainerFromIndex(index) is { } row)
        {
            GameFade.ScrollIntoView(row);
        }
    }, DispatcherPriority.Loaded);

    private void OnHitFocusMoved(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (_hits is not { IsActive: true } hits)
        {
            return;
        }

        var focused = hits.Hits.Select((hit, index) => (hit, index)).FirstOrDefault(e => e.hit.IsFocused);
        if (focused.hit is not null && HitGrid.ContainerFromIndex(focused.index) is { } tile)
        {
            HitFade.ScrollIntoView(tile);
        }
        else if (!hits.Hits.Any(h => h.IsFocused))
        {
            // The field's row is above the grid: the grid goes back to its top.
            HitScroller.Offset = default;
        }
    }, DispatcherPriority.Loaded);
}
