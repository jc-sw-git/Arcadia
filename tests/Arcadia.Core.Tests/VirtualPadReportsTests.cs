using System.Buffers.Binary;
using Arcadia.ControllerSupport;
using Arcadia.Input;
using Arcadia.Tests.Sdl;

namespace Arcadia.Core.Tests;

/// <summary>
/// The packets of VIIPER's DualShock 4, DualSense and Switch 2 Pro pads (#238 part 2): the layout of the fork's
/// <c>device/*/state.go</c>, buttons by position, and motion turned back into what SDL computes from the device.
/// </summary>
public sealed class VirtualPadReportsTests
{
    private static VirtualPadState Pressed(PadButtons buttons) => VirtualPadState.Neutral with { Buttons = buttons };

    [Theory]
    [InlineData(nameof(KnownPads.DualSense), PadKind.DualSense)]
    [InlineData(nameof(KnownPads.DualSenseEdge), PadKind.DualSenseEdge)]
    [InlineData(nameof(KnownPads.DualShock4), PadKind.DualShock4)]
    [InlineData(nameof(KnownPads.Switch2Pro), PadKind.Switch2Pro)]
    [InlineData(nameof(KnownPads.SwitchPro), PadKind.Xbox360)]
    [InlineData(nameof(KnownPads.GameCube), PadKind.Xbox360)]
    [InlineData(nameof(KnownPads.EightBitDoUltimate2), PadKind.Xbox360)]
    [InlineData(nameof(KnownPads.XboxSeries), PadKind.Xbox360)]
    public void Each_controller_is_emulated_as_its_own_kind_where_VIIPER_has_it_otherwise_as_an_Xbox_360_pad(string pad, PadKind expected)
    {
        var device = (PadDevice)typeof(KnownPads).GetField(pad)!.GetValue(null)!;

        Assert.Equal(expected, PadKinds.Default(device));
    }

    [Theory]
    [InlineData(PadKind.DualShock4, 31)]
    [InlineData(PadKind.DualSense, 33)]
    [InlineData(PadKind.DualSenseEdge, 33)]
    [InlineData(PadKind.Switch2Pro, 24)]
    public void Each_kind_has_the_packet_size_of_VIIPERs_wire_format(PadKind kind, int size)
    {
        Assert.Equal(size, VirtualPadReports.Input(kind, VirtualPadState.Neutral).Length);
    }

