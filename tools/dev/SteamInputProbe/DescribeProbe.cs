using System.Runtime.InteropServices;
using System.Text;

namespace SteamInputProbe;

/// <summary>
/// Spike #239: dumps what an emulated copy of a Valve device would have to look like to Steam: the USB device and
/// configuration descriptors (read from the hub, as USBView does), the string descriptors and every HID interface's
/// report descriptor. Serial numbers are never written, only their length. With a JSON path it also writes the
/// puck's descriptors in the form the spike's proxy (steamproxy239) reads.
/// </summary>
internal sealed class DescribeProbe(ProbeLog log, string? jsonPath)
{
    private const ushort ValveVendor = 0x28DE;
    private const string Puck = "VID_28DE&PID_1304";

    // hidapi's reconstructed report descriptors by interface number: the fallback if the device won't answer the hub.
    private readonly Dictionary<int, List<byte[]>> _reconstructed = [];
    private Dictionary<string, object>? _puck;

    public int Run()
    {
        DumpHidInterfaces();
        foreach (var instanceId in UsbDevices.ValveDevices())
            DumpUsbDevice(instanceId);
        if (jsonPath is null) return 0;
        if (_puck is null)
        {
            log.Line($"no {Puck} found, {jsonPath} not written");
            return 1;
        }
        File.WriteAllText(jsonPath, System.Text.Json.JsonSerializer.Serialize(_puck));
        log.Line($"descriptors written to {jsonPath}");
        return 0;
    }

    private void DumpHidInterfaces()
    {
        // SDL only enumerates game controllers by default; the puck's other interfaces (mouse, keyboard, vendor) count too.
        Sdl.SDL_SetHint("SDL_HIDAPI_ENUMERATE_ONLY_CONTROLLERS", "0");
        if (SdlHid.SDL_hid_init() != 0) log.Line($"SDL_hid_init FAILED: {Sdl.Text(Sdl.SDL_GetError())}");
        var list = SdlHid.SDL_hid_enumerate(ValveVendor, 0);
        var count = 0;
        for (var node = list; node != IntPtr.Zero; node = Marshal.PtrToStructure<SdlHidDeviceInfo>(node).Next)
        {
            var info = Marshal.PtrToStructure<SdlHidDeviceInfo>(node);
            count++;
            var path = Marshal.PtrToStringUTF8(info.Path) ?? "";
            log.Line($"HID {info.VendorId:X4}:{info.ProductId:X4} interface {info.InterfaceNumber} usage {info.UsagePage:X4}/{info.Usage:X4} "
                     + $"release {info.ReleaseNumber:X4} bus {info.BusType} \"{Marshal.PtrToStringUni(info.Manufacturer)}\" "
                     + $"\"{Marshal.PtrToStringUni(info.Product)}\" serial length {(Marshal.PtrToStringUni(info.Serial) ?? "").Length} path {path}");
            var device = SdlHid.SDL_hid_open_path(path);
            if (device == IntPtr.Zero)
            {
                log.Line($"  open FAILED: {Sdl.Text(Sdl.SDL_GetError())}");
                continue;
            }
            var buffer = new byte[4096];
            var length = SdlHid.SDL_hid_get_report_descriptor(device, buffer, (nuint)buffer.Length);
            log.Line(length > 0 ? $"  report descriptor (reconstructed by hidapi) {length} bytes: {Hex(buffer, length)}"
                                : $"  report descriptor FAILED: {Sdl.Text(Sdl.SDL_GetError())}");
            if (length > 0 && info.ProductId == 0x1304)
            {
                if (!_reconstructed.TryGetValue(info.InterfaceNumber, out var found)) _reconstructed[info.InterfaceNumber] = found = [];
                found.Add(buffer[..length]);
            }
            SdlHid.SDL_hid_close(device);
        }
        SdlHid.SDL_hid_free_enumeration(list);
        log.Line($"HID interfaces of vendor {ValveVendor:X4}: {count}");
    }

