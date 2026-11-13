using Arcadia.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Arcadia.App.Views;

/// <summary>"Artwork ändern" (#490): keeps the focused tile in sight when the grid is taller than the modal.</summary>
public partial class ChangeArtworkView : UserControl
{
    private ChangeArtworkViewModel? _viewModel;

    public ChangeArtworkView()
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

        _viewModel = DataContext as ChangeArtworkViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusMoved += OnFocusMoved;
        }
    }

    private void OnFocusMoved(object? sender, int index) => Dispatcher.UIThread.Post(() =>
    {
        if (TileList.ContainerFromIndex(index) is { } container)
        {
            TileFade.ScrollIntoView(container);
        }
    }, DispatcherPriority.Loaded);
}
