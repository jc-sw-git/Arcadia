using Avalonia;
using Avalonia.Controls;

namespace Arcadia.App.Controls;

/// <summary>
/// Shows <see cref="Reveal"/> (0 to 1) of its child's height, the child's top edge first, so a
/// transition on <see cref="Reveal"/> slides the child in from the bottom when the panel sits at the
/// bottom of the window. Used for the search's on-screen keyboard. While partly shown it clips the
/// child; fully shown it doesn't, so a menu may stick out above it.
/// </summary>
public sealed class RevealPanel : Decorator
{
    public static readonly StyledProperty<double> RevealProperty =
        AvaloniaProperty.Register<RevealPanel, double>(nameof(Reveal), coerce: (_, value) => Math.Clamp(value, 0, 1));

    static RevealPanel()
    {
        AffectsMeasure<RevealPanel>(RevealProperty);
    }

    public RevealPanel() => ClipToBounds = true;

    public double Reveal
    {
        get => GetValue(RevealProperty);
        set => SetValue(RevealProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RevealProperty)
        {
            ClipToBounds = Reveal < 1;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }

        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        return new Size(child.DesiredSize.Width, child.DesiredSize.Height * Reveal);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }
}
