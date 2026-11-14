using System.Collections;
using System.Globalization;
using System.Resources;
using Arcadia.ControllerSupport;
using Arcadia.Core;
using Arcadia.Input;
using Arcadia.Launchers.Demo;
using Arcadia.Launchers.Epic;
using Arcadia.Launchers.Steam;

namespace Arcadia.App.Tests;

/// <summary>
/// Every text of every <c>*Texts.resx</c> in every language Arcadia offers (#480, #554, #555): German UI cultures get the
/// German neutral texts, every other offered language its own translation. That the translations
/// are complete is <c>TranslationsTests</c>' job; this checks the built satellite assemblies are found.
/// </summary>
public sealed class EveryUiTextTests
{
    private static readonly ResourceManager[] AllTexts =
    [
        AppTexts.Resources, ControllerSupportTexts.Resources, CoreTexts.Resources, InputTexts.Resources,
        SteamTexts.Resources, EpicTexts.Resources, DemoTexts.Resources,
    ];

    [Theory]
    [Trait("TestCase", "T480-11")]
    [InlineData("de-DE", "")]
    [InlineData("de-AT", "")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("fr-FR", "fr")]
    [InlineData("fr-CA", "fr")]
    [InlineData("fr-BE", "fr")]
    [InlineData("fr-CH", "fr")]
    [InlineData("es-ES", "es")]
    [InlineData("es-MX", "es")]
    [Trait("TestCase", "T554-17")]
    [Trait("TestCase", "T555-2")]
    [Trait("TestCase", "T556-3")]
    public void Every_offered_ui_culture_gets_every_text_in_its_language(string culture, string language)
    {
        var differing = new List<string>();
        foreach (var resources in AllTexts)
        {
            var expected = resources.GetResourceSet(CultureInfo.GetCultureInfo(language), createIfNotExists: true, tryParents: false);
            var neutral = resources.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)!;
            if (expected is null)
            {
                differing.Add($"{resources.BaseName}: no texts for \"{language}\"");
                continue;
            }

            foreach (DictionaryEntry text in neutral)
            {
                var shown = resources.GetString((string)text.Key, CultureInfo.GetCultureInfo(culture));
                if (shown is null || shown != expected.GetString((string)text.Key))
                {
                    differing.Add($"{resources.BaseName}.{text.Key}");
                }
            }
        }

        Assert.True(differing.Count == 0, string.Join("\n", differing));
        Assert.True(AppTexts.Resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)!.Cast<object>().Count() > 500);
    }
}
