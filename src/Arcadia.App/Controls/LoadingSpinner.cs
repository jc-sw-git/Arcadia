using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Path = Avalonia.Controls.Shapes.Path;

namespace Arcadia.App.Controls;

/// <summary>
/// The loading spinner (<c>Path.spinner</c> in the theme): turns only while it can be seen (#636). A style animation of a
/// spinner inside a hidden panel kept turning in Avalonia, and its clock kept the window drawing every frame: CPU and
/// garbage at idle. The class <c>turning</c>, which starts the animation, follows <see cref="Visual.IsEffectivelyVisible"/>;
/// Avalonia raises no public event for it, so any change of a visual's <see cref="Visual.IsVisible"/> checks the spinners
/// in the window, once per dispatcher round.
/// </summary>
public sealed class LoadingSpinner : Path
{
    private static readonly HashSet<LoadingSpinner> Attached = [];
    private static bool _checkPosted;

    static LoadingSpinner()
    {
        IsVisibleProperty.Changed.AddClassHandler<Visual>((_, _) => PostCheck());
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
                spinner.Classes.Set("turning", spinner.IsEffectivelyVisible);
            }
        }, DispatcherPriority.Background);
    }
}
