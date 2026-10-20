using Arcadia.Launchers;

namespace Arcadia.Core.Tests;

public sealed class ProcessWatchTests
{
    [Fact]
    public void The_folder_a_running_program_was_started_from_is_found()
    {
        var ownFolder = Path.GetDirectoryName(Environment.ProcessPath)!;
        var elsewhere = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Assert.Equal(ownFolder, ProcessWatch.FirstRunningFrom([elsewhere, ownFolder]));
            Assert.True(ProcessWatch.AnyRunningFrom(ownFolder));
        }
        finally
        {
            Directory.Delete(elsewhere);
        }
    }

    [Fact]
    public void A_folder_nothing_runs_from_is_not_found()
    {
        var empty = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Assert.Null(ProcessWatch.FirstRunningFrom([empty]));
            Assert.Null(ProcessWatch.FirstRunningFrom([]));
        }
        finally
        {
            Directory.Delete(empty);
        }
    }

    [Fact]
    public void The_path_of_a_process_is_its_executable_on_Windows()
    {
        var expected = OperatingSystem.IsWindows() ? Environment.ProcessPath : null;

        Assert.Equal(expected, ProcessWatch.PathOf(Environment.ProcessId), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_process_that_does_not_exist_has_no_path()
    {
        Assert.Null(ProcessWatch.PathOf(int.MaxValue));
    }
}
