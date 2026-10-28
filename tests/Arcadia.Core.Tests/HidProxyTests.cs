using System.Buffers.Binary;
using Arcadia.ControllerSupport.Copy;
using Arcadia.Input;

namespace Arcadia.Tests.Pads;

/// <summary>
/// The stream protocol of VIIPER's <c>hidproxy</c> (#242 part B) and the puck's descriptors the copy enumerates with:
/// the same framing as the fork's <c>protocol.go</c>, and nothing of the connected puck but a checked serial number.
/// </summary>
public sealed class HidProxyTests
{
    [Fact]
    [Trait("TestCase", "T242-4")]
    public async Task A_frame_written_is_read_back_whole_and_a_clean_end_reads_as_none()
    {
        var buffer = new byte[16];
        var size = HidProxyFrames.Write(buffer, HidProxyFrames.Input, 3, [0x42, 0x01, 0x02]);
        using var stream = new MemoryStream(buffer[..size]);

        var frame = await HidProxyFrames.ReadAsync(stream, CancellationToken.None);
        var end = await HidProxyFrames.ReadAsync(stream, CancellationToken.None);

        Assert.Equal([0x01, 0x03, 0x03, 0x00, 0x42, 0x01, 0x02], buffer[..size]);
        Assert.Equal((HidProxyFrames.Input, (byte)3), (frame!.Value.Kind, frame.Value.Interface));
        Assert.Equal([0x42, 0x01, 0x02], frame.Value.Payload);
        Assert.Null(end);
    }

    [Fact]
    [Trait("TestCase", "T242-4")]
    public void A_reply_carries_the_sequence_the_status_and_the_data()
    {
        var buffer = new byte[16];
        var size = HidProxyFrames.WriteReply(buffer, 2, 0x0102, done: false, [0xAA]);

        Assert.Equal([HidProxyFrames.ControlReply, 0x02, 0x04, 0x00, 0x02, 0x01, HidProxyFrames.StatusRefused, 0xAA], buffer[..size]);
    }

