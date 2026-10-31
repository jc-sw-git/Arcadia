namespace Arcadia.Input;

/// <summary>
/// One XInput (XUSB) device as Windows' device tree shows it: whether its USB host controller is a software one
/// (usbip-win2's for SISR's VIIPER pad, P3 of #212), and which XInput slot it holds, if that could be read.
/// </summary>
public readonly record struct XusbDevice(bool OnSoftwareHost, int? Slot);

/// <summary>
/// Recognises emulated Xbox pads by their place in the device tree (#212). The XInput slot ties SDL's pad
/// (<c>XInput#n</c>) to a device; when the slots can't be read, a pad counts as emulated only if every XInput device
/// whose slot is unknown is emulated, so a real pad is never lost to a guess (it would rather count twice).
/// The copy of the Steam Controller's puck (#242) and Arcadia's own PlayStation and Switch 2 Pro pads (#238) are told by
/// SDL's HID path, which leads to their device.
/// </summary>
/// <param name="onSoftwareHost">Whether the device behind a HID path is on a software host (<see cref="DeviceTree.InterfaceOnSoftwareHost"/>).</param>
/// <param name="ownPads">Arcadia's own pads plugged in now: where the device tree can't be read, a pad with their ids is taken for one.</param>
public sealed class DeviceTreeEmulatedPads(
    Func<IReadOnlyList<XusbDevice>> listDevices,
    Action<string> log,
    Func<string, bool>? onSoftwareHost = null,
    OwnPadModels? ownPads = null) : IEmulatedPads
{
    // Reading a copy as real only shows a puck twice: the safe side.
    public bool IsCopy(PadDevice device) => OnSoftwareHostByPath(device, unknown: false);

    // Reading Arcadia's own pad as real would give it a pad of its own, and that one another (QE review of #346):
    // unknown, a pad with the ids of one Arcadia has plugged in counts as Arcadia's.
    public bool IsVirtualHidPad(PadDevice device) =>
        OnSoftwareHostByPath(device, unknown: ownPads?.Has(device.Vendor, device.Product) == true);

    private bool OnSoftwareHostByPath(PadDevice device, bool unknown)
    {
        if (onSoftwareHost is null || device.XInputSlot is not null || device.Path.Length == 0)
        {
            return false;
        }

        try
        {
            return onSoftwareHost(device.Path);
        }
        catch (Exception ex)
        {
            // Called on the poll thread while a pad connects; reading it is the safe side (it would rather count twice).
            log($"Controller: Geräteliste von Windows nicht lesbar, {device.Name} gilt als {(unknown ? "Arcadias eigener" : "echt")}: {ex.Message}");
            return unknown;
        }
    }

    public bool IsEmulated(PadDevice device)
    {
        if (device.XInputSlot is not { } slot)
        {
            return false;
        }

        IReadOnlyList<XusbDevice> devices;
        try
        {
            devices = listDevices();
        }
        catch (Exception ex)
        {
            // Called on the poll thread while a pad connects; reading it as a real pad is the safe side.
            log($"Controller: Geräteliste von Windows nicht lesbar, XInput#{slot} gilt als echter Controller: {ex.Message}");
            return false;
        }

        var emulated = Decide(slot, devices);
        if (emulated is null)
        {
            log($"Controller: unklar, ob XInput#{slot} virtuell ist ({Describe(devices)}); wird gelesen");
        }

        return emulated == true;
    }

    /// <returns>True or false when the device tree says so, null when it can't tell (then the pad is read).</returns>
    public static bool? Decide(int slot, IReadOnlyList<XusbDevice> devices)
    {
        foreach (var device in devices)
        {
            if (device.Slot == slot)
            {
                return device.OnSoftwareHost;
            }
        }

        var unknown = devices.Where(device => device.Slot is null).ToList();
        if (unknown.Count == 0)
        {
            // No device holds the slot: whatever SDL reads there isn't on a software host.
            return false;
        }

        if (unknown.All(device => device.OnSoftwareHost))
        {
            return true;
        }

        return unknown.Any(device => device.OnSoftwareHost) ? null : false;
    }

    private static string Describe(IReadOnlyList<XusbDevice> devices) =>
        string.Join(", ", devices.Select(device => $"{(device.OnSoftwareHost ? "virtuell" : "echt")} auf {(device.Slot is { } slot ? $"#{slot}" : "?")}"));
}
