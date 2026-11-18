using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Path = Avalonia.Controls.Shapes.Path;

namespace Arcadia.App.Controls;

/// <summary>
/// The loading spinner (<c>Path.spinner</c> in the theme): turns only while it can be seen and its window is in front
/// (#636, #680). A turning spinner keeps the window drawing every frame: about a fifth of a core and 1.5 MB of garbage a
/// second on the owner's PC, and in the background no trim gives that memory back (<c>MemoryTrim</c>), so there it
/// stands still, as everything else of Arcadia does. The class <c>turning</c>, which starts the animation, follows
/// <see cref="Visual.IsEffectivelyVisible"/> and <see cref="WindowBase.IsActive"/>; Avalonia raises no public event for
/// the first, so any change of a visual's <see cref="Visual.IsVisible"/> or a window's activation checks the spinners,
/// once per dispatcher round.
/// </summary>
public sealed class LoadingSpinner : Path
{
    private static readonly HashSet<LoadingSpinner> Attached = [];
    private static bool _checkPosted;

    static LoadingSpinner()
    {
        IsVisibleProperty.Changed.AddClassHandler<Visual>((_, _) => PostCheck());
        WindowBase.IsActiveProperty.Changed.AddClassHandler<WindowBase>((_, _) => PostCheck());
    }

    public LoadingSpinner()
    {
        // The animation turns this transform's angle; every spinner needs one of its own.
        RenderTransform = new RotateTransform();
    }

    protected override Type StyleKeyOverride => typeof(Path);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attached.Add(this);
        PostCheck();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Attached.Remove(this);
        Classes.Set("turning", false);
    }

    private static void PostCheck()
    {
        if (_checkPosted)
        {
            return;
        }

        _checkPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _checkPosted = false;
            foreach (var spinner in Attached)
            {
                spinner.Classes.Set("turning", spinner.IsEffectivelyVisible && TopLevel.GetTopLevel(spinner) is WindowBase { IsActive: true });
            }
        }, DispatcherPriority.Background);
    }
}
