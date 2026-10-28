using Arcadia.ControllerSupport.Pan;

namespace Arcadia.Core.Tests;

/// <summary>Compatible means exactly the bundled Pan (#297): any other exe, newer or older, or none readable, is replaced.</summary>
public sealed class PanCompatibilityTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("arcadia-pan-compat-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string Exe(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void The_bundled_exe_byte_for_byte_is_compatible()
    {
        var bundled = PanCompatibilityCheck.HashOf(Exe("bundled.exe", "Pan 1.2"));
        var installed = Exe("installed.exe", "Pan 1.2");

        Assert.Equal(PanCompatibility.Compatible, PanCompatibilityCheck.Judge(true, true, () => bundled, () => PanCompatibilityCheck.HashOf(installed)));
    }

    [Theory]
    [InlineData("Pan 1.1")] // older, after an Arcadia update
    [InlineData("Pan 1.3")] // newer, after an Arcadia downgrade
    [InlineData("Pan 1.2 rebuilt")] // same file version, other build
    public void Any_other_exe_must_be_replaced(string content)
    {
        var bundled = PanCompatibilityCheck.HashOf(Exe("bundled.exe", "Pan 1.2"));
        var installed = Exe("installed.exe", content);

        Assert.Equal(PanCompatibility.UpdateRequired, PanCompatibilityCheck.Judge(true, true, () => bundled, () => PanCompatibilityCheck.HashOf(installed)));
    }

    [Fact]
    public void An_installed_exe_that_is_missing_or_unreadable_counts_as_incompatible()
    {
        var bundled = PanCompatibilityCheck.HashOf(Exe("bundled.exe", "Pan 1.2"));

        Assert.Null(PanCompatibilityCheck.HashOf(Path.Combine(_directory, "missing.exe")));
        Assert.Equal(PanCompatibility.UpdateRequired, PanCompatibilityCheck.Judge(true, true, () => bundled, () => PanCompatibilityCheck.HashOf(Path.Combine(_directory, "missing.exe"))));

        // Locked by another process, as a running service's exe may be for a moment.
        var locked = Exe("locked.exe", "Pan 1.2");
        using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Null(PanCompatibilityCheck.HashOf(locked));
        }
    }

    [Fact]
    public void Without_Pan_installed_nothing_is_read_or_due()
    {
        var read = false;

        Assert.Equal(PanCompatibility.NotInstalled, PanCompatibilityCheck.Judge(false, true, () => "ABC", () =>
        {
            read = true;
            return null;
        }));
        Assert.False(read);
    }

    [Fact]
    public void Without_a_bundled_Pan_nothing_can_be_compared()
    {
        Assert.Equal(PanCompatibility.NotBundled, PanCompatibilityCheck.Judge(true, false, () => null, () => "ABC"));
    }

    [Fact]
    public void A_bundled_exe_that_can_t_be_read_counts_as_incompatible_not_as_missing()
    {
        Assert.Equal(PanCompatibility.UpdateRequired, PanCompatibilityCheck.Judge(true, true, () => null, () => "ABC"));
    }
}
