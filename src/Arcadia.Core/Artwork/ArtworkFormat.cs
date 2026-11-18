using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork;

/// <summary>
/// The fitting-format rule (#13, #562): an image fits its frame when cropping it centred to the frame's shape cuts away at
/// most <see cref="Tolerance"/> of its area (#566). Cover 2 : 3: Steam's and SteamGridDB's covers fit exactly, a 3 : 4 one
/// (about 11 % cut away) still fits. Background (hero) 16 : 9, as the full-screen detail page shows it, or flat (about
/// 3.1 : 1, Steam's and SteamGridDB's format): both fit, so the order of the sources decides between them, and of one
/// source's images whose sizes are known before a download (SteamGridDB's) a 16 : 9 one is taken first.
/// </summary>
public static class ArtworkFormat
{
    /// <summary>The share of an image a centred crop may cut away for it to fit, for covers and backgrounds alike.</summary>
    public const double Tolerance = 0.15;

    /// <summary>The flat background that fits besides a 16 : 9 one.</summary>
    public const double FlatHeroAspect = 3.1;

    /// <summary>The frame's shape: for a background the 16 : 9 of the detail page, which a flat one is cropped to.</summary>
    public static double AspectOf(ArtworkKind kind) => kind == ArtworkKind.Cover ? 2.0 / 3.0 : 16.0 / 9.0;

    /// <summary>The share of the image a centred crop to <paramref name="kind"/>'s shape cuts away, 0 to below 1.</summary>
    public static double CutAway(ImageSize size, ArtworkKind kind) => CutAway(size, AspectOf(kind));

    /// <summary>
    /// Whether the image fits: a cover in 2 : 3, a background in 16 : 9 or flat, each within <see cref="Tolerance"/>; a hair
    /// of rounding counts as within.
    /// </summary>
    public static bool Fits(ImageSize size, ArtworkKind kind) => RankOf(size, kind) < 2;

    /// <summary>How well the image suits its frame, lower is better: 0 its own shape, 1 a flat background, 2 doesn't fit.</summary>
    public static int RankOf(ImageSize size, ArtworkKind kind) =>
        Within(CutAway(size, kind)) ? 0
        : kind == ArtworkKind.Hero && Within(CutAway(size, FlatHeroAspect)) ? 1
        : 2;

    private static double CutAway(ImageSize size, double frame)
    {
        var image = (double)size.Width / size.Height;
        return image > frame ? 1 - (frame / image) : 1 - (image / frame);
    }

    private static bool Within(double cutAway) => cutAway <= Tolerance + 1e-9;
}
