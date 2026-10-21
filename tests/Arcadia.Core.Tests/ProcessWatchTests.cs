using System.Diagnostics;
using Arcadia.Launchers;

namespace Arcadia.Core.Tests;

public sealed class ProcessWatchTests
{
    [Fact]
    public void The_folder_a_running_program_was_started_from_is_found_unless_it_is_a_service()
    {
        // Windows services run in session 0 and never count; a test run as a service (some CI agents) checks just that.
        var ownFolder = Path.GetDirectoryName(Environment.ProcessPath)!;
        using var self = Process.GetCurrentProcess();
        var isService = OperatingSystem.IsWindows() && self.SessionId == 0;
        var elsewhere = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Assert.Equal(isService ? null : ownFolder, ProcessWatch.FirstRunningFrom([elsewhere, ownFolder]));
            Assert.Equal(!isService, ProcessWatch.AnyRunningFrom(ownFolder));
        }
        finally
        {
            Directory.Delete(elsewhere);
        }
    }

    [Fact]
    public void The_running_processes_include_this_one() =>
        Assert.Contains(Environment.ProcessId, ProcessWatch.RunningProcessIds());

    [Fact]
    public void The_process_table_describes_this_process_on_windows_and_nothing_elsewhere()
    {
        var table = new SystemProcessTable();
        if (!OperatingSystem.IsWindows())
        {
            Assert.Empty(table.Ids());
            return;
        }

        Assert.Contains(Environment.ProcessId, table.Ids());
        Assert.Null(table.Describe(-1));
        using var self = Process.GetCurrentProcess();
        var details = table.Describe(Environment.ProcessId);
        if (self.SessionId == 0)
        {
            // A test run as a service: services are no games.
            Assert.Null(details);
            return;
        }

        Assert.Equal(Environment.ProcessPath, details?.Path, ignoreCase: true);
        Assert.Equal(new DateTimeOffset(self.StartTime), details?.StartedAt);
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
