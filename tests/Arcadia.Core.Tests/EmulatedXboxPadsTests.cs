using Arcadia.Input;
using Arcadia.Tests.Sdl;

namespace Arcadia.Core.Tests;

/// <summary>
/// SISR's virtual Xbox pad told from a real one by the device tree (#212, P3): its host controller is a software one,
/// and the XInput slot ties it to SDL's <c>XInput#n</c>.
/// </summary>
public sealed class EmulatedXboxPadsTests
{
    private static readonly XusbDevice RealOn0 = new(OnSoftwareHost: false, Slot: 0);
    private static readonly XusbDevice VirtualOn1 = new(OnSoftwareHost: true, Slot: 1);

    [Fact]
    public void The_device_holding_the_slot_decides()
    {
        Assert.False(DeviceTreeEmulatedPads.Decide(0, [RealOn0, VirtualOn1]));
        Assert.True(DeviceTreeEmulatedPads.Decide(1, [RealOn0, VirtualOn1]));
    }

    [Fact]
    public void A_slot_no_device_holds_is_not_emulated() =>
        Assert.False(DeviceTreeEmulatedPads.Decide(2, [RealOn0, VirtualOn1]));

    /// <summary>The LED says nothing: only virtual devices among the unknown ones make the pad virtual.</summary>
    [Fact]
    public void Without_slots_the_pad_is_emulated_only_if_every_device_without_one_is()
    {
        Assert.True(DeviceTreeEmulatedPads.Decide(0, [new XusbDevice(true, null)]));
        Assert.True(DeviceTreeEmulatedPads.Decide(1, [RealOn0, new XusbDevice(true, null)]));
        Assert.False(DeviceTreeEmulatedPads.Decide(0, [new XusbDevice(false, null)]));
        Assert.Null(DeviceTreeEmulatedPads.Decide(0, [new XusbDevice(false, null), new XusbDevice(true, null)]));
    }

    [Fact]
    public void An_unclear_pad_is_read_and_logged()
    {
        var log = new List<string>();
        var pads = new DeviceTreeEmulatedPads(() => [new XusbDevice(false, null), new XusbDevice(true, null)], log.Add);

        Assert.False(pads.IsEmulated(KnownPads.Xbox360));
        Assert.Equal(["Controller: unklar, ob XInput#0 virtuell ist (echt auf ?, virtuell auf ?); wird gelesen"], log);
    }

    [Fact]
    public void A_device_list_that_fails_reads_the_pad_as_real_instead_of_throwing()
    {
        var log = new List<string>();
        var pads = new DeviceTreeEmulatedPads(() => throw new InvalidOperationException("SetupDiGetClassDevs: Fehler 5"), log.Add);

        Assert.False(pads.IsEmulated(KnownPads.Xbox360));
        Assert.Equal(["Controller: Geräteliste von Windows nicht lesbar, XInput#0 gilt als echter Controller: SetupDiGetClassDevs: Fehler 5"], log);
    }

    [Fact]
    public void A_pad_SDL_doesn_t_read_through_XInput_is_never_looked_up()
    {
        var pads = new DeviceTreeEmulatedPads(() => throw new InvalidOperationException("must not be asked"), _ => { });

        Assert.False(pads.IsEmulated(KnownPads.SteamController));
    }

    [Theory]
    [InlineData(@"ROOT\USB\0000", true)]
    [InlineData(@"root\system\0001", true)]
    [InlineData(@"SWD\MSRRAS\MS_NDISWANIP", true)]
    [InlineData(@"PCI\VEN_8086&DEV_7AE0&SUBSYS_7D251462&REV_11\3&11583659&0&A0", false)]
    [InlineData(@"ACPI\PNP0A08\0", false)]
    [InlineData("", false)]
    public void A_root_enumerated_host_controller_is_a_software_one(string deviceId, bool software) =>
        Assert.Equal(software, DeviceTree.IsSoftwareHost(deviceId));

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, null)]
    [InlineData(2, 0)]
    [InlineData(5, 3)]
    [InlineData(6, 0)]
    [InlineData(9, 3)]
    [InlineData(10, null)]
    public void The_player_LED_names_the_slot(byte led, int? slot) => Assert.Equal(slot, XusbDevices.SlotOfLed(led));
    [Fact]
    [Trait("TestCase", "T242-17")]
    public void The_copy_of_the_puck_is_ignored_and_the_real_one_read()
    {
        var copyPath = @"\\?\hid#vid_28de&pid_1304&mi_02#copy";
        var pads = new DeviceTreeEmulatedPads(() => [], _ => { }, path => path == copyPath);
        var puck = new PadDevice("Steam Controller", PadDevice.ValveVendor, 0x1304, PadType.Steam, StickDeviceType.Gamepad);

        Assert.Equal("Kopie des Steam Controllers für Steam", GamepadDevices.WhyIgnored(puck with { Path = copyPath }, pads));
        Assert.Null(GamepadDevices.WhyIgnored(puck with { Path = @"\\?\hid#vid_28de&pid_1304&mi_02#real" }, pads));
        // A pad read through XInput is never the copy: its path leads nowhere in the device tree.
        Assert.False(pads.IsCopy(puck with { Path = "XInput#0" }));
    }

    [Fact]
    [Trait("TestCase", "T242-17")]
    public void A_device_tree_that_can_t_be_read_keeps_the_puck_read_and_says_so()
    {
        var log = new List<string>();
        var pads = new DeviceTreeEmulatedPads(() => [], log.Add, _ => throw new InvalidOperationException("kaputt"));
        var puck = new PadDevice("Steam Controller", PadDevice.ValveVendor, 0x1304, PadType.Steam, StickDeviceType.Gamepad) { Path = "hid#x" };

        Assert.False(pads.IsCopy(puck));
        Assert.Contains(log, line => line.Contains("kaputt", StringComparison.Ordinal));
    }

    /// <summary>
    /// #238 part 2: Arcadia's own DualSense, DualShock 4 and Switch 2 Pro pads carry the real ids; their place on the
    /// software host keeps them out, so a controller never counts twice and its pad never feeds itself.
    /// </summary>
    [Theory]
    [InlineData(0x054C, 0x0CE6, PadType.PS5)]
    [InlineData(0x054C, 0x09CC, PadType.PS4)]
    [InlineData(0x057E, 0x2069, PadType.SwitchPro)]
    public void Arcadias_own_PlayStation_and_Switch_pads_are_ignored_and_the_real_ones_read(ushort vendor, ushort product, PadType type)
    {
        var ownPath = @"\\?\hid#own-pad";
        var pads = new DeviceTreeEmulatedPads(() => [], _ => { }, path => path == ownPath);
        var pad = new PadDevice("Pad", vendor, product, type, StickDeviceType.Gamepad);

        Assert.Equal("virtueller Controller, z. B. Arcadias für ein Spiel", GamepadDevices.WhyIgnored(pad with { Path = ownPath }, pads));
        Assert.Null(GamepadDevices.WhyIgnored(pad with { Path = @"\\?\hid#real-pad" }, pads));
    }
}
