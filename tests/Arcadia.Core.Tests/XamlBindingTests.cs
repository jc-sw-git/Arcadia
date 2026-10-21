using System.Text.RegularExpressions;

namespace Arcadia.Core.Tests;

/// <summary>
/// A binding path through a property that can be null logs a binding error each time it is null, and a null
/// reaching a bool target logs one too (CLAUDE.md → Avalonia 12). New views brought such bindings back again and
/// again, so the rule is checked here on the view files rather than only written down.
/// </summary>
public sealed partial class XamlBindingTests
{
    [Fact]
    public void Bindings_go_through_nullable_properties_only_with_null_conditional()
    {
        var app = Path.Combine(RepoRoot(), "src", "Arcadia.App");
        var nullable = NullablePropertiesIn(app);
        var throughNullable = new Regex($@"(?:Binding\s+|Path=""?)!?(?:{string.Join('|', nullable)})\.\w");

        var offending = ViewLines(app)
            .Where(l => throughNullable.IsMatch(l.Text) || UncheckedWindowDataContext().IsMatch(l.Text))
            .Select(l => l.Where)
            .ToList();

        Assert.True(offending.Count == 0, string.Join(Environment.NewLine, offending));
    }

    [Fact]
    public void A_null_conditional_binding_to_a_bool_target_has_a_target_null_value()
    {
        var app = Path.Combine(RepoRoot(), "src", "Arcadia.App");

        var offending = ViewLines(app)
            .Where(l => BoolTargetBinding().Matches(l.Text).Any(m => m.Value.Contains("?.") && !m.Value.Contains("TargetNullValue")))
            .Select(l => l.Where)
            .ToList();

        Assert.True(offending.Count == 0, string.Join(Environment.NewLine, offending));
    }

    /// <summary>Names of the App's properties declared nullable, e.g. <c>Detail</c> or <c>FocusedGame</c>.</summary>
    private static string[] NullablePropertiesIn(string app) =>
        SourceFiles(app, "*.cs")
            .SelectMany(f => NullableProperty().Matches(File.ReadAllText(f)))
            .Select(m => m.Groups["name"].Value)
            .Distinct()
            .ToArray();

    private static IEnumerable<(string Text, string Where)> ViewLines(string app) =>
        SourceFiles(app, "*.axaml").SelectMany(f => File.ReadLines(f).Select((text, i) =>
            (text, $"{Path.GetRelativePath(app, f)}:{i + 1}: {text.Trim()}")));

    private static IEnumerable<string> SourceFiles(string folder, string pattern) =>
        Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Arcadia.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Arcadia.slnx not found above the test's folder.");
    }

    [GeneratedRegex(@"public\s+(?:partial\s+)?[\w<>.,]+\?\s+(?<name>\w+)\s*(?:\{|=>)")]
    private static partial Regex NullableProperty();

    /// <summary>The window's view model is null until it is set: <c>$parent[Window].((vm:X)DataContext).Prop</c>.</summary>
    [GeneratedRegex(@"\)DataContext\)\.\w")]
    private static partial Regex UncheckedWindowDataContext();

    [GeneratedRegex(@"(?:IsVisible|IsEnabled|Classes\.[\w-]+)=""\{Binding [^}]*\}""")]
    private static partial Regex BoolTargetBinding();
}
