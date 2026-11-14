using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Arcadia.ControllerSupport;
using Arcadia.Core;
using Arcadia.Input;
using Arcadia.Launchers.Demo;
using Arcadia.Launchers.Epic;
using Arcadia.Launchers.Steam;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests.Guards;

/// <summary>
/// No text of Arcadia's own is cut off (#587), in any language Arcadia offers, on any page, dialog or settings category of
/// <see cref="GuardPages"/> in any state of its focus: a label, a button, a row's title or subtitle, an area's value line.
/// What comes from a game or a launcher (a title, a path, a genre, a tag) may end in "…"; a text counts as Arcadia's own
/// when it is one of the <c>*Texts.resx</c> texts of the language in use, with anything in place of its placeholders.
/// </summary>
[Collection(LocalizationSettingsTests.Name)]
public sealed class NoLabelCutTests
{
    private static readonly ResourceManager[] AllTexts =
    [
        AppTexts.Resources, ControllerSupportTexts.Resources, CoreTexts.Resources, InputTexts.Resources,
        SteamTexts.Resources, EpicTexts.Resources, DemoTexts.Resources,
    ];

    [AvaloniaTheory]
    [Trait("TestCase", "T587-1")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("es-ES")]
    public async Task No_text_of_Arcadias_own_is_cut_off_on_any_page_dialog_or_settings_category(string language)
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            var own = OwnTexts(CultureInfo.CurrentUICulture);
            // Gathered over every state, so one cut text doesn't hide the others of the same page.
            var cut = new List<string>();
            await GuardWalk.EveryAsync(_ => true, (arcadia, _, state) =>
                cut.AddRange(CutTexts(arcadia.Window).Where(text => own.IsMatch(text)).Select(text => $"{state}: „{text}“")));
            Assert.True(cut.Count == 0, $"{language}, abgeschnitten:\n{string.Join("\n", cut.Distinct())}");
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    /// <summary>
    /// The detail page's areas from the inside (the page list walks only its rail): every row of every area focused once,
    /// with its info box, as the player sees them after A.
    /// </summary>
    [AvaloniaTheory]
    [Trait("TestCase", "T587-2")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("es-ES")]
    public async Task No_text_of_Arcadias_own_is_cut_off_in_any_row_of_the_detail_pages_areas(string language)
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            var own = OwnTexts(CultureInfo.CurrentUICulture);
            using var arcadia = await ArcadiaWindow.ShowAsync();
            await GuardPages.OpenDetail(arcadia);
            var page = arcadia.Arcadia.Detail!.Current;
            var cut = new List<string>();
            void Look(string state) =>
                cut.AddRange(CutTexts(arcadia.Settle().WaitForTransitions().Window).Where(text => own.IsMatch(text)).Select(text => $"{state}: „{text}“"));

            arcadia.Press(Input.UiAction.Down);
            for (var area = 0; area < page.Areas.Count; area++)
            {
                Look($"{page.OpenArea.Name}, im Rand");
                arcadia.Press(Input.UiAction.Accept);
                for (var row = 0; page.IsInRows && row < page.OpenArea.Rows.Count; row++)
                {
                    Look($"{page.OpenArea.Name}, Zeile {row + 1}");
                    arcadia.Press(Input.UiAction.Down);
                }

                if (page.IsInRows)
                {
                    arcadia.Press(Input.UiAction.Back);
                }

                arcadia.Press(Input.UiAction.Down);
            }

            Assert.True(cut.Count == 0, $"{language}, abgeschnitten:\n{string.Join("\n", cut.Distinct())}");
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    /// <summary>The texts drawn shorter than they are: ended with "…", or running past their bounds on one line.</summary>
    internal static IEnumerable<string> CutTexts(Avalonia.Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible && block.Bounds.Width > 0 && IsCut(block))
            .Select(Shown)
            .Where(text => text.Length > 0);

    private static bool IsCut(TextBlock block)
    {
        var layout = block.TextLayout;
        if (layout.TextLines.Any(line => line.HasCollapsed))
        {
            return true;
        }

        return (block.TextWrapping == TextWrapping.NoWrap && layout.WidthIncludingTrailingWhitespace > block.Bounds.Width + 0.5)
            || GlyphOverflowsItsLine(block);
    }

    /// <summary>
    /// A button glyph in the text taller than a fixed line height pushes its line down, and the text's lower edge is cut
    /// off (#587: a value box with [R]). The glyph needs its height and a little room for the text's descenders.
    /// </summary>
    private static bool GlyphOverflowsItsLine(TextBlock block) =>
        !double.IsNaN(block.LineHeight) && block.LineHeight > 0
        && (block.Inlines ?? []).OfType<InlineUIContainer>().Any(glyph => glyph.Child.Bounds.Height + MinGlyphRoom > block.LineHeight);

    /// <summary>The line height a glyph needs beyond its own: what keeps the descenders of the text beside it inside.</summary>
    private const double MinGlyphRoom = 2;

    /// <summary>The text as written, glyph names included, as the resource holds it.</summary>
    private static string Shown(TextBlock block) =>
        Controls.ButtonTextBlock.GetText(block) ?? (block.Text is { Length: > 0 } text ? text : string.Concat(block.Inlines?.OfType<Run>().Select(run => run.Text) ?? []));

    /// <summary>
    /// One pattern for every text of the language: a placeholder matches anything. Texts that are nothing but a
    /// placeholder and punctuation ("{0} · {1}") are left out, or every game's data would count as Arcadia's own.
    /// </summary>
    internal static Regex OwnTexts(CultureInfo culture)
    {
        var patterns = new List<string>();
        foreach (var resources in AllTexts)
        {
            var neutral = resources.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)!;
            foreach (DictionaryEntry entry in neutral)
            {
                if (resources.GetString((string)entry.Key, culture) is not { Length: > 0 } text)
                {
                    continue;
                }

                var parts = Regex.Split(text, @"\{\w+(?:[:,][^}]*)?\}");
                if (parts.Sum(part => part.Count(char.IsLetter)) < 3)
                {
                    continue;
                }

                patterns.Add(string.Join(".*", parts.Select(Regex.Escape)));
            }
        }

        return new Regex($"^(?:{string.Join("|", patterns)})$", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }
}
