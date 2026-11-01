using System.Text.Json;
using Arcadia.Input;

namespace Arcadia.ControllerSupport.Copy;

/// <summary>Bytes per report id (without the id byte) of one HID interface, from its report descriptor.</summary>
public sealed record HidReportSizes(bool UsesIds, IReadOnlyDictionary<byte, int> Input, IReadOnlyDictionary<byte, int> Output, IReadOnlyDictionary<byte, int> Feature)
{
    /// <summary>
    /// Reads a report descriptor far enough to know how long each report is: Report Size × Report Count summed per
    /// report id and main item (Input, Output, Feature), Push and Pop respected, long items skipped.
    /// </summary>
    public static HidReportSizes Parse(ReadOnlySpan<byte> descriptor)
    {
        var bits = new[] { new Dictionary<byte, int>(), new Dictionary<byte, int>(), new Dictionary<byte, int>() };
        var usesIds = false;
        (uint Size, uint Count, byte Id) globals = default;
        var stack = new Stack<(uint, uint, byte)>();
        for (var at = 0; at < descriptor.Length;)
        {
            var prefix = descriptor[at];
            if (prefix == 0xFE)
            {
                // A long item: its data size is in the next byte.
                if (at + 1 >= descriptor.Length)
                {
                    break;
                }

                at += 3 + descriptor[at + 1];
                continue;
            }

            var size = (prefix & 0x03) == 3 ? 4 : prefix & 0x03;
            if (at + 1 + size > descriptor.Length)
            {
                break;
            }

            uint value = 0;
            for (var i = 0; i < size; i++)
            {
                value |= (uint)descriptor[at + 1 + i] << (8 * i);
            }

            switch (prefix & 0xFC)
            {
                case 0x74:
                    globals.Size = value;
                    break;
                case 0x94:
                    globals.Count = value;
                    break;
                case 0x84:
                    globals.Id = (byte)value;
                    usesIds = true;
                    break;
                case 0xA4:
                    stack.Push(globals);
                    break;
                case 0xB4 when stack.Count > 0:
                    globals = stack.Pop();
                    break;
                case 0x80:
                    Add(bits[0], globals.Id, globals.Size * globals.Count);
                    break;
                case 0x90:
                    Add(bits[1], globals.Id, globals.Size * globals.Count);
                    break;
                case 0xB0:
                    Add(bits[2], globals.Id, globals.Size * globals.Count);
                    break;
            }

            at += 1 + size;
        }

        return new HidReportSizes(usesIds, Bytes(bits[0]), Bytes(bits[1]), Bytes(bits[2]));
    }

    private static void Add(Dictionary<byte, int> bits, byte id, uint count) =>
        bits[id] = bits.GetValueOrDefault(id) + (int)Math.Min(count, int.MaxValue / 2);

    private static Dictionary<byte, int> Bytes(Dictionary<byte, int> bits) => bits.ToDictionary(e => e.Key, e => (e.Value + 7) / 8);
}

/// <summary>How the Steam Controller is connected: the device Arcadia copies for Steam.</summary>
public enum SteamControllerConnection
{
    /// <summary>The wireless puck <c>28DE:1304</c>.</summary>
    Puck,

    /// <summary>The controller itself by USB cable <c>28DE:1302</c> (#345).</summary>
    Cable,
}

/// <summary>
/// The descriptors of the Steam Controller's puck <c>28DE:1304</c> (spike #239) and of the controller by cable
/// <c>28DE:1302</c> (spike #249) as read from the real ones: the copy (#242) enumerates with them, so Steam takes it for
/// the real device. Only the serial number is the connected device's own.
/// </summary>
public sealed class PuckDescriptors
{
    public const ushort Vendor = 0x28DE;

    private const byte HidClass = 0x03;
    private const int SerialIndex = 3;
    private const int MaxSerialLength = 32;

    private readonly string _device;
    private readonly string _config;
    private readonly Dictionary<string, string> _strings;
    private readonly Dictionary<string, string> _reports;
    private readonly uint _speed;

    private PuckDescriptors(SteamControllerConnection connection, string device, string config, Dictionary<string, string> strings, Dictionary<string, string> reports, uint speed)
    {
        Connection = connection;
        _device = device;
        _config = config;
        _strings = strings;
        _reports = reports;
        _speed = speed;
        InterfaceClasses = ReadInterfaceClasses(Convert.FromHexString(config));
        // idProduct, little endian at offset 10 of the device descriptor.
        var deviceBytes = Convert.FromHexString(device);
        Product = (ushort)(deviceBytes[10] | deviceBytes[11] << 8);
        ReportSizes = reports.ToDictionary(e => byte.Parse(e.Key, System.Globalization.CultureInfo.InvariantCulture), e => HidReportSizes.Parse(Convert.FromHexString(e.Value)));
    }

    /// <summary>The puck <c>28DE:1304</c>: seven interfaces, the HID ones 2–6.</summary>
    public static PuckDescriptors Triton { get; } = Load(SteamControllerConnection.Puck, "puck-descriptors.json");

    /// <summary>The controller by cable <c>28DE:1302</c>: one HID interface, number 0, so its paths carry no <c>&amp;MI_</c>.</summary>
    public static PuckDescriptors Cable { get; } = Load(SteamControllerConnection.Cable, "cable-descriptors.json");

    public static PuckDescriptors Of(SteamControllerConnection connection) => connection == SteamControllerConnection.Cable ? Cable : Triton;

