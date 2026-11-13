using System.Text.RegularExpressions;

namespace Arcadia.Core.Tests;

/// <summary>
/// Every warning the header's status chip stands for needs its own symbol in the chip: without one the chip showed only
/// the button glyph for a rejected IGDB access (#538). Checked on the source, since the chip lists its symbols by hand.
/// </summary>
public sealed partial class StatusChipIconTests
{
    [Fact]
    [Trait("TestCase", "T538-7")]
    public void Every_warning_of_the_status_bar_has_a_symbol_in_the_header_chip()
    {
        var app = Path.Combine(RepoRoot(), "src", "Arcadia.App");
        var statusBar = File.ReadAllText(Path.Combine(app, "ViewModels", "StatusBarViewModel.cs"));
        var window = File.ReadAllText(Path.Combine(app, "Views", "MainWindow.axaml"));

        var warnings = WarningProperty().Matches(statusBar).Select(m => m.Groups["name"].Value).ToList();

        Assert.Contains("HasIgdbWarning", warnings);
        Assert.All(warnings, name => Assert.Contains($"{{Binding Status.{name}}}", window));
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

        throw new InvalidOperationException("Arcadia.slnx not found above the test's folder.");
    }

    [GeneratedRegex(@"public\s+partial\s+bool\s+(?<name>Has\w+Warning)\b")]
    private static partial Regex WarningProperty();
}
