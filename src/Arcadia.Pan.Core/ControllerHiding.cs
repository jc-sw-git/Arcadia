using System.Globalization;
using System.Text.RegularExpressions;

namespace Arcadia.Pan;

/// <summary>
/// A controller model by its USB ids (#335): what the client names in <see cref="PanCommand.HideController"/>, never an
/// instance. Pan finds the model's devices itself, so a message can't make it hide anything else.
/// </summary>
public readonly partial record struct ControllerModel(ushort Vendor, ushort Product)
{
    /// <summary>The model as a message's value: vendor in the high, product in the low half.</summary>
    public uint Value => (uint)Vendor << 16 | Product;

    public static ControllerModel From(uint value) => new((ushort)(value >> 16), (ushort)value);

    /// <summary>
    /// The model named in a device instance id or interface link, or null: USB (<c>VID_054C&amp;PID_0CE6</c>), classic
    /// Bluetooth (<c>VID&amp;0002054C_PID&amp;0CE6</c>, source then vendor) and Bluetooth LE (<c>VID&amp;02045E_PID&amp;0B13</c>).
    /// </summary>
    public static ControllerModel? Of(string deviceId)
    {
        var match = IdsPattern().Match(deviceId);
        if (!match.Success)
        {
            return null;
        }

        var vendor = match.Groups["vendor"].Value;
        return new ControllerModel(
            ushort.Parse(vendor[^4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            ushort.Parse(match.Groups["product"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    public override string ToString() => $"{Vendor:X4}:{Product:X4}";

    [GeneratedRegex(@"VID[_&](?<vendor>[0-9A-F]{8}|[0-9A-F]{6}|[0-9A-F]{4})[_&]PID[_&](?<product>[0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdsPattern();
}

/// <summary>How a game reaches a controller's device: its HID collections, or its XInput interface (<c>xusb22</c>).</summary>
public enum ControllerInterface
{
    Hid,
    XInput,
}

/// <summary>One device of a controller that Pan hides.</summary>
public sealed record ControllerInstance(string InstanceId, ControllerInterface Interface);

/// <summary>A model's devices: whether any is connected, and which of them Pan may hide.</summary>
public sealed record ControllerScan(bool Connected, IReadOnlyList<ControllerInstance> Hideable);

/// <summary>Every controller but the puck on this PC (#335); both calls may hang on Windows' PnP lock.</summary>
public interface IControllerDevices
{
    /// <summary>
    /// The model's devices on a hardware host: its HID collections other than keyboard and mouse, and its XInput
    /// interfaces, provided one of them is a gamepad (a HID gamepad or joystick collection, or an XInput interface).
    /// </summary>
    ControllerScan Scan(ControllerModel model);

    /// <summary>
    /// Restarts the devices the HID instances belong to (their USB interface or Bluetooth device), so programs holding
    /// them lose them and Windows announces them again. Returns the instances whose device needs a reconnect for it.
    /// </summary>
    /// <exception cref="Exception">The devices couldn't be read or restarted; they stay pending.</exception>
    Task<IReadOnlyList<string>> RestartAsync(IReadOnlyList<string> hidInstances, CancellationToken cancellationToken);
}

/// <summary>The rules for taking over controllers other than the puck (#335), free of Windows.</summary>
public static class ControllerList
{
    public const ushort Valve = 0x28DE;
    public const ushort Microsoft = 0x045E;
    public const ushort Sony = 0x054C;

    /// <summary>
    /// Whether Pan may take the model over: Valve's devices never (the puck has its own path, the others stay visible
    /// until verified, #248, #249), nor ids that name no device.
    /// </summary>
    public static bool MayTakeOver(ControllerModel model) => model.Vendor is not 0 and not Valve && model.Product != 0;

    /// <summary>An instance id Pan may journal for a controller: a HID collection or a USB interface of a model it may take over.</summary>
    public static bool IsControllerInstance(string instanceId) =>
        (IsHid(instanceId) || instanceId.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase))
        && ControllerModel.Of(instanceId) is { } model && MayTakeOver(model);

    public static bool IsHid(string instanceId) => instanceId.StartsWith(@"HID\", StringComparison.OrdinalIgnoreCase);

    public static bool IsOfModel(string instanceId, ControllerModel model) => ControllerModel.Of(instanceId) == model;

    /// <summary>A vendor's firmware tool (#238 D8): while it runs, Pan hides nothing of that vendor, so an update isn't cut off.</summary>
    public sealed record FirmwareTool(ushort Vendor, string Name, IReadOnlyList<string> ProcessNames);

    /// <summary>
    /// The tools and their process names (without <c>.exe</c>). The names are what the tools were published with and
    /// still to be confirmed on the owner's PC (T335-5); a name that never runs only means no wait.
    /// </summary>
    public static readonly IReadOnlyList<FirmwareTool> FirmwareTools =
    [
        new(Microsoft, "Xbox Accessories", ["XboxDevices", "XboxAccessories"]),
        new(Sony, "DualSense firmware updater", ["FWupdater", "FWupdaterDualSense"]),
    ];

    /// <summary>The firmware tool of the model's vendor that runs now, or null; <paramref name="runs"/> asks for one process name.</summary>
    public static FirmwareTool? RunningToolFor(ControllerModel model, Func<string, bool> runs) =>
        FirmwareTools.FirstOrDefault(tool => tool.Vendor == model.Vendor && tool.ProcessNames.Any(runs));
}
