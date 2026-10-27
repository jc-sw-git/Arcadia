using System.Runtime.Versioning;
using Arcadia.Pan.Windows;

namespace Arcadia.Pan.Tests;

/// <summary>The real uninstall entries, read only (nothing is removed): where the CI installed HidHide 1.5.230.</summary>
[SupportedOSPlatform("windows")]
public sealed class DriverRemovalTests
{
    [HidHideFact]
    [Trait("TestCase", "T243-38")]
    public void The_real_HidHide_entry_gives_Pan_an_msiexec_command_by_its_product_code()
    {
        var command = DriverUninstall.ForHidHide(DriverRemoval.InstalledEntries(), Environment.SystemDirectory);

        Assert.NotNull(command);
        Assert.Equal("/x", command.Arguments[0]);
        Assert.True(Guid.TryParseExact(command.Arguments[1], "B", out _), command.Arguments[1]);
    }
}
