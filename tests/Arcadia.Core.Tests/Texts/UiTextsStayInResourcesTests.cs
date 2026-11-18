namespace Arcadia.Core.Tests.Texts;

/// <summary>
/// #480: every text the player reads comes from a <c>*Texts.resx</c>, so a text change or a translation has one
/// place. Pan and Pan.Core are left out: Pan runs as a Windows service without Arcadia's assemblies and keeps its few
/// texts itself (<c>docs/architecture/ui-texts.md</c>).
/// </summary>
public sealed class UiTextsStayInResourcesTests
{
    private static readonly string[] ProjectsWithTexts =
        ["Arcadia.App", "Arcadia.ControllerSupport", "Arcadia.Core", "Arcadia.Input", "Arcadia.Launchers", "Arcadia.Launchers.Abstractions", "Arcadia.Platform"];

    [Fact]
    [Trait("TestCase", "T480-15")]
    public void No_German_text_is_left_in_the_code()
    {
        var offending = ProjectsWithTexts
            .SelectMany(project => RepositoryFiles.SourceFiles(project, "*.cs"))
            .SelectMany(file => GermanUiLiterals.InCSharp(File.ReadAllText(file)).Select(hit => $"{Relative(file)}:{hit}"))
            .ToList();

        Assert.True(offending.Count == 0, Explain(offending));
    }

    [Fact]
    [Trait("TestCase", "T480-15")]
    public void No_text_is_written_into_a_view()
    {
        var offending = RepositoryFiles.SourceFiles("Arcadia.App", "*.axaml")
            .SelectMany(file => GermanUiLiterals.InAxaml(File.ReadAllText(file)).Select(hit => $"{Relative(file)}:{hit}"))
            .ToList();

        Assert.True(offending.Count == 0, Explain(offending));
    }

    [Fact]
    [Trait("TestCase", "T480-9")]
    public void A_text_with_two_or_more_values_is_called_with_named_arguments()
    {
        var textClasses = GeneratedTextClasses();
        var offending = CSharpFiles(Path.Combine(RepositoryFiles.Root(), "src"))
            .Concat(CSharpFiles(Path.Combine(RepositoryFiles.Root(), "tests")))
            .SelectMany(file => TextCalls.WithoutNames(File.ReadAllText(file), textClasses).Select(hit => $"{Relative(file)}:{hit}"))
            .ToList();

        Assert.True(offending.Count == 0, "Name the values, e.g. AppTexts.Key(game: title, launcher: name), so a reworded text can't swap them:\n" + string.Join("\n", offending));
    }

    [Theory]
    [Trait("TestCase", "T480-9")]
    [InlineData("""var text = AppTexts.GameOpening(title, launcher);""", 1)]
    [InlineData("""var text = AppTexts.GameOpening(game: title, launcher: name);""", 0)]
    [InlineData("""var text = AppTexts.GameStarting(title);""", 0)]
    [InlineData("""var text = Path.Combine(folder, file);""", 0)]
    public void A_text_call_with_unnamed_values_is_found(string code, int expected) =>
        Assert.Equal(expected, TextCalls.WithoutNames($"class C {{ void M() {{ {code} }} }}", new HashSet<string> { "AppTexts" }).Count());

    [Theory]
    [Trait("TestCase", "T480-15")]
    [InlineData("""var title = "Abbrechen";""")]
    [InlineData("""Subtitle = $"{count} Spiele gefunden";""")]
    [InlineData("""return game is null ? "Läuft" : "";""")]
    [InlineData("""Show(HealthSeverity.Info, $"„{tile.Title}“ wird gestartet");""")]
    [InlineData("""var badge = "Pausiert";""")]
    [InlineData("""return $"Vor {days} Tagen";""")]
    [InlineData("""var urgency = "Dringlichkeit";""")]
    [InlineData("""var heading = new InfoHeading("GRENZEN");""")]
    [InlineData("""var facts = new InfoFacts("ORT", rows);""")]
    [InlineData("""throw new InvalidOperationException("Der Puck meldet eine ungültige Seriennummer");""")]
    public void Code_with_a_German_text_fails(string code) =>
        Assert.NotEmpty(GermanUiLiterals.InCSharp($"class C {{ void M() {{ {code} }} }}"));

