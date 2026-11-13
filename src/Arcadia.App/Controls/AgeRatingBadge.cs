using Arcadia.App.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Arcadia.App.Controls;

/// <summary>
/// The border of an age rating (#541) in its board's colour: <see cref="Tone"/> picks the brush <c>AgeRating{Tone}</c>
/// from <c>Theme.axaml</c>, so a theme replaces the colours in one place. Styled as <c>Border.ageBadge</c>.
/// </summary>
public sealed class AgeRatingBadge : Border
{
    public static readonly StyledProperty<AgeRatingTone> ToneProperty =
        AvaloniaProperty.Register<AgeRatingBadge, AgeRatingTone>(nameof(Tone));

    // Styles match Border.ageBadge on this control too, as on the plain Border it replaces.
    protected override Type StyleKeyOverride => typeof(Border);

    public AgeRatingTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyTone();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty)
        {
            ApplyTone();
        }
    }

    private void ApplyTone()
    {
        if (this.TryFindResource($"AgeRating{Tone}", out var found) && found is IBrush brush)
        {
            BorderBrush = brush;
        }
    }
}