    public SteamControllerConnection Connection { get; }

    /// <summary>The USB product id, from the device descriptor.</summary>
    public ushort Product { get; }

    /// <summary>The class of every interface (HID 3, CDC 2 and 10), by interface number.</summary>
    public IReadOnlyDictionary<byte, byte> InterfaceClasses { get; }

    /// <summary>The report sizes of every HID interface, by interface number.</summary>
    public IReadOnlyDictionary<byte, HidReportSizes> ReportSizes { get; }

    public bool IsHid(byte iface) => InterfaceClasses.TryGetValue(iface, out var cls) && cls == HidClass;

    /// <summary>
    /// Fail closed (QE review of PR #294): the copy (on usbip-win2's software host) and a device whose place can't be
    /// read are never taken for the real puck, or Arcadia would read its own copy and send Steam's requests back into it.
    /// Pan's <c>HidingList.OnSoftwareHost</c> fails the other way (an unknown host is hidden as a puck): a puck on such a
    /// host is hidden but never copied, so the copy doesn't start (docs/controller-support/pan-puck-restart.md).
    /// </summary>
    public static bool IsRealPuck(DeviceHost host) => host == DeviceHost.Hardware;

    /// <summary>
    /// The collections of one real puck when several are plugged in (review of PR #294): those of the first USB device by
    /// its id, so a copy never mixes interfaces of two pucks under one serial. Collections whose USB device can't be read
    /// are left out. <paramref name="pucks"/>: how many pucks were found.
    /// </summary>
    public static List<(byte Interface, string Path)> OnePuck(IEnumerable<(byte Interface, string Path, string? UsbDevice)> collections, out int pucks)
    {
        var byDevice = collections.Where(c => c.UsbDevice is not null)
            .GroupBy(c => c.UsbDevice!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        pucks = byDevice.Count;
        return byDevice.Count == 0 ? [] : [.. byDevice[0].Select(c => (c.Interface, c.Path))];
    }

    /// <summary>
    /// The device and interface number of a collection's path: the puck's carry <c>&amp;mi_NN</c>; the controller by cable
    /// has one interface, so its paths carry none and it is interface 0. Null for any other device.
    /// </summary>
    public static (SteamControllerConnection Connection, byte Interface)? PlaceOf(string path)
    {
        var lower = path.ToLowerInvariant();
        if (Names(lower, Cable))
        {
            return lower.Contains("&mi_", StringComparison.Ordinal) ? null : (SteamControllerConnection.Cable, 0);
        }

        if (!Names(lower, Triton))
        {
            return null;
        }

        var at = lower.IndexOf("&mi_", StringComparison.Ordinal);
        return at >= 0 && at + 6 <= lower.Length && byte.TryParse(lower.AsSpan(at + 4, 2), System.Globalization.NumberStyles.HexNumber, null, out var number)
            ? (SteamControllerConnection.Puck, number)
            : null;

        // Followed by the next part of the id, so a product id that only starts the same doesn't count.
        static bool Names(string lower, PuckDescriptors device) =>
            lower.Contains($"vid_{Vendor:x4}&pid_{device.Product:x4}&", StringComparison.Ordinal)
            || lower.Contains($"vid_{Vendor:x4}&pid_{device.Product:x4}#", StringComparison.Ordinal);
    }

    /// <summary>A serial number as Windows read it from the puck: letters and digits only, at most 32.</summary>
    public static bool IsValidSerial(string serial) =>
        serial.Length is > 0 and <= MaxSerialLength && serial.All(char.IsAsciiLetterOrDigit);

    /// <summary>The <c>deviceSpecific</c> of VIIPER's <c>hidproxy</c>: these descriptors with the connected puck's serial number.</summary>
    public Dictionary<string, object?> DeviceSpecific(string serial)
    {
        if (!IsValidSerial(serial))
        {
            throw new ArgumentException("Die Seriennummer des Pucks ist ungültig", nameof(serial));
        }

        var strings = new Dictionary<string, string>(_strings) { [SerialIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)] = serial };
        return new Dictionary<string, object?>
        {
            ["device"] = _device,
            ["config"] = _config,
            ["strings"] = strings,
            ["reports"] = new Dictionary<string, string>(_reports),
            ["speed"] = _speed,
        };
    }

    private static PuckDescriptors Load(SteamControllerConnection connection, string file)
    {
        using var stream = typeof(PuckDescriptors).Assembly.GetManifestResourceStream($"Arcadia.ControllerSupport.Copy.{file}")
            ?? throw new InvalidOperationException($"Die Beschreibung des Steam Controllers fehlt in Arcadia ({file})");
        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement;
        return new PuckDescriptors(
            connection,
            root.GetProperty("device").GetString()!,
            root.GetProperty("config").GetString()!,
            root.GetProperty("strings").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!),
            root.GetProperty("reports").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!),
            root.GetProperty("speed").GetUInt32());
    }

    private static Dictionary<byte, byte> ReadInterfaceClasses(byte[] config)
    {
        var classes = new Dictionary<byte, byte>();
        for (var at = (int)config[0]; at + 1 < config.Length && config[at] >= 2;)
        {
            if (config[at + 1] == 0x04 && config[at] >= 9 && at + 9 <= config.Length)
            {
                classes.TryAdd(config[at + 2], config[at + 5]);
            }

            at += config[at];
        }

        return classes;
    }
}
