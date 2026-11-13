using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using System.Xml.Linq;
using Arcadia.Launchers.Abstractions.Texts;

namespace Arcadia.Core.Tests.Texts;

/// <summary>
/// #339: every text of every resource file, translations included (#554), filled with a sample value per placeholder, comes out whole: no brace left,
/// no key shown, nothing logged. A broken placeholder would otherwise reach the player as "{game}" or a raw brace and
/// only show up in <c>arcadia.log</c>. It runs for every PR, contributors' included, as a normal test.
/// </summary>
[Collection(UiTextTests.Name)]
public sealed class ResourceTextPlaceholderTests : IDisposable
{
    private readonly ConcurrentQueue<string> _logged = new();

    private readonly Action<string>? _previousLog = UiText.Log;

    public ResourceTextPlaceholderTests() => UiText.Log = _logged.Enqueue;

    public void Dispose() => UiText.Log = _previousLog;

    [Fact]
    [Trait("TestCase", "T339-1")]
    [Trait("TestCase", "T339-3")]
    public void Every_resource_text_filled_with_sample_values_comes_out_whole()
    {
        var files = Directory.EnumerateFiles(Path.Combine(RepositoryFiles.Root(), "src"), "*Texts*.resx", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        var problems = files.SelectMany(file => Problems(file, XDocument.Load(file))).ToList();

        Assert.True(files.Count >= 7, $"Only {files.Count} resource files found below src/.");
        Assert.True(problems.Count == 0, "Fix these texts (docs/architecture/ui-texts.md):\n" + string.Join("\n", problems));
        Assert.Empty(_logged);
    }

    [Theory]
    [Trait("TestCase", "T339-2")]
    [InlineData("„{game“ wird gestartet", "unclosed {")]
    [InlineData("„game}“ wird gestartet", "stray }")]
    [InlineData("„{}“ wird gestartet", "empty {}")]
    [InlineData("„{0}“ wird gestartet", "{0}")]
    [InlineData("„{Spiel}“ wird gestartet", "{Spiel}")]
    public void A_broken_placeholder_fails_naming_file_and_key(string text, string problem)
    {
        var problems = Problems("FixtureTexts.resx", Resx(("GameStarting", text))).ToList();

        var only = Assert.Single(problems);
        Assert.Contains("FixtureTexts.resx: GameStarting:", only, StringComparison.Ordinal);
        Assert.Contains(problem, only, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("TestCase", "T339-1")]
    [InlineData("„{game}“ wird in {launcher} geöffnet …")]
    [InlineData("Pan war nach {seconds:0} Sekunden nicht erreichbar.")]
    [InlineData("Zuletzt gesucht: {time:HH:mm} Uhr.")]
    [InlineData("Keine Platzhalter")]
    public void A_whole_text_passes(string text) =>
        Assert.Empty(Problems("FixtureTexts.resx", Resx(("Fixture", text))));

    private static IEnumerable<string> Problems(string file, XDocument resx)
    {
        var name = Path.GetFileName(file);
        foreach (var data in resx.Descendants("data"))
        {
            var key = (string?)data.Attribute("name") ?? string.Empty;
            var text = (string?)data.Element("value") ?? string.Empty;
            if (BraceProblem(text) is { } problem)
            {
                yield return $"{name}: {key}: {problem}";
                continue;
            }

            var filled = UiText.Fill(new OneText(key, text), key, [.. Placeholders(text).Select(placeholder => (placeholder.Name, SampleValue(placeholder.Format)))]);
            if (filled.Contains('{', StringComparison.Ordinal) || filled.Contains('}', StringComparison.Ordinal) || filled == key && text != key)
            {
                yield return $"{name}: {key}: not filled whole: {filled}";
            }
        }
    }

    // What UiText reads as text and not as a placeholder must not be in a resource file: the player would see it.
    private static string? BraceProblem(string text)
    {
        var position = 0;
        while (position < text.Length)
        {
            var open = text.IndexOf('{', position);
            var strayClose = text.IndexOf('}', position);
            if (strayClose >= 0 && (open < 0 || strayClose < open))
            {
                return "stray } without {";
            }

            if (open < 0)
            {
                return null;
            }

            var close = text.IndexOf('}', open + 1);
            var nextOpen = text.IndexOf('{', open + 1);
            if (close < 0 || nextOpen >= 0 && nextOpen < close)
            {
                return "unclosed { without }";
            }

            var placeholder = text[(open + 1)..close];
            var nameEnd = placeholder.IndexOf(':', StringComparison.Ordinal);
            var placeholderName = nameEnd < 0 ? placeholder : placeholder[..nameEnd];
            if (placeholderName.Length == 0)
            {
                return "empty {} without a name";
            }

            if (!IsEnglishName(placeholderName))
            {
                return $"placeholder {{{placeholder}}} is not an English name in camelCase, e.g. {{game}}";
            }

            position = close + 1;
        }

        return null;
    }

    private static bool IsEnglishName(string name) => char.IsAsciiLetterLower(name[0]) && name.All(char.IsAsciiLetterOrDigit);

    private static IEnumerable<(string Name, string? Format)> Placeholders(string text)
    {
        var position = 0;
        while (text.IndexOf('{', position) is var open and >= 0)
        {
            var close = text.IndexOf('}', open + 1);
            var placeholder = text[(open + 1)..close];
            var colon = placeholder.IndexOf(':', StringComparison.Ordinal);
            yield return colon < 0 ? (placeholder, null) : (placeholder[..colon], placeholder[(colon + 1)..]);
            position = close + 1;
        }
    }

    // A format with date or time letters ({time:HH:mm}) gets a time, any other format ({seconds:0}) a number.
    private static object SampleValue(string? format) => format switch
    {
        null => "Beispiel",
        _ when format.Any(letter => "Hhmsdy".Contains(letter, StringComparison.Ordinal)) => new DateTime(2026, 1, 2, 13, 45, 0, DateTimeKind.Local),
        _ => 42.5,
    };

    private static XDocument Resx(params (string Key, string Text)[] texts) =>
        new(new XElement("root", texts.Select(text =>
            new XElement("data", new XAttribute("name", text.Key), new XElement("value", text.Text)))));

    private sealed class OneText(string key, string text) : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture) => name == key ? text : null;
    }
}
