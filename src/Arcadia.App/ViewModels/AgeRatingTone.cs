using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The colour of an age rating's border (#541), after the colours the boards give their own ratings: USK white, yellow,
/// green, blue, red; PEGI green, orange, red. ESRB marks its ratings in black and white only, so they take the colour
/// of PEGI's rating for about the same age (M is 17+, so red). The brushes are <c>AgeRating{Tone}</c> in <c>Theme.axaml</c>.
/// </summary>
public enum AgeRatingTone
{
    White,
    Yellow,
    Green,
    Blue,
    Orange,
    Red,
}

public static class AgeRatingTones
{
    /// <summary>The tone of a checked rating; a value the board doesn't have (none gets this far) reads white.</summary>
    public static AgeRatingTone Of(AgeRating rating) => (rating.Board, rating.Rating) switch
    {
        (AgeRatingBoard.Usk, "6") => AgeRatingTone.Yellow,
        (AgeRatingBoard.Usk, "12") => AgeRatingTone.Green,
        (AgeRatingBoard.Usk, "16") => AgeRatingTone.Blue,
        (AgeRatingBoard.Usk, "18") => AgeRatingTone.Red,
        (AgeRatingBoard.Pegi, "3" or "7") => AgeRatingTone.Green,
        (AgeRatingBoard.Pegi, "12" or "16") => AgeRatingTone.Orange,
        (AgeRatingBoard.Pegi, "18") => AgeRatingTone.Red,
        (AgeRatingBoard.Esrb, "E" or "E10+") => AgeRatingTone.Green,
        (AgeRatingBoard.Esrb, "T") => AgeRatingTone.Orange,
        (AgeRatingBoard.Esrb, "M" or "AO") => AgeRatingTone.Red,
        _ => AgeRatingTone.White,
    };
}
