using System.Reflection;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests;

/// <summary>
/// #373: a built-in launcher uses only the launcher contract, as a plugin would, and the layers above don't reach into a
/// launcher. Checked on the compiled assemblies, which keep a reference only when a type of it is used.
/// </summary>
public sealed class ArchitectureTests
{
    private static IReadOnlyList<string> ArcadiaReferencesOf(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("Arcadia.", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

    [Fact]
    public void The_launcher_contract_depends_on_nothing_of_Arcadia() =>
        Assert.Empty(ArcadiaReferencesOf(typeof(ILauncherIntegration).Assembly));

    [Fact]
    public void Built_in_launchers_use_only_the_launcher_contract_and_the_process_helpers() =>
        Assert.Equal(["Arcadia.Launchers.Abstractions", "Arcadia.Platform"], ArcadiaReferencesOf(typeof(BuiltInLaunchers).Assembly));

    [Fact]
    public void The_controller_support_knows_Steam_only_as_a_launcher_program() =>
        Assert.DoesNotContain("Arcadia.Launchers", ArcadiaReferencesOf(typeof(ControllerSupportService).Assembly));

    [Fact]
    public void The_App_names_no_single_launcher()
    {
        // The App composes the built-in launchers (BuiltInLaunchers) but reaches none of them directly; a reference to a
        // launcher's own namespace would be a special case a plugin launcher couldn't get.
        var app = Path.Combine(RepositoryRoot(), "src", "Arcadia.App");
        var offending = Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadLines(file).Any(line => SingleLauncherNames.Any(ns => line.Contains(ns, StringComparison.Ordinal))))
            .Select(file => Path.GetRelativePath(app, file))
            .ToList();

        Assert.Empty(offending);
    }

    // KnownLaunchers names Steam and Epic too, by their identity.
    private static readonly string[] SingleLauncherNames = ["Arcadia.Launchers.Steam", "Arcadia.Launchers.Epic", "Arcadia.Launchers.Demo", "KnownLaunchers"];

    private static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Arcadia.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new InvalidOperationException("Arcadia.slnx not found above the test's folder.");
    }

    [Fact]
    public void Core_knows_launchers_only_through_the_contract() =>
        Assert.Equal(["Arcadia.Launchers.Abstractions"], ArcadiaReferencesOf(typeof(LauncherRegistry).Assembly));
}
