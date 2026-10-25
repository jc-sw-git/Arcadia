using Arcadia.Core.Diagnostics;

namespace Arcadia.Core.Tests;

public sealed class LogLocationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "arcadia-logs-" + Guid.NewGuid());

    private string LocalAppData => Path.Combine(_root, "Local");

    private string DataFolder => LogLocation.DataFolderIn(Path.Combine(_root, "Roaming"));

    private string InstallFolder => Path.Combine(LocalAppData, "Arcadia");

    private string LogsOfRc1 => Path.Combine(InstallFolder, "logs");

    private string LogsOfMain => Path.Combine(LocalAppData, "Arcadia-Logs");

    private string Logs => LogLocation.For(DataFolder);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void The_logs_live_beside_the_settings()
    {
        Assert.Equal(Path.Combine(_root, "Roaming", "Arcadia", "logs"), Logs);
    }

    [Fact]
    public void Logs_of_a_build_that_was_never_installed_move_and_leave_no_install_folder()
    {
        WriteLog(LogsOfRc1, "alt");

        LogLocation.MoveOldLogs(LocalAppData, DataFolder);

        Assert.Equal("alt", ReadLog(Logs));
        Assert.False(Directory.Exists(InstallFolder));
    }

    [Fact]
    public void Logs_in_an_installation_move_and_the_installation_stays()
    {
        WriteLog(LogsOfRc1, "alt");
        Directory.CreateDirectory(Path.Combine(InstallFolder, "current"));

        LogLocation.MoveOldLogs(LocalAppData, DataFolder);

        Assert.Equal("alt", ReadLog(Logs));
        Assert.False(Directory.Exists(LogsOfRc1));
        Assert.True(Directory.Exists(Path.Combine(InstallFolder, "current")));
    }

    [Fact]
    public void Logs_of_the_short_lived_separate_folder_move_too()
    {
        WriteLog(LogsOfMain, "kurz");

        LogLocation.MoveOldLogs(LocalAppData, DataFolder);

        Assert.Equal("kurz", ReadLog(Logs));
        Assert.False(Directory.Exists(LogsOfMain));
    }

    [Fact]
    public void Old_logs_are_dropped_when_current_ones_exist()
    {
        WriteLog(Logs, "neu");
        WriteLog(LogsOfRc1, "alt");
        WriteLog(LogsOfMain, "kurz");

        LogLocation.MoveOldLogs(LocalAppData, DataFolder);

        Assert.Equal("neu", ReadLog(Logs));
        Assert.False(Directory.Exists(InstallFolder));
        Assert.False(Directory.Exists(LogsOfMain));
    }

    [Fact]
    public void Without_old_logs_nothing_happens()
    {
        LogLocation.MoveOldLogs(LocalAppData, DataFolder);

        Assert.False(Directory.Exists(InstallFolder));
        Assert.False(Directory.Exists(Logs));
    }

    private static void WriteLog(string folder, string text)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "arcadia.log"), text);
    }

    private static string ReadLog(string folder) => File.ReadAllText(Path.Combine(folder, "arcadia.log"));
}
