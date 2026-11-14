using System.Globalization;

namespace Arcadia.Core.Localization;

/// <summary>
/// The languages Arcadia's interface speaks (#554, #556) and which one a choice in the settings gives. The neutral texts are
/// German; every other language is a translation beside each <c>*Texts.resx</c>. A language is offered only once all
/// its texts exist (<c>TranslationsTests</c>).
/// </summary>
public static class UiLanguages
{
    /// <summary>The choice that follows the system's display language.</summary>
    public const string System = "system";

    /// <summary>What a choice falls back to: the system language when it isn't supported, a missing text.</summary>
    public static CultureInfo English => Arcadia.Launchers.Abstractions.Texts.UiText.English;

    /// <summary>The languages besides the neutral German texts, by their two-letter code.</summary>
    public static IReadOnlyList<string> Translations { get; } = ["en", "fr", "es"];

    /// <summary>The values of the row "Sprache" in their order: the system language, then each language by its code.</summary>
    public static IReadOnlyList<string> Choices { get; } = [System, "en", "fr", "es", "de"];

    /// <summary>Whether <paramref name="value"/> is one of <see cref="Choices"/>; anything else is read as <see cref="System"/>.</summary>
    public static bool IsChoice(string? value) => value is not null && Choices.Contains(value, StringComparer.Ordinal);

    /// <summary>Whether Arcadia speaks the language of <paramref name="culture"/>.</summary>
    public static bool Speaks(CultureInfo culture) =>
        culture.TwoLetterISOLanguageName != CultureInfo.InvariantCulture.TwoLetterISOLanguageName
        && Choices.Skip(1).Contains(culture.TwoLetterISOLanguageName, StringComparer.Ordinal);

    /// <summary>
    /// The UI culture a choice gives: a language as chosen; "system" the system's display language when Arcadia speaks
    /// it (de-AT stays de-AT), English otherwise. An unknown value counts as "system".
    /// </summary>
    public static CultureInfo Resolve(string? choice, CultureInfo systemLanguage)
    {
        if (IsChoice(choice) && choice != System)
        {
            return CultureInfo.GetCultureInfo(choice!);
        }

        return Speaks(systemLanguage) ? systemLanguage : English;
    }

    /// <summary>Whether two UI cultures speak the same language, as "Sprache anwenden" compares them.</summary>
    public static bool SameLanguage(CultureInfo one, CultureInfo other) =>
        one.TwoLetterISOLanguageName == other.TwoLetterISOLanguageName;

    /// <summary>A language's name in its own language, as the row shows it whatever the UI language.</summary>
    public static string OwnName(string code) => code switch
    {
        "en" => "English", // not a UI text (a language's own name, the same in every language)
        "de" => "Deutsch", // not a UI text (a language's own name, the same in every language)
        "fr" => "Français", // not a UI text (a language's own name, the same in every language)
        "es" => "Español", // not a UI text (a language's own name, the same in every language)
        _ => code,
    };
}
