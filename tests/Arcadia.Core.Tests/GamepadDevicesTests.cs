using Arcadia.Input;
using Arcadia.Tests.Sdl;

namespace Arcadia.Core.Tests;

public sealed class GamepadDevicesTests
{
    public static TheoryData<string> Read => new(nameof(KnownPads.Xbox360), nameof(KnownPads.XboxSeries), nameof(KnownPads.EightBitDoUltimate2),
        nameof(KnownPads.DualSense), nameof(KnownPads.DualShock4), nameof(KnownPads.SwitchPro), nameof(KnownPads.JoyConPair));

    [Theory]
    [MemberData(nameof(Read))]
    public void Physical_gamepads_are_read(string pad) => Assert.Null(GamepadDevices.WhyIgnored(Pad(pad)));

    /// <summary>A third-party pad unknown to SDL's type list but in its controller database (e.g. a cheap USB pad).</summary>
    [Fact]
    public void A_pad_of_unknown_type_and_kind_is_read() =>
        Assert.Null(GamepadDevices.WhyIgnored(new PadDevice("USB Gamepad", 0x0079, 0x0006, PadType.Unknown, StickDeviceType.Unknown)));

    [Theory]
    [InlineData(nameof(KnownPads.SteamVirtual), "virtueller Controller von Steam")]
    [InlineData(nameof(KnownPads.SteamController), "Steam Controller, kommt über SISR")]
    [InlineData(nameof(KnownPads.JoyConLeft), "einzelner Joy-Con")]
    [InlineData(nameof(KnownPads.Wheel), "kein Gamepad (Wheel)")]
    public void Steam_s_virtual_pads_the_Steam_Controller_single_Joy_Cons_and_wheels_are_ignored(string pad, string reason) =>
        Assert.Equal(reason, GamepadDevices.WhyIgnored(Pad(pad)));

    [Fact]
    public void Any_Valve_device_waits_for_212_even_if_SDL_types_it_otherwise()
    {
        Assert.NotNull(GamepadDevices.WhyIgnored(new PadDevice("Steam Deck", PadDevice.ValveVendor, 0x1205, PadType.Standard, StickDeviceType.Gamepad)));
        Assert.NotNull(GamepadDevices.WhyIgnored(new PadDevice("Steam Controller", 0, 0, PadType.Steam, StickDeviceType.Gamepad)));
        Assert.NotNull(GamepadDevices.WhyIgnored(KnownPads.JoyConLeft with { Type = PadType.JoyConRight }));
        Assert.NotNull(GamepadDevices.WhyIgnored(KnownPads.Wheel with { StickType = StickDeviceType.FlightStick }));
    }

    [Fact]
    public void The_log_names_the_pad_by_name_type_and_USB_ids() =>
        Assert.Equal("\"DualSense Wireless Controller\" (PS5, 054C:0CE6)", KnownPads.DualSense.ToString());

    private static PadDevice Pad(string name) => (PadDevice)typeof(KnownPads).GetField(name)!.GetValue(null)!;
}
