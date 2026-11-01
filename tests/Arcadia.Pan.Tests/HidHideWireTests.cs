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
    [InlineData(@"HID\VID_28DE&PID_1302&COL01\8&3C4D&0&0000", true)]
    [InlineData(@"HID\VID_28DE&PID_1302&COL03\8&3C4D&0&0002", true)]
    [InlineData(@"HID\VID_28DE&PID_1005\8&3C4D&0&0000", false)]
    [InlineData(@"USB\VID_28DE&PID_1302\ABCDEF123", false)]
    public void Only_the_HID_instances_of_puck_and_cable_are_listed(string instanceId, bool listed)
    {
        Assert.Equal(listed, HidingList.IsListedHid(instanceId));
    }

    [Theory]
    [InlineData(@"USB\VID_28DE&PID_1304\ABCDEF1234567", true)]
    [InlineData(@"USB\VID_28DE&PID_1304&MI_02\7&1&0&2", false)]
    [InlineData(@"USB\VID_28DE&PID_1205\ABC", false)]
    [InlineData(@"USB\VID_28DE&PID_1302\ABCDEF1234567", true)]
    [InlineData(@"USB\VID_28DE&PID_1005\ABCDEF1234567", false)]
    public void Only_the_puck_and_the_cable_device_themselves_are_restarted(string instanceId, bool listed)
    {
        Assert.Equal(listed, HidingList.IsListedUsbParent(instanceId));
    }

    [Theory]
    [InlineData(@"\\?\HID#VID_28DE&PID_1302&Col03#8&3c4d&0&0002#{4d1e55b2-f16f-11cf-88cb-001111000030}", true)]
    [InlineData(@"\\?\HID#VID_28DE&PID_1304&MI_02&Col03#8&1a2b&0&0002#{4d1e55b2-f16f-11cf-88cb-001111000030}", true)]
    [InlineData(@"\\?\HID#VID_28DE&PID_1005#8&3c4d&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", false)]
    [InlineData(@"\\?\HID#VID_054C&PID_0CE6&MI_03#7&2b7f&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", false)]
    public void Arrivals_of_puck_and_cable_are_picked_out_by_their_link(string link, bool listed)
    {
        Assert.Equal(listed, HidingList.IsListedInterface(link));
    }

    [Fact]
    public void Each_HID_instance_belongs_to_its_device()
    {
        Assert.Same(HidingList.Cable, HidingList.DeviceOf(@"HID\VID_28DE&PID_1302&COL02\8&3C4D&0&0001"));
        Assert.Same(HidingList.Puck, HidingList.DeviceOf(@"HID\VID_28DE&PID_1304&MI_03&COL03\8&1A2B&0&0002"));
        Assert.Null(HidingList.DeviceOf(@"HID\VID_054C&PID_0CE6&MI_03\7&2B7F&0&0000"));
    }

    // After the device tree of the owner's PC (2026-10-29): the real puck on Intel's xHCI, Arcadia's copy on VIIPER's
    // bus. The USB ids are the owner's; the HID instances' suffixes and the PCI id's tail are made up.
    private static readonly Dictionary<string, string> OwnersTree = new(StringComparer.OrdinalIgnoreCase)
    {
        [@"HID\VID_28DE&PID_1304&MI_02&COL03\7&30D307B7&0&0002"] = @"USB\VID_28DE&PID_1304&MI_02\6&2F4EFF51&0&0002",
        [@"USB\VID_28DE&PID_1304&MI_02\6&2F4EFF51&0&0002"] = @"USB\VID_28DE&PID_1304\FXB9961802B23",
        [@"USB\VID_28DE&PID_1304\FXB9961802B23"] = @"USB\ROOT_HUB30\4&C7355C0&0&0",
        [@"USB\ROOT_HUB30\4&C7355C0&0&0"] = @"PCI\VEN_8086&DEV_7A60&SUBSYS_7D251462&REV_11\3&11583659&0&A0",
        [@"HID\VID_28DE&PID_1304&MI_02&COL03\4&1A2B3C4D&0&0002"] = @"USB\VID_28DE&PID_1304&MI_02\3&33C446F8&0&0002",
        [@"USB\VID_28DE&PID_1304&MI_02\3&33C446F8&0&0002"] = @"USB\VID_28DE&PID_1304\2&387D3FEA&0&1",
        [@"USB\VID_28DE&PID_1304\2&387D3FEA&0&1"] = @"USB\ROOT_HUB30\1&2B53A856&4&0",
        [@"USB\ROOT_HUB30\1&2B53A856&4&0"] = @"ROOT\USB\0000",
    };

    [Theory]
    [Trait("TestCase", "T308-5")]
    [InlineData(@"HID\VID_28DE&PID_1304&MI_02&COL03\4&1A2B3C4D&0&0002", true)]
    [InlineData(@"USB\VID_28DE&PID_1304\2&387D3FEA&0&1", true)]
    [InlineData(@"HID\VID_28DE&PID_1304&MI_02&COL03\7&30D307B7&0&0002", false)]
    [InlineData(@"USB\VID_28DE&PID_1304\FXB9961802B23", false)]
    public void Arcadias_copy_on_VIIPERs_bus_is_told_from_the_real_puck(string instanceId, bool copy)
    {
        Assert.Equal(copy, HidingList.OnSoftwareHost(instanceId, id => OwnersTree.GetValueOrDefault(id)));
    }

    [Fact]
    [Trait("TestCase", "T308-5")]
    public void A_device_tree_that_cant_be_read_counts_as_the_real_puck_so_it_is_hidden()
    {
        Assert.False(HidingList.OnSoftwareHost(@"USB\VID_28DE&PID_1304\FXB9961802B23", _ => null));
        Assert.False(HidingList.OnSoftwareHost(@"USB\VID_28DE&PID_1304\FXB9961802B23", id => id + @"\USB\deeper"));
        Assert.True(HidingList.OnSoftwareHost(@"USB\VID_28DE&PID_1304\X", _ => @"SWD\SOMETHING\1"));
    }

    private const string Driver = @"C:\Windows\System32\drivers\HidHide.sys";

    [Fact]
    [Trait("TestCase", "T243-41")]
    public void An_uninstalled_HidHide_waits_for_the_restart_by_its_delete_flag_or_its_driver_file_s_pending_deletion()
    {
        Assert.True(HidHideWire.RemovalPending(1, null, Driver));
        Assert.True(HidHideWire.RemovalPending(null, [@"\??\C:\other.tmp", @"\??\C:\other.dll", @"\??\c:\windows\system32\DRIVERS\HidHide.sys", ""], Driver));

        Assert.False(HidHideWire.RemovalPending(0, null, Driver));
        Assert.False(HidHideWire.RemovalPending(null, [], Driver));
        // Replaced by an update (a target), not deleted; another file deleted; no driver file known.
        Assert.False(HidHideWire.RemovalPending(null, [@"\??\" + Driver, @"\??\C:\new\HidHide.sys"], Driver));
        Assert.False(HidHideWire.RemovalPending(null, [@"\??\C:\other.tmp", ""], Driver));
        Assert.False(HidHideWire.RemovalPending(null, [@"\??\" + Driver, ""], null));
    }
}
