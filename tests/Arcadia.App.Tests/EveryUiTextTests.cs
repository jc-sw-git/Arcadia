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
/// Every text of every <c>*Texts.resx</c> (#480): no UI culture picks another or an empty resource, so all get the
/// German neutral texts until a translation exists.
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
    [InlineData("de-DE")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("")]
    public void Every_ui_culture_gets_every_German_text(string culture)
    {
        var differing = new List<string>();
        foreach (var resources in AllTexts)
        {
            var neutral = resources.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)!;
            foreach (DictionaryEntry text in neutral)
            {
                var shown = resources.GetString((string)text.Key, CultureInfo.GetCultureInfo(culture));
                if (string.IsNullOrEmpty(shown) || shown != (string)text.Value!)
                {
                    differing.Add($"{resources.BaseName}.{text.Key}");
                }
            }
        }

        Assert.True(differing.Count == 0, string.Join("\n", differing));
        Assert.True(AppTexts.Resources.GetResourceSet(CultureInfo.InvariantCulture, true, false)!.Cast<object>().Count() > 500);
    }
}
