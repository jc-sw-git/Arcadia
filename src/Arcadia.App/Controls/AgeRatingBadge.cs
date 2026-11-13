using Arcadia.App.ViewModels;
using Avalonia;
using Avalonia.Controls;

namespace Arcadia.App.Controls;

/// <summary>
/// The border of an age rating (#541) in its board's colour: <see cref="Tone"/> picks the brush <c>AgeRating{Tone}</c>
/// from <c>Theme.axaml</c> and follows it, so a theme replaces the colours in one place. Styled as <c>Border.ageBadge</c>.
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

    private IDisposable? _toneBrush;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToneProperty)
        {
            _toneBrush?.Dispose();
            _toneBrush = Bind(BorderBrushProperty, this.GetResourceObservable($"AgeRating{Tone}"));
        }
    }

    // The default tone never raises a change, so the first binding is made here.
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _toneBrush ??= Bind(BorderBrushProperty, this.GetResourceObservable($"AgeRating{Tone}"));
    }
}
