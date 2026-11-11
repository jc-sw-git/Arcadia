using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork;

/// <summary>An image in the cache, ready to be shown.</summary>
/// <param name="Version">Changes whenever the file is written again in this session, so a view loads it anew.</param>
public sealed record ArtworkFile(string GameKey, ArtworkKind Kind, string Path, ImageSize Size, long Version);

/// <summary>The cached images of one game; null where it has none (the view shows its placeholder).</summary>
public sealed record GameArtwork(ArtworkFile? Cover, ArtworkFile? Hero)
{
    public ArtworkFile? Of(ArtworkKind kind) => kind == ArtworkKind.Cover ? Cover : Hero;
}

/// <summary>A launcher's library and the launcher's own images (null if it has none), for <see cref="ArtworkService.Update"/>.</summary>
public sealed record LibraryArtwork(Library.LauncherLibrary Library, ILauncherArtwork? Source);
