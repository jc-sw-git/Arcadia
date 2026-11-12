namespace Arcadia.Launchers.Abstractions;

/// <summary>The two images a game shows (#13): the cover (2 : 3) and the background above the detail page (about 3.1 : 1).</summary>
public enum ArtworkKind
{
    Cover,
    Hero,
}

/// <summary>
/// One image the launcher offers for a game: a file on this PC or an address to download it from. Arcadia checks either
/// before using it (an image it can decode, at most 20 MB) and copies it into its own cache.
/// </summary>
/// <param name="Origin">Where it comes from, short and stable, e.g. <c>steam-grid</c>; kept in the cache's metadata and logs.</param>
/// <param name="File">The image on this PC; null for one to download.</param>
/// <param name="Download">An <c>https</c> address to download it from, without any account data; null for a file.</param>
public sealed record ArtworkCandidate(string Origin, string? File = null, Uri? Download = null);

/// <summary>
/// A game's id in a store that image collections know it by (#489), e.g. its Steam AppID: SteamGridDB finds the game's
/// images by it, without searching by name.
/// </summary>
/// <param name="Store">The store, one of the constants below.</param>
/// <param name="Id">The game's id in that store, as the store writes it.</param>
public sealed record ArtworkStoreId(string Store, string Id)
{
    /// <summary>Steam: the AppID.</summary>
    public const string Steam = "steam";

    /// <summary>The Epic Games Store.</summary>
    public const string Epic = "egs";
}

/// <summary>The launcher's own images for its games (#13), in the order it prefers them; Arcadia picks by format among them.</summary>
public interface ILauncherArtwork
{
    /// <summary>
    /// Looks at the launcher's image folders once, for a run over many games. May read the disk; call it off the UI thread.
    /// </summary>
    IArtworkLookup Look();
}

/// <summary>The launcher's images as <see cref="ILauncherArtwork.Look"/> found them.</summary>
public interface IArtworkLookup
{
    /// <summary>
    /// The images for one game of this launcher, best first; none if it has none. Files are listed only while they exist.
    /// Never throws for a game it can't name images for (a malformed id): it offers none.
    /// </summary>
    IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind);

    /// <summary>The game's id in its store, for image collections (#489); null if the launcher can't name one.</summary>
    ArtworkStoreId? StoreIdOf(Game game) => null;
}
