using Arcadia.Core.Settings;

namespace Arcadia.Core.Search;

/// <summary>
/// The layouts an on-screen keyboard offers (#553): it opens with <see cref="Default"/>, and R3 switches among
/// <see cref="Quick"/> only until it closes. With a single layout in the quick selection, R3 is greyed out.
/// </summary>
public sealed record KeyboardLayoutChoice(KeyboardLayout Default, IReadOnlyList<KeyboardLayout> Quick)
{
    /// <summary>R3 has something to switch to.</summary>
    public bool CanSwitch => Quick.Count > 1;

    /// <summary>
    /// The choice the settings make (scenario 13): the default the player saved; without one, the system's keyboard
    /// layout if it is one of the five, else the one matching the UI language. The quick selection is the default plus
    /// the layouts switched on (all five while none was switched), in the order of <see cref="KeyboardLayout.All"/>.
    /// </summary>
    /// <param name="systemLayoutId">The id of the system's keyboard layout, null when it isn't one of the five.</param>
    /// <param name="uiLanguage">The UI language as a culture name, e.g. "de-DE".</param>
    public static KeyboardLayoutChoice From(LocalizationSettings settings, string? systemLayoutId, string uiLanguage)
    {
        var defaultLayout = DefaultLayout(settings.KeyboardLayout, systemLayoutId, uiLanguage);
        var on = settings.QuickLayouts;
        return new(defaultLayout, [.. KeyboardLayout.All.Where(layout => layout == defaultLayout || on is null || on.Contains(layout.Id))]);
    }

    /// <summary>The layout "Standard-Layout" stands on.</summary>
    public static KeyboardLayout DefaultLayout(string? savedId, string? systemLayoutId, string uiLanguage) =>
        KeyboardLayout.TryFind(savedId) ?? KeyboardLayout.TryFind(systemLayoutId) ?? ForLanguage(uiLanguage);

    /// <summary>The layout of a UI language: British English for "en-GB", American for every other English; German when none fits.</summary>
    public static KeyboardLayout ForLanguage(string uiLanguage)
    {
        var language = uiLanguage.Split('-')[0].ToLowerInvariant();
        var id = language switch
        {
            "en" when uiLanguage.Equals("en-GB", StringComparison.OrdinalIgnoreCase) => "en-gb",
            "en" => "en-us",
            _ => language,
        };
        return KeyboardLayout.TryFind(id) ?? KeyboardLayout.German;
    }
}
