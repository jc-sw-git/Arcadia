using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;

namespace Arcadia.App.Views;

public partial class GameDetailView : UserControl
{
    // Title, buttons and the panel's content fade in and rise a little when the shown game changes (#499); the covers move
    // and the background blends as long (DepthSlot.TurnTime).
    private static readonly Animation ShowGame = new()
    {
        Duration = TimeSpan.FromMilliseconds(420),
        Easing = new CubicEaseOut(),
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0d), new Setter(TranslateTransform.YProperty, 10d) } },
            new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1d), new Setter(TranslateTransform.YProperty, 0d) } },
        },
    };

    private GameDetailViewModel? _viewModel;

    // Wheel movement not yet turned into a turn; touchpads report fractions of a notch.
    private double _wheel;

    public GameDetailView()
    {
        InitializeComponent();
        Heading.RenderTransform = new TranslateTransform();
        AreaContent.RenderTransform = new TranslateTransform();
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

        _wheel = 0;
    }

    /// <summary>One notch of the wheel turns the carousel by one game: down is the next, up the previous.</summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_viewModel is null)
        {
            return;
        }

        e.Handled = true;
        _wheel -= e.Delta.Y;
        for (; _wheel >= 1; _wheel--)
        {
            _viewModel.Rotate(1);
        }

        for (; _wheel <= -1; _wheel++)
        {
            _viewModel.Rotate(-1);
        }
    }

    /// <summary>The mouse over a row focuses it, so the info box explains it before a click (as in the settings).</summary>
    private void OnRowPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control { DataContext: DetailRowViewModel row })
        {
            row.Point();
        }
    }

    /// <summary>Another game is shown: its title and content fade in while the covers move and the background blends.</summary>
    private void OnRotated(int delta)
    {
        ShowGame.RunAsync(Heading).Forget("Detailseite: Titel einblenden");
        ShowGame.RunAsync(AreaContent).Forget("Detailseite: Inhalt einblenden");
    }
}
