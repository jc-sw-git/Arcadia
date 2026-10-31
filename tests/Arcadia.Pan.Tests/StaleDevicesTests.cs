using Arcadia.Pan;

namespace Arcadia.Pan.Tests;

/// <summary>Windows' device entries as a tree (#347): usbip-win2's host with stale copies, a real puck, other hosts.</summary>
public sealed class FakeStaleDevices : IStaleDeviceStore
{
    public const string UsbipHost = @"ROOT\USB\0000";
    public const string UsbipHub = @"USB\ROOT_HUB30\1&2B&4&0";
    public const string StaleCopy = @"USB\VID_28DE&PID_1304\2&387D&0&1";
    public const string StaleInterface = @"USB\VID_28DE&PID_1304&MI_02\3&1C5D&0&0002";
    public const string StaleCollection = @"HID\VID_28DE&PID_1304&MI_02&COL01\4&613&0&0000";
    public const string StalePad = @"HID\VID_045E&PID_028E&IG_00\4&115&0&0000";
    public const string LiveCopy = @"USB\VID_28DE&PID_1304\2&387D&0&2";
    public const string PciHost = @"PCI\VEN_8086&DEV_7A60\3&11583659&0&A0";
    public const string RealHub = @"USB\ROOT_HUB30\4&1E2F&0&0";
    public const string RealPuckEntry = @"USB\VID_28DE&PID_1304\FXB0000000001";
    public const string OtherHost = @"ROOT\SYSTEM\0003";
    public const string OtherHostPad = @"HID\VID_045E&PID_028E&IG_00\5&ABC&0&0000";
    public const string OldUsbipHost = @"ROOT\USB\0001";
    public const string OldUsbipEntry = @"USB\VID_28DE&PID_1304\6&DEF&0&1";
    public const string Unreadable = @"HID\VID_1209&PID_0001&MI_02\7&1&0&0000";

