using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>
/// #382, S2: <c>--install</c> keeps an installed Pan that already is the bundled one, so a HidHide install doesn't
/// recreate the service (and end a running emulation). Pan sets the service's configuration again in place then
/// (<c>ServiceSetup.KeepUnchanged</c>); the data folder is secured before, whatever this answers.
/// </summary>
public sealed class UnchangedPanTests
{
    private const string Bundled = "AB12CD";

    [Fact]
    [Trait("TestCase", "T382-3")]
    public void The_same_exe_with_its_service_is_kept()
    {
        Assert.True(UnchangedPan.IsKept(installedHash: Bundled, sourceHash: Bundled, expectedHash: Bundled, serviceExists: true));
        Assert.True(UnchangedPan.IsKept(installedHash: "ab12cd", sourceHash: Bundled, expectedHash: null, serviceExists: true));
    }

    [Theory]
    [Trait("TestCase", "T382-4")]
    [InlineData(Bundled, Bundled, Bundled, false)] // no service: created as before
    [InlineData("FF0000", Bundled, Bundled, true)] // another Pan installed: replaced, service recreated (#326)
    [InlineData(null, Bundled, Bundled, true)] // no installed exe
    [InlineData(Bundled, Bundled, "FF0000", true)] // Arcadia expects another Pan than the one it started: installs, and the staging refuses
    public void Anything_else_installs_as_before(string? installed, string source, string? expected, bool serviceExists)
    {
        Assert.False(UnchangedPan.IsKept(installed, source, expected, serviceExists));
    }
}
