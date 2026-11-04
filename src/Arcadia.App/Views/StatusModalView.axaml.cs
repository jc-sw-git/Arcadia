using Arcadia.App.Status;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Arcadia.App.Views;

/// <summary>"Status" (#166): keeps the focused entry in sight; the shared fade shows where more entries follow.</summary>
public partial class StatusModalView : UserControl
{
    private StatusModalViewModel? _viewModel;

    public StatusModalView()
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

        _viewModel = DataContext as StatusModalViewModel;
        if (_viewModel is not null)
        {
            _viewModel.FocusMoved += OnFocusMoved;
            OnFocusMoved(_viewModel, EventArgs.Empty);
        }
    }

    private void OnFocusMoved(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (_viewModel?.Focused is { } entry && EntryList.ContainerFromItem(entry) is { } container)
        {
            EntriesFade.ScrollIntoView(container);
        }
    }, DispatcherPriority.Loaded);
}
