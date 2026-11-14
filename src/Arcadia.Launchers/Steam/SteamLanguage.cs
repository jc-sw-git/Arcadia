using System.Globalization;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Steam's name of a language (#557), as its store (<c>l=</c>) and its tag lists take it: Arcadia's UI languages by their
/// two-letter code; any other is English, the language Steam falls back to itself.
/// </summary>
internal static class SteamLanguage
{
    /// <summary>The languages Arcadia has a tag list of, by Steam's name.</summary>
    public static IReadOnlyList<string> Known { get; } = ["german", "english", "french", "spanish"];

    public static string NameOf(CultureInfo language) => language.TwoLetterISOLanguageName switch
    {
        "de" => "german",
        "fr" => "french",
        "es" => "spanish",
        _ => "english",
    };
}
