using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Arcadia.Input;
using Microsoft.Win32.SafeHandles;

namespace Arcadia.ControllerSupport.Copy;

/// <summary>
/// The real puck through Windows' HID API (#242 part C): the vendor collection (usage page <c>FF00</c>) of each HID
/// interface, found by its ids and its place in the device tree, so the copy (same ids, on usbip-win2's software host)
/// is never taken for it. Windows won't open the mouse and keyboard collections for reading (spike #239), and Steam
/// needs only the vendor ones.
/// </summary>
/// <remarks>
/// Each collection is opened twice: one handle only reads (a synchronous read blocks the handle until the puck sends,
/// and an empty controller slot never does), the other sends feature and output reports. Windows' buffers are led by
/// the report id and padded to the collection's longest report; <see cref="HidReportSizes"/> cuts them to USB's length.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed partial class WindowsPuck : IRealPuck
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 0x03;
    private const uint OpenExisting = 3;
    private const uint DigcfPresent = 0x02;
    private const uint DigcfDeviceInterface = 0x10;
    private const int ErrorNoMoreItems = 259;
    private const int ErrorOperationAborted = 995;
    private const int HidpStatusSuccess = 0x00110000;
    private const ushort VendorUsagePage = 0xFF00;
    private const int SerialChars = 127;

    private readonly Dictionary<byte, Collection> _collections;

    private WindowsPuck(Dictionary<byte, Collection> collections, string serial, int pucksFound)
    {
        _collections = collections;
        Serial = serial;
        PucksFound = pucksFound;
    }

    /// <summary>How many real pucks were plugged in when it opened; only one of them is copied.</summary>
    public int PucksFound { get; }

    /// <summary>The puck's serial number; the copy shows Steam the same one, so Steam keeps its settings for it.</summary>
    public string Serial { get; }

    public IReadOnlyCollection<byte> Interfaces => _collections.Keys;

    /// <summary>The real puck's vendor collections are there (Arcadia sees them past HidHide). Never throws.</summary>
    public static bool Present()
    {
        try
        {
            return PuckDescriptors.OnePuck(Find(), out _).Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Opens every vendor collection of the real puck; throws when there is none or one can't be opened.</summary>
    public static WindowsPuck Open(PuckDescriptors descriptors)
    {
        var found = PuckDescriptors.OnePuck(Find(), out var pucks);
        if (found.Count == 0)
        {
            throw new InvalidOperationException("Der Puck des Steam Controllers ist nicht da (oder Arcadia steht nicht auf der Liste von HidHide)");
        }

        var collections = new Dictionary<byte, Collection>();
        try
        {
            foreach (var (iface, path) in found)
            {
                if (!descriptors.ReportSizes.TryGetValue(iface, out var sizes) || collections.ContainsKey(iface))
                {
                    continue;
                }

                collections[iface] = Collection.Open(path, sizes);
            }

            var serial = collections.Values.Select(c => c.Serial()).FirstOrDefault(s => s is not null)
                ?? throw new InvalidOperationException("Die Seriennummer des Pucks ist nicht lesbar");
            if (!PuckDescriptors.IsValidSerial(serial))
            {
                throw new InvalidOperationException("Der Puck meldet eine ungültige Seriennummer");
            }

            return new WindowsPuck(collections, serial, pucks);
        }
        catch
        {
            foreach (var collection in collections.Values)
            {
                collection.Dispose();
            }

            throw;
        }
    }

    public int ReadInput(byte iface, Span<byte> report) => _collections[iface].Read(report);

    public void SetFeature(byte iface, ReadOnlySpan<byte> report) => _collections[iface].SetFeature(report);

    public void WriteOutput(byte iface, ReadOnlySpan<byte> report) => _collections[iface].WriteOutput(report);

    public int GetReport(byte iface, HidReportType type, byte id, Span<byte> report) => _collections[iface].GetReport(type, id, report);

    public void Dispose()
    {
        foreach (var collection in _collections.Values)
        {
            collection.Dispose();
        }
    }

    /// <summary>
    /// The vendor collections of every real puck, by interface number (<c>&amp;mi_02</c> in the path) and the USB device
    /// they belong to.
    /// </summary>
    private static List<(byte Interface, string Path, string? UsbDevice)> Find()
    {
        HidD_GetHidGuid(out var guid);
        var set = SetupDiGetClassDevsW(ref guid, 0, 0, DigcfPresent | DigcfDeviceInterface);
        if (set == -1)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        try
        {
            var found = new List<(byte, string, string?)>();
            for (uint index = 0; ; index++)
            {
                var data = new DeviceInterfaceData { Size = (uint)Marshal.SizeOf<DeviceInterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(set, 0, ref guid, index, ref data))
                {
                    return Marshal.GetLastPInvokeError() == ErrorNoMoreItems ? found : throw new Win32Exception(Marshal.GetLastPInvokeError());
                }

                // Only a puck known to sit on a hardware host: one whose place can't be read might be the copy itself.
                if (PathOf(set, ref data) is { } path && InterfaceOf(path) is { } iface && IsVendorCollection(path)
                    && PuckDescriptors.IsRealPuck(DeviceTree.HostOfInterface(path)))
                {
                    found.Add((iface, path, DeviceTree.UsbDeviceOfInterface(path)));
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
    }

    /// <summary>The interface number of a puck collection's path, or null for any other device.</summary>
    public static byte? InterfaceOf(string path)
    {
        var lower = path.ToLowerInvariant();
        if (!lower.Contains($"vid_{PuckDescriptors.Vendor:x4}&pid_{PuckDescriptors.Product:x4}", StringComparison.Ordinal))
        {
            return null;
        }

        var at = lower.IndexOf("&mi_", StringComparison.Ordinal);
        return at >= 0 && at + 6 <= lower.Length && byte.TryParse(lower.AsSpan(at + 4, 2), System.Globalization.NumberStyles.HexNumber, null, out var number)
            ? number
            : null;
    }

    private static bool IsVendorCollection(string path)
    {
        // Opened without access rights: enough to read the capabilities, also of collections Windows keeps for itself.
        using var handle = CreateFileW(path, 0, FileShareReadWrite, 0, OpenExisting, 0, 0);
        return !handle.IsInvalid && Caps(handle) is { } caps && caps.UsagePage >= VendorUsagePage;
    }

    private static HidpCaps? Caps(SafeFileHandle handle)
    {
        if (!HidD_GetPreparsedData(handle, out var preparsed))
        {
            return null;
        }

        try
        {
            return HidP_GetCaps(preparsed, out var caps) == HidpStatusSuccess ? caps : null;
        }
        finally
        {
            HidD_FreePreparsedData(preparsed);
        }
    }

    private static unsafe string? PathOf(nint set, ref DeviceInterfaceData data)
    {
        SetupDiGetDeviceInterfaceDetailW(set, ref data, 0, 0, out var size, 0);
        if (size == 0)
        {
            return null;
        }

        var detail = Marshal.AllocHGlobal((int)size);
        try
        {
            // SP_DEVICE_INTERFACE_DETAIL_DATA_W: cbSize is the struct's own size (8 on x64), the path follows the DWORD.
            Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
            return SetupDiGetDeviceInterfaceDetailW(set, ref data, detail, size, out _, 0) ? Marshal.PtrToStringUni(detail + 4) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(detail);
        }
    }

    private sealed class Collection : IDisposable
    {
        private readonly SafeFileHandle _reader;
        private readonly SafeFileHandle _writer;
        private readonly HidpCaps _caps;
        private readonly HidReportSizes _sizes;
        private readonly byte[] _readBuffer;
        private static readonly TimeSpan CloseWait = TimeSpan.FromSeconds(1);

        private volatile bool _closed;
        private int _reading;

        private Collection(SafeFileHandle reader, SafeFileHandle writer, HidpCaps caps, HidReportSizes sizes)
        {
            _reader = reader;
            _writer = writer;
            _caps = caps;
            _sizes = sizes;
            _readBuffer = new byte[Math.Max((int)caps.InputReportByteLength, 1)];
        }

        public static Collection Open(string path, HidReportSizes sizes)
        {
            var reader = CreateFileW(path, GenericRead | GenericWrite, FileShareReadWrite, 0, OpenExisting, 0, 0);
            if (reader.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Puck nicht zu öffnen");
            }

            var writer = CreateFileW(path, GenericRead | GenericWrite, FileShareReadWrite, 0, OpenExisting, 0, 0);
            if (writer.IsInvalid)
            {
                var error = Marshal.GetLastPInvokeError();
                reader.Dispose();
                throw new Win32Exception(error, "Puck nicht zu öffnen");
            }

            if (Caps(reader) is not { } caps)
            {
                reader.Dispose();
                writer.Dispose();
                throw new InvalidOperationException("Puck: Fähigkeiten nicht lesbar");
            }

            return new Collection(reader, writer, caps, sizes);
        }

        public unsafe string? Serial()
        {
            var buffer = stackalloc char[SerialChars + 1];
            return HidD_GetSerialNumberString(_writer, buffer, (SerialChars + 1) * sizeof(char)) ? new string(buffer) : null;
        }

        public unsafe int Read(Span<byte> report)
        {
            // Counted before _closed is checked: Dispose cancels until no read is left, also one that began after its
            // first cancel (QE review of PR #294), or that read would wait for a report an empty slot never sends.
            Interlocked.Increment(ref _reading);
            try
            {
                if (_closed)
                {
                    return 0;
                }

                uint read;
                fixed (byte* buffer = _readBuffer)
                {
                    if (!ReadFile(_reader, buffer, (uint)_readBuffer.Length, out read, 0))
                    {
                        var error = Marshal.GetLastPInvokeError();
                        return _closed || error == ErrorOperationAborted ? 0 : throw new Win32Exception(error);
                    }
                }

                return FromWindows(_readBuffer.AsSpan(0, (int)read), _sizes.Input, report);
            }
            finally
            {
                Interlocked.Decrement(ref _reading);
            }
        }

        public void SetFeature(ReadOnlySpan<byte> report)
        {
            var buffer = ToWindows(report, _caps.FeatureReportByteLength);
            if (!HidD_SetFeature(_writer, buffer, buffer.Length))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }
        }

        public unsafe void WriteOutput(ReadOnlySpan<byte> report)
        {
            var buffer = ToWindows(report, _caps.OutputReportByteLength);
            fixed (byte* bytes = buffer)
            {
                if (!WriteFile(_writer, bytes, (uint)buffer.Length, out _, 0))
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError());
                }
            }
        }

        public int GetReport(HidReportType type, byte id, Span<byte> report)
        {
            var length = type == HidReportType.Feature ? _caps.FeatureReportByteLength : _caps.InputReportByteLength;
            var buffer = new byte[Math.Max((int)length, 1)];
            buffer[0] = _sizes.UsesIds ? id : (byte)0;
            var ok = type == HidReportType.Feature ? HidD_GetFeature(_writer, buffer, buffer.Length) : HidD_GetInputReport(_writer, buffer, buffer.Length);
            if (!ok)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            return FromWindows(buffer, type == HidReportType.Feature ? _sizes.Feature : _sizes.Input, report);
        }

        /// <summary>A Windows buffer (led by the report id, 0 without ids, padded) as USB sends it.</summary>
        private int FromWindows(ReadOnlySpan<byte> buffer, IReadOnlyDictionary<byte, int> sizes, Span<byte> report)
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }

            var data = _sizes.UsesIds ? buffer : buffer[1..];
            var size = sizes.TryGetValue(_sizes.UsesIds ? buffer[0] : (byte)0, out var known) ? known + (_sizes.UsesIds ? 1 : 0) : data.Length;
            size = Math.Min(Math.Min(size, data.Length), report.Length);
            data[..size].CopyTo(report);
            return size;
        }

        /// <summary>A USB report (led by the id only if the interface uses ids) as a Windows buffer of the collection's length.</summary>
        private byte[] ToWindows(ReadOnlySpan<byte> report, ushort length)
        {
            var offset = _sizes.UsesIds ? 0 : 1;
            if (report.Length + offset > length)
            {
                throw new ArgumentException($"Report mit {report.Length} Bytes ist länger als die {length} Bytes des Pucks");
            }

            var buffer = new byte[length];
            report.CopyTo(buffer.AsSpan(offset));
            return buffer;
        }

        public void Dispose()
        {
            _closed = true;
            // Ends every read that waits for the puck, then closes both handles.
            var until = Environment.TickCount64 + (long)CloseWait.TotalMilliseconds;
            do
            {
                CancelIoEx(_reader, 0);
                if (Volatile.Read(ref _reading) == 0)
                {
                    break;
                }

                Thread.Sleep(5);
            }
            while (Environment.TickCount64 < until);

            _reader.Dispose();
            _writer.Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        private unsafe fixed ushort _reserved[17];
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData
    {
        public uint Size;
        public Guid InterfaceClass;
        public uint Flags;
        public nint Reserved;
    }

    [LibraryImport("hid.dll")]
    private static partial void HidD_GetHidGuid(out Guid guid);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool HidD_GetPreparsedData(SafeFileHandle device, out nint preparsed);

    [LibraryImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool HidD_FreePreparsedData(nint preparsed);

    [LibraryImport("hid.dll")]
    private static partial int HidP_GetCaps(nint preparsed, out HidpCaps caps);

    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static unsafe partial bool HidD_GetSerialNumberString(SafeFileHandle device, char* buffer, int length);

    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool HidD_SetFeature(SafeFileHandle device, byte[] buffer, int length);

    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool HidD_GetFeature(SafeFileHandle device, byte[] buffer, int length);

    [LibraryImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool HidD_GetInputReport(SafeFileHandle device, byte[] buffer, int length);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint creation, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool ReadFile(SafeFileHandle file, byte* buffer, uint length, out uint read, nint overlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool WriteFile(SafeFileHandle file, byte* buffer, uint length, out uint written, nint overlapped);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CancelIoEx(SafeFileHandle file, nint overlapped);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    private static partial nint SetupDiGetClassDevsW(ref Guid classGuid, nint enumerator, nint parent, uint flags);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiEnumDeviceInterfaces(nint set, nint deviceInfo, ref Guid interfaceClass, uint index, ref DeviceInterfaceData data);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiGetDeviceInterfaceDetailW(nint set, ref DeviceInterfaceData data, nint detail, uint detailSize, out uint requiredSize, nint deviceInfo);

    [LibraryImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiDestroyDeviceInfoList(nint set);
}
