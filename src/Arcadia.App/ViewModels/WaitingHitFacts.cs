using Arcadia.App.Services;
using Arcadia.Core.Metadata;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.ViewModels;

/// <summary>One line of a metadata hit's info box: its caption and value.</summary>
public sealed record WaitingHitFact(string Caption, string Value);

/// <summary>
/// The facts of an IGDB hit on "Warten auf deine Auswahl" (#539): what the player decides by when several games share a
/// name. Release date, genres, developer, publisher, age ratings and the start of the description, each only when IGDB
/// has it; checked as any metadata (#493), so nothing shows raw.
/// </summary>
public sealed class WaitingHitFacts
{
    /// <summary>The description is cut after this many characters; the box shows its start, not the whole text.</summary>
    public const int MaxDescription = 600;

    public WaitingHitFacts(GameMetadata delivered)
    {
        var data = MetadataCheck.Checked(delivered);
        var genres = data.Genres.Select(GenreNames.Of).ToList();
        var facts = new List<WaitingHitFact>();
        Add(facts, AppTexts.DetailReleasedCaption, data.ReleaseDate?.ToString("dd.MM.yyyy", Formatting.Culture));
        Add(facts, AppTexts.DetailGenresCaption, List(genres));
        Add(facts, AppTexts.DetailDevelopersCaption, List(data.Developers));
        Add(facts, AppTexts.DetailPublishersCaption, List(data.Publishers));
        Add(facts, AppTexts.DetailAgeRatingCaption, List(data.AgeRatings.Select(DetailMetadata.BadgeOf)));
        Facts = facts;
        Description = Shortened(data.ShortDescription ?? data.Description);
        Line = string.Join(AppTexts.DetailInfoSeparator, new[] { genres.FirstOrDefault(), data.Developers.FirstOrDefault() }.OfType<string>());
    }

    public IReadOnlyList<WaitingHitFact> Facts { get; }

    /// <summary>The start of the description; null without one.</summary>
    public string? Description { get; }

    public bool HasDescription => Description is not null;

    /// <summary>"Rollenspiel · Nordkai Studio": first genre and developer; empty without both.</summary>
    public string Line { get; }

    /// <summary>IGDB has nothing on the game but its name (and maybe the year): the box says so.</summary>
    public bool IsEmpty => Facts.Count == 0 && Description is null;

    private static void Add(List<WaitingHitFact> facts, string caption, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            facts.Add(new WaitingHitFact(caption, value));
        }
    }

    private static string List(IEnumerable<string> names) => string.Join(AppTexts.DetailListSeparator, names);

    /// <summary>The text up to <see cref="MaxDescription"/>, cut at a word where there is one.</summary>
    private static string? Shortened(string? text)
    {
        if (text is null || text.Length <= MaxDescription)
        {
            return text;
        }

        var cut = text.LastIndexOf(' ', MaxDescription);
        return text[..(cut > 0 ? cut : MaxDescription)].TrimEnd(',', ';', ':', '.', ' ') + " …";
    }
}
