using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

public class JournalTests
{
    private static JournalState Sample() => new()
    {
        Generation = 5,
        ClientPid = 1234,
        ClientStartTime = 133_000_000_000_000_000,
        Emulating = true,
        HeldProcesses = [new HeldProcess(4321, 133_000_000_000_000_500, "viiper.exe")],
    };

    [Fact]
    public void A_saved_journal_comes_back_the_same_and_keeps_the_previous_version()
    {
        using var journal = new TempJournal();
        journal.File.Save(Sample());
        var state = Sample();
        state.Generation = 6;
        journal.File.Save(state);

        var load = journal.File.Load();
        Assert.Null(load.Problem);
        Assert.Equal(6u, load.State.Generation);
        Assert.Equal(1234, load.State.ClientPid);
        Assert.Equal(133_000_000_000_000_000, load.State.ClientStartTime);
        Assert.True(load.State.Emulating);
        Assert.Equal(new HeldProcess(4321, 133_000_000_000_000_500, "viiper.exe"), Assert.Single(load.State.HeldProcesses));
        Assert.True(File.Exists(journal.File.PreviousPath));
        Assert.Contains("\"Generation\": 5", File.ReadAllText(journal.File.PreviousPath));
    }

    [Fact]
    public void A_journal_of_a_Pan_before_the_firmware_hold_is_read_with_all_it_has_to_undo()
    {
        // #326: the bundled Pan's --restore undoes the journal an older Pan left, which lacks the fields added since.
        using var journal = new TempJournal();
        Directory.CreateDirectory(Path.GetDirectoryName(journal.File.Path)!);
        File.WriteAllText(journal.File.Path, """
            {
              "Version": 1,
              "Generation": 3,
              "ClientPid": 1234,
              "ClientStartTime": 133000000000000000,
              "Emulating": true,
              "HeldProcesses": [ { "Pid": 4321, "StartTime": 133000000000000500, "ImageName": "viiper.exe" } ],
              "HiddenDevices": [ "HID\\VID_28DE&PID_1304&MI_00\\1&2&3" ],
              "AllowListed": [ "C:\\Arcadia\\Arcadia.exe" ],
              "CloakingWasOn": false
            }
            """);

        var load = journal.File.Load();

        Assert.Null(load.Problem);
        Assert.True(load.State.HasWork);
        Assert.True(load.State.HidHideChanged);
        Assert.Equal(@"HID\VID_28DE&PID_1304&MI_00\1&2&3", Assert.Single(load.State.HiddenDevices));
        Assert.Equal(@"C:\Arcadia\Arcadia.exe", Assert.Single(load.State.AllowListed));
        Assert.False(load.State.FirmwareHold);
        Assert.False(load.State.PuckRestartPending);
    }

    [Fact]
    public void No_journal_means_nothing_to_undo()
    {
        using var journal = new TempJournal();
        var load = journal.File.Load();
        Assert.Null(load.Problem);
        Assert.False(load.State.HasWork);
    }

    [Fact]
    public void A_torn_journal_falls_back_to_the_previous_version_and_is_moved_aside()
    {
        using var journal = new TempJournal();
        journal.File.Save(Sample());
        var state = Sample();
        state.Generation = 6;
        journal.File.Save(state);
        File.WriteAllText(journal.File.Path, journal.Text[..20]);

        var load = journal.File.Load();

        Assert.True(load.FromPrevious);
        Assert.Contains("unreadable", load.Problem);
        Assert.Equal(5u, load.State.Generation);
        Assert.True(load.State.HasWork);
        Assert.True(File.Exists(journal.File.BrokenPath));

        // The fallback is the journal now (review point 3): whoever loads next, e.g. the recovery after a look at the
        // journal, sees the same work and the same generation, not an empty journal.
        Assert.True(journal.Exists);
        var again = journal.File.Load();
        Assert.Null(again.Problem);
        Assert.False(again.FromPrevious);
        Assert.Equal(5u, again.State.Generation);
        Assert.True(again.State.HasWork);
    }

