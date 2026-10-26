using Arcadia.ControllerSupport.Pan;
using Arcadia.Input;
using Arcadia.Tests.Pads;

namespace Arcadia.Core.Tests;

public class GuardedXboxPadsTests
{
    private readonly FakeVirtualXboxPads _inner = new();
    private readonly FakeEmulationGuardian _pan = new();
    private readonly GuardedXboxPads _pads;

    public GuardedXboxPadsTests()
    {
        _pads = new GuardedXboxPads(_inner, _pan);
    }

    [Fact]
    public void Without_Pan_there_is_no_pad_and_the_reason_is_Pan_s()
    {
        Assert.Null(_pads.Unavailable());
        _pan.Missing = "Pan ist nicht eingerichtet";
        Assert.Equal("Pan ist nicht eingerichtet", _pads.Unavailable());
        _inner.Missing = "Treiber usbip nicht geladen";
        Assert.Equal("Treiber usbip nicht geladen", _pads.Unavailable());
    }

    [Fact]
    public async Task A_pad_is_registered_with_Pan_after_it_exists_and_unregistered_when_it_goes()
    {
        var pad = await _pads.PlugAsync(CancellationToken.None);

        Assert.Equal(1, _inner.Plugged);
        Assert.Equal([1u], _pan.Started);
        Assert.True(pad.IsConnected);

        await pad.SendAsync(XboxPadState.Neutral with { Buttons = 1 }, CancellationToken.None);
        Assert.Single(_inner.Current!.Sent);

        await pad.DisposeAsync();
        Assert.Equal(1, _inner.Unplugged);
        Assert.Equal([1u], _pan.Stopped);
        Assert.Empty(_pan.Acknowledged);
    }

    [Fact]
    public async Task A_refusal_by_Pan_unplugs_the_pad_again_and_fails_the_plug()
    {
        _pan.Refuse = true;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _pads.PlugAsync(CancellationToken.None));

        Assert.Contains("lehnt die Emulation ab", ex.Message);
        Assert.Equal(1, _inner.Plugged);
        Assert.Equal(1, _inner.Unplugged);
    }

    [Fact]
    public async Task A_teardown_by_Pan_makes_the_pad_lost_and_its_removal_acknowledges_it()
    {
        var pad = await _pads.PlugAsync(CancellationToken.None);
        _pan.TearDown();

        Assert.False(pad.IsConnected);
        await pad.DisposeAsync();

        Assert.Equal([1u], _pan.Acknowledged);
        Assert.Empty(_pan.Stopped);
        var next = await _pads.PlugAsync(CancellationToken.None);
        Assert.True(next.IsConnected);
        Assert.Equal([1u, 2u], _pan.Started);
    }

    [Fact]
    public async Task A_lost_Pan_makes_the_pad_lost_too_and_shutdown_says_goodbye()
    {
        var pad = await _pads.PlugAsync(CancellationToken.None);
        _pan.Lose("Pan antwortet nicht");

        Assert.False(pad.IsConnected);
        Assert.Equal("Pan antwortet nicht", _pads.Unavailable());

        await _pads.ShutdownAsync();
        Assert.True(_inner.ShutDown);
        Assert.True(_pan.PlannedExit);
    }
}
