using System.Text;
using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

public sealed class HidHideWireTests
{
    [Fact]
    public void The_IOCTL_codes_are_HidHides()
    {
        // CTL_CODE(32769, 2048 + n, METHOD_BUFFERED, FILE_READ_DATA), as in HidHide's client and CLI.
        Assert.Equal(0x80016000u, HidHideWire.GetAllowList);
        Assert.Equal(0x80016004u, HidHideWire.SetAllowList);
        Assert.Equal(0x80016008u, HidHideWire.GetHiddenList);
        Assert.Equal(0x8001600Cu, HidHideWire.SetHiddenList);
        Assert.Equal(0x80016010u, HidHideWire.GetCloaking);
        Assert.Equal(0x80016014u, HidHideWire.SetCloaking);
        Assert.Equal(0x80016018u, HidHideWire.GetInverse);
    }

    [Fact]
    public void A_list_goes_as_a_multi_string_and_comes_back_the_same()
    {
        var entries = new[] { FakeDevices.Slot2, @"\Device\HarddiskVolume3\Program Files\Arcadia\Arcadia.exe" };

        var bytes = HidHideWire.EncodeList(entries);

        Assert.Equal(string.Join("\0", entries) + "\0\0", Encoding.Unicode.GetString(bytes));
        Assert.Equal(entries, HidHideWire.DecodeList(bytes));
    }

    [Fact]
    public void An_empty_list_is_two_nulls_and_decoding_stops_at_the_first_empty_string_or_the_buffers_end()
    {
        Assert.Equal(new byte[4], HidHideWire.EncodeList([]));
        Assert.Empty(HidHideWire.DecodeList(new byte[4]));
        Assert.Empty(HidHideWire.DecodeList([]));
        Assert.Equal(["a"], HidHideWire.DecodeList(Encoding.Unicode.GetBytes("a\0\0b\0\0")));
        Assert.Equal(["a", "bc"], HidHideWire.DecodeList(Encoding.Unicode.GetBytes("a\0bc").Append((byte)0).ToArray()));
    }

    [Fact]
    public void An_entry_that_would_break_the_list_is_refused()
    {
        Assert.Throws<ArgumentException>(() => HidHideWire.EncodeList(["a\0b"]));
        Assert.Throws<ArgumentException>(() => HidHideWire.EncodeList([""]));
    }

    [Theory]
    [InlineData(@"\SystemRoot\System32\drivers\HidHide.sys", @"C:\Windows\System32\drivers\HidHide.sys")]
    [InlineData(@"System32\drivers\HidHide.sys", @"C:\Windows\System32\drivers\HidHide.sys")]
    [InlineData(@"\??\C:\Windows\System32\DriverStore\FileRepository\hidhide.inf_amd64_1\HidHide.sys", @"C:\Windows\System32\DriverStore\FileRepository\hidhide.inf_amd64_1\HidHide.sys")]
    [InlineData(@"""D:\Drivers\HidHide.sys""", @"D:\Drivers\HidHide.sys")]
    [InlineData(@"\Device\HarddiskVolume3\HidHide.sys", null)]
    [InlineData(@"HidHide.sys", null)]
    public void The_driver_file_comes_from_the_services_image_path(string imagePath, string? expected)
    {
        Assert.Equal(expected, HidHideWire.DriverFile(imagePath, @"C:\Windows\"));
    }

    [Theory]
    [InlineData(@"HID\VID_28DE&PID_1304&MI_02&COL03\8&1A2B&0&0002", true)]
    [InlineData(@"hid\vid_28de&pid_1304&mi_06\8&1A2B&0&0000", true)]
    [InlineData(@"HID\VID_28DE&PID_1205&MI_02\8&1A2B&0&0000", false)]
    [InlineData(@"HID\VID_28DE&PID_13040&MI_02\8&1A2B&0&0000", false)]
    [InlineData(@"USB\VID_28DE&PID_1304\ABCDEF123", false)]
    [InlineData(@"HID\VID_054C&PID_0CE6&MI_03\7&2B7F&0&0000", false)]
    public void Only_the_pucks_HID_instances_are_listed(string instanceId, bool listed)
    {
        Assert.Equal(listed, HidingList.IsListedHid(instanceId));
    }

    [Theory]
    [InlineData(@"USB\VID_28DE&PID_1304\ABCDEF1234567", true)]
    [InlineData(@"USB\VID_28DE&PID_1304&MI_02\7&1&0&2", false)]
    [InlineData(@"USB\VID_28DE&PID_1205\ABC", false)]
    public void Only_the_puck_itself_is_restarted(string instanceId, bool listed)
    {
        Assert.Equal(listed, HidingList.IsListedUsbParent(instanceId));
    }
}
