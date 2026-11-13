using System.Globalization;

namespace Arcadia.App.Services;

/// <summary>The language Arcadia's texts are in: upper case, sorting, the search, day and month names (#553 A7).</summary>
public static class UiLanguage
{
    /// <summary>German until the language can be chosen (#554).</summary>
    public static CultureInfo Culture { get; } = CultureInfo.GetCultureInfo("de-DE");
}
