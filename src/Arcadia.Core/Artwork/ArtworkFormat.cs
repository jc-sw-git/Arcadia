using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork;

/// <summary>
/// The fitting-format rule (#13): an image fits its frame when cropping it centred to the frame's shape cuts away at most the
/// tolerance of its area. Cover 2 : 3, background (hero) about 3.1 : 1, as the detail page header (#24).
/// </summary>
public static class ArtworkFormat
{
    /// <summary>"Normal" of "Zuschneiden erlaubt": until #489 adds the setting, the only tolerance.</summary>
    public const double NormalTolerance = 0.15;

    public static double AspectOf(ArtworkKind kind) => kind == ArtworkKind.Cover ? 2.0 / 3.0 : 3.1;

    /// <summary>The share of the image a centred crop to <paramref name="kind"/>'s shape cuts away, 0 to below 1.</summary>
    public static double CutAway(ImageSize size, ArtworkKind kind)
    {
        var image = (double)size.Width / size.Height;
        var frame = AspectOf(kind);
        return image > frame ? 1 - (frame / image) : 1 - (image / frame);
    }

    /// <summary>Whether the crop cuts away no more than <paramref name="tolerance"/>; a hair of rounding counts as within.</summary>
    public static bool Fits(ImageSize size, ArtworkKind kind, double tolerance = NormalTolerance) =>
        CutAway(size, kind) <= tolerance + 1e-9;
}
