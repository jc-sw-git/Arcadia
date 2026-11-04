using Avalonia;
using Avalonia.Controls;

namespace Arcadia.App.Controls;

/// <summary>
/// Shows the first of its children that fits the width it gets, else the last: a line that is never wrapped or cut, but
/// says less when it has to, e.g. a filter category's chosen values or else "Mehrere" (#44). Measured, not counted in
/// characters, so it holds at every scaling and font.
/// </summary>
public sealed class FirstThatFits : Panel
{
    private Control? _shown;

    // The others are laid out beyond the right edge, where this clips what they would draw.
    public FirstThatFits() => ClipToBounds = true;

    protected override Size MeasureOverride(Size availableSize)
    {
        _shown = null;
        var children = Children.Where(child => child.IsVisible).ToList();
        foreach (var child in children)
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            if (_shown is null && child.DesiredSize.Width <= availableSize.Width)
            {
                _shown = child;
            }
        }

        _shown ??= children.LastOrDefault();
        return _shown is null ? default : new Size(Math.Min(_shown.DesiredSize.Width, availableSize.Width), _shown.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            child.Arrange(child == _shown ? new Rect(finalSize) : new Rect(finalSize.Width, 0, 0, finalSize.Height));
        }

        return finalSize;
    }
}