    private void DumpUsbDevice(string instanceId)
    {
        // The instance id's last part can be the serial number; only the hardware part goes into the log.
        var shown = instanceId.Split('\\') is [var bus, var hardware, ..] ? $@"{bus}\{hardware}\…" : "?";
        if (UsbDevices.HubAndPort(instanceId) is not { } location)
        {
            log.Line($"USB {shown}: hub or port not found");
            return;
        }
        var (hubPath, port) = location;
        using var hub = UsbHub.Open(hubPath);
        if (hub is null)
        {
            log.Line($"USB {shown}: hub open FAILED: error {Marshal.GetLastPInvokeError()}");
            return;
        }
        // USB_DEVICE_SPEED: 0 low, 1 full, 2 high, 3 super.
        var speed = hub.GetSpeed(port);
        log.Line($"USB {shown} on hub port {port}, speed {speed?.ToString() ?? "?"}");
        var device = hub.GetDescriptor(port, 0x0100, 0, 18);
        log.Line($"  device descriptor: {Describe(device)}");
        if (device is not { Length: 18 }) return;

        var head = hub.GetDescriptor(port, 0x0200, 0, 9);
        if (head is not { Length: 9 }) return;
        var config = hub.GetDescriptor(port, 0x0200, 0, BitConverter.ToUInt16(head, 2));
        log.Line($"  configuration descriptor: {Describe(config)}");

        var strings = new Dictionary<string, string>();
        foreach (var index in StringIndices(device, config))
        {
            var text = hub.GetDescriptor(port, (ushort)(0x0300 | index), 0x0409, 255) is { Length: > 2 } s
                ? Encoding.Unicode.GetString(s, 2, s.Length - 2) : null;
            if (index == device[16])
            {
                log.Line($"  string {index} (serial): length {text?.Length ?? 0}");
                continue;
            }
            log.Line($"  string {index}: \"{text}\"");
            if (text is not null) strings[index.ToString()] = text;
        }
        if (config is null) return;
        var reports = DumpReportDescriptors(hub, port, config);
        if (!instanceId.Contains(Puck, StringComparison.OrdinalIgnoreCase)) return;
        _puck = new Dictionary<string, object>
        {
            ["device"] = Hex(device, device.Length),
            ["config"] = Hex(config, config.Length),
            ["strings"] = strings,
            ["reports"] = reports,
            ["speed"] = (speed ?? 1) + 1, // VIIPER counts 1 low, 2 full, 3 high
        };
    }

    /// <summary>Manufacturer, product, serial, and the configuration's and interfaces' own strings.</summary>
    private static IEnumerable<byte> StringIndices(byte[] device, byte[]? config)
    {
        var indices = new SortedSet<byte> { device[14], device[15], device[16] };
        for (var at = 0; config is not null && at + 1 < config.Length && config[at] > 0; at += config[at])
        {
            if (config[at + 1] == 0x02 && at + 6 < config.Length) indices.Add(config[at + 6]);
            if (config[at + 1] == 0x04 && at + 8 < config.Length) indices.Add(config[at + 8]);
        }
        indices.Remove(0);
        return indices;
    }

    /// <summary>
    /// The hub IOCTL always sends a device-recipient request; most devices answer a report descriptor request anyway.
    /// hidapi's reconstruction above is the fallback.
    /// </summary>
    private Dictionary<string, string> DumpReportDescriptors(UsbHub hub, uint port, byte[] config)
    {
        var reports = new Dictionary<string, string>();
        var interfaceNumber = -1;
        for (var at = 0; at + 1 < config.Length && config[at] > 0; at += config[at])
        {
            var type = config[at + 1];
            if (type == 0x04 && at + 2 < config.Length) interfaceNumber = config[at + 2];
            if (type != 0x21 || at + 8 >= config.Length) continue;
            var length = BitConverter.ToUInt16(config, at + 7);
            var report = hub.GetDescriptor(port, 0x2200, (ushort)interfaceNumber, length);
            log.Line($"  interface {interfaceNumber} report descriptor from device ({length} bytes expected): {Describe(report)}");
            if (report?.Length == length) reports[interfaceNumber.ToString()] = Hex(report, report.Length);
            else if (_reconstructed.TryGetValue(interfaceNumber, out var list) && list.Count == 1)
            {
                reports[interfaceNumber.ToString()] = Hex(list[0], list[0].Length);
                log.Line($"  interface {interfaceNumber}: using hidapi's reconstruction instead");
            }
        }
        return reports;
    }

    private static string Describe(byte[]? data) =>
        data is null ? $"FAILED (error {Marshal.GetLastPInvokeError()})" : $"{data.Length} bytes: {Hex(data, data.Length)}";

    private static string Hex(byte[] data, int length) => Convert.ToHexString(data, 0, length);
}

[StructLayout(LayoutKind.Sequential)]
internal struct SdlHidDeviceInfo
{
    public IntPtr Path;
    public ushort VendorId;
    public ushort ProductId;
    public IntPtr Serial;
    public ushort ReleaseNumber;
    public IntPtr Manufacturer;
    public IntPtr Product;
    public ushort UsagePage;
    public ushort Usage;
    public int InterfaceNumber;
    public int InterfaceClass;
    public int InterfaceSubclass;
    public int InterfaceProtocol;
    public int BusType;
    public IntPtr Next;
}

internal static class SdlHid
{
    private const string Lib = "SDL3";

    [DllImport(Lib)]
    public static extern int SDL_hid_init();

    [DllImport(Lib)]
    public static extern IntPtr SDL_hid_enumerate(ushort vendorId, ushort productId);

    [DllImport(Lib)]
    public static extern void SDL_hid_free_enumeration(IntPtr devices);

    [DllImport(Lib)]
    public static extern IntPtr SDL_hid_open_path([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(Lib)]
    public static extern int SDL_hid_close(IntPtr device);

    [DllImport(Lib)]
    public static extern int SDL_hid_get_report_descriptor(IntPtr device, byte[] buffer, nuint size);
}

/// <summary>Finds Valve's USB devices and the hub port each one is plugged into, through the configuration manager.</summary>
internal static class UsbDevices
{
    private static readonly Guid HubInterface = new("f18a0e88-c30c-11d0-8815-00a0c906bed8");

