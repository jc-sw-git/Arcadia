using System.Text.RegularExpressions;

namespace Arcadia.Core.Tests;

/// <summary>
/// Definition of Done rules (CLAUDE.md) that hold for every contributor and can be read off the source: checked here so
/// they hold without anyone having to remember them.
/// </summary>
public sealed partial class SourceRuleTests
{
    [Fact]
    public void The_App_drops_no_task_unguarded()
    {
        // A discarded task loses its exception unseen; work nobody awaits goes through AppErrors.Guard / .Forget(context).
        var offending = LinesMatching("Arcadia.App", "*.cs", DiscardedTask());

        Assert.Empty(offending);
    }

    [Fact]
    public void Views_and_styles_take_colours_only_from_the_theme()
    {
        // A theme replaces all colours in one place only if no view or style carries its own (Themeable).
        var offending = LinesMatching("Arcadia.App", "*.axaml", ColourLiteral())
            .Where(line => !line.StartsWith($"Theme{Path.DirectorySeparatorChar}Theme.axaml:", StringComparison.Ordinal));

        Assert.Empty(offending);
    }

    private static List<string> LinesMatching(string project, string pattern, Regex rule)
    {
        var folder = Path.Combine(RepositoryFiles.Root(), "src", project);
        return [.. RepositoryFiles.SourceFiles(project, pattern)
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, number: index + 1)))
            .Where(entry => rule.IsMatch(entry.line))
            .Select(entry => $"{Path.GetRelativePath(folder, entry.file)}:{entry.number}: {entry.line.Trim()}")];
    }

    [GeneratedRegex(@"\b_\s*=\s*[\w.]+Async\s*\(")]
    private static partial Regex DiscardedTask();

    [GeneratedRegex(@"""#[0-9A-Fa-f]{3,8}""|\b(rgba?|hsla?)\(")]
    private static partial Regex ColourLiteral();
}
