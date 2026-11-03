using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>Where Pan looks for Arcadia at its start, and the installs <c>--install</c> records (#422).</summary>
public sealed class ArcadiaInstallationsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-pan-422-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static Func<string, ArcadiaPresence> Probe(params (string File, ArcadiaPresence Presence)[] known) =>
        file => known.FirstOrDefault(k => k.File == file) is { File: not null } hit ? hit.Presence : ArcadiaPresence.Absent;

    [Fact]
    [Trait("TestCase", "T422-5")]
    public void Arcadia_of_any_user_counts()
    {
        var other = Path.Combine(_root, "Users", "Alex");
        var probe = Probe((ArcadiaInstallations.VelopackExecutable(other), ArcadiaPresence.Found));

        Assert.Equal(ArcadiaPresence.Found, ArcadiaInstallations.Look([Path.Combine(_root, "Users", "Sam"), other], [], probe));
    }

    [Fact]
    [Trait("TestCase", "T422-5")]
    public void A_profile_that_can_t_be_read_counts_as_Arcadia_may_be_there()
    {
        var locked = Path.Combine(_root, "Users", "Alex");
        var probe = Probe((ArcadiaInstallations.VelopackExecutable(locked), ArcadiaPresence.Unknown));

        Assert.Equal(ArcadiaPresence.Unknown, ArcadiaInstallations.Look([Path.Combine(_root, "Users", "Sam"), locked], [], probe));
    }

    [Fact]
    [Trait("TestCase", "T422-5")]
    public void A_profile_list_that_can_t_be_read_keeps_Pan()
    {
        Assert.Equal(ArcadiaPresence.Unknown, ArcadiaInstallations.Look(null, [], Probe()));
    }

    [Fact]
    [Trait("TestCase", "T422-4")]
    public void No_Arcadia_anywhere_is_absent()
    {
        Assert.Equal(ArcadiaPresence.Absent, ArcadiaInstallations.Look([Path.Combine(_root, "Users", "Sam")], [Path.Combine(_root, "dev", "Arcadia.exe")], Probe()));
    }

    [Fact]
    [Trait("TestCase", "T422-10")]
    public void A_recorded_build_without_the_installer_counts()
    {
        var build = Path.Combine(_root, "dev", "bin", "Arcadia.exe");

        Assert.Equal(ArcadiaPresence.Found, ArcadiaInstallations.Look([], [build], Probe((build, ArcadiaPresence.Found))));
    }

    [Fact]
    [Trait("TestCase", "T422-10")]
    public void The_Arcadia_beside_a_bundled_Pan_is_the_one_recorded()
    {
        var arcadia = Path.Combine(_root, "dev", "bin");

        Assert.Equal(Path.Combine(arcadia, "Arcadia.exe"), ArcadiaInstallations.BesideBundledPan(Path.Combine(arcadia, "Pan", "Pan.exe")));
    }

    [Fact]
    [Trait("TestCase", "T422-10")]
    public void Recorded_installs_keep_the_newest_once_each_and_drop_lines_that_are_not_an_Arcadia_exe()
    {
        var file = Path.Combine(_root, RecordedInstallsFile.FileName);
        var record = new RecordedInstallsFile(file);
        var paths = Enumerable.Range(0, ArcadiaInstallations.MaxRecorded + 2).Select(i => Path.Combine(_root, $"a{i}", "Arcadia.exe")).ToList();
        foreach (var path in paths)
        {
            record.Add(path);
        }

        record.Add(paths[^1]);
        File.AppendAllLines(file, ["relative\\Arcadia.exe", Path.Combine(_root, "other.exe")]);

        Assert.Equal(paths.TakeLast(ArcadiaInstallations.MaxRecorded), record.Load());
    }

    [Fact]
    [Trait("TestCase", "T422-10")]
    public void A_record_too_large_to_be_Pans_is_not_read()
    {
        var file = Path.Combine(_root, RecordedInstallsFile.FileName);
        File.WriteAllText(file, Path.Combine(_root, "Arcadia.exe") + Environment.NewLine + new string('x', (int)RecordedInstallsFile.MaxBytes));

        Assert.Empty(new RecordedInstallsFile(file).Load());
    }

    [Fact]
    [Trait("TestCase", "T422-6")]
    public void The_marker_survives_a_restart_of_Pan_and_an_unreadable_one_never_removes_the_drivers()
    {
        var path = Path.Combine(_root, PendingUninstallFile.FileName);
        var marker = new PendingUninstallFile(path);
        Assert.Null(marker.Load());

        marker.Save(new PendingUninstall(RemoveDrivers: true, DriverAttempts: 2));
        Assert.Equal(new PendingUninstall(true, 2), new PendingUninstallFile(path).Load());

        File.WriteAllText(path, "{ broken");
        Assert.Equal(new PendingUninstall(false, 0), marker.Load());

        Assert.True(marker.Delete());
        Assert.False(marker.Delete());
    }
}
