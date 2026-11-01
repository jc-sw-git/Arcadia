using System.Runtime.Versioning;

namespace Arcadia.Pan.Windows;

/// <summary>Windows' device entries through cfgmgr32 (#347); listing all of them takes 30–75 ms on the owner's PC.</summary>
[SupportedOSPlatform("windows")]
public sealed unsafe class WindowsStaleDevices : IStaleDeviceStore
{
    private const int MaxDeviceIdLength = 200;

    public IReadOnlyList<KnownDevice> KnownDevices()
    {
        var devices = new List<KnownDevice>();
        foreach (var id in AllInstanceIds())
        {
            if (Native.CM_Locate_DevNodeW(out var device, id, Native.CmLocateDevnodeNormal) == Native.CrSuccess)
            {
                devices.Add(new KnownDevice(id, Present: true, LiveParent(device)));
            }
            else if (Native.CM_Locate_DevNodeW(out device, id, Native.CmLocateDevnodePhantom) == Native.CrSuccess)
            {
                devices.Add(new KnownDevice(id, Present: false, LastKnownParent(device)));
            }
        }

        return devices;
    }

    public IReadOnlyList<string> HardwareIdsOf(string instanceId)
    {
        if (Native.CM_Locate_DevNodeW(out var device, instanceId, Native.CmLocateDevnodePhantom) != Native.CrSuccess)
        {
            return [];
        }

        uint length = 0;
        Native.CM_Get_DevNode_Registry_PropertyW(device, Native.CmDrpHardwareId, out _, null, ref length, 0);
        if (length == 0)
        {
            return [];
        }

        var buffer = new char[length / sizeof(char) + 1];
        fixed (char* chars = buffer)
        {
            if (Native.CM_Get_DevNode_Registry_PropertyW(device, Native.CmDrpHardwareId, out _, chars, ref length, 0) != Native.CrSuccess)
            {
                return [];
            }
        }

        return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    public bool IsPresent(string instanceId) => Native.CM_Locate_DevNodeW(out _, instanceId, Native.CmLocateDevnodeNormal) == Native.CrSuccess;

    public uint Remove(string instanceId)
    {
        var located = Native.CM_Locate_DevNodeW(out var device, instanceId, Native.CmLocateDevnodePhantom);
        return located != Native.CrSuccess ? located : Native.CM_Uninstall_DevNode(device, 0);
    }

    private static List<string> AllInstanceIds()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = Native.CM_Get_Device_ID_List_SizeW(out var length, null, 0);
            if (result != Native.CrSuccess)
            {
                throw new InvalidOperationException($"device list size not read (CONFIGRET 0x{result:X})");
            }

            var buffer = new char[length];
            fixed (char* chars = buffer)
            {
                result = Native.CM_Get_Device_ID_ListW(null, chars, length, 0);
            }

            if (result == Native.CrBufferSmall)
            {
                // A device arrived in between.
                continue;
            }

            if (result != Native.CrSuccess)
            {
                throw new InvalidOperationException($"device list not read (CONFIGRET 0x{result:X})");
            }

            return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        throw new InvalidOperationException("device list kept changing");
    }

    private static string? LiveParent(uint device)
    {
        if (Native.CM_Get_Parent(out var parent, device, 0) != Native.CrSuccess)
        {
            return null;
        }

        var buffer = stackalloc char[MaxDeviceIdLength + 1];
        return Native.CM_Get_Device_IDW(parent, buffer, MaxDeviceIdLength + 1, 0) == Native.CrSuccess ? new string(buffer) : null;
    }

    private static string? LastKnownParent(uint device)
    {
        var buffer = stackalloc char[MaxDeviceIdLength + 1];
        var length = (uint)((MaxDeviceIdLength + 1) * sizeof(char));
        if (Native.CM_Get_DevNode_PropertyW(device, Native.LastKnownParent, out _, buffer, ref length, 0) != Native.CrSuccess)
        {
            return null;
        }

        return new string(buffer, 0, (int)(length / sizeof(char))).TrimEnd('\0') is { Length: > 0 } parent ? parent : null;
    }
}
