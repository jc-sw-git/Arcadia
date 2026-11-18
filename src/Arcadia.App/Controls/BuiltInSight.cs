using Arcadia.App.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

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
/// <para>Nothing is let go while the player moves through the list: building again costs a frame. Only once the player
/// has been quiet for a while does <see cref="LetGoOutOfSight"/> drop the children far from sight, except the first
/// <see cref="KeptPlaces"/>: a page turn shows those at once. A hidden page has no viewport; it keeps where it stood.</para>
/// </remarks>
public sealed class BuiltInSight : Decorator
{
    public static readonly StyledProperty<IDataTemplate?> TemplateProperty =
        AvaloniaProperty.Register<BuiltInSight, IDataTemplate?>(nameof(Template));

    public static readonly StyledProperty<int> KeptPlacesProperty =
        AvaloniaProperty.Register<BuiltInSight, int>(nameof(KeptPlaces));

    /// <summary>How far ahead of sight, in the place's own heights, children are built after the frame.</summary>
    public const double NearRows = 2;

    /// <summary>How far out of sight, in the place's own heights, a child is let go when the player is quiet.</summary>
    public const double FarRows = 3;

    // Only touched on the UI thread.
    private static readonly HashSet<BuiltInSight> Attached = [];

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
        Kept,
        Far,
    }

    /// <summary>Builds the child; its <c>DataContext</c> is this element's.</summary>
    public IDataTemplate? Template
    {
        get => GetValue(TemplateProperty);
        set => SetValue(TemplateProperty, value);
    }

    /// <summary>The first places of the list, by index, that are never let go: what the list shows from its top.</summary>
    public int KeptPlaces
    {
        get => GetValue(KeptPlacesProperty);
        set => SetValue(KeptPlacesProperty, value);
    }

    /// <summary>The child is built, for tests.</summary>
    public bool IsBuilt => Child is not null;

    /// <summary>
    /// Lets go of the children far from sight in every list, but the kept places; call it while the player is quiet. The
    /// count of children let go: a full collection after it gives their memory back.
    /// </summary>
    public static int LetGoOutOfSight()
    {
        var letGo = 0;
        foreach (var place in Attached.ToList())
        {
            if (place is { _distance: Distance.Far, Child: not null } && !place.IsKeptPlace())
            {
                place.Child = null;
                letGo++;
            }
        }

        return letGo;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attached.Add(this);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Attached.Remove(this);
    }

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
            : viewport.Inflate(new Thickness(0, FarRows * height)).Intersects(self) ? Distance.Kept
            : Distance.Far;

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

    private bool IsKeptPlace()
    {
        if (KeptPlaces <= 0 || this.FindAncestorOfType<ContentPresenter>() is not { } container)
        {
            return false;
        }

        var index = ItemsControl.ItemsControlFromItemContainer(container)?.IndexFromContainer(container) ?? -1;
        return index >= 0 && index < KeptPlaces;
    }
}
