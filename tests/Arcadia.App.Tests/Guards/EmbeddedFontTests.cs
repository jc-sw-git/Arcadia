using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Arcadia.App.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests.Guards;

/// <summary>
/// Every view sets its text in Figtree, with Inter only for the symbols Figtree lacks (#24). Both are embedded, so a
/// character that neither has would fall to whatever font the system has, or show as an empty box.
/// </summary>
public sealed partial class EmbeddedFontTests
{
    /// <summary>Drawn as theme icons (<c>PadIconView</c>, <c>PadIconOptions</c>, <c>KeyIconTab</c>), never as characters.</summary>
    private const string DrawnAsIcons = "⧉≡↹";

    [AvaloniaTheory]
    [InlineData("Normal")]
    [InlineData("Medium")]
    [InlineData("SemiBold")]
    [InlineData("Bold")]
    [InlineData("ExtraBold")]
    [Trait("TestCase", "T24-10")]
    public void Figtree_is_embedded_in_every_weight_the_views_use(string weight)
    {
        var wanted = Enum.Parse<FontWeight>(weight);

        var found = FontManager.Current.TryGetGlyphTypeface(new Typeface(ArcadiaFonts.Figtree, FontStyle.Normal, wanted), out var typeface);

        Assert.True(found);
        // The face's own name carries its weight ("Figtree SemiBold"); the family it was found under is Figtree.
        Assert.StartsWith("Figtree", typeface!.FamilyName, StringComparison.Ordinal);
        Assert.Equal(wanted, typeface.Weight);
    }

    /// <summary>T24-8: every page, dialog and settings category of <see cref="GuardPages"/>, and the mouse mode's keyboard.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T24-8")]
    public async Task Every_view_sets_its_texts_in_Figtree()
    {
        var notFigtree = new List<string>();
        await GuardWalk.EveryAsync(_ => true, (arcadia, _, state) =>
            notFigtree.AddRange(NotInFigtree(arcadia.WaitForTransitions().Window).Select(text => $"{state}: {text}")), walks: _ => false);
        using (var mouseMode = MouseModeKeyboard.Show())
        {
            Dispatcher.UIThread.RunJobs();
            notFigtree.AddRange(NotInFigtree(mouseMode.Window).Select(text => $"Maus-Tastatur: {text}"));
        }

        Assert.Empty(notFigtree);
    }

    // Visible texts whose font isn't Figtree first (Inter behind it is only the fallback for its missing symbols).
    private static IEnumerable<string> NotInFigtree(Window window)
    {
        var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(texts);
        return texts.Where(text => text.FontFamily.FamilyNames[0] != "Figtree").Select(text => $"{text.Text} ({text.FontFamily})");
    }

    /// <summary>
    /// Every character in the App's texts and views is in Figtree or Inter, except the few drawn as icons. A new symbol
    /// in a text fails here, before it shows as a box on a machine without a font for it.
    /// </summary>
    [AvaloniaFact]
    [Trait("TestCase", "T24-9")]
    public void Every_character_of_the_views_and_texts_is_in_an_embedded_font()
    {
        var fonts = new[] { ArcadiaFonts.Figtree, ArcadiaFonts.Inter }
            .Select(name => FontManager.Current.TryGetGlyphTypeface(new Typeface(name), out var typeface) ? typeface : throw new InvalidOperationException(name))
            .ToList();

        var missing = UiCharacters()
            .Where(entry => !DrawnAsIcons.Contains(entry.Character, StringComparison.Ordinal))
            .Where(entry => !fonts.Any(font => font.CharacterToGlyphMap.ContainsGlyph(char.ConvertToUtf32(entry.Character, 0))))
            .Select(entry => $"{entry.Character} (U+{char.ConvertToUtf32(entry.Character, 0):X4}) in {entry.Where}")
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>⧉ ≡ ↹ are only names of buttons and keys, which <c>PadGlyph</c> and the keyboard draw as icons; never prose.</summary>
    [AvaloniaFact]
    [Trait("TestCase", "T24-11")]
    public void Symbols_no_embedded_font_has_appear_only_as_names_of_buttons()
    {
        var asText = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src", "Arcadia.App"), "*.axaml", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadAllLines(file).Select((line, i) => (Where: $"{Path.GetFileName(file)}:{i + 1}", Line: line)))
            .Where(entry => !entry.Line.TrimStart().StartsWith("<!--", StringComparison.Ordinal))
            .SelectMany(entry => ShownAttribute().Matches(entry.Line).Select(match => (entry.Where, match.Value)))
            .Where(entry => entry.Value.AsSpan().IndexOfAny(DrawnAsIcons) >= 0)
            .Select(entry => $"{entry.Where}: {entry.Value}")
            .ToList();

        Assert.Empty(asText);
    }

    // The characters of everything a view can show: string literals of the App's code (no comments, no log calls) and
    // the views' markup outside comments. Text elements, so a surrogate pair counts as one character.
    private static IEnumerable<(string Character, string Where)> UiCharacters()
    {
        var app = Path.Combine(RepoRoot(), "src", "Arcadia.App");
        var buildOutput = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        var sources = Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(buildOutput, StringComparison.Ordinal))
            .SelectMany(file => File.ReadAllLines(file).Select((line, i) => (Where: $"{Path.GetFileName(file)}:{i + 1}", Line: line.Trim())))
            .Where(entry => !entry.Line.StartsWith("//", StringComparison.Ordinal) && !LogCall().IsMatch(entry.Line))
            .SelectMany(entry => StringLiteral().Matches(entry.Line).Select(match => (entry.Where, Text: match.Groups[1].Value)));
        var views = Directory.EnumerateFiles(app, "*.axaml", SearchOption.AllDirectories)
            .SelectMany(file => WithoutComments(File.ReadAllText(file)).Split('\n').Select((line, i) => (Where: $"{Path.GetFileName(file)}:{i + 1}", Text: line)));

        foreach (var (where, text) in sources.Concat(views))
        {
            var elements = StringInfo.GetTextElementEnumerator(text);
            while (elements.MoveNext())
            {
                var character = (string)elements.Current;
                if (character[0] > 0x7F)
                {
                    yield return (character, where);
                }
            }
        }
    }

    // Comments are blanked line by line, so the line numbers stay right.
    private static string WithoutComments(string xaml) =>
        XamlComment().Replace(xaml, comment => new string(comment.Value.Select(c => c == '\n' ? '\n' : ' ').ToArray()));

    // Found from this source file, not from the test binaries: CI runs each shard from a copy of them outside the repo
    // (tools/ci/run-tests.sh).
    private static string RepoRoot([CallerFilePath] string thisFile = "")
    {
        var folder = new DirectoryInfo(Path.GetDirectoryName(thisFile)!);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Arcadia.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException($"Arcadia.slnx not found above {thisFile}.");
    }

    [GeneratedRegex("\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex StringLiteral();

    // A line that writes to the log; its texts go to arcadia.log, not to the screen.
    [GeneratedRegex(@"\bAppLog\.Write\(|(?<![\w.])_?log\(")]
    private static partial Regex LogCall();

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex XamlComment();

    // Attributes a view shows as plain text.
    [GeneratedRegex("\\b(?:Text|Content|ToolTip\\.Tip|Watermark|Title|Header)=\"[^\"]*\"")]
    private static partial Regex ShownAttribute();
}
