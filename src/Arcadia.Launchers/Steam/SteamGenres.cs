using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Steam's genres onto Arcadia's genre list (#493). <c>appinfo.vdf</c> and the store page name a genre by the same id
/// (the store adds its name in the asked language), so the mapping goes by id; genres without a place in the list
/// (free to play, early access, the software genres) are dropped.
/// </summary>
internal static class SteamGenres
{
    private static readonly Dictionary<string, Genre> ById = new(StringComparer.Ordinal)
    {
        ["1"] = Genre.Action,
        ["2"] = Genre.Strategy,
        ["3"] = Genre.RolePlaying,
        ["4"] = Genre.Casual,
        ["9"] = Genre.Racing,
        ["18"] = Genre.Sports,
        ["23"] = Genre.Indie,
        ["25"] = Genre.Adventure,
        ["28"] = Genre.Simulation,
        ["29"] = Genre.MassivelyMultiplayer,
    };

    /// <summary>The genres of <paramref name="steamIds"/> in their order, each once.</summary>
    public static IReadOnlyList<Genre> Of(IEnumerable<string> steamIds) =>
        [.. steamIds.Select(id => ById.TryGetValue(id, out var genre) ? genre : (Genre?)null).OfType<Genre>().Distinct()];
}
