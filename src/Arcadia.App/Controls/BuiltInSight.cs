using Arcadia.App.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Arcadia.App.Controls;

/// <summary>
/// A place in a long list whose child, built from <see cref="Template"/>, exists only once the place came in or near sight
/// of the scroller around it (#685). A page of 128 covers shows about four of its sixteen rows: each cover is some 40
/// elements with a few hundred style records, so the covers never seen were most of Arcadia's memory. Its size comes from
/// its parent (<see cref="CoverFrame"/>), so a place without a child keeps the layout as it is.
/// </summary>
/// <remarks>
/// <para>What comes into sight is built during layout, before the frame is drawn, so a cover never shows up empty. What
/// comes within <see cref="NearRows"/> of sight is built after the frame, one place per dispatcher job, so a focus move
/// shows at once and the rows ahead are ready before the next press.</para>
/// <para>A child once built stays: building it again would cost a frame when the player comes back, and a held stick
/// would stutter. A list's places keep their children when it shows other items (#229).</para>
/// </remarks>
public sealed class BuiltInSight : Decorator
{
    public static readonly StyledProperty<IDataTemplate?> TemplateProperty =
        AvaloniaProperty.Register<BuiltInSight, IDataTemplate?>(nameof(Template));

    /// <summary>How far ahead of sight, in the place's own heights, children are built after the frame.</summary>
    public const double NearRows = 2;

    private Distance _distance = Distance.Unknown;
    private bool _buildQueued;

    public BuiltInSight()
    {
        EffectiveViewportChanged += OnEffectiveViewportChanged;
    }

    private enum Distance
    {
        Unknown,
        InSight,
        Near,
        Away,
    }

    /// <summary>Builds the child; its <c>DataContext</c> is this element's.</summary>
    public IDataTemplate? Template
    {
        get => GetValue(TemplateProperty);
        set => SetValue(TemplateProperty, value);
    }

    /// <summary>The child is built, for tests.</summary>
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
            : viewport.Inflate(new Thickness(0, NearRows * height)).Intersects(self) ? Distance.Near
            : Distance.Away;

        if (Child is not null)
        {
            return;
        }

        if (_distance == Distance.InSight)
        {
            Child = Template?.Build(DataContext);
        }
        else if (_distance == Distance.Near && !_buildQueued)
        {
            _buildQueued = true;
            Dispatcher.UIThread.Post(BuildAfterFrame, DispatcherPriority.Background);
        }
    }

    // The place may have moved on since: it is built only if it is still in or near sight.
    private void BuildAfterFrame()
    {
        _buildQueued = false;
        try
        {
            if (_distance is Distance.InSight or Distance.Near && Child is null)
            {
                Child = Template?.Build(DataContext);
            }
        }
        catch (Exception exception)
        {
            AppErrors.Report(exception, AppTexts.ErrorContextUi);
        }
    }
}
