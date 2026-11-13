using System.Globalization;
using Arcadia.Core.Localization;

namespace Arcadia.App.Services;

/// <summary>
/// The language Arcadia's texts are in (#554): every text, upper case, sorting, the search, day and month names (#553 A7).
/// Chosen once at the start from "Sprache" (<see cref="Use"/>); a change applies after a restart.
/// </summary>
public static class UiLanguage
{
    private static CultureInfo? _chosen;

    /// <summary>The system's display language, as it was before <see cref="Use"/> replaced it for Arcadia.</summary>
    public static CultureInfo System { get; } = CultureInfo.CurrentUICulture;

    /// <summary>The language in use: as <see cref="Use"/> set it; before that (the tests), the thread's.</summary>
    public static CultureInfo Culture => _chosen ?? CultureInfo.CurrentUICulture;

    /// <summary>
    /// Sets the language "Sprache" gives (<see cref="UiLanguages.Resolve"/>) for every thread, before the first text is
    /// read: an unknown value has already become null in the settings, which is the system language.
    /// </summary>
    public static void Use(string? choice)
    {
        var culture = UiLanguages.Resolve(choice, System);
        _chosen = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