    [Theory]
    [Trait("TestCase", "T480-15")]
    [InlineData("""AppLog.Write($"Spiel {key} gestartet");""")]
    [InlineData("""_log("Controller: Gerät geöffnet");""")]
    [InlineData("""Log?.Invoke($"Pan geprüft in {ms} ms");""")]
    [InlineData("""throw new InvalidOperationException("Pan ist nicht verbunden"); // not a UI text (protocol check)""")]
    [InlineData("""var name = AppTexts.Cancel;""")]
    [InlineData("""var file = Path.Combine(folder, "arcadia.log");""")]
    [InlineData("""var url = "https://github.com/jc-sw-git/Arcadia";""")]
    [InlineData("""var brand = "Steam Controller";""")]
    public void Logs_marked_exceptions_keys_files_urls_and_names_pass(string code) =>
        Assert.Empty(GermanUiLiterals.InCSharp($"class C {{ void M() {{ {code} }} }}"));

    [Theory]
    [Trait("TestCase", "T480-15")]
    [InlineData("""<TextBlock Text="Abbrechen" />""")]
    [InlineData("""<Button ToolTip.Tip="Schließen (Esc)" />""")]
    [InlineData("""<Style><Setter Property="Text" Value="Weiter" /></Style>""")]
    [InlineData("""<TextBlock>Bibliothek wird geladen</TextBlock>""")]
    [InlineData("""<KeyHint Pad="A" Keys="Leertaste" />""")]
    [InlineData("""<KeyHint Pad="A" Keys="Esc" Alt="Klick" />""")]
    [InlineData("""<TextBlock Text="{Binding Hint, TargetNullValue='Spielen'}" />""")]
    public void A_view_with_a_written_text_fails(string element) =>
        Assert.NotEmpty(GermanUiLiterals.InAxaml($"<UserControl xmlns=\"https://github.com/avaloniaui\">{element}</UserControl>"));

    [Theory]
    [Trait("TestCase", "T480-15")]
    [InlineData("""<TextBlock Text="{x:Static app:AppTexts.Cancel}" />""")]
    [InlineData("""<TextBlock Text="{Binding Title}" />""")]
    [InlineData("""<TextBlock Text="ARCADIA" />""")]
    [InlineData("""<TextBlock Text="Esc" />""")]
    [InlineData("""<KeyHint Pad="D-Pad" Keys="WASD" Alt="{x:Static app:AppTexts.Click}" />""")]
    [InlineData("""<KeyHint Pad="LB|RB" Keys="⇧ Q|⇧ E" Alt="1 … 0" />""")]
    [InlineData("""<KeyHint Keys="Enter" />""")]
    [InlineData("""<TextBlock IsVisible="{Binding Shown, FallbackValue=False, TargetNullValue='False'}" />""")]
    public void A_view_with_resources_bindings_and_names_passes(string element) =>
        Assert.Empty(GermanUiLiterals.InAxaml($"<UserControl xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"x\" xmlns:app=\"app\">{element}</UserControl>"));

    // The classes the generator makes, one per *Texts.resx; hand-written helpers such as LauncherTexts are not among them.
    private static HashSet<string> GeneratedTextClasses() =>
        Directory.EnumerateFiles(RepositoryFiles.Root(), "*Texts.resx", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension).OfType<string>().ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> CSharpFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string Explain(IReadOnlyList<string> offending) =>
        "Move these texts into the project's *Texts.resx (docs/architecture/ui-texts.md):\n" + string.Join("\n", offending);

    private static string Relative(string file) => Path.GetRelativePath(Path.Combine(RepositoryFiles.Root(), "src"), file);
}
