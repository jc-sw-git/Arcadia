using Avalonia;

namespace Arcadia.App.Controls;

/// <summary>How an <see cref="ArtworkImage"/> fits its image into its box (#565).</summary>
public enum ArtworkFit
{
    /// <summary>By the image's kind: a cover <see cref="Fill"/>s its box, a background shows <see cref="AlmostWhole"/>.</summary>
    Auto,

    /// <summary>Scaled to cover the box and cropped centred: a cover's 2 : 3 tile.</summary>
    Fill,

    /// <summary>
    /// Scaled to cover the box with at most <see cref="ArtworkPlacement.MaxCut"/> of the image cut, centred; the rest of the
    /// box shows the blurred copy. Every frame showing a background.
    /// </summary>
    AlmostWhole,

    /// <summary>
    /// The detail page: an image as wide as 16 : 9 or wider runs edge to edge, top aligned, uncut, and fades at its lower
    /// edge into the blurred copy below; a taller one is shown <see cref="AlmostWhole"/>.
    /// </summary>
    DetailPage,

    /// <summary>"Bild ansehen": the whole image, centred, its edges towards the blurred margins fading softly.</summary>
    Whole,
}

/// <summary>Edges of the sharp image that fade into the blurred copy around it.</summary>
[Flags]
public enum FadingEdges
{
    None = 0,
    Bottom = 1,
    TopAndBottom = 2,
    LeftAndRight = 4,
}

/// <summary>
/// Where an image is drawn in its box: the part of the image (<see cref="Source"/>, in the image's pixels) and where it lands
/// (<see cref="Target"/>, in the box). Where the target leaves the box uncovered, the blurred copy shows.
/// </summary>
public readonly record struct ArtworkPlacement(Rect Source, Rect Target, FadingEdges Fading)
{
    /// <summary>At most this share of a background is cut away (#565): the owner accepts about 10 %.</summary>
    public const double MaxCut = 0.10;

    // Below half a pixel a margin isn't a margin.
    private const double Tolerance = 0.5;

    /// <summary>The target leaves part of the box uncovered, so the blurred copy is drawn beneath it.</summary>
    public bool HasMargins(Size box) => Target.Width < box.Width - Tolerance || Target.Height < box.Height - Tolerance;

    /// <summary>Where <paramref name="image"/> lands in <paramref name="box"/> with <paramref name="fit"/> (<see cref="ArtworkFit.Auto"/> counts as <see cref="ArtworkFit.Fill"/>).</summary>
    public static ArtworkPlacement Of(Size image, Size box, ArtworkFit fit) => fit switch
    {
        ArtworkFit.AlmostWhole => Centred(image, box, MaxScale(image, box, MaxCut), FadingEdges.None),
        ArtworkFit.DetailPage => DetailPage(image, box),
        ArtworkFit.Whole => Whole(image, box),
        _ => Centred(image, box, CoverScale(image, box), FadingEdges.None),
    };

    private static ArtworkPlacement DetailPage(Size image, Size box)
    {
        if (image.Width / image.Height < box.Width / box.Height - 0.01)
        {
            return Centred(image, box, MaxScale(image, box, MaxCut), FadingEdges.None);
        }

        var scale = box.Width / image.Width;
        var height = image.Height * scale;
        var fading = height < box.Height - Tolerance ? FadingEdges.Bottom : FadingEdges.None;
        return new ArtworkPlacement(new Rect(image), new Rect(0, 0, box.Width, height), fading);
    }

    private static ArtworkPlacement Whole(Size image, Size box)
    {
        var placement = Centred(image, box, ContainScale(image, box), FadingEdges.None);
        var fading = placement.Target.Height < box.Height - Tolerance ? FadingEdges.TopAndBottom
            : placement.Target.Width < box.Width - Tolerance ? FadingEdges.LeftAndRight
            : FadingEdges.None;
        return placement with { Fading = fading };
    }

    /// <summary>The image scaled by <paramref name="scale"/>, centred, cropped to the box where it is larger.</summary>
    private static ArtworkPlacement Centred(Size image, Size box, double scale, FadingEdges fading)
    {
        var width = Math.Min(box.Width, image.Width * scale);
        var height = Math.Min(box.Height, image.Height * scale);
        var target = new Rect((box.Width - width) / 2, (box.Height - height) / 2, width, height);
        var sourceWidth = width / scale;
        var sourceHeight = height / scale;
        var source = new Rect((image.Width - sourceWidth) / 2, (image.Height - sourceHeight) / 2, sourceWidth, sourceHeight);
        return new ArtworkPlacement(source, target, fading);
    }

    private static double CoverScale(Size image, Size box) => Math.Max(box.Width / image.Width, box.Height / image.Height);

    private static double ContainScale(Size image, Size box) => Math.Min(box.Width / image.Width, box.Height / image.Height);

    /// <summary>The largest scale that still keeps <c>1 - cut</c> of the image along the axis it overflows, never more than covering the box.</summary>
    private static double MaxScale(Size image, Size box, double cut) => Math.Min(CoverScale(image, box), ContainScale(image, box) / (1 - cut));
}
