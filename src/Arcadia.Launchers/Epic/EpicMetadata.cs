using System.Text.Json;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Epic;

/// <summary>
/// Epic's catalog cache as a metadata provider (#493): only the developer. Its "description" is often just the title
/// or German with every umlaut a <c>?</c>, and it has no release date (only <c>dateAdded</c>), so neither is used
/// (docs/launchers/epic.md). A base game wins over a DLC or edition that names the same app.
/// </summary>
/// <param name="catalogPath">The catalog cache; null: where Epic keeps it on this PC.</param>
public sealed class EpicMetadata(string? catalogPath = null) : ILauncherMetadata
{
    private const int MaxDeveloper = 200;

    private readonly string _catalogPath = catalogPath ?? EpicCatalog.DefaultPath();

    public IReadOnlySet<MetadataField> Fields { get; } = new HashSet<MetadataField> { MetadataField.Developers };

    public IMetadataLookup Look(IReadOnlyCollection<Game> games)
    {
        var byApp = new Dictionary<string, (string Developer, bool OfBaseGame)>(StringComparer.Ordinal);
        var byItem = new Dictionary<string, (string Developer, bool OfBaseGame)>(StringComparer.Ordinal);
        if (games.Count > 0 && Read(_catalogPath) is { } content && EpicCatalog.Decode(content) is { } document)
        {
            using (document)
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    Index(item, byApp, byItem);
                }
            }
        }

        return new Lookup(byApp, byItem);
    }

    private static string? Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning($"Epic-Metadaten: Katalog nicht lesbar: {e.GetType().Name}: {e.Message}");
            return null;
        }
    }

    private static void Index(JsonElement item, Dictionary<string, (string, bool)> byApp, Dictionary<string, (string, bool)> byItem)
    {
        if (item.ValueKind != JsonValueKind.Object
            || EpicCatalog.GetString(item, "developer")?.Trim() is not { Length: > 0 and <= MaxDeveloper } developer)
        {
            return;
        }

        var isBase = !(item.TryGetProperty("mainGameItem", out var main)
            && main.ValueKind == JsonValueKind.Object
            && !string.IsNullOrEmpty(EpicCatalog.GetString(main, "id")));
        if (EpicCatalog.GetString(item, "id") is { Length: > 0 } id)
        {
            Add(byItem, id, developer, isBase);
        }

        if (item.TryGetProperty("releaseInfo", out var releases) && releases.ValueKind == JsonValueKind.Array)
        {
            foreach (var release in releases.EnumerateArray())
            {
                if (release.ValueKind == JsonValueKind.Object && EpicCatalog.GetString(release, "appId") is { Length: > 0 } app)
                {
                    Add(byApp, app, developer, isBase);
                }
            }
        }
    }

    private static void Add(Dictionary<string, (string Developer, bool OfBaseGame)> index, string key, string developer, bool isBase)
    {
        if (!index.TryGetValue(key, out var held) || (isBase && !held.OfBaseGame))
        {
            index[key] = (developer, isBase);
        }
    }

    private sealed class Lookup(
        Dictionary<string, (string Developer, bool OfBaseGame)> byApp, Dictionary<string, (string Developer, bool OfBaseGame)> byItem) : IMetadataLookup
    {
        public GameMetadata? Of(Game game)
        {
            if (game.LauncherId != EpicIntegration.Info.Id)
            {
                return null;
            }

            // As for the images: an installed game's manifest names the app of its Windows release, its launch address the item.
            var found = byApp.TryGetValue(game.Id, out var byAppName)
                ? byAppName
                : game.Launch is LaunchTarget.ProtocolUri launch && EpicScanner.CatalogItemIdOf(launch.Uri) is { } item && byItem.TryGetValue(item, out var byItemId)
                    ? byItemId
                    : default;
            return found.Developer is { } developer ? new GameMetadata { Developers = [developer] } : null;
        }
    }
}
