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
/// The copy of the Steam Controller's puck (#242) is told by SDL's HID path, which leads to its device.
/// </summary>
/// <param name="onSoftwareHost">Whether the device behind a HID path is on a software host (<see cref="DeviceTree.InterfaceOnSoftwareHost"/>).</param>
public sealed class DeviceTreeEmulatedPads(Func<IReadOnlyList<XusbDevice>> listDevices, Action<string> log, Func<string, bool>? onSoftwareHost = null) : IEmulatedPads
{
    public bool IsCopy(PadDevice device)
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
            log($"Controller: Geräteliste von Windows nicht lesbar, der Steam Controller gilt als echt: {ex.Message}");
            return false;
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
