using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>
/// <c>--uninstall</c> from Arcadia's folder hands off to a verified copy in Program Files (#270), so Velopack can delete
/// the bundled exe; anything failing falls back to the uninstall in place.
/// </summary>
public sealed class UninstallHandOffTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-pan-tests", Guid.NewGuid().ToString("N"));
    private readonly List<string> _log = [];
    private readonly List<string> _started = [];

    public UninstallHandOffTests()
    {
        Directory.CreateDirectory(Bundled);
        File.WriteAllBytes(BundledExe, [1, 2, 3, 4, 5]);
    }

    private string Bundled => Path.Combine(_root, "LocalAppData", "Arcadia", "current", "Pan");

    private string BundledExe => Path.Combine(Bundled, "Pan.exe");

    private string ArcadiaProgramFiles => Path.Combine(_root, "Program Files", "Arcadia");

    private string HandOff => Path.Combine(ArcadiaProgramFiles, UninstallHandOff.DirectoryName);

    private string Copy => Path.Combine(HandOff, "Pan.exe");

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private HandOffResult HandOffFrom(string ownPath, Action<string>? start = null) =>
        UninstallHandOff.TryHandOff(ownPath, ArcadiaProgramFiles, HandOff, start ?? _started.Add, _log.Add);

    [Fact]
    [Trait("TestCase", "T270-4")]
    public void From_Arcadias_folder_the_verified_copy_is_started_and_the_bundled_Pan_stays()
    {
        Assert.Equal(HandOffResult.HandedOff, HandOffFrom(BundledExe));

        Assert.Equal([Copy], _started);
        Assert.Equal(File.ReadAllBytes(BundledExe), File.ReadAllBytes(Copy));
        Assert.Contains(_log, line => line.Contains("Handing the uninstall off", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Pan")]
    [InlineData(UninstallHandOff.DirectoryName)]
    public void From_Program_Files_Pan_uninstalls_itself(string folder)
    {
        var installed = Path.Combine(ArcadiaProgramFiles, folder, "Pan.exe");

        Assert.Equal(HandOffResult.NotNeeded, HandOffFrom(installed));
        Assert.Empty(_started);
    }

    [Fact]
    [Trait("TestCase", "T270-5")]
    public void A_copy_that_fails_leaves_the_uninstall_in_place_and_the_log_says_why()
    {
        // A file where the folder belongs: the folder can't be created, as with a scanner or a full disk.
        Directory.CreateDirectory(ArcadiaProgramFiles);
        File.WriteAllText(HandOff, "in the way");

        Assert.Equal(HandOffResult.RunInPlace, HandOffFrom(BundledExe));

        Assert.Empty(_started);
        Assert.Contains(_log, line => line.StartsWith("Uninstalling in place, the hand-off copy failed: ", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T270-5")]
    public void A_copy_that_cant_be_started_leaves_the_uninstall_in_place_and_goes_again()
    {
        Assert.Equal(HandOffResult.RunInPlace, HandOffFrom(BundledExe, _ => throw new System.ComponentModel.Win32Exception(5, "Access is denied")));

        Assert.False(File.Exists(Copy), "the copy that didn't start is deleted");
        Assert.Contains(_log, line => line.StartsWith("Uninstalling in place, the hand-off copy could not be started: Access is denied", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Pan_that_needs_its_dll_beside_it_is_not_handed_off()
    {
        File.WriteAllBytes(Path.Combine(Bundled, "Pan.dll"), [0]);

        Assert.Equal(HandOffResult.RunInPlace, HandOffFrom(BundledExe));

        Assert.Empty(_started);
        Assert.False(Directory.Exists(HandOff));
    }

    [Fact]
    [Trait("TestCase", "T270-6")]
    public void A_leftover_hand_off_folder_goes_at_the_next_install_or_uninstall()
    {
        Directory.CreateDirectory(HandOff);
        File.WriteAllBytes(Copy, [9]);

        UninstallHandOff.RemoveLeftover(BundledExe, HandOff, _log.Add);

        Assert.False(Directory.Exists(HandOff));
        Assert.Contains(_log, line => line.StartsWith("Deleted the leftover ", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T270-6")]
    public void The_hand_off_copy_leaves_its_own_folder_to_the_reboot()
    {
        Directory.CreateDirectory(HandOff);
        File.WriteAllBytes(Copy, [9]);

        UninstallHandOff.RemoveLeftover(Copy, HandOff, _log.Add);

        Assert.True(File.Exists(Copy));
        Assert.Empty(_log);
    }

    [Theory]
    [InlineData(@"Arcadia-Old", false)]
    [InlineData(@"Arcadia", true)]
    public void Only_paths_inside_the_folder_count_not_ones_that_merely_start_like_it(string folder, bool inside)
    {
        var path = Path.Combine(_root, "Program Files", folder, "Pan", "Pan.exe");

        Assert.Equal(inside, UninstallHandOff.IsInside(path, ArcadiaProgramFiles));
    }
}