    [Fact]
    public void A_DualSense_gets_its_face_buttons_by_position_and_its_other_buttons_in_VIIPERs_bits()
    {
        var state = Pressed(PadButtons.A | PadButtons.B | PadButtons.X | PadButtons.Y | PadButtons.LeftShoulder | PadButtons.View | PadButtons.Menu | PadButtons.DPadUp | PadButtons.DPadLeft)
            with
        { Guide = true, Extra = ExtraButtons.Misc1, Touchpad = new TouchpadReading(false, 0, 0, Clicked: true) };

        var packet = VirtualPadReports.Input(PadKind.DualSense, state);

        // Cross 0x20, Circle 0x40, Square 0x10, Triangle 0x80, L1 0x100, Create 0x1000, Options 0x2000, PS, touchpad, mute.
        Assert.Equal(0x0007_31F0u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)));
        Assert.Equal(0x05, packet[8]);
    }

    [Fact]
    public void The_Edge_gets_its_back_paddles_and_Fn_buttons_and_a_plain_DualSense_the_same_bits()
    {
        var state = VirtualPadState.Neutral with
        {
            Extra = ExtraButtons.LeftPaddle1 | ExtraButtons.RightPaddle1 | ExtraButtons.LeftPaddle2 | ExtraButtons.RightPaddle2,
        };

        var packet = VirtualPadReports.Input(PadKind.DualSenseEdge, state);

        Assert.Equal(0x00F0_0000u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)));
    }

    [Fact]
    public void A_DualShock_4_gets_PS_and_the_touchpad_click_in_its_low_bits_and_no_mute()
    {
        var state = Pressed(PadButtons.A) with { Guide = true, Extra = ExtraButtons.Misc1, Touchpad = new TouchpadReading(false, 0, 0, Clicked: true) };

        var packet = VirtualPadReports.Input(PadKind.DualShock4, state);

        Assert.Equal(0x0023, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(4)));
    }

    [Fact]
    public void PlayStation_sticks_have_down_positive_and_triggers_set_their_button_as_soon_as_they_move()
    {
        var state = VirtualPadState.Neutral with
        {
            LeftStick = new StickPosition(1, 1),
            RightStick = new StickPosition(-1, -1),
            Triggers = new TriggerPosition(0.01, 0),
        };

        var packet = VirtualPadReports.Input(PadKind.DualShock4, state);

        Assert.Equal([127, unchecked((byte)-128), unchecked((byte)-128), 127], packet[..4]);
        Assert.Equal(0x0400, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(4)));
        Assert.Equal(3, packet[7]);
        Assert.Equal(0, packet[8]);
    }

    [Fact]
    public void Centred_sticks_report_the_centre()
    {
        var packet = VirtualPadReports.Input(PadKind.DualSense, VirtualPadState.Neutral);

        Assert.Equal([0, 0, 0, 0], packet[..4]);
    }

    [Fact]
    public void The_touchpad_goes_from_Arcadias_centred_axes_to_the_pads_corner_and_its_size()
    {
        var topLeft = VirtualPadState.Neutral with { Touchpad = new TouchpadReading(true, -1, 1, false) };
        var bottomRight = VirtualPadState.Neutral with { Touchpad = new TouchpadReading(true, 1, -1, false) };

        var ds4 = VirtualPadReports.Input(PadKind.DualShock4, bottomRight);
        var dualSense = VirtualPadReports.Input(PadKind.DualSense, bottomRight);
        var corner = VirtualPadReports.Input(PadKind.DualSense, topLeft);

        Assert.Equal((1920, 942, 1), (BinaryPrimitives.ReadUInt16LittleEndian(ds4.AsSpan(9)), BinaryPrimitives.ReadUInt16LittleEndian(ds4.AsSpan(11)), ds4[13]));
        Assert.Equal((1920, 1080, 1), (BinaryPrimitives.ReadUInt16LittleEndian(dualSense.AsSpan(11)), BinaryPrimitives.ReadUInt16LittleEndian(dualSense.AsSpan(13)), dualSense[15]));
        Assert.Equal((0, 0, 1), (BinaryPrimitives.ReadUInt16LittleEndian(corner.AsSpan(11)), BinaryPrimitives.ReadUInt16LittleEndian(corner.AsSpan(13)), corner[15]));
        Assert.Equal(0, VirtualPadReports.Input(PadKind.DualSense, VirtualPadState.Neutral)[15]);
    }

    [Fact]
    public void PlayStation_motion_goes_back_to_the_pads_counts_on_SDLs_axes()
    {
        // 90 °/s about each axis; 1 g on X, -1 g on Z.
        var gyro = (float)(Math.PI / 2);
        var state = VirtualPadState.Neutral with
        {
            Motion = new MotionReading(true, new SensorAxes(gyro, -gyro, gyro), new SensorAxes(9.81f, 0, -9.81f)),
        };

        var ds4 = VirtualPadReports.Input(PadKind.DualShock4, state);
        var dualSense = VirtualPadReports.Input(PadKind.DualSense, state);

        Assert.Equal([1440, -1440, 1440, 8192, 0, -8192], Shorts(ds4, 19, 6));
        Assert.Equal([1475, -1475, 1475, 8192, 0, -8192], Shorts(dualSense, 21, 6));
    }

    [Fact]
    public void Without_motion_a_PlayStation_pad_lies_flat_and_still()
    {
        var packet = VirtualPadReports.Input(PadKind.DualSense, VirtualPadState.Neutral);

        Assert.Equal([0, 0, 0, 0, 8189, 0], Shorts(packet, 21, 6));
    }

    [Fact]
    public void A_Switch_2_Pro_gets_its_buttons_by_position_so_the_bottom_one_is_Nintendos_B()
    {
        var state = Pressed(PadButtons.A | PadButtons.Y | PadButtons.RightTrigger | PadButtons.View | PadButtons.DPadUp)
            with
        { Guide = true, Extra = ExtraButtons.Misc1 | ExtraButtons.Misc2 | ExtraButtons.LeftPaddle1 | ExtraButtons.RightPaddle1 };

        var packet = VirtualPadReports.Input(PadKind.Switch2Pro, state);

        // B, X, ZR, −, up, Home, Capture, GR, GL, C.
        Assert.Equal((1u << 0) | (1u << 3) | (1u << 5) | (1u << 14) | (1u << 11) | (0x1Fu << 16), BinaryPrimitives.ReadUInt32LittleEndian(packet));
    }

    [Fact]
    public void Switch_2_Pro_sticks_are_12_bits_around_the_centre_with_up_positive()
    {
        var state = VirtualPadState.Neutral with { LeftStick = new StickPosition(1, 1), RightStick = new StickPosition(-1, 0) };

        var packet = VirtualPadReports.Input(PadKind.Switch2Pro, state);

        Assert.Equal([0xFFF, 0xFFF, 0x001, 0x800], Enumerable.Range(0, 4).Select(i => (int)BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(4 + i * 2))));
    }

    [Fact]
    public void Switch_2_Pro_motion_is_the_inverse_of_SDLs_axis_swap()
    {
        // SDL reads accelerometer (x, z, -y) and gyro (x, z, -y) from the report.
        var state = VirtualPadState.Neutral with
        {
            Motion = new MotionReading(true, new SensorAxes(1, 2, 3), new SensorAxes(9.80665f, 0, -9.80665f)),
        };

        var packet = VirtualPadReports.Input(PadKind.Switch2Pro, state);

        var counts = Shorts(packet, 12, 6);
        Assert.Equal([4096, 4096, 0], counts[..3]);
        Assert.Equal([942, -2825, 1883], counts[3..]);
    }

    [Fact]
    public void A_DualShock_4s_feedback_carries_rumble_and_light_only_where_its_flags_say_so()
    {
        Assert.Equal(new PadFeedback(new XboxRumble(Large: 200, Small: 50), null), VirtualPadReports.Feedback(PadKind.DualShock4, [0x01, 50, 200, 1, 2, 3, 0, 0]));
        Assert.Equal(new PadFeedback(null, new PadLight(1, 2, 3)), VirtualPadReports.Feedback(PadKind.DualShock4, [0x02, 50, 200, 1, 2, 3, 0, 0]));
    }

    [Fact]
    public void A_DualSenses_feedback_always_carries_rumble_and_a_colour_only_when_one_was_set()
    {
        Assert.Equal(new PadFeedback(new XboxRumble(Large: 9, Small: 8), null), VirtualPadReports.Feedback(PadKind.DualSense, [8, 9, 0, 0, 0, 0]));
        Assert.Equal(new PadFeedback(new XboxRumble(0, 0), new PadLight(0, 0, 255)), VirtualPadReports.Feedback(PadKind.DualSenseEdge, [0, 0, 0, 0, 255, 1]));
    }

    [Fact]
    public void A_Switch_2_Pros_HD_rumble_from_SDL_comes_back_as_two_motors()
    {
        var packet = new byte[34];
        EncodeLikeSdl(packet.AsSpan(0, 16), highAmp: 29000, lowAmp: 14500);
        EncodeLikeSdl(packet.AsSpan(16, 16), highAmp: 0, lowAmp: 29000);
        packet[32] = 0x01;

        var feedback = VirtualPadReports.Feedback(PadKind.Switch2Pro, packet);

        Assert.Equal(new XboxRumble(Large: 255, Small: 255), feedback.Rumble);
        Assert.Null(VirtualPadReports.Feedback(PadKind.Switch2Pro, new byte[34]).Rumble);
    }

    [Fact]
    public void A_Switch_2_Pros_quiet_rumble_stays_quiet()
    {
        var packet = new byte[34];
        EncodeLikeSdl(packet.AsSpan(0, 16), highAmp: 0, lowAmp: 0);
        EncodeLikeSdl(packet.AsSpan(16, 16), highAmp: 0, lowAmp: 0);
        packet[32] = 0x01;

        Assert.Equal(new XboxRumble(0, 0), VirtualPadReports.Feedback(PadKind.Switch2Pro, packet).Rumble);
    }

    [Fact]
    public void An_Xbox_360_pad_takes_what_it_has_with_Guide()
    {
        var state = Pressed(PadButtons.A | PadButtons.LeftTrigger) with
        {
            Guide = true,
            Extra = ExtraButtons.Misc1,
            Triggers = new TriggerPosition(1, 0),
            LeftStick = new StickPosition(1, -1),
        };

        Assert.Equal(new XboxPadState((ushort)(0x1000 | XboxPadState.GuideBit), 255, 0, 32767, -32767, 0, 0), XboxPadState.From(state));
    }

    /// <summary>SDL's <c>EncodeHDRumble</c> after the sequence byte, with its default frequencies.</summary>
    private static void EncodeLikeSdl(Span<byte> side, ushort highAmp, ushort lowAmp)
    {
        const ushort highFreq = 0x187;
        const ushort lowFreq = 0x112;
        side[0] = 0x50;
        side[1] = (byte)(highFreq & 0xFF);
        side[2] = (byte)(((highAmp >> 4) & 0xFC) | ((highFreq >> 8) & 0x03));
        side[3] = (byte)((highAmp >> 12) | (lowFreq << 4));
        side[4] = (byte)((lowAmp & 0xC0) | ((lowFreq >> 4) & 0x3F));
        side[5] = (byte)(lowAmp >> 8);
    }

    private static short[] Shorts(byte[] packet, int at, int count) =>
        [.. Enumerable.Range(0, count).Select(i => BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(at + i * 2)))];
}
