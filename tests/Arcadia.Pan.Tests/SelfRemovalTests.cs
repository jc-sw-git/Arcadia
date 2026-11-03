using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>What the service does at its start about a pending uninstall or a PC without Arcadia (#422).</summary>
public class SelfRemovalTests
{
    private readonly List<string> _log = [];
    private int _waits;

    private bool _clientCame;

    private Task<bool> Decide(FakeMarker marker, bool undoComplete, params ArcadiaPresence[] looks)
    {
        var next = new Queue<ArcadiaPresence>(looks);
        return SelfRemoval.ShouldFinishAsync(undoComplete, marker, () => next.Dequeue(), () =>
        {
            _waits++;
            return Task.CompletedTask;
        }, () => _clientCame, _log.Add);
    }

    [Fact]
    [Trait("TestCase", "T422-1")]
    public async Task A_pending_uninstall_is_finished_once_the_journal_is_empty_and_Arcadia_is_gone()
    {
        Assert.True(await Decide(new FakeMarker(new(RemoveDrivers: true, 1)), undoComplete: true, ArcadiaPresence.Absent, ArcadiaPresence.Absent));
        Assert.Equal(1, _waits);
    }

    [Fact]
    [Trait("TestCase", "T422-1")]
    public async Task A_pending_uninstall_waits_while_the_journal_still_lists_something()
    {
        var marker = new FakeMarker(new(RemoveDrivers: true, 0));

        Assert.False(await Decide(marker, undoComplete: false, ArcadiaPresence.Absent));
        Assert.NotNull(marker.Load());
    }

    [Fact]
    [Trait("TestCase", "T422-3")]
    public async Task Arcadia_installed_again_cancels_the_pending_uninstall()
    {
        var marker = new FakeMarker(new(RemoveDrivers: true, 1));

        Assert.False(await Decide(marker, undoComplete: true, ArcadiaPresence.Found));
        Assert.Null(marker.Load());
    }

    [Fact]
    [Trait("TestCase", "T422-4")]
    public async Task Without_Arcadia_and_without_a_marker_Pan_removes_itself()
    {
        Assert.True(await Decide(new FakeMarker(), undoComplete: true, ArcadiaPresence.Absent, ArcadiaPresence.Absent));
    }

    [Fact]
    [Trait("TestCase", "T422-4")]
    public async Task With_Arcadia_installed_Pan_serves_at_once_without_waiting()
    {
        Assert.False(await Decide(new FakeMarker(), undoComplete: true, ArcadiaPresence.Found));
        Assert.Equal(0, _waits);
    }

    [Fact]
    [Trait("TestCase", "T422-5")]
    public async Task A_place_Pan_can_t_read_keeps_Pan()
    {
        var marker = new FakeMarker(new(RemoveDrivers: true, 0));

        Assert.False(await Decide(marker, undoComplete: true, ArcadiaPresence.Unknown));
        Assert.NotNull(marker.Load());
    }

    [Fact]
    [Trait("TestCase", "T422-7")]
    public async Task Arcadia_that_is_back_at_the_second_look_keeps_Pan()
    {
        Assert.False(await Decide(new FakeMarker(), undoComplete: true, ArcadiaPresence.Absent, ArcadiaPresence.Found));
    }

    [Fact]
    [Trait("TestCase", "T422-4")]
    public async Task Without_Arcadia_but_with_something_left_to_undo_Pan_stays()
    {
        Assert.False(await Decide(new FakeMarker(), undoComplete: false, ArcadiaPresence.Absent));
        Assert.Equal(0, _waits);
    }

    [Fact]
    [Trait("TestCase", "T422-10")]
    public async Task A_client_that_connects_while_Pan_looks_keeps_Pan_as_it_is_an_Arcadia_Pan_doesn_t_know()
    {
        _clientCame = true;
        var marker = new FakeMarker(new(RemoveDrivers: true, 0));

        Assert.False(await Decide(marker, undoComplete: true, ArcadiaPresence.Absent, ArcadiaPresence.Absent));
        Assert.Null(marker.Load());
    }
}
