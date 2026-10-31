using System.Buffers.Binary;
using Arcadia.Input;

namespace Arcadia.ControllerSupport;

/// <summary>
/// The raw stream of VIIPER's DualShock 4, DualSense (Edge) and Switch 2 Pro devices (#238 part 2): fixed-size input
/// packets from Arcadia, fixed-size feedback packets from VIIPER, as the fork's <c>device/*/state.go</c> lays them out.
/// Sticks, gyro and accelerometer are turned back into what SDL on the host computes from the device's reports, so a
/// game reading the emulated pad through SDL sees the real controller's values.
/// </summary>
public static class VirtualPadReports
{
    /// <summary>VIIPER's device type for the kind.</summary>
    public static string DeviceType(PadKind kind) => kind switch
    {
        PadKind.DualShock4 => "dualshock4",
        PadKind.DualSense => "dualsense",
        PadKind.DualSenseEdge => "dualsenseedge",
        PadKind.Switch2Pro => "ns2pro",
        _ => "xbox360",
    };

    /// <summary>The size of one feedback packet VIIPER sends for the kind.</summary>
    public static int FeedbackSize(PadKind kind) => kind switch
    {
        PadKind.DualShock4 => 8,
        PadKind.DualSense or PadKind.DualSenseEdge => 6,
        PadKind.Switch2Pro => 34,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "kein Rohdatenstrom"),
    };

    /// <summary>One input packet for the kind.</summary>
    public static byte[] Input(PadKind kind, VirtualPadState state) => kind switch
    {
        PadKind.DualShock4 => PlayStation(state, Ds4),
        PadKind.DualSense or PadKind.DualSenseEdge => PlayStation(state, DualSense),
        PadKind.Switch2Pro => Switch2Pro(state),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "kein Rohdatenstrom"),
    };

    /// <summary>One feedback packet of the kind: rumble and light bar where it changed them.</summary>
    public static PadFeedback Feedback(PadKind kind, ReadOnlySpan<byte> packet) => kind switch
    {
        PadKind.DualShock4 => new PadFeedback(
            (packet[0] & 0x01) != 0 ? new XboxRumble(Large: packet[2], Small: packet[1]) : null,
            (packet[0] & 0x02) != 0 ? new PadLight(packet[3], packet[4], packet[5]) : null),
        // VIIPER passes the motors of every report on, and the colour only from a report that sets it (zero otherwise).
        PadKind.DualSense or PadKind.DualSenseEdge => new PadFeedback(
            new XboxRumble(Large: packet[1], Small: packet[0]),
            packet[2] != 0 || packet[3] != 0 || packet[4] != 0 ? new PadLight(packet[2], packet[3], packet[4]) : null),
        PadKind.Switch2Pro => new PadFeedback((packet[32] & 0x01) != 0 ? Switch2ProRumble(packet) : null, null),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "kein Rohdatenstrom"),
    };

    private sealed record PlayStationLayout(
        int Size,
        bool WideButtons,
        uint Guide,
        uint TouchpadClick,
        uint Mute,
        uint LeftPaddle,
        uint RightPaddle,
        uint LeftFunction,
        uint RightFunction,
        ushort TouchpadHeight,
        double GyroCountsPerDps);

    // dualshock4/const.go: 16-bit buttons, PS and the touchpad's click in the low bits; touchpad 1920 × 942.
    private static readonly PlayStationLayout Ds4 = new(31, false, 0x0001, 0x0002, 0, 0, 0, 0, 0, 942, 16.0);

    // dualsense/const.go: 32-bit buttons with mute and the Edge's paddles and Fn buttons; touchpad 1920 × 1080.
    private static readonly PlayStationLayout DualSense = new(33, true, 0x0001_0000, 0x0002_0000, 0x0004_0000, 0x0040_0000, 0x0080_0000, 0x0010_0000, 0x0020_0000, 1080, 16.384);

    private const ushort TouchpadWidth = 1920;

    /// <summary>Accelerometer counts per m/s² of both PlayStation pads (8192 per g), as VIIPER's calibration reports.</summary>
    private const double PlayStationAccelCountsPerMs2 = 835.07;

    private static readonly (PadButtons From, uint To)[] PlayStationButtons =
    [
        (PadButtons.X, 0x0010),
        (PadButtons.A, 0x0020),
        (PadButtons.B, 0x0040),
        (PadButtons.Y, 0x0080),
        (PadButtons.LeftShoulder, 0x0100),
        (PadButtons.RightShoulder, 0x0200),
        (PadButtons.View, 0x1000),
        (PadButtons.Menu, 0x2000),
        (PadButtons.LeftStickClick, 0x4000),
        (PadButtons.RightStickClick, 0x8000),
    ];

    private const uint L2 = 0x0400;
    private const uint R2 = 0x0800;

    private static byte[] PlayStation(VirtualPadState state, PlayStationLayout layout)
    {
        uint buttons = 0;
        foreach (var (from, to) in PlayStationButtons)
        {
            if ((state.Buttons & from) != 0)
            {
                buttons |= to;
            }
        }

        // The real pads set the trigger's button as soon as it moves.
        var l2 = TriggerByte(state.Triggers.Left);
        var r2 = TriggerByte(state.Triggers.Right);
        buttons |= (l2 > 0 ? L2 : 0) | (r2 > 0 ? R2 : 0);
        buttons |= (state.Guide ? layout.Guide : 0) | (state.Touchpad.Clicked ? layout.TouchpadClick : 0);
        buttons |= Has(state, ExtraButtons.Misc1, layout.Mute) | Has(state, ExtraButtons.LeftPaddle1, layout.LeftPaddle)
            | Has(state, ExtraButtons.RightPaddle1, layout.RightPaddle) | Has(state, ExtraButtons.LeftPaddle2, layout.LeftFunction)
            | Has(state, ExtraButtons.RightPaddle2, layout.RightFunction);

        var packet = new byte[layout.Size];
        var span = packet.AsSpan();
        span[0] = (byte)StickByte(state.LeftStick.X);
        span[1] = (byte)StickByte(-state.LeftStick.Y);
        span[2] = (byte)StickByte(state.RightStick.X);
        span[3] = (byte)StickByte(-state.RightStick.Y);
        int at;
        if (layout.WideButtons)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[4..], buttons);
            at = 8;
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[4..], (ushort)buttons);
            at = 6;
        }

        span[at] = DPad(state.Buttons);
        span[at + 1] = l2;
        span[at + 2] = r2;
        at += 3;

        // Arcadia's touchpad runs from -1 to 1 with up positive; the pad's from the top left corner.
        if (state.Touchpad.Touched)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[at..], Scale((state.Touchpad.X + 1) / 2, TouchpadWidth));
            BinaryPrimitives.WriteUInt16LittleEndian(span[(at + 2)..], Scale((1 - state.Touchpad.Y) / 2, layout.TouchpadHeight));
            span[at + 4] = 1;
        }

        // The second finger isn't read (SDL's first finger only): it stays lifted.
        at += 10;
        var (gyro, accel) = Motion(state.Motion);
        WriteAxes(span[at..], gyro, RadiansToDegrees * layout.GyroCountsPerDps);
        WriteAxes(span[(at + 6)..], accel, PlayStationAccelCountsPerMs2);
        return packet;
    }

    private static uint Has(VirtualPadState state, ExtraButtons button, uint bit) => (state.Extra & button) != 0 ? bit : 0;

    private static byte DPad(PadButtons buttons) => (byte)(
        ((buttons & PadButtons.DPadUp) != 0 ? 0x01 : 0)
        | ((buttons & PadButtons.DPadDown) != 0 ? 0x02 : 0)
        | ((buttons & PadButtons.DPadLeft) != 0 ? 0x04 : 0)
        | ((buttons & PadButtons.DPadRight) != 0 ? 0x08 : 0));

    // ns2pro/const.go: B, A, Y, X, R, ZR, +, right stick, down, right, left, up, L, ZL, −, left stick, Home, Capture, GR, GL, C.
    private static readonly (PadButtons From, uint To)[] SwitchButtons =
    [
        (PadButtons.A, 1u << 0),
        (PadButtons.B, 1u << 1),
        (PadButtons.X, 1u << 2),
        (PadButtons.Y, 1u << 3),
        (PadButtons.RightShoulder, 1u << 4),
        (PadButtons.RightTrigger, 1u << 5),
        (PadButtons.Menu, 1u << 6),
        (PadButtons.RightStickClick, 1u << 7),
        (PadButtons.DPadDown, 1u << 8),
        (PadButtons.DPadRight, 1u << 9),
        (PadButtons.DPadLeft, 1u << 10),
        (PadButtons.DPadUp, 1u << 11),
        (PadButtons.LeftShoulder, 1u << 12),
        (PadButtons.LeftTrigger, 1u << 13),
        (PadButtons.View, 1u << 14),
        (PadButtons.LeftStickClick, 1u << 15),
    ];

    private const uint SwitchHome = 1u << 16;
    private const uint SwitchCapture = 1u << 17;
    private const uint SwitchGr = 1u << 18;
    private const uint SwitchGl = 1u << 19;
    private const uint SwitchC = 1u << 20;

    private const int SwitchStickCenter = 0x800;
    private const int SwitchStickReach = 0x7FF;

    /// <summary>SDL's full scale of the Switch 2 Pro's gyro, in rad/s per 32767 counts (its sensor clock in microseconds).</summary>
    private const double SwitchGyroFullScale = 34.8;

    /// <summary>SDL's full scale of the Switch 2 Pro's accelerometer: 8 g per 32767 counts.</summary>
    private const double SwitchAccelFullScale = StandardGravity * 8;

    /// <summary>SDL clamps the motors' amplitude to this before it encodes them (<c>RUMBLE_MAX</c>).</summary>
    private const double SwitchRumbleMax = 29000;

    private static byte[] Switch2Pro(VirtualPadState state)
    {
        // By position, as everywhere (#72): the bottom face button is Nintendo's B.
        uint buttons = 0;
        foreach (var (from, to) in SwitchButtons)
        {
            if ((state.Buttons & from) != 0)
            {
                buttons |= to;
            }
        }

        buttons |= (state.Guide ? SwitchHome : 0) | Has(state, ExtraButtons.Misc1, SwitchCapture) | Has(state, ExtraButtons.Misc2, SwitchC)
            | Has(state, ExtraButtons.RightPaddle1, SwitchGr) | Has(state, ExtraButtons.LeftPaddle1, SwitchGl);

        var packet = new byte[24];
        var span = packet.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, buttons);

        // 12 bits around 0x800, up positive (SDL inverts the Y axes).
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], SwitchStick(state.LeftStick.X));
        BinaryPrimitives.WriteUInt16LittleEndian(span[6..], SwitchStick(state.LeftStick.Y));
        BinaryPrimitives.WriteUInt16LittleEndian(span[8..], SwitchStick(state.RightStick.X));
        BinaryPrimitives.WriteUInt16LittleEndian(span[10..], SwitchStick(state.RightStick.Y));

        // SDL's Switch 2 driver reads accelerometer (x, z, -y) and gyro (x, z, -y) from the report; this is its inverse.
        var (gyro, accel) = Motion(state.Motion);
        var accelCounts = short.MaxValue / SwitchAccelFullScale;
        var gyroCounts = short.MaxValue / SwitchGyroFullScale;
        WriteAxes(span[12..], new SensorAxes(accel.X, -accel.Z, accel.Y), accelCounts);
        WriteAxes(span[18..], new SensorAxes(gyro.X, -gyro.Z, gyro.Y), gyroCounts);
        return packet;
    }

    private static ushort SwitchStick(double value) =>
        (ushort)(SwitchStickCenter + (int)Math.Round(Math.Clamp(value, -1, 1) * SwitchStickReach, MidpointRounding.AwayFromZero));

    /// <summary>
    /// The HD rumble SDL sends a Switch 2 Pro, back to two motors: each side's low band to the large motor, its high band
    /// to the small one, the stronger side of each.
    /// </summary>
    private static XboxRumble Switch2ProRumble(ReadOnlySpan<byte> packet)
    {
        var left = packet[..16];
        var right = packet[16..32];
        return new XboxRumble(
            Large: Math.Max(SwitchAmplitude(LowAmplitude(left)), SwitchAmplitude(LowAmplitude(right))),
            Small: Math.Max(SwitchAmplitude(HighAmplitude(left)), SwitchAmplitude(HighAmplitude(right))));
    }

    // SDL's EncodeHDRumble after the sequence byte: high frequency (10 bits), high amplitude, low frequency, low amplitude.
    private static int HighAmplitude(ReadOnlySpan<byte> side) => ((side[3] & 0x0F) << 12) | ((side[2] & 0xFC) << 4);

    private static int LowAmplitude(ReadOnlySpan<byte> side) => (side[5] << 8) | (side[4] & 0xC0);

    private static byte SwitchAmplitude(int amplitude) => (byte)Math.Min(255, Math.Round(amplitude * 255 / SwitchRumbleMax));

    private const double StandardGravity = 9.80665;
    private const double RadiansToDegrees = 180 / Math.PI;

    /// <summary>The controller's motion; off, a pad lying flat and still (SDL's axes: gravity on +Y).</summary>
    private static (SensorAxes Gyro, SensorAxes Accel) Motion(MotionReading motion) =>
        motion.IsOn ? (motion.Gyro, motion.Accelerometer) : (default, new SensorAxes(0, (float)StandardGravity, 0));

    private static void WriteAxes(Span<byte> span, SensorAxes axes, double countsPerUnit)
    {
        BinaryPrimitives.WriteInt16LittleEndian(span, Counts(axes.X, countsPerUnit));
        BinaryPrimitives.WriteInt16LittleEndian(span[2..], Counts(axes.Y, countsPerUnit));
        BinaryPrimitives.WriteInt16LittleEndian(span[4..], Counts(axes.Z, countsPerUnit));
    }

    private static short Counts(double value, double countsPerUnit) =>
        (short)Math.Clamp(Math.Round(value * countsPerUnit, MidpointRounding.AwayFromZero), short.MinValue, short.MaxValue);

    /// <summary>A stick axis to VIIPER's -128..127 (centre 0), the direction as given.</summary>
    private static sbyte StickByte(double value) =>
        (sbyte)((int)Math.Round((Math.Clamp(value, -1, 1) + 1) / 2 * 255, MidpointRounding.AwayFromZero) - 128);

    private static byte TriggerByte(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255, MidpointRounding.AwayFromZero);

    private static ushort Scale(double fraction, ushort max) => (ushort)Math.Round(Math.Clamp(fraction, 0, 1) * max, MidpointRounding.AwayFromZero);
}
