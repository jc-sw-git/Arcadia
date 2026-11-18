using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// The names of Steam's store tags (#493, #557): <c>appinfo.vdf</c> holds only their ids. Arcadia keeps Steam's public tag
/// list in each UI language (<c>store.steampowered.com/tagdata/populartags/&lt;language&gt;</c>, saved into
/// <c>steam-tags-&lt;language&gt;.json</c>) rather than asking for it, so naming tags needs no request; a tag Steam added
/// later has no name yet and is dropped until the lists are saved again.
/// </summary>
internal static class SteamTags
{
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Lists = new(StringComparer.Ordinal);

    /// <summary>The names of the tags <paramref name="ids"/> in <paramref name="language"/>, in Steam's order; unknown ids dropped.</summary>
    public static IReadOnlyList<string> NamesOf(IEnumerable<string> ids, CultureInfo language)
    {
        var names = Lists.GetOrAdd(SteamLanguage.NameOf(language), Load);
        return [.. ids.Select(id => names.GetValueOrDefault(id)).OfType<string>().Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The resource of a language's list, as the project embeds it.</summary>
    internal static string ResourceOf(string steamLanguage) => $"Arcadia.Launchers.Steam.steam-tags-{steamLanguage}.json";

    private static Dictionary<string, string> Load(string steamLanguage)
    {
        var resource = ResourceOf(steamLanguage);
        using var stream = typeof(SteamTags).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"{resource} is missing from the assembly."); // not a UI text (a build error)
        using var document = JsonDocument.Parse(stream);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in document.RootElement.EnumerateArray())
        {
            if (tag.TryGetProperty("tagid", out var id) && id.TryGetInt32(out var number)
                && tag.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 } text)
            {
                names[number.ToString(CultureInfo.InvariantCulture)] = text;
            }
        }

        return names;
    }
}
