using Arcadia.App.Services;
using Arcadia.Core.Artwork;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Arcadia.App.Controls;

/// <summary>
/// A game's cover or background from the artwork cache (#487) with rounded corners, never stretched, never with bars. A
/// cover fills its box cropped centred; a background shows almost whole (#565, <see cref="ArtworkFit"/>), and where it leaves
/// the box uncovered a blurred, darkened copy of it shows (<see cref="BlurShade"/>). The blurred copy (<see cref="BlurredCopy"/>) is made
/// once per decode off the UI thread and drawn scaled up: no blur is computed while drawing. It loads only once it is drawn, so a tile that isn't visible decodes nothing, and decodes the
/// image off the UI thread at the size it is drawn on the screen. Until the image is there it draws nothing and
/// <see cref="IsShowing"/> is false, so the placeholder below shows; a slot that changes its game drops the old image at once.
/// </summary>
public sealed class ArtworkImage : Control
{
    public static readonly StyledProperty<ArtworkFile?> FileProperty =
        AvaloniaProperty.Register<ArtworkImage, ArtworkFile?>(nameof(File));

    public static readonly StyledProperty<ArtworkLoader?> LoaderProperty =
        AvaloniaProperty.Register<ArtworkImage, ArtworkLoader?>(nameof(Loader));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<ArtworkImage, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<ArtworkFit> FitProperty =
        AvaloniaProperty.Register<ArtworkImage, ArtworkFit>(nameof(Fit));

    /// <summary>Darkens the blurred copy, so the sharp image stands out from it; a theme resource (style in <c>Theme.axaml</c>).</summary>
    public static readonly StyledProperty<IBrush?> BlurShadeProperty =
        AvaloniaProperty.Register<ArtworkImage, IBrush?>(nameof(BlurShade));

    public static readonly DirectProperty<ArtworkImage, bool> IsShowingProperty =
        AvaloniaProperty.RegisterDirect<ArtworkImage, bool>(nameof(IsShowing), image => image.IsShowing);

    // Masks of the sharp image's fading edges (#565): only their alpha counts, so they are no theme colours.
    private static readonly IImmutableBrush FadeBottom = Fade(new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(0, 1, RelativeUnit.Relative), (0, 1), (0.78, 1), (1, 0));
    private static readonly IImmutableBrush FadeTopAndBottom = Fade(new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(0, 1, RelativeUnit.Relative), (0, 0), (0.06, 1), (0.94, 1), (1, 0));
    private static readonly IImmutableBrush FadeLeftAndRight = Fade(new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(1, 0, RelativeUnit.Relative), (0, 0), (0.06, 1), (0.94, 1), (1, 0));

    private Bitmap? _bitmap;
    private Bitmap? _blurred;
    private ArtworkFile? _bitmapFile;
    private int _bitmapWidth;
    private ArtworkFile? _loadingFile;
    private ArtworkFile? _failedFile;
    private int _loadingWidth;
    private bool _isShowing;

