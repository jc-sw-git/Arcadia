using System.Text.RegularExpressions;
using System.Xml.Linq;
using Arcadia.Core.Localization;

namespace Arcadia.Core.Tests.Texts;

/// <summary>
/// #554: every language Arcadia offers has every text of every <c>*Texts.resx</c>, with the same placeholders as the
/// German one. A missing text would show in English at runtime (<c>UiText</c>), a missing or renamed placeholder would
/// leave a value out or show "{game}", so both fail here first.
/// </summary>
public sealed partial class TranslationsTests
{
    [Fact]
    [Trait("TestCase", "T554-17")]
    public void Every_language_has_every_text_with_the_German_placeholders()
    {
        var neutralFiles = NeutralFiles().ToList();
        var problems = neutralFiles
            .SelectMany(neutral => UiLanguages.Translations.SelectMany(language => ProblemsOf(neutral, language)))
            .ToList();

        Assert.True(neutralFiles.Count >= 7, $"Only {neutralFiles.Count} resource files found below src/.");
        Assert.True(problems.Count == 0, "Fix these translations (docs/architecture/ui-texts.md):\n" + string.Join("\n", problems));
    }

    [Fact]
    [Trait("TestCase", "T554-17")]
    public void No_translation_exists_for_a_language_Arcadia_does_not_offer()
    {
        var unknown = Directory.EnumerateFiles(Path.Combine(RepositoryFiles.Root(), "src"), "*Texts.*.resx", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .Select(file => Path.GetFileName(file))
            .Where(name => !UiLanguages.Translations.Contains(LanguageOf(name), StringComparer.Ordinal))
            .ToList();

        Assert.Empty(unknown);
    }

    [Theory]
    [Trait("TestCase", "T554-17")]
    [InlineData(new[] { "Cancel", "Abbrechen" }, new string[0], "Cancel: missing")]
    [InlineData(new[] { "Cancel", "Abbrechen" }, new[] { "Cancel", "Cancel", "Extra", "More" }, "Extra: not in the German texts")]
    [InlineData(new[] { "Starting", "„{game}“ wird gestartet …" }, new[] { "Starting", "Starting “{title}”…" }, "Starting: placeholders {title} instead of {game}")]
    [InlineData(new[] { "Starting", "„{game}“ wird in {launcher} gestartet …" }, new[] { "Starting", "Starting “{game}”…" }, "Starting: placeholders {game} instead of {game}, {launcher}")]
    public void A_missing_text_an_extra_one_or_other_placeholders_fail_naming_the_key(string[] german, string[] translated, string problem)
    {
        var problems = Compare("FixtureTexts.en.resx", Resx(german), Resx(translated)).ToList();

        Assert.Contains($"FixtureTexts.en.resx: {problem}", problems);
    }

    [Theory]
    [Trait("TestCase", "T554-17")]
    [InlineData("„{game}“ wird in {launcher} geöffnet …", "Opening “{game}” in {launcher}…")]
    [InlineData("Noch {minutes:0} Minuten", "{minutes:0.0} minutes left")]
    public void The_same_placeholders_in_another_order_or_format_pass(string german, string translated) =>
        Assert.Empty(Compare("FixtureTexts.en.resx", Resx(["Key", german]), Resx(["Key", translated])));

    private static IEnumerable<string> ProblemsOf(string neutral, string language)
    {
        var translation = Path.ChangeExtension(neutral, $".{language}.resx");
        var name = Path.GetFileName(translation);
        if (!File.Exists(translation))
        {
            return [$"{name}: missing; every {Path.GetFileName(neutral)} needs one per language"];
        }

        return Compare(name, XDocument.Load(neutral), XDocument.Load(translation));
    }

    private static IEnumerable<string> Compare(string name, XDocument german, XDocument translated)
    {
        var germanTexts = Texts(german);
        var translatedTexts = Texts(translated);
        foreach (var (key, text) in germanTexts)
        {
            if (!translatedTexts.TryGetValue(key, out var translation))
            {
                yield return $"{name}: {key}: missing";
                continue;
            }

            var expected = PlaceholdersOf(text);
            var actual = PlaceholdersOf(translation);
            if (!expected.SequenceEqual(actual))
            {
                yield return $"{name}: {key}: placeholders {Listed(actual)} instead of {Listed(expected)}";
            }
        }

        foreach (var key in translatedTexts.Keys.Where(key => !germanTexts.ContainsKey(key)))
        {
            yield return $"{name}: {key}: not in the German texts";
        }
    }

    private static Dictionary<string, string> Texts(XDocument resx) =>
        resx.Descendants("data").Where(data => data.Attribute("type") is null)
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? string.Empty, StringComparer.Ordinal);

    // The names only: a translation may write a number with another format ({minutes:0.0}) or move it.
    private static List<string> PlaceholdersOf(string text) =>
        [.. Placeholder().Matches(text).Select(match => match.Groups[1].Value).Distinct().Order(StringComparer.Ordinal)];

    private static string Listed(List<string> names) => names.Count == 0 ? "none" : string.Join(", ", names.Select(name => "{" + name + "}"));

    private static IEnumerable<string> NeutralFiles() =>
        Directory.EnumerateFiles(Path.Combine(RepositoryFiles.Root(), "src"), "*Texts.resx", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file));

    private static string LanguageOf(string fileName) => fileName.Split('.')[^2];

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static XDocument Resx(string[] keysAndTexts) =>
        new(new XElement("root", keysAndTexts.Chunk(2).Select(pair =>
            new XElement("data", new XAttribute("name", pair[0]), new XElement("value", pair[1])))));

    [GeneratedRegex(@"\{([A-Za-z][A-Za-z0-9]*)(?::[^{}]*)?\}")]
    private static partial Regex Placeholder();
}
