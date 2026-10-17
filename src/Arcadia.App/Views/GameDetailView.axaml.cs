using Arcadia.App.ViewModels;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace Arcadia.App.Views;

public partial class GameDetailView : UserControl
{
    private static readonly TimeSpan SlideTime = TimeSpan.FromMilliseconds(340);

    private GameDetailViewModel? _viewModel;

    public GameDetailView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.Rotated -= OnRotated;
        }

        _viewModel = DataContext as GameDetailViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Rotated += OnRotated;
        }

        if (Track.RenderTransform is TranslateTransform slide)
        {
            slide.Transitions = null;
            slide.X = 0;
        }
    }

    /// <summary>
    /// The pages already moved one place along. The track jumps back by one page, so nothing
    /// visibly changes, and slides to its rest position from there; a turn during a slide
    /// continues from where it is.
    /// </summary>
    private void OnRotated(int delta)
    {
        if (Track.RenderTransform is not TranslateTransform slide)
        {
            return;
        }

        // Reading X during a slide gives the animated value; removing the transition stops it.
        var from = Math.Clamp(slide.X + (delta * GameDetailPageViewModel.Pitch), -GameDetailPageViewModel.Pitch, GameDetailPageViewModel.Pitch);
        slide.Transitions = null;
        slide.X = from;
        slide.Transitions =
        [
            new DoubleTransition
            {
                Property = TranslateTransform.XProperty,
                Duration = SlideTime,
                Easing = new SplineEasing(0.25, 0.8, 0.25, 1),
            },
        ];
        slide.X = 0;
    }
}