    static ArtworkImage()
    {
        AffectsRender<ArtworkImage>(CornerRadiusProperty, FitProperty, BlurShadeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ArtworkImage>(false);
    }

    /// <summary>The image to show; null shows nothing.</summary>
    public ArtworkFile? File
    {
        get => GetValue(FileProperty);
        set => SetValue(FileProperty, value);
    }

    public ArtworkLoader? Loader
    {
        get => GetValue(LoaderProperty);
        set => SetValue(LoaderProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public ArtworkFit Fit
    {
        get => GetValue(FitProperty);
        set => SetValue(FitProperty, value);
    }

    public IBrush? BlurShade
    {
        get => GetValue(BlurShadeProperty);
        set => SetValue(BlurShadeProperty, value);
    }

    /// <summary>The image is drawn; the placeholder's monogram and title hide behind it.</summary>
    public bool IsShowing
    {
        get => _isShowing;
        private set => SetAndRaise(IsShowingProperty, ref _isShowing, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FileProperty || change.Property == LoaderProperty)
        {
            if (_bitmapFile != File)
            {
                // Another game in the same slot (#229) never shows the image of the one before.
                Drop();
            }

            InvalidateVisual();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Drop();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (File is not { } file || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var fit = FitOf(file);
        var (width, height) = ShownPixels(bounds.Size);
        var wanted = DecodeWidth(file.Size, width, height, fit);
        if (_bitmap is null || _bitmapWidth < wanted)
        {
            Load(file, wanted);
        }

        if (_bitmap is not { } bitmap)
        {
            return;
        }

        var placement = ArtworkPlacement.Of(bitmap.Size, bounds.Size, fit);
        using (context.PushClip(new RoundedRect(bounds, CornerRadius)))
        {
            if (placement.HasMargins(bounds.Size) && _blurred is { } blurred)
            {
                context.DrawImage(blurred, ArtworkPlacement.Of(blurred.Size, bounds.Size, ArtworkFit.Fill).Source, bounds);
                if (BlurShade is { } shade)
                {
                    context.FillRectangle(shade, bounds);
                }
            }

            if (FadeOf(placement.Fading) is { } fade)
            {
                using (context.PushOpacityMask(fade, placement.Target))
                {
                    context.DrawImage(bitmap, placement.Source, placement.Target);
                }
            }
            else
            {
                context.DrawImage(bitmap, placement.Source, placement.Target);
            }
        }
    }

    /// <summary>The width to decode at so the image is still sharp at <paramref name="width"/> × <paramref name="height"/> pixels with <paramref name="fit"/>; never more than its own.</summary>
    internal static int DecodeWidth(ImageSize image, double width, double height, ArtworkFit fit = ArtworkFit.Fill)
    {
        var placement = ArtworkPlacement.Of(new Size(image.Width, image.Height), new Size(width, height), fit);
        var scale = placement.Target.Width / placement.Source.Width;
        return (int)Math.Min(image.Width, Math.Ceiling(image.Width * scale));
    }

    private ArtworkFit FitOf(ArtworkFile file) => Fit != ArtworkFit.Auto ? Fit
        : file.Kind == Arcadia.Launchers.Abstractions.ArtworkKind.Hero ? ArtworkFit.AlmostWhole
        : ArtworkFit.Fill;

    private static IImmutableBrush? FadeOf(FadingEdges edges) => edges switch
    {
        FadingEdges.Bottom => FadeBottom,
        FadingEdges.TopAndBottom => FadeTopAndBottom,
        FadingEdges.LeftAndRight => FadeLeftAndRight,
        _ => null,
    };

    private static IImmutableBrush Fade(RelativePoint start, RelativePoint end, params (double Offset, double Alpha)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        foreach (var (offset, alpha) in stops)
        {
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(alpha * 255), 0, 0, 0), offset));
        }

        return brush.ToImmutable();
    }

    /// <summary>The box's size in screen pixels: Big Picture is laid out at 1920 × 1080 and scaled to the window (#24).</summary>
    private (double Width, double Height) ShownPixels(Size box)
    {
        if (TopLevel.GetTopLevel(this) is not { } top || this.TransformToVisual(top) is not { } transform)
        {
            return (box.Width, box.Height);
        }

        var scale = Math.Sqrt(Math.Abs(transform.M11 * transform.M22 - transform.M12 * transform.M21)) * top.RenderScaling;
        return (box.Width * scale, box.Height * scale);
    }

    private void Load(ArtworkFile file, int width)
    {
        // A file that couldn't be decoded isn't tried again; the next scan brings a new one (a new version).
        if (Loader is not { } loader || file == _failedFile || (_loadingFile == file && _loadingWidth >= width))
        {
            return;
        }

        _loadingFile = file;
        _loadingWidth = width;
        DecodeAsync(loader, file, width, blurred: FitOf(file) != ArtworkFit.Fill).Forget(AppTexts.ErrorContextArtwork);
    }

    private async Task DecodeAsync(ArtworkLoader loader, ArtworkFile file, int width, bool blurred)
    {
        (Bitmap? Sharp, Bitmap? Blurred) decoded = default;
        try
        {
            decoded = await Task.Run(async () =>
            {
                var sharp = await loader.DecodeAsync(file, width);
                return (sharp, blurred && sharp is not null ? BlurredCopy.Of(sharp) : null);
            });
        }
        finally
        {
            // Started on the UI thread, so this goes on there; a decode that threw counts as failed.
            Take(file, width, decoded.Sharp, decoded.Blurred);
        }
    }

    private void Take(ArtworkFile file, int width, Bitmap? bitmap, Bitmap? blurred)
    {
        if (_loadingFile == file && _loadingWidth == width)
        {
            _loadingFile = null;
        }

        if (bitmap is null)
        {
            _failedFile = file;
            return;
        }

        if (File != file || TopLevel.GetTopLevel(this) is null)
        {
            bitmap.Dispose();
            blurred?.Dispose();
            return;
        }

        _bitmap?.Dispose();
        _blurred?.Dispose();
        (_bitmap, _blurred, _bitmapFile, _bitmapWidth) = (bitmap, blurred, file, width);
        IsShowing = true;
        InvalidateVisual();
    }

    private void Drop()
    {
        _bitmap?.Dispose();
        _blurred?.Dispose();
        (_bitmap, _blurred, _bitmapFile, _bitmapWidth, _loadingFile) = (null, null, null, 0, null);
        IsShowing = false;
    }
}
