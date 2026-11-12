using Arcadia.App.Services;
using Arcadia.Core.Artwork;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Arcadia.App.Controls;

/// <summary>
/// A game's cover or background from the artwork cache (#487), cropped centred to fill the box (never stretched, never with
/// bars) with rounded corners. It loads only once it is drawn, so a tile that isn't visible decodes nothing, and decodes the
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

    public static readonly DirectProperty<ArtworkImage, bool> IsShowingProperty =
        AvaloniaProperty.RegisterDirect<ArtworkImage, bool>(nameof(IsShowing), image => image.IsShowing);

    private Bitmap? _bitmap;
    private ArtworkFile? _bitmapFile;
    private int _bitmapWidth;
    private ArtworkFile? _loadingFile;
    private ArtworkFile? _failedFile;
    private int _loadingWidth;
    private bool _isShowing;

    static ArtworkImage()
    {
        AffectsRender<ArtworkImage>(CornerRadiusProperty);
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

        var (width, height) = ShownPixels(bounds.Size);
        var wanted = DecodeWidth(file.Size, width, height);
        if (_bitmap is null || _bitmapWidth < wanted)
        {
            Load(file, wanted);
        }

        if (_bitmap is not { } bitmap)
        {
            return;
        }

        using (context.PushClip(new RoundedRect(bounds, CornerRadius)))
        {
            context.DrawImage(bitmap, CentredCrop(bitmap.Size, bounds.Size), bounds);
        }
    }

    /// <summary>The part of the image that fills the box when it is scaled to cover it, centred.</summary>
    internal static Rect CentredCrop(Size image, Size box)
    {
        var scale = Math.Max(box.Width / image.Width, box.Height / image.Height);
        var width = box.Width / scale;
        var height = box.Height / scale;
        return new Rect((image.Width - width) / 2, (image.Height - height) / 2, width, height);
    }

    /// <summary>The width to decode at so the image still covers <paramref name="width"/> × <paramref name="height"/> pixels; never more than its own.</summary>
    internal static int DecodeWidth(ImageSize image, double width, double height)
    {
        var scale = Math.Max(width / image.Width, height / image.Height);
        return (int)Math.Min(image.Width, Math.Ceiling(image.Width * scale));
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
        DecodeAsync(loader, file, width).Forget(AppTexts.ErrorContextArtwork);
    }

    private async Task DecodeAsync(ArtworkLoader loader, ArtworkFile file, int width)
    {
        Bitmap? bitmap = null;
        try
        {
            bitmap = await Task.Run(() => loader.DecodeAsync(file, width));
        }
        finally
        {
            // Started on the UI thread, so this goes on there; a decode that threw counts as failed.
            Take(file, width, bitmap);
        }
    }

    private void Take(ArtworkFile file, int width, Bitmap? bitmap)
    {
        var dbgWatch = System.Diagnostics.Stopwatch.StartNew(); // [DEBUG-stall]
        try { // [DEBUG-stall]

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
            return;
        }

        _bitmap?.Dispose();
        (_bitmap, _bitmapFile, _bitmapWidth) = (bitmap, file, width);
        IsShowing = true;
        InvalidateVisual();
    
        } finally { if (dbgWatch.Elapsed.TotalMilliseconds > 3) System.IO.File.AppendAllText("/tmp/claude-0/shots/dbg.txt", $"Take {dbgWatch.Elapsed.TotalMilliseconds:0.0}\n"); } // [DEBUG-stall]
    }

    private void Drop()
    {
        _bitmap?.Dispose();
        (_bitmap, _bitmapFile, _bitmapWidth, _loadingFile) = (null, null, 0, null);
        IsShowing = false;
    }
}
