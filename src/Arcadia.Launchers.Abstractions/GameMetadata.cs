namespace Arcadia.Launchers.Abstractions;

/// <summary>The fields of a game's metadata (#43); a provider declares which of them it can deliver.</summary>
public enum MetadataField
{
    Description,
    ShortDescription,
    Genres,
    Developers,
    Publishers,
    ReleaseDate,
    Tags,
    AgeRatings,
}

/// <summary>
/// Arcadia's fixed genre list (#43); every provider maps its own genres onto it and drops the rest. The names the player
/// reads are UI texts in Core.
/// </summary>
public enum Genre
{
    Action,
    Adventure,
    RolePlaying,
    Strategy,
    Simulation,
    Racing,
    Sports,
    Shooter,
    Puzzle,
    Platformer,
    Fighting,
    Casual,
    Indie,
    MassivelyMultiplayer,
    Arcade,
    Music,
    PointAndClick,
    VisualNovel,
    CardAndBoard,
    Tactics,
}

/// <summary>The rating boards Arcadia shows (#43).</summary>
public enum AgeRatingBoard
{
    Usk,
    Pegi,
    Esrb,
}

/// <summary>
/// One age rating as a provider delivers it. Arcadia checks <paramref name="Rating"/> against the board's real values and
/// drops any other.
/// </summary>
/// <param name="Rating">The board's own value: <c>0</c>, <c>6</c>, <c>12</c>, <c>16</c>, <c>18</c> (USK), <c>3</c> … <c>18</c> (PEGI), <c>E</c>, <c>E10+</c>, <c>T</c>, <c>M</c>, <c>AO</c> (ESRB).</param>
/// <param name="ByStore">
/// Not the board's rating but the store's own one for that country (Steam's <c>steam_germany</c>); it counts only when no
/// provider has the board's real rating.
/// </param>
public sealed record AgeRating(AgeRatingBoard Board, string Rating, bool ByStore = false);

/// <summary>
/// One provider's metadata of one game (#43), as it delivered it: nothing merged, texts not yet checked. Arcadia reduces
/// the descriptions to plain text, checks every value and merges the providers per field.
/// </summary>
public sealed record GameMetadata
{
    public static GameMetadata Empty { get; } = new();

    /// <summary>The full description; HTML is allowed, Arcadia keeps only its text.</summary>
    public string? Description { get; init; }

    /// <summary>A few sentences for the overview; HTML is allowed, Arcadia keeps only its text.</summary>
    public string? ShortDescription { get; init; }

    public IReadOnlyList<Genre> Genres { get; init; } = [];

    public IReadOnlyList<string> Developers { get; init; } = [];

    public IReadOnlyList<string> Publishers { get; init; } = [];

    public DateOnly? ReleaseDate { get; init; }

    /// <summary>The provider's tags (Steam's store tags), in its order and as it names them.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<AgeRating> AgeRatings { get; init; } = [];

    /// <summary>
    /// This provider's data with every field missing here taken from <paramref name="other"/>, e.g. a launcher's local
    /// file first and its store page after it.
    /// </summary>
    public GameMetadata Or(GameMetadata? other) => other is null ? this : new GameMetadata
    {
        Description = Description ?? other.Description,
        ShortDescription = ShortDescription ?? other.ShortDescription,
        Genres = Genres.Count > 0 ? Genres : other.Genres,
        Developers = Developers.Count > 0 ? Developers : other.Developers,
        Publishers = Publishers.Count > 0 ? Publishers : other.Publishers,
        ReleaseDate = ReleaseDate ?? other.ReleaseDate,
        Tags = Tags.Count > 0 ? Tags : other.Tags,
        AgeRatings = AgeRatings.Count > 0 ? AgeRatings : other.AgeRatings,
    };
}