    /// <summary>Whole devices only (<c>USB\VID_28DE&amp;PID_xxxx\…</c>), not their interfaces (<c>…&amp;MI_02</c>).</summary>
    public static IEnumerable<string> ValveDevices()
    {
        const uint filterEnumerator = 0x1;
        if (CM_Get_Device_ID_List_SizeW(out var size, "USB", filterEnumerator) != 0) return [];
        var buffer = new char[size];
        if (CM_Get_Device_ID_ListW("USB", buffer, size, filterEnumerator) != 0) return [];
        return new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(id => id.StartsWith(@"USB\VID_28DE&", StringComparison.OrdinalIgnoreCase) && !id.Contains("&MI_", StringComparison.OrdinalIgnoreCase));
    }

    public static (string HubPath, uint Port)? HubAndPort(string instanceId)
    {
        if (CM_Locate_DevNodeW(out var node, instanceId, 0) != 0 || CM_Get_Parent(out var parent, node, 0) != 0) return null;
        var hubId = new char[400];
        if (CM_Get_Device_IDW(parent, hubId, (uint)hubId.Length, 0) != 0) return null;
        const uint address = 0x1D; // CM_DRP_ADDRESS: for a USB device, its port number on the hub
        var port = 0u;
        var length = 4u;
        if (CM_Get_DevNode_Registry_PropertyW(node, address, out _, ref port, ref length, 0) != 0) return null;

        var hub = new string(hubId).TrimEnd('\0');
        var guid = HubInterface;
        if (CM_Get_Device_Interface_List_SizeW(out var size, ref guid, hub, 0) != 0 || size <= 1) return null;
        var paths = new char[size];
        if (CM_Get_Device_Interface_ListW(ref guid, hub, paths, size, 0) != 0) return null;
        return (new string(paths).Split('\0', StringSplitOptions.RemoveEmptyEntries)[0], port);
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_List_SizeW(out uint length, string filter, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_ListW(string filter, [Out] char[] buffer, uint length, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(out uint node, string instanceId, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_Parent(out uint parent, uint node, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_IDW(uint node, [Out] char[] buffer, uint length, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_DevNode_Registry_PropertyW(uint node, uint property, out uint type, ref uint value, ref uint length, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_Interface_List_SizeW(out uint length, ref Guid interfaceClass, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_Interface_ListW(ref Guid interfaceClass, string deviceId, [Out] char[] buffer, uint length, uint flags);
}

/// <summary>A USB hub opened to send GET_DESCRIPTOR requests to the device on one of its ports.</summary>
internal sealed class UsbHub : IDisposable
{
    private const uint GetDescriptorFromNodeConnection = 0x220410; // IOCTL_USB_GET_DESCRIPTOR_FROM_NODE_CONNECTION
    private readonly Microsoft.Win32.SafeHandles.SafeFileHandle _handle;

    private UsbHub(Microsoft.Win32.SafeHandles.SafeFileHandle handle) => _handle = handle;

    public static UsbHub? Open(string path)
    {
        const uint genericWrite = 0x40000000, shareWrite = 0x2, openExisting = 3;
        var handle = CreateFileW(path, genericWrite, shareWrite, IntPtr.Zero, openExisting, 0, IntPtr.Zero);
        if (!handle.IsInvalid) return new UsbHub(handle);
        handle.Dispose();
        return null;
    }

    /// <summary>USB_NODE_CONNECTION_INFORMATION_EX (packed): port, device descriptor, configuration value, then speed.</summary>
    public int? GetSpeed(uint port)
    {
        const uint getConnectionInformationEx = 0x220448; // IOCTL_USB_GET_NODE_CONNECTION_INFORMATION_EX
        var buffer = new byte[512];
        BitConverter.TryWriteBytes(buffer.AsSpan(0), port);
        return DeviceIoControl(_handle, getConnectionInformationEx, buffer, buffer.Length, buffer, buffer.Length, out var returned, IntPtr.Zero)
               && returned > 23 ? buffer[23] : null;
    }

    /// <summary>USB_DESCRIPTOR_REQUEST: the port, an 8-byte setup packet, then the data the device answered.</summary>
    public byte[]? GetDescriptor(uint port, ushort value, ushort index, ushort length)
    {
        var buffer = new byte[12 + length];
        BitConverter.TryWriteBytes(buffer.AsSpan(0), port);
        buffer[4] = 0x80; // device to host (the hub driver sets the recipient itself)
        buffer[5] = 0x06; // GET_DESCRIPTOR
        BitConverter.TryWriteBytes(buffer.AsSpan(6), value);
        BitConverter.TryWriteBytes(buffer.AsSpan(8), index);
        BitConverter.TryWriteBytes(buffer.AsSpan(10), length);
        if (!DeviceIoControl(_handle, GetDescriptorFromNodeConnection, buffer, buffer.Length, buffer, buffer.Length, out var returned, IntPtr.Zero)
            || returned < 12)
            return null;
        return buffer[12..returned];
    }

    public void Dispose() => _handle.Dispose();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(Microsoft.Win32.SafeHandles.SafeFileHandle device, uint code, byte[] input, int inputLength,
        byte[] output, int outputLength, out int returned, IntPtr overlapped);
}
