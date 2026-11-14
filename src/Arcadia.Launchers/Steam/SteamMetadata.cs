using System.Globalization;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Steam as a metadata provider (#493): genres, developers, publishers, release date and store tags from the local
/// <c>appcache/appinfo.vdf</c>, at once and offline; description, short description and age ratings from the store page
/// (<see cref="SteamAppDetails"/>), which needs no key and gets only the AppID and the language.
/// </summary>
public sealed class SteamMetadata(Func<SteamInstallation?> locate) : ILauncherMetadata
{
    public string Summary => SteamTexts.SteamMetadataSummary;

    public string About => SteamTexts.SteamMetadataAbout;

    public IReadOnlySet<MetadataField> Fields { get; } = new HashSet<MetadataField>
    {
        MetadataField.Description, MetadataField.ShortDescription, MetadataField.Genres, MetadataField.Developers,
        MetadataField.Publishers, MetadataField.ReleaseDate, MetadataField.Tags, MetadataField.AgeRatings,
    };

    public IMetadataStore? Store { get; } = new SteamAppDetails();

    /// <summary>The AppID, by which IGDB knows Steam's games (#494); Steam's own shortcuts have none.</summary>
    public ArtworkStoreId? StoreIdOf(Game game) =>
        SteamAppDetails.AppIdOf(game) is { } appId ? new ArtworkStoreId(ArtworkStoreId.Steam, appId.ToString(CultureInfo.InvariantCulture)) : null;

    public IMetadataLookup Look(IReadOnlyCollection<Game> games)
    {
        var appIds = games.Select(SteamAppDetails.AppIdOf).OfType<uint>().ToHashSet();
        if (appIds.Count == 0 || locate() is not { } steam)
        {
            return new Lookup(new Dictionary<uint, GameMetadata>());
        }

        var path = Path.Combine(steam.RootPath, "appcache", "appinfo.vdf");
        try
        {
            using var file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return new Lookup(SteamAppInfo.ReadEntries(file, appIds, SteamAppMetadata.Of, skipBroken: true));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            System.Diagnostics.Trace.TraceWarning($"Steam-Metadaten: appinfo.vdf nicht lesbar: {e.GetType().Name}: {e.Message}");
            return new Lookup(new Dictionary<uint, GameMetadata>());
        }
    }

    private sealed class Lookup(IReadOnlyDictionary<uint, GameMetadata> byApp) : IMetadataLookup
    {
        public GameMetadata? Of(Game game) => SteamAppDetails.AppIdOf(game) is { } appId ? byApp.GetValueOrDefault(appId) : null;
    }
}

/// <summary>
/// An app's metadata in <c>appinfo.vdf</c> (#493, checked on the owner's PC in #43): <c>common/genres</c> and
/// <c>primary_genre</c> (Steam's genre ids), <c>store_tags</c> (tag ids), <c>associations</c> (developer and publisher,
/// else <c>extended/developer|publisher</c>) and <c>steam_release_date</c> (else <c>original_release_date</c>, Unix time).
/// No age ratings: they come only from the store page.
/// </summary>
internal static class SteamAppMetadata
{
    private const int MaxNames = 10;

    public static GameMetadata? Of(VdfMap root)
    {
        var info = root.GetMap("appinfo");
        if (info?.GetMap("common") is not { } common)
        {
            return null;
        }

        var extended = info.GetMap("extended");
        var genreIds = Values(common.GetMap("genres"));
        if (Text(common["primary_genre"]) is { } primary)
        {
            genreIds.Insert(0, primary);
        }

        var metadata = new GameMetadata
        {
            Genres = SteamGenres.Of(genreIds),
            Developers = Associated(common, "developer") is { Count: > 0 } developers ? developers : Names(Text(extended?["developer"])),
            Publishers = Associated(common, "publisher") is { Count: > 0 } publishers ? publishers : Names(Text(extended?["publisher"])),
            ReleaseDate = DateOf(common["steam_release_date"]) ?? DateOf(common["original_release_date"]),
            // In the UI language at every scan: tags come from the local files, not from a cached store answer (#557).
            Tags = SteamTags.NamesOf(Values(common.GetMap("store_tags")), CultureInfo.CurrentUICulture),
        };
        return metadata;
    }

    private static List<string> Values(VdfMap? map) =>
        map is null ? [] : [.. map.Select(entry => Text(entry.Value)).OfType<string>()];

    private static List<string> Associated(VdfMap common, string type) =>
    [
        .. (common.GetMap("associations") ?? [])
            .Select(entry => entry.Value as VdfMap)
            .Where(association => Text(association?["type"]) == type)
            .Select(association => Text(association!["name"]))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(MaxNames),
    ];

    private static List<string> Names(string? name) => name is null ? [] : [name];

    /// <summary>A value Steam writes as a string or a number; null for an empty one or a map.</summary>
    private static string? Text(object? value) => value switch
    {
        string text when !string.IsNullOrWhiteSpace(text) => text.Trim(),
        int number => number.ToString(CultureInfo.InvariantCulture),
        _ => null,
    };

    private static DateOnly? DateOf(object? value) =>
        long.TryParse(Text(value), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0 && seconds < 32503680000
            ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime)
            : null;
}
