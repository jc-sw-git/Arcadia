using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Arcadia.Input;

/// <summary>
/// Lists the XInput devices (XUSB interface of Windows' Xbox 360 driver) with what <see cref="DeviceTreeEmulatedPads"/>
/// needs: is the USB host controller above them a software one, and which XInput slot they hold.
/// </summary>
/// <remarks>
/// SISR's VIIPER pad sits under a USB root hub of its own whose host controller usbip-win2 creates as a root-enumerated
/// device (<c>ROOT\…</c>, owner's PC, #212 P3); a real pad's host controller is a PCI (or ACPI) device. The slot comes
/// from the pad's player LED, which XInput sets to the slot (<c>IOCTL_XUSB_GET_LED_STATE</c>, undocumented; read in
/// reverse-engineered XInput code, not seen on the owner's PC yet), so a pad whose LED says nothing has no slot here.
/// </remarks>
public static partial class XusbDevices
{
    private static readonly Guid XusbInterface = new("EC87F1E3-C13B-4100-B5F7-8B84D54260CB");

    private const uint DigcfPresent = 0x02;
    private const uint DigcfDeviceInterface = 0x10;
    private const int ErrorNoMoreItems = 259;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 0x03;
    private const uint OpenExisting = 3;
    private const uint IoctlXusbGetLedState = 0x8000E008;

    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<XusbDevice> List()
    {
        var guid = XusbInterface;
        var set = SetupDiGetClassDevsW(ref guid, 0, 0, DigcfPresent | DigcfDeviceInterface);
        if (set == -1)
        {
            throw new InvalidOperationException($"SetupDiGetClassDevs: Fehler {Marshal.GetLastPInvokeError()}");
        }

        try
        {
            var devices = new List<XusbDevice>();
            for (uint index = 0; ; index++)
            {
                var interfaceData = new DeviceInterfaceData { Size = (uint)Marshal.SizeOf<DeviceInterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(set, 0, ref guid, index, ref interfaceData))
                {
                    if (Marshal.GetLastPInvokeError() == ErrorNoMoreItems)
                    {
                        return devices;
                    }

                    throw new InvalidOperationException($"SetupDiEnumDeviceInterfaces: Fehler {Marshal.GetLastPInvokeError()}");
                }

                if (Describe(set, ref interfaceData) is { } device)
                {
                    devices.Add(device);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
    }

    private static XusbDevice? Describe(nint set, ref DeviceInterfaceData interfaceData)
    {
        SetupDiGetDeviceInterfaceDetailW(set, ref interfaceData, 0, 0, out var size, 0);
        if (size == 0)
        {
            return null;
        }

        var detail = Marshal.AllocHGlobal((int)size);
        try
        {
            // SP_DEVICE_INTERFACE_DETAIL_DATA_W: cbSize is the struct's own size (8 on x64, 6 on x86), the path follows the DWORD.
            Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
            var info = new DeviceInfoData { Size = (uint)Marshal.SizeOf<DeviceInfoData>() };
            if (!SetupDiGetDeviceInterfaceDetailW(set, ref interfaceData, detail, size, out _, ref info))
            {
                return null;
            }

            var path = Marshal.PtrToStringUni(detail + 4) ?? string.Empty;
            return new XusbDevice(DeviceTree.OnSoftwareHost(info.DevInst), Slot(path));
        }
        finally
        {
            Marshal.FreeHGlobal(detail);
        }
    }

    private static unsafe int? Slot(string path)
    {
        using var handle = CreateFileW(path, GenericRead | GenericWrite, FileShareReadWrite, 0, OpenExisting, 0, 0);
        if (handle.IsInvalid)
        {
            return null;
        }

        // Input: the request's version (0x0101) and a zero byte; output: the LED state in its third byte.
        byte* input = stackalloc byte[] { 0x01, 0x01, 0x00 };
        byte* output = stackalloc byte[3];
        if (!DeviceIoControl(handle, IoctlXusbGetLedState, input, 3, output, 3, out var returned, 0) || returned < 3)
        {
            return null;
        }

        return SlotOfLed(output[2]);
    }

    /// <summary>XInput's LED states: flashing (2–5) and then lit (6–9) for slot 0–3; all other states name none.</summary>
    public static int? SlotOfLed(byte led) => led switch
    {
        >= 2 and <= 5 => led - 2,
        >= 6 and <= 9 => led - 6,
        _ => null,
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData
    {
        public uint Size;
        public Guid InterfaceClass;
        public uint Flags;
        public nint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfoData
    {
        public uint Size;
        public Guid ClassGuid;
        public uint DevInst;
        public nint Reserved;
    }

    [LibraryImport("setupapi.dll", SetLastError = true)]
    private static partial nint SetupDiGetClassDevsW(ref Guid classGuid, nint enumerator, nint parent, uint flags);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiEnumDeviceInterfaces(nint set, nint deviceInfo, ref Guid interfaceClass, uint index, ref DeviceInterfaceData data);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiGetDeviceInterfaceDetailW(nint set, ref DeviceInterfaceData data, nint detail, uint detailSize, out uint requiredSize, nint deviceInfo);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiGetDeviceInterfaceDetailW(nint set, ref DeviceInterfaceData data, nint detail, uint detailSize, out uint requiredSize, ref DeviceInfoData deviceInfo);

    [LibraryImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiDestroyDeviceInfoList(nint set);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint creation, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool DeviceIoControl(Microsoft.Win32.SafeHandles.SafeFileHandle device, uint code, byte* input, uint inputSize, byte* output, uint outputSize, out uint returned, nint overlapped);
}