    public Dictionary<string, KnownDevice> Devices { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string[]> HardwareIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Removed { get; } = [];
    public HashSet<string> Failing { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Runs inside every removal, as if Windows was busy with it then.</summary>
    public Action<string>? DuringRemove;

    public bool ListFails;

    public static FakeStaleDevices Typical()
    {
        var store = new FakeStaleDevices();
        store.Add(UsbipHost, present: true, parent: @"HTREE\ROOT\0", hardwareIds: [StaleDeviceList.UsbipWin2HostHardwareId]);
        store.Add(UsbipHub, present: true, parent: UsbipHost);
        store.Add(StaleCopy, present: false, parent: UsbipHub);
        store.Add(StaleInterface, present: false, parent: StaleCopy);
        store.Add(StaleCollection, present: false, parent: StaleInterface);
        store.Add(StalePad, present: false, parent: UsbipHub);
        store.Add(LiveCopy, present: true, parent: UsbipHub);
        store.Add(PciHost, present: true, parent: @"ACPI\PNP0A08\0");
        store.Add(RealHub, present: true, parent: PciHost);
        store.Add(RealPuckEntry, present: false, parent: RealHub);
        store.Add(OtherHost, present: true, parent: @"HTREE\ROOT\0", hardwareIds: [@"Root\ViGEmBus"]);
        store.Add(OtherHostPad, present: false, parent: OtherHost);
        store.Add(OldUsbipHost, present: true, parent: @"HTREE\ROOT\0", hardwareIds: [@"ROOT\USBIP\root", "USBIP"]);
        store.Add(OldUsbipEntry, present: false, parent: OldUsbipHost);
        store.Add(Unreadable, present: false, parent: null);
        return store;
    }

    public void Add(string id, bool present, string? parent, string[]? hardwareIds = null)
    {
        Devices[id] = new KnownDevice(id, present, parent);
        if (hardwareIds is not null)
        {
            HardwareIds[id] = hardwareIds;
        }
    }

    public IReadOnlyList<KnownDevice> KnownDevices() =>
        ListFails ? throw new InvalidOperationException("device list not read (CONFIGRET 0x3)") : Devices.Values.ToList();

    public IReadOnlyList<string> HardwareIdsOf(string instanceId) => HardwareIds.GetValueOrDefault(instanceId) ?? [];

    public bool IsPresent(string instanceId) => Devices.GetValueOrDefault(instanceId)?.Present == true;

    public uint Remove(string instanceId)
    {
        lock (Removed)
        {
            Removed.Add(instanceId);
        }

        DuringRemove?.Invoke(instanceId);
        if (Failing.Contains(instanceId))
        {
            return 0x17;
        }

        Devices.Remove(instanceId);
        return 0;
    }
}

public sealed class StaleDevicesTests
{
    private readonly List<string> _log = [];

    private StaleCleanupResult Run(FakeStaleDevices store, Func<bool>? mayContinue = null, TimeSpan? budget = null) =>
        StaleDeviceCleanup.Run(store, mayContinue ?? (() => true), budget ?? TimeSpan.FromMinutes(1), _log.Add);

    [Fact]
    [Trait("TestCase", "T347-1")]
    public void Only_entries_that_are_not_plugged_in_below_usbip_win2s_host_are_stale_deepest_first()
    {
        var store = FakeStaleDevices.Typical();

        var stale = StaleDeviceList.Select(store.KnownDevices(), store.HardwareIdsOf).ToList();

        Assert.Equal(
            new[] { FakeStaleDevices.StaleCollection, FakeStaleDevices.StaleInterface, FakeStaleDevices.StaleCopy, FakeStaleDevices.StalePad }.Order(),
            stale.Order());
        Assert.True(stale.IndexOf(FakeStaleDevices.StaleCollection) < stale.IndexOf(FakeStaleDevices.StaleInterface));
        Assert.True(stale.IndexOf(FakeStaleDevices.StaleInterface) < stale.IndexOf(FakeStaleDevices.StaleCopy));
    }

    [Fact]
    [Trait("TestCase", "T347-1")]
    public void Real_devices_other_software_hosts_other_usbip_drivers_and_unreadable_chains_are_kept()
    {
        var store = FakeStaleDevices.Typical();

        var stale = StaleDeviceList.Select(store.KnownDevices(), store.HardwareIdsOf);

        Assert.DoesNotContain(FakeStaleDevices.RealPuckEntry, stale);
        Assert.DoesNotContain(FakeStaleDevices.OtherHostPad, stale);
        Assert.DoesNotContain(FakeStaleDevices.OldUsbipEntry, stale);
        Assert.DoesNotContain(FakeStaleDevices.Unreadable, stale);
        Assert.DoesNotContain(FakeStaleDevices.LiveCopy, stale);
        Assert.DoesNotContain(FakeStaleDevices.UsbipHub, stale);
    }

    [Fact]
    [Trait("TestCase", "T347-1")]
    public void A_missing_or_unplugged_usbip_win2_host_keeps_every_entry()
    {
        var gone = FakeStaleDevices.Typical();
        gone.Devices.Remove(FakeStaleDevices.UsbipHost);
        var unplugged = FakeStaleDevices.Typical();
        unplugged.Add(FakeStaleDevices.UsbipHost, present: false, parent: @"HTREE\ROOT\0", hardwareIds: [StaleDeviceList.UsbipWin2HostHardwareId]);

        Assert.Empty(StaleDeviceList.Select(gone.KnownDevices(), gone.HardwareIdsOf));
        Assert.Empty(StaleDeviceList.Select(unplugged.KnownDevices(), unplugged.HardwareIdsOf));
    }

    [Fact]
    [Trait("TestCase", "T347-1")]
    public void A_chain_deeper_than_the_level_limit_is_kept()
    {
        var store = FakeStaleDevices.Typical();
        var parent = FakeStaleDevices.UsbipHub;
        for (var level = 0; level < 20; level++)
        {
            var id = $@"USB\VID_1234&PID_0001\DEEP{level}";
            store.Add(id, present: false, parent: parent);
            parent = id;
        }

        var stale = StaleDeviceList.Select(store.KnownDevices(), store.HardwareIdsOf);

        Assert.Contains(@"USB\VID_1234&PID_0001\DEEP0", stale);
        Assert.DoesNotContain(@"USB\VID_1234&PID_0001\DEEP19", stale);
    }

    [Fact]
    [Trait("TestCase", "T347-2")]
    public void The_cleanup_removes_every_stale_entry_and_nothing_else()
    {
        var store = FakeStaleDevices.Typical();

        var result = Run(store);

        Assert.Equal(new StaleCleanupResult(Found: 4, Removed: 4, Failed: 0, Kept: 0, Stopped: false), result);
        Assert.Equal(4, store.Removed.Count);
        Assert.Contains(FakeStaleDevices.LiveCopy, store.Devices.Keys);
        Assert.Contains(FakeStaleDevices.RealPuckEntry, store.Devices.Keys);
        Assert.Contains(FakeStaleDevices.OtherHostPad, store.Devices.Keys);
        Assert.Contains(FakeStaleDevices.OldUsbipEntry, store.Devices.Keys);
        Assert.Contains(FakeStaleDevices.Unreadable, store.Devices.Keys);
    }

    [Fact]
    [Trait("TestCase", "T347-3")]
    public void A_parent_whose_child_failed_stays_until_the_child_is_gone()
    {
        var store = FakeStaleDevices.Typical();
        store.Failing.Add(FakeStaleDevices.StaleCollection);

        var first = Run(store);

        Assert.Equal(1, first.Failed);
        Assert.Contains(FakeStaleDevices.StaleInterface, store.Devices.Keys);
        Assert.Contains(FakeStaleDevices.StaleCopy, store.Devices.Keys);
        Assert.DoesNotContain(FakeStaleDevices.StaleInterface, store.Removed);
        Assert.Contains("Stale entry of 28DE:1304 not removed: CONFIGRET 0x17", _log);
        Assert.DoesNotContain(_log, line => line.Contains("4&613", StringComparison.Ordinal));

        store.Failing.Clear();
        var second = Run(store);

        Assert.Equal(3, second.Removed);
        Assert.DoesNotContain(FakeStaleDevices.StaleCopy, store.Devices.Keys);
    }

    [Fact]
    [Trait("TestCase", "T347-4")]
    public void An_entry_plugged_in_again_since_the_list_is_kept()
    {
        var store = FakeStaleDevices.Typical();
        store.DuringRemove = id =>
        {
            if (id == FakeStaleDevices.StaleCollection)
            {
                // The pad comes back while the collection goes.
                store.Add(FakeStaleDevices.StalePad, present: true, parent: FakeStaleDevices.UsbipHub);
            }
        };

        var result = Run(store);

        Assert.DoesNotContain(FakeStaleDevices.StalePad, store.Removed);
        Assert.Equal(1, result.Kept);
    }

    [Fact]
    [Trait("TestCase", "T347-5")]
    public void The_cleanup_stops_between_two_entries_and_leaves_the_rest_for_next_time()
    {
        var store = FakeStaleDevices.Typical();
        var go = true;
        store.DuringRemove = _ => go = false;

        var result = Run(store, () => go);

        Assert.True(result.Stopped);
        Assert.Single(store.Removed);

        var spent = Run(FakeStaleDevices.Typical(), budget: TimeSpan.Zero);
        Assert.True(spent.Stopped);
        Assert.Equal(0, spent.Removed);
    }

    [Fact]
    [Trait("TestCase", "T347-4")]
    public void A_list_or_removal_that_fails_is_logged_and_never_thrown()
    {
        var store = FakeStaleDevices.Typical();
        store.ListFails = true;
        Assert.Equal(0, Run(store).Found);
        Assert.Contains(_log, line => line.StartsWith("Stale device entries not listed", StringComparison.Ordinal));

        var throwing = FakeStaleDevices.Typical();
        throwing.DuringRemove = _ => throw new InvalidOperationException("PnP lock");
        Assert.Equal(2, Run(throwing).Failed);
    }

    [Theory]
    [InlineData(@"HID\VID_28DE&PID_1304&MI_02&COL01\4&613&0&0000", "28DE:1304")]
    [InlineData(@"USB\VID_045e&PID_028e\FXB0000000001", "045E:028E")]
    [InlineData(@"SWD\SOMETHING\1", "SWD")]
    public void The_log_names_the_model_never_the_instance_id(string instanceId, string model) =>
        Assert.Equal(model, StaleDeviceList.ModelOf(instanceId));

    [Fact]
    public void HostOf_climbs_usb_and_hid_levels_to_the_host()
    {
        var store = FakeStaleDevices.Typical();
        string? ParentOf(string id) => store.Devices.GetValueOrDefault(id)?.Parent;

        Assert.Equal(FakeStaleDevices.UsbipHost, HidingList.HostOf(FakeStaleDevices.StaleCollection, ParentOf));
        Assert.Equal(FakeStaleDevices.PciHost, HidingList.HostOf(FakeStaleDevices.RealPuckEntry, ParentOf));
        Assert.Null(HidingList.HostOf(FakeStaleDevices.Unreadable, ParentOf));
    }
}
