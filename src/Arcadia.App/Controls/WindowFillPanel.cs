using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Arcadia.App.Controls;

/// <summary>
/// A panel that reaches past its parent to the window's edges (#612): over the bars a window of another shape leaves
/// beside or above and below the scaled 1920 × 1080 layout. For what has to fill the whole window, such as the detail
/// page's background and "Bild ansehen". Its layout slot must be its parent's whole area.
/// </summary>
public sealed class WindowFillPanel : Panel
{
    public WindowFillPanel()
    {
        // A resize scales the layout without arranging it anew, so the margin follows every layout pass; it changes only
        // when the window's area moved.
        LayoutUpdated += (_, _) => FillWindow();
    }

    // Styled as a panel: "Bild ansehen"'s styles select Panel.imageShow.
    protected override Type StyleKeyOverride => typeof(Panel);

    private void FillWindow()
    {
        if (this.GetVisualParent() is not Visual parent || TopLevel.GetTopLevel(this) is not { } window
            || parent.TransformToVisual(window) is not { } toWindow || WindowArea.Of(toWindow, window.Bounds.Size) is not { } area)
        {
            return;
        }

        var margin = new Thickness(area.Left, area.Top, parent.Bounds.Width - area.Right, parent.Bounds.Height - area.Bottom);
        if (Margin != margin)
        {
            Margin = margin;
        }
    }
}
