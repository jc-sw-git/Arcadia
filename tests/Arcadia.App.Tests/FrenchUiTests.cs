using System.Globalization;
using Arcadia.App.Input;
using Arcadia.App.Settings;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #555: French joins the row "Sprache" with its texts. A French system language (France, Canada, Belgium, Switzerland)
/// gives a French Arcadia; "Français" applied restarts into French. That every text exists in French is
/// <c>TranslationsTests</c>' job, that the satellite assembly is found <c>EveryUiTextTests</c>'.
/// Not parallel: one test changes the Arcadia-wide region.
/// </summary>
[Collection(LocalizationSettingsTests.Name)]
public sealed class FrenchUiTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T555-2")]
    public async Task A_French_system_language_gives_a_French_Arcadia_and_the_info_says_so()
    {
        foreach (var system in new[] { "fr-FR", "fr-CA", "fr-BE", "fr-CH" })
        {
            using var arcadia = await ArcadiaDriver.StartAsync(arguments: new(SystemLanguage: system));
            Assert.Equal(system, arcadia.Main.Languages.InUse.Name);
        }

        Assert.Equal(
            AppTexts.LocalizationLanguageSystemSpoken(language: "Französisch"),
            UiLanguageRows.SystemLanguageText(CultureInfo.GetCultureInfo("fr-CA")));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T555-3")]
    public async Task Francais_applied_restarts_into_French_on_the_language_row()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        OpenLocalization(arcadia);
        var language = Assert.IsAssignableFrom<ChoiceRowViewModel>(arcadia.FocusRow(UiLanguageRows.LanguageKey));
        arcadia.Press(UiAction.Right).Press(UiAction.Right); // Systemsprache → English → Français
        Assert.Equal("Français", language.ValueText);
        Assert.Equal("fr", arcadia.Settings.Current.Localization.Language);

        var apply = Assert.IsAssignableFrom<ActionRowViewModel>(arcadia.FocusRow(UiLanguageRows.ApplyKey));
        Assert.True(apply.IsEnabled);
        arcadia.Press(UiAction.Accept);
        Assert.Equal([ArcadiaDriver.LanguageRestart], arcadia.Restarts);

        using var restarted = await arcadia.RestartAsync(arguments: new(ShowsLanguageSetting: true));
        Assert.Equal("fr", restarted.Main.Languages.InUse.Name);
        Assert.Equal(UiLanguageRows.LanguageKey, restarted.Main.Settings!.FocusedRow?.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T555-3")]
    public async Task On_a_French_system_Systemsprache_and_Francais_are_the_same_language()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(arguments: new(SystemLanguage: "fr-CA"));
        OpenLocalization(arcadia);
        arcadia.FocusRow(UiLanguageRows.LanguageKey);
        arcadia.Press(UiAction.Right).Press(UiAction.Right); // Français

        Assert.False(Assert.IsAssignableFrom<ActionRowViewModel>(arcadia.FocusRow(UiLanguageRows.ApplyKey)).IsEnabled);
    }

    [Fact]
    [Trait("TestCase", "T555-4")]
    public void The_size_unit_is_French_and_the_number_keeps_the_region()
    {
        var ui = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
        Services.Formatting.UseRegion(CultureInfo.GetCultureInfo("en-US"));
        try
        {
            Assert.Equal("12.5 Go", Services.Formatting.Size((long)(12.5 * 1024 * 1024 * 1024)));
        }
        finally
        {
            CultureInfo.CurrentUICulture = ui;
            Services.Formatting.UseRegion(TestSystem.GermanRegion);
        }
    }

    private static void OpenLocalization(ArcadiaDriver arcadia)
    {
        arcadia.Press(UiAction.Settings);
        for (var tabs = 0; tabs < 8 && arcadia.Main.Settings!.Selected is not LocalizationSettingsViewModel; tabs++)
        {
            arcadia.Press(UiAction.NextTab);
        }

        Assert.IsType<LocalizationSettingsViewModel>(arcadia.Main.Settings!.Selected);
    }
}