    [Fact]
    public void A_torn_journal_without_a_previous_version_leaves_an_empty_journal_behind()
    {
        using var journal = new TempJournal();
        Directory.CreateDirectory(journal.Folder);
        File.WriteAllText(journal.File.Path, "{ \"Generation\": ");

        journal.File.Load();

        Assert.True(journal.Exists);
        Assert.Null(journal.File.Load().Problem);
    }

    [Fact]
    public void A_fallback_that_cannot_be_written_back_is_still_found_by_the_next_load()
    {
        // Review follow-up 1 (reproduced there): the write-back failed, the torn journal was already moved aside, and
        // the next load found no journal and nothing to undo although the previous version listed work.
        using var journal = new TempJournal();
        journal.File.Save(Sample());
        journal.File.Save(Sample());
        File.WriteAllText(journal.File.Path, "{ torn");
        Directory.CreateDirectory(journal.File.Path + ".tmp");   // the save's temp file can't be created

        var first = journal.File.Load();
        Assert.True(first.State.HasWork);
        Assert.Contains("could not be written back", first.Problem);
        Assert.False(journal.Exists);

        var second = journal.File.Load();
        Assert.True(second.FromPrevious);
        Assert.Contains("missing; using the previous version", second.Problem);
        Assert.True(second.State.HasWork);
        Assert.Equal(5u, second.State.Generation);
    }

    [Theory]
    [InlineData("{\"Version\":1,\"Generation\":7,\"HeldProcesses\":null}", "a list is missing")]
    [InlineData("{\"Version\":1,\"Generation\":7,\"HiddenDevices\":null}", "a list is missing")]
    [InlineData("{\"Version\":1,\"Generation\":7,\"HeldProcesses\":[{\"Pid\":0,\"StartTime\":1,\"ImageName\":\"viiper.exe\"}]}", "incomplete")]
    [InlineData("{\"Version\":1,\"Generation\":7,\"HeldProcesses\":[{\"Pid\":5,\"StartTime\":1}]}", "incomplete")]
    [InlineData("{\"Version\":1,\"Generation\":7,\"AllowListed\":[null]}", "HidHide entry is missing")]
    [InlineData("{\"Version\":2,\"Generation\":7}", "unknown journal version")]
    [InlineData("{\"Version\":1,\"Generation\":7,\"UsbipServers\":null}", "a list is missing")]
    [InlineData("{\"Version\":1,\"Generation\":7,\"UsbipServers\":[{\"Generation\":7,\"Port\":0,\"Pid\":5}]}", "usbip server entry is incomplete")]
    [InlineData("{\"Version\":1,\"Generation\":7,\"UsbipServers\":[{\"Generation\":7,\"Port\":70000,\"Pid\":5}]}", "usbip server entry is incomplete")]
    public void A_journal_that_parses_but_makes_no_sense_counts_as_unreadable(string json, string problem)
    {
        // Review point 4: `{"HeldProcesses":null}` deserializes, and HasWork then threw at every start of Pan.
        using var journal = new TempJournal();
        journal.File.Save(Sample());
        journal.File.Save(Sample());
        File.WriteAllText(journal.File.Path, json);

        var load = journal.File.Load();

        Assert.True(load.FromPrevious);
        Assert.Contains(problem, load.Problem);
        Assert.Equal(5u, load.State.Generation);
        Assert.True(load.State.HasWork);
        Assert.True(File.Exists(journal.File.BrokenPath));
    }

    [Fact]
    public void A_torn_journal_without_a_previous_version_is_empty_but_never_an_error()
    {
        using var journal = new TempJournal();
        Directory.CreateDirectory(journal.Folder);
        File.WriteAllText(journal.File.Path, "{ \"Generation\": ");

        var load = journal.File.Load();

        Assert.False(load.FromPrevious);
        Assert.Contains("no previous version", load.Problem);
        Assert.False(load.State.HasWork);
    }

    [Fact]
    public void The_journal_with_nothing_open_has_no_work()
    {
        var state = new JournalState { Generation = 3 };
        Assert.False(state.HasWork);
        state.CloakingWasOn = false;
        Assert.True(state.HasWork);
    }
}
