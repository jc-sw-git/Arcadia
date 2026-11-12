using System.Globalization;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Steam's images of its games (#13, #487), best first: the account's own images in <c>userdata/&lt;id&gt;/config/grid</c>,
/// then Steam's cache in <c>appcache/librarycache/&lt;appid&gt;/</c> (directly or in a hash-named subfolder), then Steam's
/// image server by AppID, which needs no key. The cache holds only images Steam has shown (docs/launchers/steam/artwork.md).
/// </summary>
public sealed class SteamArtwork(Func<SteamInstallation?> locate) : ILauncherArtwork
{
    /// <summary>The extensions Steam accepts for an own image.</summary>
    private static readonly string[] GridExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    // Two hosts of the same files: the newer one first, the older one as a second try.
    private static readonly string[] ImageServers =
    [
        "https://shared.steamstatic.com/store_item_assets/steam/apps/",
        "https://cdn.cloudflare.steamstatic.com/steam/apps/",
    ];

    public IArtworkLookup Look()
    {
        var steam = locate();
        var grid = steam?.UserDataDirectory is { } user ? Path.Combine(user, "config", "grid") : null;
        var cache = steam is null ? null : Path.Combine(steam.RootPath, "appcache", "librarycache");
        return new Lookup(GridFiles(grid), cache);
    }

    /// <summary>The own images by lower-case file name; none if the folder is missing or can't be read now.</summary>
    private static Dictionary<string, string> GridFiles(string? grid)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (grid is null || !Directory.Exists(grid))
        {
            return files;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(grid))
            {
                files[Path.GetFileName(file)] = file;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Steam grid unreadable: {e.GetType().Name}: {e.Message}");
        }

        return files;
    }

    /// <summary>A Steam AppID is a plain number; anything else (a manipulated manifest) names no file and no address.</summary>
    internal static string? AppIdOf(Game game) =>
        game.LauncherId == SteamIntegration.Info.Id
        && game.Id.Length is > 0 and <= 10
        && game.Id.All(char.IsAsciiDigit)
        && uint.TryParse(game.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var appId)
            ? appId.ToString(CultureInfo.InvariantCulture)
            : null;

    private sealed class Lookup(Dictionary<string, string> grid, string? cache) : IArtworkLookup
    {
        public ArtworkStoreId? StoreIdOf(Game game) => AppIdOf(game) is { } appId ? new ArtworkStoreId(ArtworkStoreId.Steam, appId) : null;

        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind)
        {
            if (AppIdOf(game) is not { } appId)
            {
                System.Diagnostics.Trace.TraceWarning($"Steam: {game.Key} hat keine gültige AppID, keine Bilder");
                return [];
            }

            var candidates = new List<ArtworkCandidate>();
            var ownName = kind == ArtworkKind.Cover ? appId + "p" : appId + "_hero";
            foreach (var extension in GridExtensions)
            {
                if (grid.TryGetValue(ownName + extension, out var own))
                {
                    candidates.Add(new ArtworkCandidate("steam-grid", File: own));
                    break;
                }
            }

            var fileName = kind == ArtworkKind.Cover ? "library_600x900.jpg" : "library_hero.jpg";
            if (cache is not null && CachedFile(Path.Combine(cache, appId), fileName) is { } cached)
            {
                candidates.Add(new ArtworkCandidate("steam-cache", File: cached));
            }

            foreach (var server in ImageServers)
            {
                candidates.Add(new ArtworkCandidate("steam-server", Download: new Uri($"{server}{appId}/{fileName}")));
            }

            return candidates;
        }

        /// <summary>The file in the game's cache folder, else in one of its subfolders; null if neither has it.</summary>
        private static string? CachedFile(string folder, string fileName)
        {
            var direct = Path.Combine(folder, fileName);
            if (File.Exists(direct))
            {
                return direct;
            }

            try
            {
                return Directory.Exists(folder)
                    ? Directory.EnumerateDirectories(folder).Select(sub => Path.Combine(sub, fileName)).FirstOrDefault(File.Exists)
                    : null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning($"Steam library cache unreadable: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }
    }
}
