using System.Text.Json;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Epic;

/// <summary>
/// Epic's images of its games (#13, #488): the <c>keyImages</c> of the catalog cache, <c>DieselGameBoxTall</c> (3 : 4) as
/// the cover and <c>DieselGameBox</c> (16 : 9) as the background, downloaded once into Arcadia's cache. The cache file can
/// be written by any local program, so only an <c>https</c> address on Epic's own hosts is offered
/// (docs/launchers/epic.md → Covers and backgrounds).
/// </summary>
/// <param name="catalogPath">The catalog cache; null: where Epic keeps it on this PC.</param>
public sealed class EpicArtwork(string? catalogPath = null) : ILauncherArtwork
{
    /// <summary>Kept in the cache's metadata and the logs.</summary>
    public const string Origin = "epic-catalog";

    internal const string CoverType = "DieselGameBoxTall";

    internal const string HeroType = "DieselGameBox";

    /// <summary>The domains Epic serves its catalog images from; a subdomain of one counts as well.</summary>
    private static readonly string[] ImageDomains = ["epicgames.com", "unrealengine.com"];

    private readonly string _catalogPath = catalogPath ?? EpicCatalog.DefaultPath();

    public IArtworkLookup Look()
    {
        var byApp = new Dictionary<string, Images>(StringComparer.Ordinal);
        var byItem = new Dictionary<string, Images>(StringComparer.Ordinal);
        if (Read(_catalogPath) is not { } content)
        {
            return new Lookup(byApp, byItem);
        }

        if (EpicCatalog.Decode(content) is not { } document)
        {
            System.Diagnostics.Trace.TraceWarning("Epic-Katalogbilder: Katalog ist kein lesbares Base64-JSON, keine Bilder");
            return new Lookup(byApp, byItem);
        }

        using (document)
        {
            foreach (var item in document.RootElement.EnumerateArray())
            {
                Index(item, byApp, byItem);
            }
        }

        return new Lookup(byApp, byItem);
    }

    /// <summary>The cache's text; null if it is missing or can't be read now (the next scan looks again).</summary>
    private static string? Read(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }

            System.Diagnostics.Trace.TraceWarning("Epic-Katalogbilder: kein Katalog, keine Bilder");
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Epic-Katalogbilder: Katalog nicht lesbar: {e.GetType().Name}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Files the item's images under each of its apps and its item id. A base game wins over a DLC or edition that names
    /// the same app, so a game never shows the image of its add-on.
    /// </summary>
    private static void Index(JsonElement item, Dictionary<string, Images> byApp, Dictionary<string, Images> byItem)
    {
        if (item.ValueKind != JsonValueKind.Object || ImagesOf(item) is not { } images)
        {
            return;
        }

        var isBase = !(item.TryGetProperty("mainGameItem", out var main)
            && main.ValueKind == JsonValueKind.Object
            && !string.IsNullOrEmpty(EpicCatalog.GetString(main, "id")));
        if (EpicCatalog.GetString(item, "id") is { Length: > 0 } id)
        {
            Add(byItem, id, images, isBase);
        }

        if (item.TryGetProperty("releaseInfo", out var releases) && releases.ValueKind == JsonValueKind.Array)
        {
            foreach (var release in releases.EnumerateArray())
            {
                if (release.ValueKind == JsonValueKind.Object && EpicCatalog.GetString(release, "appId") is { Length: > 0 } app)
                {
                    Add(byApp, app, images, isBase);
                }
            }
        }
    }

    private static void Add(Dictionary<string, Images> index, string key, Images images, bool isBase)
    {
        if (!index.TryGetValue(key, out var held) || (isBase && !held.OfBaseGame))
        {
            index[key] = images with { OfBaseGame = isBase };
        }
    }

    /// <summary>The first usable address of each kind; null if the item has neither. An unusable one is logged once per item.</summary>
    private static Images? ImagesOf(JsonElement item)
    {
        if (!item.TryGetProperty("keyImages", out var keyImages) || keyImages.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        Uri? cover = null;
        Uri? hero = null;
        var refused = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var image in keyImages.EnumerateArray())
        {
            if (image.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var type = EpicCatalog.GetString(image, "type");
            if ((type == CoverType && cover is null) || (type == HeroType && hero is null))
            {
                var url = EpicCatalog.GetString(image, "url");
                var address = ImageAddress(url);
                if (address is null)
                {
                    refused.Add(Uri.TryCreate(url, UriKind.Absolute, out var given) && given.HostNameType == UriHostNameType.Dns ? given.IdnHost : "no address");
                }

                if (type == CoverType)
                {
                    cover = address;
                }
                else
                {
                    hero = address;
                }
            }
        }

        if (refused.Count > 0)
        {
            var name = EpicCatalog.GetString(item, "id") ?? "?";
            System.Diagnostics.Trace.TraceWarning($"Epic-Katalogbilder: Bild von Katalogeintrag {name} übergangen, keine https-Adresse auf Epics Servern ({string.Join(", ", refused)})");
        }

        return cover is null && hero is null ? null : new Images(cover, hero, OfBaseGame: false);
    }

    /// <summary>
    /// An <c>https</c> address on one of Epic's image domains, on the default port and without user data; null for anything
    /// else, so a manipulated cache can't make Arcadia send requests elsewhere, e.g. into the local network.
    /// </summary>
    internal static Uri? ImageAddress(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var host = address.IdnHost;
        var onEpic = address.HostNameType == UriHostNameType.Dns
            && ImageDomains.Any(domain => host.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
        return onEpic && address.UserInfo.Length == 0 && address.IsDefaultPort ? address : null;
    }

    private sealed record Images(Uri? Cover, Uri? Hero, bool OfBaseGame);

    private sealed class Lookup(Dictionary<string, Images> byApp, Dictionary<string, Images> byItem) : IArtworkLookup
    {
        public IReadOnlyList<ArtworkCandidate> For(Game game, ArtworkKind kind)
        {
            if (game.LauncherId != EpicIntegration.Info.Id)
            {
                return [];
            }

            // An installed game's manifest names the app of its Windows release; its catalog item id stands in the launch
            // address, for an app name the catalog files under another release.
            var images = byApp.GetValueOrDefault(game.Id)
                ?? (game.Launch is LaunchTarget.ProtocolUri launch && EpicScanner.CatalogItemIdOf(launch.Uri) is { } item
                    ? byItem.GetValueOrDefault(item)
                    : null);
            var address = kind == ArtworkKind.Cover ? images?.Cover : images?.Hero;
            return address is null ? [] : [new ArtworkCandidate(Origin, Download: address)];
        }
    }
}
