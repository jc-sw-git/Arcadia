using Avalonia;

namespace Arcadia.App.Controls;

/// <summary>
/// The window's whole client area in a control's own coordinates. The 1920 × 1080 layout is scaled into the window
/// (MainWindow's Viewbox), so a window of another shape shows bars beside or above and below it; what has to fill the
/// whole window, such as the detail page's background (#612), is placed on this area instead of the layout's.
/// </summary>
public static class WindowArea
{
    /// <summary>
    /// The window of size <paramref name="window"/> in the coordinates of a control placed into it by
    /// <paramref name="controlToWindow"/>; null when that transform can't be inverted (a control scaled to nothing).
    /// </summary>
    public static Rect? Of(Matrix controlToWindow, Size window) =>
        controlToWindow.TryInvert(out var windowToControl) ? new Rect(window).TransformToAABB(windowToControl) : null;
}
