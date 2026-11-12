using System.Text.Json;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// The names of Steam's store tags (#493, A3): <c>appinfo.vdf</c> holds only their ids. Arcadia keeps Steam's public tag
/// list in German (<c>store.steampowered.com/tagdata/populartags/german</c>, saved into <c>steam-tags-german.json</c>)
/// rather than asking for it, so naming tags needs no request; a tag Steam added later has no name yet and is dropped
/// until the list is saved again.
/// </summary>
internal static class SteamTags
{
    private const string Resource = "Arcadia.Launchers.Steam.steam-tags-german.json";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Names = new(Load);

    /// <summary>The names of the tags <paramref name="ids"/>, in Steam's order; unknown ids dropped.</summary>
    public static IReadOnlyList<string> NamesOf(IEnumerable<string> ids) =>
        [.. ids.Select(id => Names.Value.GetValueOrDefault(id)).OfType<string>().Distinct(StringComparer.Ordinal)];

    private static Dictionary<string, string> Load()
    {
        using var stream = typeof(SteamTags).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"{Resource} is missing from the assembly."); // not a UI text (a build error)
        using var document = JsonDocument.Parse(stream);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in document.RootElement.EnumerateArray())
        {
            if (tag.TryGetProperty("tagid", out var id) && id.TryGetInt32(out var number)
                && tag.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 } text)
            {
                names[number.ToString(System.Globalization.CultureInfo.InvariantCulture)] = text;
            }
        }

        return names;
    }
}
