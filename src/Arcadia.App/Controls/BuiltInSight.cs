using Arcadia.App.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Arcadia.App.Controls;

/// <summary>
/// Builds its child from <see cref="Template"/> only while it lies in sight of the scroller around it or within one of its
/// own heights of it, and lets the child go once it is more than <see cref="ReleaseDistance"/> of its heights away (#685).
/// A page of 128 covers shows about four of its sixteen rows: each cover is some 40 elements with a few hundred style
/// records, so the covers out of sight were most of Arcadia's memory. Its size comes from its parent
/// (<see cref="CoverFrame"/>), so a place without a child keeps the layout as it is.
/// </summary>
/// <remarks>
/// What comes into sight is built during layout, before the frame is drawn, so a cover never shows up empty. What only
/// comes near, and what goes far, is built or let go after the frame, one place per dispatcher job: a press that moves
/// the focus a row shows at once, and the row behind it is ready before the next press. A hidden page has no viewport;
/// its children are kept, so a page shown again builds and decodes nothing.
/// </remarks>
public sealed class BuiltInSight : Decorator
{
    public static readonly StyledProperty<IDataTemplate?> TemplateProperty =
        AvaloniaProperty.Register<BuiltInSight, IDataTemplate?>(nameof(Template));

    /// <summary>How far out of sight, in the element's own heights, the child stays: a row scrolled past and back keeps it.</summary>
    public const double ReleaseDistance = 2;

    private Distance _distance = Distance.Unknown;
    private bool _stepQueued;

    public BuiltInSight()
    {
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    private enum Distance
    {
        Unknown,
        InSight,
        Near,
        Kept,
        Far,
    }

    /// <summary>Builds the child; its <c>DataContext</c> is this element's.</summary>
    public IDataTemplate? Template
    {
        get => GetValue(TemplateProperty);
        set => SetValue(TemplateProperty, value);
    }

    /// <summary>The child is built (in or near sight), for tests.</summary>
    public bool IsBuilt => Child is not null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TemplateProperty && Child is not null)
        {
            Child = Template?.Build(DataContext);
        }
    }

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var viewport = e.EffectiveViewport;
        var height = Bounds.Height;
        if (viewport.Width <= 0 || viewport.Height <= 0 || height <= 0)
        {
            return;
        }

        var self = new Rect(Bounds.Size);
        _distance = viewport.Intersects(self) ? Distance.InSight
            : viewport.Inflate(new Thickness(0, height)).Intersects(self) ? Distance.Near
            : viewport.Inflate(new Thickness(0, ReleaseDistance * height)).Intersects(self) ? Distance.Kept
            : Distance.Far;

        if (_distance == Distance.InSight && Child is null)
        {
            Child = Template?.Build(DataContext);
        }
        else if (!_stepQueued && ((_distance == Distance.Near && Child is null) || (_distance == Distance.Far && Child is not null)))
        {
            _stepQueued = true;
            Dispatcher.UIThread.Post(StepAfterFrame, DispatcherPriority.Background);
        }
    }

    // The place may have moved again since: only what still holds is done.
    private void StepAfterFrame()
    {
        _stepQueued = false;
        try
        {
            if (_distance is Distance.InSight or Distance.Near && Child is null)
            {
                Child = Template?.Build(DataContext);
            }
            else if (_distance == Distance.Far && Child is not null)
            {
                Child = null;
            }
        }
        catch (Exception exception)
        {
            AppErrors.Report(exception, AppTexts.ErrorContextUi);
        }
    }
}
