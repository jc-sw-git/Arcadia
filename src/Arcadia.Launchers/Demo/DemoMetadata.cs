using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Demo;

/// <summary>
/// Made-up metadata for the demo libraries (#493), all from "local files" so it needs no store: long German texts with
/// markup, short ones, an English one, none at all; long developer names; USK, Steam's own rating and none. Picked by
/// the game's id, so a game shows the same each start, and the budgets are measured with metadata in memory.
/// </summary>
public sealed class DemoMetadata : ILauncherMetadata
{
    private static readonly Genre[] Genres = Enum.GetValues<Genre>();

    public string Summary => DemoTexts.DemoMetadataSummary;

    public string About => DemoTexts.DemoMetadataAbout;

    public IReadOnlySet<MetadataField> Fields { get; } = new HashSet<MetadataField>(Enum.GetValues<MetadataField>());

    public IMetadataLookup Look(IReadOnlyCollection<Game> games) => new Lookup();

    private sealed class Lookup : IMetadataLookup
    {
        public GameMetadata? Of(Game game)
        {
            var n = (int)(StableHash(game.Id) % 1000);
            if (game.Type != GameType.Game || n % 10 == 9)
            {
                return null;
            }

            string[] tags =
            [
                DemoTexts.DemoMetadataTag1, DemoTexts.DemoMetadataTag2, DemoTexts.DemoMetadataTag3, DemoTexts.DemoMetadataTag4,
                DemoTexts.DemoMetadataTag5, DemoTexts.DemoMetadataTag6, DemoTexts.DemoMetadataTag7,
            ];
            return new GameMetadata
            {
                Description = (n % 4) switch
                {
                    0 => DemoTexts.DemoMetadataDescriptionLong,
                    1 => DemoTexts.DemoMetadataDescriptionShort,
                    2 => DemoTexts.DemoMetadataDescriptionEnglish,
                    _ => null,
                },
                ShortDescription = (n % 4) switch
                {
                    0 => DemoTexts.DemoMetadataShortLong,
                    1 => DemoTexts.DemoMetadataDescriptionShort,
                    2 => DemoTexts.DemoMetadataDescriptionEnglish,
                    _ => null,
                },
                Genres = n % 5 == 4 ? [] : [Genres[n % Genres.Length], .. n % 3 == 0 ? [Genres[(n / 3) % Genres.Length]] : Array.Empty<Genre>()],
                Developers = [(n % 3) switch { 0 => DemoTexts.DemoMetadataDeveloper1, 1 => DemoTexts.DemoMetadataDeveloper2, _ => DemoTexts.DemoMetadataDeveloper3 }],
                Publishers = n % 2 == 0 ? [DemoTexts.DemoMetadataPublisher] : [],
                ReleaseDate = n % 7 == 6 ? null : new DateOnly(1995 + n % 31, 1 + n % 12, 1 + n % 28),
                Tags = [.. tags.Skip(n % 3).Take(1 + n % tags.Length)],
                AgeRatings = (n % 3) switch
                {
                    0 => [new AgeRating(AgeRatingBoard.Usk, new[] { "0", "6", "12", "16", "18" }[n % 5]), new AgeRating(AgeRatingBoard.Pegi, "16")],
                    1 => [new AgeRating(AgeRatingBoard.Usk, "12", ByStore: true), new AgeRating(AgeRatingBoard.Esrb, "T")],
                    _ => [],
                },
            };
        }

        // string.GetHashCode differs per process; the demo should look the same each start.
        private static uint StableHash(string text)
        {
            var hash = 2166136261u;
            foreach (var c in text)
            {
                hash = (hash ^ c) * 16777619u;
            }

            return hash;
        }
    }
}
