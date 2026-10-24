using Arcadia.Core.Diagnostics;

namespace Arcadia.Core.Tests;

public sealed class LogLocationTests : IDisposable
{
    private readonly string _localAppData = Path.Combine(Path.GetTempPath(), "arcadia-logs-" + Guid.NewGuid());

    private string InstallFolder => Path.Combine(_localAppData, "Arcadia");

    private string OldLogs => Path.Combine(InstallFolder, "logs");

    private string Logs => LogLocation.For(_localAppData);

    public void Dispose()
    {
        if (Directory.Exists(_localAppData))
        {
            Directory.Delete(_localAppData, recursive: true);
        }
    }

    [Fact]
    public void The_logs_live_beside_the_install_folder_not_in_it()
    {
        Assert.Equal(Path.Combine(_localAppData, "Arcadia-Logs"), Logs);
    }

    [Fact]
    public void Old_logs_of_a_build_that_was_never_installed_move_and_leave_no_install_folder()
    {
        WriteOldLog("arcadia.log", "alt");

        LogLocation.MoveOldLogs(_localAppData);

        Assert.Equal("alt", File.ReadAllText(Path.Combine(Logs, "arcadia.log")));
        Assert.False(Directory.Exists(InstallFolder));
    }

    [Fact]
    public void Old_logs_in_an_installation_move_and_the_installation_stays()
    {
        WriteOldLog("arcadia.log", "alt");
        Directory.CreateDirectory(Path.Combine(InstallFolder, "current"));

        LogLocation.MoveOldLogs(_localAppData);

        Assert.True(File.Exists(Path.Combine(Logs, "arcadia.log")));
        Assert.False(Directory.Exists(OldLogs));
        Assert.True(Directory.Exists(Path.Combine(InstallFolder, "current")));
    }

    [Fact]
    public void Old_logs_written_after_the_move_are_dropped_and_the_new_ones_kept()
    {
        Directory.CreateDirectory(Logs);
        File.WriteAllText(Path.Combine(Logs, "arcadia.log"), "neu");
        WriteOldLog("arcadia.log", "alt");

        LogLocation.MoveOldLogs(_localAppData);

        Assert.Equal("neu", File.ReadAllText(Path.Combine(Logs, "arcadia.log")));
        Assert.False(Directory.Exists(InstallFolder));
    }

    [Fact]
    public void Without_old_logs_nothing_happens()
    {
        LogLocation.MoveOldLogs(_localAppData);

        Assert.False(Directory.Exists(InstallFolder));
        Assert.False(Directory.Exists(Logs));
    }

    private void WriteOldLog(string name, string text)
    {
        Directory.CreateDirectory(OldLogs);
        File.WriteAllText(Path.Combine(OldLogs, name), text);
    }
}