    [Fact]
    [Trait("TestCase", "T242-6")]
    public async Task A_frame_cut_off_or_longer_than_the_protocol_allows_is_an_error()
    {
        using var cut = new MemoryStream([HidProxyFrames.Output, 2, 0x04, 0x00, 0x80]);
        var tooLong = new byte[4];
        tooLong[0] = HidProxyFrames.Output;
        BinaryPrimitives.WriteUInt16LittleEndian(tooLong.AsSpan(2), HidProxyFrames.MaxPayload + 1);
        using var longStream = new MemoryStream(tooLong);
        using var halfHeader = new MemoryStream([HidProxyFrames.Output, 2]);

        await Assert.ThrowsAsync<EndOfStreamException>(() => HidProxyFrames.ReadAsync(cut, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => HidProxyFrames.ReadAsync(longStream, CancellationToken.None));
        await Assert.ThrowsAsync<EndOfStreamException>(() => HidProxyFrames.ReadAsync(halfHeader, CancellationToken.None));
        Assert.Throws<ArgumentException>(() => HidProxyFrames.Write(new byte[HidProxyFrames.MaxPayload + 8], 1, 0, new byte[HidProxyFrames.MaxPayload + 1]));
    }

    [Fact]
    [Trait("TestCase", "T242-4")]
    public void The_puck_has_a_CDC_pair_and_five_HID_interfaces_with_64_byte_reports()
    {
        var puck = PuckDescriptors.Triton;

        Assert.Equal(new Dictionary<byte, byte> { [0] = 0x02, [1] = 0x0A, [2] = 3, [3] = 3, [4] = 3, [5] = 3, [6] = 3 }, puck.InterfaceClasses);
        Assert.False(puck.IsHid(0));
        Assert.True(puck.IsHid(2));
        Assert.False(puck.IsHid(7));
        Assert.Equal(new byte[] { 2, 3, 4, 5, 6 }, puck.ReportSizes.Keys.Order());
    }

    [Fact]
    [Trait("TestCase", "T242-4")]
    public void Report_sizes_are_counted_per_id_and_main_item()
    {
        // Report id 1: 8 × 1 bit input, 2 × 8 bit input (inside Push/Pop), then 8 × 1 bit again with the popped globals;
        // report id 2: 63 × 8 bit feature.
        byte[] descriptor =
        [
            0x85, 0x01, 0x75, 0x01, 0x95, 0x08, 0x81, 0x02,
            0xA4, 0x75, 0x08, 0x95, 0x02, 0x81, 0x02, 0xB4, 0x81, 0x02,
            0x85, 0x02, 0x75, 0x08, 0x95, 0x3F, 0xB1, 0x02,
        ];

        var sizes = HidReportSizes.Parse(descriptor);

        Assert.True(sizes.UsesIds);
        Assert.Equal(4, sizes.Input[1]);
        Assert.Equal(63, sizes.Feature[2]);
        Assert.Empty(sizes.Output);
    }

    [Theory]
    [Trait("TestCase", "T242-4")]
    [InlineData(@"PCI\VEN_8086&DEV_7A60\3&11583659&0&A0", DeviceHost.Hardware, true)]
    [InlineData(@"ACPI\PNP0A08\0", DeviceHost.Hardware, true)]
    [InlineData(@"ROOT\USBIP_WIN2\0000", DeviceHost.Software, false)]
    [InlineData(@"SWD\MMDEVAPI\X", DeviceHost.Software, false)]
    [InlineData("", DeviceHost.Unknown, false)]
    [InlineData(@"BTHENUM\X", DeviceHost.Unknown, false)]
    public void Only_a_puck_on_a_known_hardware_host_is_the_real_one(string hostId, DeviceHost host, bool real)
    {
        Assert.Equal(host, DeviceTree.HostOf(hostId));
        Assert.Equal(real, PuckDescriptors.IsRealPuck(DeviceTree.HostOf(hostId)));
    }

    [Theory]
    [Trait("TestCase", "T242-4")]
    [InlineData("FXA1234567", true)]
    [InlineData("", false)]
    [InlineData("FX-123", false)]
    [InlineData("FX 123", false)]
    [InlineData("FXÄ123", false)]
    [InlineData("123456789012345678901234567890123", false)]
    public void Only_a_serial_of_letters_and_digits_goes_to_VIIPER(string serial, bool valid)
    {
        Assert.Equal(valid, PuckDescriptors.IsValidSerial(serial));
        if (!valid)
        {
            Assert.Throws<ArgumentException>(() => PuckDescriptors.Triton.DeviceSpecific(serial));
        }
    }

    [Fact]
    [Trait("TestCase", "T242-4")]
    public void The_copy_is_the_puck_with_the_connected_one_s_serial_as_string_3()
    {
        var options = PuckDescriptors.Triton.DeviceSpecific("FXA1234567");

        var strings = Assert.IsType<Dictionary<string, string>>(options["strings"]);
        Assert.Equal("FXA1234567", strings["3"]);
        Assert.Equal("Steam Controller Puck", strings["2"]);
        Assert.StartsWith("12010102", (string)options["device"]!, StringComparison.Ordinal);
        Assert.Equal(5, Assert.IsType<Dictionary<string, string>>(options["reports"]).Count);
        // A second copy doesn't share the first one's strings.
        Assert.False(PuckDescriptors.Triton.DeviceSpecific("OTHER1").Equals(options));
        Assert.Equal("FXA1234567", strings["3"]);
    }

    [Theory]
    [Trait("TestCase", "T242-8")]
    [InlineData(0x21, 0x09, 0x0301, true, false, SteamReportKind.SetFeature)]
    [InlineData(0x21, 0x09, 0x0201, true, false, SteamReportKind.Output)]
    [InlineData(0x21, 0x09, 0x0101, true, false, SteamReportKind.Other)]
    [InlineData(0xA1, 0x01, 0x0301, true, false, SteamReportKind.GetReport)]
    [InlineData(0xA1, 0x01, 0x0101, true, false, SteamReportKind.GetReport)]
    [InlineData(0xA1, 0x01, 0x0201, true, false, SteamReportKind.Other)]
    [InlineData(0x21, 0x0A, 0x0000, true, false, SteamReportKind.Housekeeping)]
    [InlineData(0x21, 0x0B, 0x0001, true, false, SteamReportKind.Housekeeping)]
    [InlineData(0xA1, 0x02, 0x0000, true, false, SteamReportKind.Housekeeping)]
    [InlineData(0xA1, 0x03, 0x0000, true, false, SteamReportKind.Housekeeping)]
    [InlineData(0x21, 0x20, 0x0000, false, true, SteamReportKind.Housekeeping)]
    [InlineData(0xA1, 0x21, 0x0000, false, true, SteamReportKind.Housekeeping)]
    [InlineData(0x21, 0x22, 0x0003, false, true, SteamReportKind.Housekeeping)]
    [InlineData(0x21, 0x23, 0xFFFF, false, true, SteamReportKind.Other)]
    [InlineData(0x21, 0x00, 0x0000, false, true, SteamReportKind.Other)]
    [InlineData(0x41, 0x01, 0x0000, false, true, SteamReportKind.Other)]
    [InlineData(0x21, 0x0A, 0x0000, false, false, SteamReportKind.Other)]
    [InlineData(0x40, 0x0A, 0x0000, true, false, SteamReportKind.Other)]
    [InlineData(0xC1, 0x01, 0x0301, true, false, SteamReportKind.Other)]
    [InlineData(0x22, 0x09, 0x0301, true, false, SteamReportKind.Other)]
    public void A_control_request_is_housekeeping_only_as_a_class_request_to_its_interface(byte requestType, byte request, ushort value, bool isHid, bool isCdc, SteamReportKind kind)
    {
        var (classified, _) = SteamControlRequests.Classify(new SetupPacket(requestType, request, value, 2, 64), isHid, isCdc);

        Assert.Equal(kind, classified);
    }
}
