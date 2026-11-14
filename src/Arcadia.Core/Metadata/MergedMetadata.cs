using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Metadata;

/// <summary>A shown value and the provider it came from (#43), e.g. "Steam"; the source shows only in "Infos".</summary>
public sealed record Sourced<T>(T Value, string Source);

/// <summary>One provider's checked data of a game, with the name it is shown by.</summary>
/// <param name="Language">
/// The language its texts were asked in by two-letter code (#557), e.g. "de" for a Steam answer from before a change of the
/// UI language; null for texts nobody asked for in a language (a launcher's local files).
/// </param>
public sealed record ProviderMetadata(string Source, GameMetadata Data, string? Language = null);

/// <summary>
/// What the detail page shows of a game (#43): each field from the first provider in the order that has it, with that
/// provider's name. Without any field the info line, the description and the tags are simply missing.
/// </summary>
public sealed record MergedMetadata
{
    public static MergedMetadata None { get; } = new();

    /// <summary>
    /// The title that replaces a doubtful launcher title (#494): a provider's (IGDB's), regardless of the order; null when the
    /// launcher's title stands.
    /// </summary>
    public Sourced<string>? Title { get; init; }

    /// <summary>The doubtful launcher title <see cref="Title"/> replaces, listed only among the sources in "Infos".</summary>
    public Sourced<string>? LauncherTitle { get; init; }

    public Sourced<string>? Description { get; init; }

    public Sourced<string>? ShortDescription { get; init; }

    public Sourced<IReadOnlyList<Genre>>? Genres { get; init; }

    public Sourced<IReadOnlyList<string>>? Developers { get; init; }

    public Sourced<IReadOnlyList<string>>? Publishers { get; init; }

    public Sourced<DateOnly>? ReleaseDate { get; init; }

    public Sourced<IReadOnlyList<string>>? Tags { get; init; }

    /// <summary>
    /// The region's board first, then USK, PEGI, ESRB (<see cref="AgeRatingRegion"/>), each once; a USK by the store only
    /// when no provider has a real one.
    /// </summary>
    public IReadOnlyList<Sourced<AgeRating>> AgeRatings { get; init; } = [];

    public bool IsEmpty =>
        Title is null && Description is null && ShortDescription is null && Genres is null && Developers is null && Publishers is null
        && ReleaseDate is null && Tags is null && AgeRatings.Count == 0;

    /// <summary>
    /// Merges <paramref name="providers"/>, best first, per field (#43): a field missing from one comes from the next.
    /// A description follows the language rule (#557, <see cref="Text"/>); the age ratings start with
    /// <paramref name="firstBoard"/>, the region's.
    /// </summary>
    /// <param name="uiLanguage">The UI language's two-letter code.</param>
    public static MergedMetadata Of(IReadOnlyList<ProviderMetadata> providers, string uiLanguage, AgeRatingBoard firstBoard)
    {
        if (providers.Count == 0)
        {
            return None;
        }

        var merged = new MergedMetadata
        {
            Description = Text(providers, data => data.Description, uiLanguage),
            ShortDescription = Text(providers, data => data.ShortDescription, uiLanguage),
            Genres = First(providers, data => data.Genres.Count > 0 ? data.Genres : null),
            Developers = First(providers, data => data.Developers.Count > 0 ? data.Developers : null),
            Publishers = First(providers, data => data.Publishers.Count > 0 ? data.Publishers : null),
            ReleaseDate = providers.Where(p => p.Data.ReleaseDate is not null).Select(p => new Sourced<DateOnly>(p.Data.ReleaseDate!.Value, p.Source)).FirstOrDefault(),
            Tags = First(providers, data => data.Tags.Count > 0 ? data.Tags : null),
            AgeRatings = Ratings(providers, firstBoard),
        };
        return merged.IsEmpty ? None : merged;
    }

    private static Sourced<T>? First<T>(IReadOnlyList<ProviderMetadata> providers, Func<GameMetadata, T?> field)
        where T : class =>
        providers.Select(p => field(p.Data) is { } value ? new Sourced<T>(value, p.Source) : null).FirstOrDefault(found => found is not null);

    /// <summary>
    /// The language rule (#557): the first provider's text in the UI language, else in English; else one still in the
    /// language it was loaded in before the UI language changed (it stays until "Alle Metadaten neu laden"). A text with
    /// a broken character only when none of these is whole; a text in any other language never. A Latin text too short
    /// to tell counts as the language it was asked in (a launcher's own files: the UI language).
    /// </summary>
    private static Sourced<string>? Text(IReadOnlyList<ProviderMetadata> providers, Func<GameMetadata, string?> field, string uiLanguage)
    {
        var all = providers
            .Select(p => field(p.Data) is { } text ? (Text: new Sourced<string>(text, p.Source), Language: LanguageOf(text, p.Language ?? uiLanguage), Asked: p.Language) : default)
            .Where(found => found.Text is not null && found.Language is not null)
            .ToList();
        bool Fits(string language, string? asked) => language == uiLanguage || language == "en" || language == asked;
        return all.FirstOrDefault(t => t.Language == uiLanguage && !TextLanguage.IsBroken(t.Text.Value)).Text
            ?? all.FirstOrDefault(t => t.Language == "en" && !TextLanguage.IsBroken(t.Text.Value)).Text
            ?? all.FirstOrDefault(t => t.Language == t.Asked && !TextLanguage.IsBroken(t.Text.Value)).Text
            ?? all.FirstOrDefault(t => Fits(t.Language!, t.Asked)).Text;
    }

    private static string? LanguageOf(string text, string asked) =>
        TextLanguage.LanguageOf(text) ?? (TextLanguage.IsLatin(text) ? asked : null);

    private static List<Sourced<AgeRating>> Ratings(IReadOnlyList<ProviderMetadata> providers, AgeRatingBoard firstBoard)
    {
        var shown = new List<Sourced<AgeRating>>();
        foreach (var board in AgeRatingRegion.Order(firstBoard))
        {
            var ofBoard = providers
                .SelectMany(p => p.Data.AgeRatings.Where(r => r.Board == board).Select(r => new Sourced<AgeRating>(r, p.Source)))
                .ToList();
            if ((ofBoard.FirstOrDefault(r => !r.Value.ByStore) ?? ofBoard.FirstOrDefault()) is { } rating)
            {
                shown.Add(rating);
            }
        }

        return shown;
    }
}
