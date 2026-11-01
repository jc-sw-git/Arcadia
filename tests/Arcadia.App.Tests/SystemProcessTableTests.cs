using System.Diagnostics;
using Arcadia.App.Services;

namespace Arcadia.App.Tests;

public sealed class SystemProcessTableTests
{
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
}
