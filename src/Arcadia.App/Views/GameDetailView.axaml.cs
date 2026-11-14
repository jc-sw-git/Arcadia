using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.ComponentModel;

namespace Arcadia.App.Views;

public partial class GameDetailView : UserControl
{
    // Title, buttons and the panel's content fade in and rise a little when the shown game changes (#499); the covers move
    // and the background blends as long (DepthSlot.TurnTime).
    private static readonly Animation ShowGame = new()
    {
        Duration = GameDetailViewModel.JumpTime,
        Easing = new CubicEaseOut(),
        // No fill: a jump stopped by a quick turn (#506) leaves the title and content as they are, fully shown.
        FillMode = FillMode.None,
        Children =
        {
            new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0d), new Setter(TranslateTransform.YProperty, 10d) } },
            new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1d), new Setter(TranslateTransform.YProperty, 0d) } },
        },
    };

    private const double InfoScrollStep = 120;

    private GameDetailViewModel? _viewModel;
    private GameDetailPageViewModel? _page;
    private DetailRowViewModel? _infoRow;

    // Wheel movement not yet turned into a turn; touchpads report fractions of a notch.
    private double _wheel;

    public GameDetailView()
    {
        InitializeComponent();
        Heading.RenderTransform = new TranslateTransform();
        AreaContent.RenderTransform = new TranslateTransform();
        InfoScroller.ScrollChanged += (_, _) => UpdateInfoOverflow();
        InfosScroller.ScrollChanged += (_, _) => UpdateInfoOverflow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewModel?.StopJump();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.Jumped -= OnJumped;
            _viewModel.InfoScrollRequested -= OnInfoScrollRequested;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as GameDetailViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Jumped += OnJumped;
            _viewModel.InfoScrollRequested += OnInfoScrollRequested;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        _wheel = 0;
        FollowPage();
    }

    /// <summary>
    /// One notch of the wheel over the background turns the carousel by one game: down is the next, up the previous. Over
    /// the panel it never turns (#530): there it scrolls the text or info box under it, so reading never switches the game.
    /// </summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_viewModel is null)
        {
            return;
        }

        e.Handled = true;
        if (e.Source is Visual source && (source == Panel || Panel.IsVisualAncestorOf(source)))
        {
            return;
        }

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

    /// <summary>
    /// Another game is shown on its own: its title and content fade in while the covers move and the background blends.
    /// A quick turn cancels <paramref name="jump"/> and shows the next game at once (#506).
    /// </summary>
    private void OnJumped(CancellationToken jump)
    {
        ShowGame.RunAsync(Heading, jump).Forget("Detailseite: Titel einblenden");
        ShowGame.RunAsync(AreaContent, jump).Forget("Detailseite: Inhalt einblenden");
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameDetailViewModel.Current))
        {
            FollowPage();
        }
    }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GameDetailPageViewModel.InfoRow))
        {
            FollowInfoRow();
        }
        else if (e.PropertyName is nameof(GameDetailPageViewModel.Zone) or nameof(GameDetailPageViewModel.IsReadingInfos))
        {
            UpdateInfoOverflow();
        }
        else if (e.PropertyName is nameof(GameDetailPageViewModel.OpenArea) or nameof(GameDetailPageViewModel.GameInfo))
        {
            // Another area, or the text of "Infos" arrived: it starts at its top.
            ShowInfosTop();
        }
    }

    private void OnInfoRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DetailRowViewModel.ValueText))
        {
            ShowInfoTop();
        }
    }

    /// <summary>The shown game changed: the info box follows its page.</summary>
    private void FollowPage()
    {
        if (_page is not null)
        {
            _page.PropertyChanged -= OnPagePropertyChanged;
        }

        _page = _viewModel?.Current;
        if (_page is not null)
        {
            _page.PropertyChanged += OnPagePropertyChanged;
        }

        FollowInfoRow();
        ShowInfosTop();
    }

    /// <summary>Another row explained (the focus moved, or the rows were built again as a row greyed): from its top.</summary>
    private void FollowInfoRow()
    {
        if (_infoRow is not null)
        {
            _infoRow.PropertyChanged -= OnInfoRowPropertyChanged;
        }

        _infoRow = _page?.InfoRow;
        if (_infoRow is not null)
        {
            _infoRow.PropertyChanged += OnInfoRowPropertyChanged;
        }

        ShowInfoTop();
    }

    /// <summary>What the info box explains changed: it starts at its top again, with the note and the name (#506).</summary>
    private void ShowInfoTop() => Dispatcher.UIThread.Post(() =>
    {
        InfoScroller.Offset = default;
        UpdateInfoOverflow();
    }, DispatcherPriority.Loaded);

    private void ShowInfosTop() => Dispatcher.UIThread.Post(() =>
    {
        InfosScroller.Offset = default;
        UpdateInfoOverflow();
    }, DispatcherPriority.Loaded);

    /// <summary>RS or Shift+W/S: the info box, or the text of "Infos" (#530), one step up (-1) or down (1). A box that fits stays.</summary>
    private void OnInfoScrollRequested(int direction)
    {
        var scroller = ShownScroller;
        var max = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
        scroller.Offset = new Vector(0, Math.Clamp(scroller.Offset.Y + (direction * InfoScrollStep), 0, max));
    }

    /// <summary>While the focus is on a row whose info box can scroll, or on a longer "Infos", RS scrolls it and the footer names RS.</summary>
    private void UpdateInfoOverflow()
    {
        if (_viewModel is not null)
        {
            _viewModel.InfoOverflows = _page is { IsInRows: true } or { IsReadingInfos: true } && ScrollsOver(ShownScroller);
        }
    }

    /// <summary>"Infos" scrolls its text; every other area its info box.</summary>
    private ScrollViewer ShownScroller => _page?.OpenArea.IsInfos == true ? InfosScroller : InfoScroller;

    private static bool ScrollsOver(ScrollViewer scroller) =>
        scroller.IsEffectivelyVisible && scroller.Extent.Height - scroller.Viewport.Height > 1;
}
