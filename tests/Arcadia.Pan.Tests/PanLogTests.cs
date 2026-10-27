using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

public class PanLogTests
{
    [Fact]
    public void Nothing_is_written_to_the_file_before_the_folder_is_trusted_and_the_early_lines_follow_then()
    {
        using var journal = new TempJournal();
        var path = Path.Combine(journal.Folder, "pan.log");
        Directory.CreateDirectory(journal.Folder);
        var log = new PanLog(path, console: false);

        log.Write("checking the folder");
        Assert.False(File.Exists(path));

        log.TrustFolder();
        log.Write("running");

        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.EndsWith("checking the folder", lines[0]);
        Assert.EndsWith("running", lines[1]);
    }

    [Fact]
    public void A_log_never_trusted_leaves_no_file()
    {
        using var journal = new TempJournal();
        var path = Path.Combine(journal.Folder, "pan.log");
        var log = new PanLog(path, console: false);

        log.Write("refusing to run");

        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(journal.Folder));
    }

    [Fact]
    public void Once_the_folder_is_being_deleted_a_folder_recreated_at_its_path_gets_no_line()
    {
        using var journal = new TempJournal();
        var path = Path.Combine(journal.Folder, "pan.log");
        Directory.CreateDirectory(journal.Folder);
        var log = new PanLog(path, console: false);
        log.TrustFolder();
        log.Write("uninstalling");

        log.StopFileLogging();
        Directory.Delete(journal.Folder, recursive: true);
        // Anyone may create the path again once it is gone; an elevated write must not follow.
        Directory.CreateDirectory(journal.Folder);
        log.Write("Pan's folder deleted");
        log.TrustFolder();
        log.Write("later");

        Assert.False(File.Exists(path));
    }
}
