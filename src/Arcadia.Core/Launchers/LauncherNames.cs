using System.Text;
using Arcadia.Core.Search;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Launchers;

/// <summary>
/// The rules for a launcher's name and search keyword the user types (#137), checked with every key: the name isn't
/// empty; the keyword holds only lower-case letters and digits (no <c>-</c>, which joins several launchers in the search
/// prefix), isn't empty and belongs to no other launcher.
/// </summary>
public static class LauncherNames
{
    /// <summary>Longer names would push the other tabs out of the header.</summary>
    public const int MaxNameLength = 24;

    /// <summary>The keyword stands before every search term, so it stays short.</summary>
    public const int MaxKeywordLength = 12;

    /// <returns>Why <paramref name="name"/> can't be the tab's name, or null if it can.</returns>
    public static string? NameProblem(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Length == 0 ? "Der Name darf nicht leer sein."
            : trimmed.Length > MaxNameLength ? $"Höchstens {MaxNameLength} Zeichen."
            : trimmed.Any(char.IsControl) ? "Der Name enthält ein Steuerzeichen."
            : null;
    }

    /// <param name="launcherId">The launcher the keyword is for.</param>
    /// <param name="launchers">Every registered launcher with the keyword it has now, also the inactive ones.</param>
    /// <returns>Why <paramref name="keyword"/> can't be its keyword, or null if it can.</returns>
    public static string? KeywordProblem(string keyword, string launcherId, IEnumerable<LauncherInfo> launchers)
    {
        if (keyword.Length == 0)
        {
            return "Das Kürzel darf nicht leer sein.";
        }

        if (keyword.Contains(SearchQuery.KeywordSeparator))
        {
            return $"Kein „{SearchQuery.KeywordSeparator}“: Er trennt mehrere Launcher.";
        }

        if (!keyword.All(IsKeywordCharacter))
        {
            return "Nur Kleinbuchstaben und Ziffern.";
        }

        if (keyword.Length > MaxKeywordLength)
        {
            return $"Höchstens {MaxKeywordLength} Zeichen.";
        }

        return launchers.FirstOrDefault(l => l.Id != launcherId && string.Equals(l.SearchKeyword, keyword, StringComparison.OrdinalIgnoreCase)) is { } other
            ? $"{SearchQuery.PrefixMarker}{keyword} hat schon {other.DisplayName}."
            : null;
    }

    /// <summary>
    /// A lower-case letter of any language (the search folds the prefix to lower case) or a digit 0–9. Not a special form
    /// of another letter, such as µ (the micro sign beside the Greek μ): it is a symbol to the user (owner, 2026-10-23).
    /// </summary>
    public static bool IsKeywordCharacter(char c) =>
        char.IsAsciiDigit(c) || (char.IsLetter(c) && char.IsLower(c) && c.ToString().IsNormalized(NormalizationForm.FormKC));
}
