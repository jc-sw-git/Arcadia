using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Metadata;

/// <summary>The German names of Arcadia's fixed genre list (#43), the same whichever provider delivered a genre.</summary>
public static class GenreNames
{
    public static string Of(Genre genre) => genre switch
    {
        Genre.Action => CoreTexts.GenreAction,
        Genre.Adventure => CoreTexts.GenreAdventure,
        Genre.RolePlaying => CoreTexts.GenreRolePlaying,
        Genre.Strategy => CoreTexts.GenreStrategy,
        Genre.Simulation => CoreTexts.GenreSimulation,
        Genre.Racing => CoreTexts.GenreRacing,
        Genre.Sports => CoreTexts.GenreSports,
        Genre.Shooter => CoreTexts.GenreShooter,
        Genre.Puzzle => CoreTexts.GenrePuzzle,
        Genre.Platformer => CoreTexts.GenrePlatformer,
        Genre.Fighting => CoreTexts.GenreFighting,
        Genre.Casual => CoreTexts.GenreCasual,
        Genre.Indie => CoreTexts.GenreIndie,
        Genre.MassivelyMultiplayer => CoreTexts.GenreMassivelyMultiplayer,
        Genre.Arcade => CoreTexts.GenreArcade,
        Genre.Music => CoreTexts.GenreMusic,
        Genre.PointAndClick => CoreTexts.GenrePointAndClick,
        Genre.VisualNovel => CoreTexts.GenreVisualNovel,
        Genre.CardAndBoard => CoreTexts.GenreCardAndBoard,
        Genre.Tactics => CoreTexts.GenreTactics,
        _ => throw new ArgumentOutOfRangeException(nameof(genre), genre, null),
    };
}
