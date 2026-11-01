using System.Reflection;
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
    public void Built_in_launchers_use_only_the_launcher_contract() =>
        Assert.Equal(["Arcadia.Launchers.Abstractions"], ArcadiaReferencesOf(typeof(BuiltInLaunchers).Assembly));

    [Fact]
    public void Core_knows_launchers_only_through_the_contract() =>
        Assert.Equal(["Arcadia.Launchers.Abstractions"], ArcadiaReferencesOf(typeof(LauncherRegistry).Assembly));
}
