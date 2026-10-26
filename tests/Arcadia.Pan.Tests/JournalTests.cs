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
        Assert.False(journal.Exists);
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
