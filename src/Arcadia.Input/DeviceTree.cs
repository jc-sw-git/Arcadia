using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.Input;

/// <summary>What a device's USB host controller is, as far as the device tree tells.</summary>
public enum DeviceHost
{
    /// <summary>The device tree couldn't be read, or the host is neither of the kinds below.</summary>
    Unknown,

    /// <summary>A PCI or ACPI host controller: a real device.</summary>
    Hardware,

    /// <summary>Root-enumerated (usbip-win2) or created by software (<c>SWD\</c>): an emulated device.</summary>
    Software,
}

/// <summary>
/// Where a device sits in Windows' device tree: on a hardware USB host controller, or on a software one, as usbip-win2
/// creates for VIIPER's devices (#212 P3: SISR's Xbox pad; #242: the copy of the Steam Controller's puck).
/// </summary>
/// <remarks>
/// usbip-win2's host controller is root-enumerated (<c>ROOT\…</c>, owner's PC, #212 P3); a real controller's is a PCI
/// (or ACPI) device.
/// </remarks>
public static partial class DeviceTree
{
    /// <summary>A USB device has a few USB levels above it (composite device, hubs); more means the tree is odd.</summary>
    private const int MaxUsbLevels = 16;

    private const int MaxDeviceIdLength = 200;

    /// <summary>Root-enumerated (usbip-win2) or created by software (<c>SWD\</c>); PCI and ACPI devices are hardware.</summary>
    public static bool IsSoftwareHost(string deviceId) => HostOf(deviceId) == DeviceHost.Software;

    /// <summary>The kind of host controller a device id names; anything but PCI, ACPI, ROOT and SWD is unknown.</summary>
    public static DeviceHost HostOf(string deviceId) =>
        deviceId.StartsWith(@"ROOT\", StringComparison.OrdinalIgnoreCase) || deviceId.StartsWith(@"SWD\", StringComparison.OrdinalIgnoreCase) ? DeviceHost.Software
        : deviceId.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase) || deviceId.StartsWith(@"ACPI\", StringComparison.OrdinalIgnoreCase) ? DeviceHost.Hardware
        : DeviceHost.Unknown;

    /// <summary>
    /// The device behind a device interface path (such as a HID collection's <c>\\?\hid#…</c>, SDL's path of a pad read
    /// over HID) is on a software host. False when the path can't be looked up, so a real device is never taken for a copy.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static bool InterfaceOnSoftwareHost(string interfacePath) => HostOfInterface(interfacePath) == DeviceHost.Software;

    /// <summary>
    /// The host of the device behind a device interface path. <see cref="DeviceHost.Unknown"/> when it can't be looked
    /// up: whoever needs a real device (the copy's puck, #242) must ask for <see cref="DeviceHost.Hardware"/>.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static DeviceHost HostOfInterface(string interfacePath) =>
        InstanceOf(interfacePath) is { } devInst ? HostOf(devInst) : DeviceHost.Unknown;

    /// <summary>Climbs the levels above the device to the first one that isn't USB or HID: the host controller.</summary>
    public static bool OnSoftwareHost(uint devInst) => HostOf(devInst) == DeviceHost.Software;

    private static DeviceHost HostOf(uint devInst)
    {
        var current = devInst;
        for (var level = 0; level < MaxUsbLevels; level++)
        {
            if (CM_Get_Parent(out var parent, current, 0) != 0)
            {
                return DeviceHost.Unknown;
            }

            var id = DeviceId(parent);
            if (!id.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase) && !id.StartsWith(@"HID\", StringComparison.OrdinalIgnoreCase))
            {
                return HostOf(id);
            }

            current = parent;
        }

        return DeviceHost.Unknown;
    }

    private static unsafe uint? InstanceOf(string interfacePath)
    {
        // DEVPKEY_Device_InstanceId: the device the interface belongs to.
        var key = new DevPropKey { FmtId = new Guid("78c34fc8-104a-4aca-9ea4-524d52996e57"), Pid = 256 };
        var buffer = stackalloc char[MaxDeviceIdLength + 1];
        var size = (uint)((MaxDeviceIdLength + 1) * sizeof(char));
        if (CM_Get_Device_Interface_PropertyW(interfacePath, ref key, out _, (byte*)buffer, ref size, 0) != 0)
        {
            return null;
        }

        var instance = new string(buffer);
        return CM_Locate_DevNodeW(out var devInst, instance, 0) == 0 ? devInst : null;
    }

    private static unsafe string DeviceId(uint devInst)
    {
        var buffer = stackalloc char[MaxDeviceIdLength + 1];
        return CM_Get_Device_IDW(devInst, buffer, MaxDeviceIdLength + 1, 0) == 0 ? new string(buffer) : string.Empty;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevPropKey
    {
        public Guid FmtId;
        public uint Pid;
    }

    [LibraryImport("cfgmgr32.dll")]
    private static partial uint CM_Get_Parent(out uint parent, uint devInst, uint flags);

    [LibraryImport("cfgmgr32.dll")]
    private static unsafe partial uint CM_Get_Device_IDW(uint devInst, char* buffer, int bufferLength, uint flags);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial uint CM_Get_Device_Interface_PropertyW(string path, ref DevPropKey key, out uint type, byte* buffer, ref uint size, uint flags);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint CM_Locate_DevNodeW(out uint devInst, string instanceId, uint flags);
}
