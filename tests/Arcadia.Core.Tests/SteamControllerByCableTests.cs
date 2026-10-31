using Arcadia.ControllerSupport.Copy;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Pan;
using Arcadia.Tests.Pads;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>
/// #345: Arcadia copies the Steam Controller by cable (<c>28DE:1302</c>) as it copies the puck, and the copy follows the
/// controller when it changes between cable and puck.
/// </summary>
public sealed class SteamControllerByCableTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _events = [];
    private readonly List<string> _log = [];
    private readonly FakeVirtualXboxPads _pads = new();
    private readonly FakeEmulationGuardian _pan = new();
    private readonly FakeSteamControllerHold _copy = new();
    private readonly PanEmulation _emulation;

    public SteamControllerByCableTests()
    {
        _pads.Events = _events;
        _pan.Events = _events;
        _copy.Events = _events;
        _emulation = new PanEmulation(_pads, _pan, _copy, _time, _log.Add)
        {
            Enabled = () => true,
            SteamControllerPresent = () => true,
        };
    }

    private string Log() => string.Join("\n", _log);

    [Fact]
    public void The_cable_has_one_HID_interface_with_the_pucks_vendor_reports()
    {
        var cable = PuckDescriptors.Cable;

        Assert.Equal(SteamControllerConnection.Cable, cable.Connection);
        Assert.Equal(0x1302, cable.Product);
        Assert.Equal(0x1304, PuckDescriptors.Triton.Product);
        Assert.Equal(new Dictionary<byte, byte> { [0] = 3 }, cable.InterfaceClasses);
        Assert.True(cable.IsHid(0));
        var sizes = cable.ReportSizes[0];
        Assert.True(sizes.UsesIds);
        // The same vendor reports as a puck slot: state 0x42 with 53 bytes, features 1 and 2 with 63.
        Assert.Equal(PuckDescriptors.Triton.ReportSizes[2].Input[0x42], sizes.Input[0x42]);
        Assert.Equal(63, sizes.Feature[1]);
        Assert.Equal(63, sizes.Feature[2]);
    }

    [Fact]
    public void The_cables_copy_says_Steam_Controller_with_the_connected_ones_serial()
    {
        var options = PuckDescriptors.Cable.DeviceSpecific("FXC7654321");

        var strings = Assert.IsType<Dictionary<string, string>>(options["strings"]);
        Assert.Equal("FXC7654321", strings["3"]);
        Assert.Equal("Steam Controller", strings["2"]);
        Assert.Equal("Valve Software", strings["1"]);
        Assert.Single(Assert.IsType<Dictionary<string, string>>(options["reports"]));
    }

    [Theory]
    [InlineData(@"\\?\hid#vid_28de&pid_1302&col03#8&3c4d5e6f&0&0002#{4d1e55b2-f16f-11cf-88cb-001111000030}", SteamControllerConnection.Cable, 0)]
    [InlineData(@"\\?\HID#VID_28DE&PID_1302&Col01#8&3c4d5e6f&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", SteamControllerConnection.Cable, 0)]
    [InlineData(@"\\?\hid#vid_28de&pid_1304&mi_02&col03#8&1a2b3c4d&0&0002#{4d1e55b2-f16f-11cf-88cb-001111000030}", SteamControllerConnection.Puck, 2)]
    [InlineData(@"\\?\hid#vid_28de&pid_1304&mi_06#8&5e6f7a8b&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", SteamControllerConnection.Puck, 6)]
    public void A_collection_path_names_its_device_and_interface(string path, SteamControllerConnection connection, byte iface)
    {
        Assert.Equal((connection, iface), PuckDescriptors.PlaceOf(path));
    }

    [Theory]
    [InlineData(@"\\?\hid#vid_28de&pid_1005#8&3c4d5e6f&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}")]
    [InlineData(@"\\?\hid#vid_28de&pid_13020&col01#8&3c4d5e6f&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}")]
    [InlineData(@"\\?\hid#vid_28de&pid_1304&col01#8&1a2b3c4d&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}")]
    [InlineData(@"\\?\hid#vid_054c&pid_0ce6&mi_03#7&2b7f&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}")]
    public void Other_devices_have_no_place(string path)
    {
        Assert.Null(PuckDescriptors.PlaceOf(path));
    }

    [Fact]
    public async Task By_cable_the_copy_is_the_cables()
    {
        _copy.Connected = SteamControllerConnection.Cable;

        await _emulation.TickAsync();

        Assert.Equal(PanEmulationState.Running, _emulation.Status.State);
        Assert.Equal(["started 1", "hide 1", "copy attached (cable)"], _events);
    }

    [Fact]
    public async Task Unplugging_the_cable_moves_the_copy_to_the_puck_without_counting_a_failure()
    {
        _copy.Connected = SteamControllerConnection.Cable;
        await _emulation.TickAsync();
        _events.Clear();

        // The controller goes over to the puck: the cable's copy loses its device.
        _copy.Connected = SteamControllerConnection.Puck;
        _copy.Current!.Lose();
        await _emulation.TickAsync();

        Assert.Equal(PanEmulationState.Running, _emulation.Status.State);
        Assert.Equal(["copy detached", "hide 1", "copy attached"], _events);
        Assert.Contains("Steam Controller jetzt am Puck, die Kopie folgt", Log());
        Assert.DoesNotContain("Kopie für Steam verloren", Log());
        Assert.Empty(_pan.Unhidden);
    }

    [Fact]
    public async Task Plugging_in_the_cable_moves_the_copy_from_the_puck_after_Pan_hid_it_again()
    {
        await _emulation.TickAsync();
        _events.Clear();

        // Switched off on the puck, then plugged in: the controller is the cable now, the puck's copy still runs.
        _copy.Connected = SteamControllerConnection.Cable;
        await _emulation.TickAsync();

        Assert.Equal(["copy detached", "hide 1", "copy attached (cable)"], _events);
        Assert.Equal(SteamControllerConnection.Cable, _copy.Current!.Connection);
        Assert.Equal(PanEmulationState.Running, _emulation.Status.State);
    }

    [Fact]
    public async Task A_hide_refused_after_the_change_ends_the_emulation_so_Steam_gets_the_real_controller()
    {
        await _emulation.TickAsync();
        _pan.HideResult = PanReason.HidingFailed;

        _copy.Connected = SteamControllerConnection.Cable;
        await _emulation.TickAsync();

        Assert.Equal(1, _copy.Attached);
        Assert.Equal([1u], _pan.Unhidden);
        Assert.Equal(PanEmulationProblem.Hiding, _emulation.Status.Problem);
        Assert.Contains("nach dem Wechsel nicht versteckt (HidingFailed)", Log());
    }

    [Fact]
    public async Task A_copy_lost_while_its_device_is_still_there_is_still_a_failure()
    {
        _copy.Connected = SteamControllerConnection.Cable;
        await _emulation.TickAsync();

        _copy.Current!.Lose();
        await _emulation.TickAsync();

        Assert.Contains("Kopie für Steam verloren", Log());
        Assert.Equal(2, _copy.Attached);
    }

    [Fact]
    public async Task While_Pan_restarts_the_cable_the_copy_waits_for_it_rather_than_copying_the_empty_puck()
    {
        await _emulation.TickAsync();
        _copy.Connected = SteamControllerConnection.Cable;
        // Pan's restart takes the cable away for a moment: only the puck is listed meanwhile.
        _pan.PuckRestart = () =>
        {
            _copy.Connected = SteamControllerConnection.Puck;
            return PanReason.None;
        };

        await _emulation.TickAsync();
        _pan.PuckRestart = null;
        await _emulation.TickAsync();

        Assert.Equal(1, _copy.Attached);
        Assert.Equal(1, _copy.Detached);

        _copy.Connected = SteamControllerConnection.Cable;
        await _emulation.TickAsync();

        Assert.Equal(2, _copy.Attached);
        Assert.Equal(SteamControllerConnection.Cable, _copy.Current!.Connection);
        Assert.Single(_log, line => line.Contains("die Kopie folgt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_device_that_doesnt_come_back_is_given_up_after_the_wait()
    {
        await _emulation.TickAsync();
        _copy.Connected = SteamControllerConnection.Cable;
        _pan.PuckRestart = () =>
        {
            _copy.Connected = SteamControllerConnection.Puck;
            return PanReason.None;
        };
        await _emulation.TickAsync();
        _pan.PuckRestart = null;

        _time.Advance(PanEmulation.FollowWait);
        await _emulation.TickAsync();

        Assert.Equal(2, _copy.Attached);
        Assert.Equal(SteamControllerConnection.Puck, _copy.Current!.Connection);
    }
}
