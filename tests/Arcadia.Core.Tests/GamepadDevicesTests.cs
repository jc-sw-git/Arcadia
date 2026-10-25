using Arcadia.Input;
using Arcadia.Tests.Sdl;

namespace Arcadia.Core.Tests;

public sealed class GamepadDevicesTests
{
    public static TheoryData<string> Read => new(nameof(KnownPads.Xbox360), nameof(KnownPads.XboxSeries), nameof(KnownPads.EightBitDoUltimate2),
        nameof(KnownPads.DualSense), nameof(KnownPads.DualShock4), nameof(KnownPads.SwitchPro), nameof(KnownPads.JoyConPair),
        nameof(KnownPads.SteamController));

    [Theory]
    [MemberData(nameof(Read))]
    public void Physical_gamepads_are_read(string pad) => Assert.Null(GamepadDevices.WhyIgnored(Pad(pad), NoEmulatedPads.Instance));

    /// <summary>A third-party pad unknown to SDL's type list but in its controller database (e.g. a cheap USB pad).</summary>
    [Fact]
    public void A_pad_of_unknown_type_and_kind_is_read() =>
        Assert.Null(GamepadDevices.WhyIgnored(new PadDevice("USB Gamepad", 0x0079, 0x0006, PadType.Unknown, StickDeviceType.Unknown), NoEmulatedPads.Instance));

    [Theory]
    [InlineData(nameof(KnownPads.SteamVirtual), "virtueller Controller von Steam")]
    [InlineData(nameof(KnownPads.JoyConLeft), "einzelner Joy-Con")]
    [InlineData(nameof(KnownPads.Wheel), "kein Gamepad (Wheel)")]
    public void Steam_s_virtual_pads_single_Joy_Cons_and_wheels_are_ignored(string pad, string reason) =>
        Assert.Equal(reason, GamepadDevices.WhyIgnored(Pad(pad), NoEmulatedPads.Instance));

    /// <summary>#212: of Valve's devices only the Steam Controllers; SDL's type Steam also covers the 2015 one and Hori's.</summary>
    [Fact]
    public void Only_the_Steam_Controller_is_read_of_Valve_s_devices()
    {
        Assert.Equal("Gerät von Valve, kein Steam Controller",
            GamepadDevices.WhyIgnored(new PadDevice("Steam Deck", PadDevice.ValveVendor, 0x1205, PadType.Standard, StickDeviceType.Gamepad), NoEmulatedPads.Instance));
        Assert.Null(GamepadDevices.WhyIgnored(new PadDevice("Steam Controller", PadDevice.ValveVendor, 0x1142, PadType.Steam, StickDeviceType.Gamepad), NoEmulatedPads.Instance));
        Assert.Null(GamepadDevices.WhyIgnored(new PadDevice("HORI Steam Controller", 0x0F0D, 0x01AB, PadType.Steam, StickDeviceType.Gamepad), NoEmulatedPads.Instance));
        Assert.Equal("virtueller Controller von Steam",
            GamepadDevices.WhyIgnored(KnownPads.SteamVirtual with { Type = PadType.Steam }, NoEmulatedPads.Instance));
        Assert.NotNull(GamepadDevices.WhyIgnored(KnownPads.JoyConLeft with { Type = PadType.JoyConRight }, NoEmulatedPads.Instance));
        Assert.NotNull(GamepadDevices.WhyIgnored(KnownPads.Wheel with { StickType = StickDeviceType.FlightStick }, NoEmulatedPads.Instance));
    }

    /// <summary>#212, P3: SISR's virtual pad is a wired Xbox 360 pad to SDL; only the device tree tells it apart.</summary>
    [Fact]
    public void An_emulated_Xbox_pad_is_ignored_and_only_pads_read_through_XInput_are_asked_about()
    {
        var asked = new List<PadDevice>();
        var emulated = new Emulated(device =>
        {
            asked.Add(device);
            return true;
        });

        Assert.Equal("virtueller Xbox Controller, z. B. von SISR", GamepadDevices.WhyIgnored(KnownPads.SisrXbox360, emulated));
        Assert.Null(GamepadDevices.WhyIgnored(KnownPads.DualSense, emulated));
        Assert.Null(GamepadDevices.WhyIgnored(KnownPads.SteamController, emulated));
        Assert.Equal([KnownPads.SisrXbox360], asked);
    }

    [Theory]
    [InlineData("XInput#0", 0)]
    [InlineData("XInput#3", 3)]
    [InlineData("XInput#", null)]
    [InlineData(@"\\?\HID#VID_054C&PID_0CE6", null)]
    [InlineData("", null)]
    public void The_XInput_slot_comes_from_SDL_s_path(string path, int? slot) =>
        Assert.Equal(slot, (KnownPads.Xbox360 with { Path = path }).XInputSlot);

    [Fact]
    public void The_log_names_the_pad_by_name_type_and_USB_ids_but_not_its_path() =>
        Assert.Equal("\"Steam Controller\" (Steam, 28DE:1304)", KnownPads.SteamController.ToString());

    private static PadDevice Pad(string name) => (PadDevice)typeof(KnownPads).GetField(name)!.GetValue(null)!;

    private sealed class Emulated(Func<PadDevice, bool> isEmulated) : IEmulatedPads
    {
        public bool IsEmulated(PadDevice device) => isEmulated(device);
    }
}
