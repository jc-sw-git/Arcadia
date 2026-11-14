using Arcadia.Core.Library;
using Arcadia.Core.Metadata;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Settings;

/// <summary>
/// A tab's filter as <c>settings.json</c> holds it (#44, #466). By name, not by number: a value this version doesn't know
/// (a newer Arcadia, a hand-edited file) drops out on its own instead of breaking the file or meaning something else.
/// </summary>
/// <param name="Installed">An <see cref="InstalledChoice"/>; null for "Alle".</param>
/// <param name="Types">The chosen <see cref="GameType"/>s; null or empty for "Alle Typen".</param>
/// <param name="Genres">The chosen genres by their <see cref="Genre"/> key, never by a shown name (#557).</param>
/// <param name="Tags">The providers' tags by name, the own tags in <see cref="SavedNames.Own"/>.</param>
public sealed record SavedFilter(
    string? Installed = null,
    IReadOnlyList<string>? Types = null,
    SavedNames? Genres = null,
    SavedNames? Tags = null,
    SavedNames? Developers = null,
    SavedNames? Publishers = null,
    SavedYears? Years = null,
    SavedRating? AgeRating = null)
{
    public GameFilter ToFilter()
    {
        var filter = GameFilter.Of(
                Parse<InstalledChoice>(Installed) ?? InstalledChoice.All,
                (Types ?? []).Select(Parse<GameType>).Where(type => type is not null).Select(type => type!.Value))
            .WithNames(FilterList.Genre, Genres?.ToChoice(name => Parse<Genre>(name) is not null, own: false) ?? NameChoice.None)
            .WithNames(FilterList.Tag, Tags?.ToChoice(IsName, own: true) ?? NameChoice.None)
            .WithNames(FilterList.Developer, Developers?.ToChoice(IsName, own: false) ?? NameChoice.None)
            .WithNames(FilterList.Publisher, Publishers?.ToChoice(IsName, own: false) ?? NameChoice.None)
            .WithYears(Years?.ToChoice());
        return AgeRating?.ToChoice() is { IsActive: true } rating ? filter.WithRating(rating) : filter;
    }

    /// <summary>Null for no filter: the tab's entry can go.</summary>
    public static SavedFilter? From(GameFilter filter) => filter.IsActive
        ? new SavedFilter(
            filter.Installed == InstalledChoice.All ? null : filter.Installed.ToString(),
            filter.Types.Count == 0 ? null : filter.Types.Select(type => type.ToString()).ToList(),
            SavedNames.From(filter.NamesOf(FilterList.Genre)),
            SavedNames.From(filter.NamesOf(FilterList.Tag)),
            SavedNames.From(filter.NamesOf(FilterList.Developer)),
            SavedNames.From(filter.NamesOf(FilterList.Publisher)),
            filter.Years is { } years ? new SavedYears(years.From, years.To, years.WithUnknown) : null,
            filter.Rating is { IsActive: true } rating ? new SavedRating(rating.Board.ToString(), rating.Ratings, rating.WithUnknown) : null)
        : null;

    // Only a member's own name: Enum.TryParse would also take "7" or "Game, Tool".
    internal static T? Parse<T>(string? name)
        where T : struct, Enum =>
        name is not null && Enum.TryParse<T>(name, ignoreCase: false, out var value) && Enum.IsDefined(value) && value.ToString() == name ? value : null;

    // As long as a provider's name may be (MetadataCheck), one line.
    private static bool IsName(string? name) =>
        name is { Length: > 0 and <= MetadataCheck.MaxName } && !name.Any(char.IsControl) && name.Trim().Length > 0;
}

/// <summary>The chosen names of a long list (#466); <paramref name="Own"/> only for tags.</summary>
public sealed record SavedNames(IReadOnlyList<string>? Names = null, IReadOnlyList<string>? Own = null, bool WithUnknown = false)
{
    internal NameChoice ToChoice(Func<string?, bool> isName, bool own) => NameChoice.Of(
        (Names ?? []).Where(isName),
        own ? (Own ?? []).Where(name => isName(name) && name.Length <= Arcadia.Core.UserData.UserDataStore.MaxTagLength) : [],
        WithUnknown);

    internal static SavedNames? From(NameChoice choice) => choice.IsActive
        ? new SavedNames(choice.Names.Count > 0 ? choice.Names : null, choice.Own.Count > 0 ? choice.Own : null, choice.WithUnknown)
        : null;
}

/// <summary>"Erscheinungsjahr" (#466): a range in the years a release date may have.</summary>
public sealed record SavedYears(int From, int To, bool WithUnknown = false)
{
    private const int Latest = 2100;

    internal YearChoice? ToChoice() =>
        From is >= YearPicks.Earliest and <= Latest && To is >= YearPicks.Earliest and <= Latest && From <= To ? new YearChoice(From, To, WithUnknown) : null;
}

/// <summary>"Altersfreigabe" (#466): a board by name and its ratings as the board writes them.</summary>
public sealed record SavedRating(string? Board = null, IReadOnlyList<string>? Ratings = null, bool WithUnknown = false)
{
    internal RatingChoice? ToChoice() => SavedFilter.Parse<AgeRatingBoard>(Board) is { } board
        ? RatingChoice.Of(board, (Ratings ?? []).Where(rating => rating is not null), WithUnknown)
        : null;
}
