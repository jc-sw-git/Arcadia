using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Arcadia.Core.Tests;

/// <summary>
/// UI prose names controller buttons by their glyph: ⧉ and ≡, the others in brackets ("[A]"), which
/// <c>ButtonTextBlock</c> draws as badges (docs/product/controls.md). A bare "A" or "LB" in a text shows as a letter,
/// a slip that kept coming back (owner, 2026-10-24), so every string of the App is checked.
/// </summary>
public sealed partial class ButtonGlyphTextTests
{
    [Fact]
    public void Ui_texts_write_controller_buttons_as_glyphs()
    {
        var app = Path.Combine(RepoRoot(), "src", "Arcadia.App");
        var bare = new List<string>();
        foreach (var file in Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("//", StringComparison.Ordinal) || line.Contains("Log", StringComparison.Ordinal) || line.Contains("CloseKeyboard(", StringComparison.Ordinal))
                {
                    // Comments and log lines aren't shown in the UI.
                    continue;
                }

                var literals = StringLiteral().Matches(line).Select(m => m.Groups[1].Value).ToList();
                if (line.StartsWith("new(\"", StringComparison.Ordinal) && literals.Count > 0)
                {
                    // A help row's input ("RT oder A") is drawn with ButtonTextBlock.Buttons, which takes names.
                    literals.RemoveAt(0);
                }

                bare.AddRange(literals
                    .Where(text => Prose().IsMatch(text) && BareButton().IsMatch(text))
                    .Select(text => $"{Path.GetFileName(file)}:{i + 1}: {text}"));
            }
        }

        Assert.Empty(bare);
    }

    /// <summary>
    /// The same in the resource files (#539: "Mit Y überspringst du" slipped through there), per language (#554): LB,
    /// RB, LT, RT, L3, R3, LS, RS and D-Pad are a word in no language, so they are checked in every one; A, B, X and Y
    /// only in German, since in English "A story" or "X marks the spot" is prose. Tooltips (<c>…Tip</c>) are plain text
    /// that can't draw a badge and name a key beside the button for the mouse; a help row's input ("RT oder A") is a
    /// list of buttons drawn with <c>ButtonTextBlock.Buttons</c>.
    /// </summary>
    [Fact]
    [Trait("TestCase", "T554-18")]
    [Trait("TestCase", "T556-4")]
    public void Resource_texts_write_controller_buttons_as_glyphs_in_every_language()
    {
        var bare = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*Texts*.resx", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => BareButtonsIn(Path.GetFileName(file), XDocument.Load(file)))
            .ToList();

        Assert.Empty(bare);
    }

    [Theory]
    [Trait("TestCase", "T554-18")]
    [InlineData("AppTexts.resx", "Drücke LB, um zu wechseln", true)]
    [InlineData("AppTexts.en.resx", "Press LB to switch", true)]
    [InlineData("AppTexts.en.resx", "Use the D-Pad to move", true)]
    [InlineData("AppTexts.resx", "Mit Y überspringst du das Spiel", true)]
    [InlineData("AppTexts.en.resx", "A story about X marks the spot", false)]
    [InlineData("AppTexts.resx", "Mit [Y] überspringst du das Spiel", false)]
    [InlineData("AppTexts.en.resx", "Press [LB] to switch", false)]
    [InlineData("AppTexts.es.resx", "Pulsa RB para cambiar", true)]
    [InlineData("AppTexts.es.resx", "Arcadia y Steam a la vez", false)]
    public void A_bare_button_fails_by_language(string file, string text, bool fails) =>
        Assert.Equal(fails, BareButtonsIn(file, new XDocument(new XElement("root",
            new XElement("data", new XAttribute("name", "Fixture"), new XElement("value", text))))).Any());

    private static IEnumerable<string> BareButtonsIn(string file, XDocument resx)
    {
        var german = file.Count(character => character == '.') == 1;
        foreach (var data in resx.Descendants("data"))
        {
            var key = (string?)data.Attribute("name") ?? string.Empty;
            var text = (string?)data.Element("value") ?? string.Empty;
            if (key.EndsWith("Tip", StringComparison.Ordinal) || ButtonListText().IsMatch(key) || !Prose().IsMatch(text))
            {
                continue;
            }

            if (BareNamedButton().IsMatch(text) || german && BareFaceButton().IsMatch(text))
            {
                yield return $"{file}: {key}: {text}";
            }
        }
    }

    /// <summary>
    /// A controller button in a view is drawn by <c>PadGlyph</c>, <c>KeyText</c> or <c>KeyHint</c>, which show the symbols of
    /// the controller in use (#215); a plain TextBlock keeps Xbox's name on every controller (owner's test, 2026-10-29).
    /// </summary>
    [Fact]
    public void Views_draw_controller_buttons_in_the_printing_of_the_controller_in_use()
    {
        var app = Path.Combine(RepoRoot(), "src", "Arcadia.App");
        var plain = Directory.EnumerateFiles(app, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadAllLines(file).Select((line, i) => (file, line, i)))
            .Where(entry => PlainButtonText().IsMatch(entry.line))
            .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.i + 1}: {entry.line.Trim()}")
            .ToList();

        Assert.Empty(plain);
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Arcadia.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Arcadia.slnx not found above the test binaries.");
    }

    [GeneratedRegex(@"<TextBlock[^>]*\bText=""(?:LB|RB|LT|RT|L3|R3|LS|RS|A|B|X|Y|D-Pad|⧉|≡)""")]
    private static partial Regex PlainButtonText();

    [GeneratedRegex(@"^MouseModeHelp\w+Or\w+$")]
    private static partial Regex ButtonListText();

    [GeneratedRegex(@"\$?""((?:[^""\\]|\\.)*)""")]
    private static partial Regex StringLiteral();

    /// <summary>A sentence, not a key name or an id.</summary>
    [GeneratedRegex(@"\s.*\p{Ll}{3}|\p{Ll}{3}.*\s")]
    private static partial Regex Prose();

    /// <summary>A named button standing alone in any language, not in brackets and not part of a word.</summary>
    [GeneratedRegex(@"(?<![\[\w/+\-–.])(?:LB|RB|LT|RT|L3|R3|LS|RS|D-Pad)(?![\]\w–\-.])")]
    private static partial Regex BareNamedButton();

    /// <summary>A face button standing alone, checked in German only: in English "A" is an article.</summary>
    [GeneratedRegex(@"(?<![\[\w/+\-–.’'])(?:A|B|X|Y)(?![\]\w–\-.’'])")]
    private static partial Regex BareFaceButton();

    /// <summary>A button name standing alone, not in brackets and not part of a word, "A–Z" or "D-Pad".</summary>
    [GeneratedRegex(@"(?<![\[\w/+\-–.])(?:LB|RB|LT|RT|L3|R3|LS|RS|A|B|X|Y)(?![\]\w–\-.])")]
    private static partial Regex BareButton();
}
