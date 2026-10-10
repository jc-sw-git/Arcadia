using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Arcadia.Launchers.Epic;

/// <summary>A game from the Epic launcher's catalog cache.</summary>
/// <param name="CatalogItemId">Matches <c>CatalogItemId</c> in the install manifests.</param>
/// <param name="AppName">Epic's app name (<c>releaseInfo[].appId</c>); manifests call it <c>AppName</c>.</param>
public sealed record EpicCatalogItem(string Namespace, string CatalogItemId, string AppName, string Title);

/// <summary>
/// Reads the Epic Games Launcher's catalog cache,
/// <c>%PROGRAMDATA%\Epic\EpicGamesLauncher\Data\Catalog\catcache.bin</c>: a Base64-encoded JSON
/// array of catalog items for the signed-in account's library. The launcher refreshes it; it is
/// the only local source for owned games that are not installed.
/// </summary>
public static partial class EpicCatalog
{
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Epic", "EpicGamesLauncher", "Data", "Catalog", "catcache.bin");

    /// <summary>Reads the cache file; a missing or unreadable file yields no items.</summary>
    public static IReadOnlyList<EpicCatalogItem> Load(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Returns the games in the cache. Skips add-ons, DLC and editions of another game (items
    /// whose <c>mainGameItem</c> points elsewhere), non-game items and items without an app.
    /// </summary>
    public static IReadOnlyList<EpicCatalogItem> Parse(string base64)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException)
        {
            return [];
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var items = new List<EpicCatalogItem>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (ParseItem(element) is { } item)
                {
                    items.Add(item);
                }
            }

            return items;
        }
    }

    private static EpicCatalogItem? ParseItem(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var categories = Categories(item);
        if (!categories.Contains("games") || categories.Contains("addons") || categories.Contains("digitaldownloads"))
        {
            return null;
        }

        // Base games carry an empty mainGameItem; DLC, season passes and upgrades name their game.
        if (item.TryGetProperty("mainGameItem", out var main)
            && main.ValueKind == JsonValueKind.Object
            && !string.IsNullOrEmpty(GetString(main, "id")))
        {
            return null;
        }

        var id = GetString(item, "id");
        var ns = GetString(item, "namespace");
        var title = GetString(item, "title");
        var appName = AppNameOf(item);
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(ns) || string.IsNullOrWhiteSpace(title) || appName is null)
        {
            return null;
        }

        return new EpicCatalogItem(ns, id, appName, RepairTitle(title));
    }

    /// <summary>
    /// The cache is ASCII-only: every other character is stored as <c>?</c> ("Batman? Arkham Knight",
    /// "Schnellfahrstrecke K?ln-Aachen"). Trademark signs are the common case; they sit right
    /// after a word and before a space, punctuation or the end, so those question marks are
    /// dropped. "?s" after a letter is an apostrophe. Umlauts inside words can't be recovered —
    /// metadata providers will supply proper titles later.
    /// </summary>
    public static string RepairTitle(string title)
    {
        var repaired = ApostropheS().Replace(title.Trim(), "’s");
        repaired = TrademarkSign().Replace(repaired, string.Empty);
        repaired = TrademarkBeforeNumber().Replace(repaired, " ");
        return MultipleSpaces().Replace(repaired, " ").Trim();
    }

    [GeneratedRegex(@"(?<=\p{L})\?s\b")]
    private static partial Regex ApostropheS();

    [GeneratedRegex(@"(?<=[\p{L}\p{N})])\?+(?=[\s:,.\-]|$)")]
    private static partial Regex TrademarkSign();

    // "Astro Duel?2": the sign sat between the name and the sequel number.
    [GeneratedRegex(@"(?<=\p{L})\?+(?=\p{N})")]
    private static partial Regex TrademarkBeforeNumber();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpaces();

    private static HashSet<string> Categories(JsonElement item)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        if (item.TryGetProperty("categories", out var categories) && categories.ValueKind == JsonValueKind.Array)
        {
            foreach (var category in categories.EnumerateArray())
            {
                if (category.ValueKind == JsonValueKind.Object && GetString(category, "path") is { } path)
                {
                    paths.Add(path);
                }
            }
        }

        return paths;
    }

    /// <summary>The app of the Windows release, else of the first release that has one.</summary>
    private static string? AppNameOf(JsonElement item)
    {
        if (!item.TryGetProperty("releaseInfo", out var releases) || releases.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? first = null;
        foreach (var release in releases.EnumerateArray())
        {
            if (release.ValueKind != JsonValueKind.Object || GetString(release, "appId") is not { Length: > 0 } appId)
            {
                continue;
            }

            if (release.TryGetProperty("platform", out var platforms)
                && platforms.ValueKind == JsonValueKind.Array
                && platforms.EnumerateArray().Any(p => p.ValueKind == JsonValueKind.String && p.GetString() == "Windows"))
            {
                return appId;
            }

            first ??= appId;
        }

        return first;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
