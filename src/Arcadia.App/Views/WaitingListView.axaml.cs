using Arcadia.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Arcadia.App.Views;

/// <summary>"Warten auf deine Auswahl" (#491): keeps the current game in sight when the list is taller than the modal.</summary>
public partial class WaitingListView : UserControl
{
    private WaitingListViewModel? _viewModel;

    public WaitingListView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.FocusMoved -= OnFocusMoved;
        }

        _viewModel = DataContext as WaitingListViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusMoved += OnFocusMoved;
        }
    }

    private void OnFocusMoved(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (_viewModel?.CurrentGame is { } current && _viewModel.Games.IndexOf(current) is var index and >= 0)
        {
            // By index: with the virtualised list the current game may have no row yet.
            GameList.ScrollIntoView(index);
        }
    }, DispatcherPriority.Loaded);
}
