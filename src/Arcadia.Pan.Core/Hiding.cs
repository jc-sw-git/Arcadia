namespace Arcadia.Pan;

/// <summary>
/// One kind of Steam Controller device Pan hides: its HID instances, the USB device a restart restarts, and the part of
/// a HID interface's symbolic link that names it, to pick its arrivals out cheaply.
/// </summary>
public sealed record ListedDevice(string Name, string HidPrefix, string UsbPrefix, string InterfaceMarker)
{
    public bool IsHid(string instanceId) => instanceId.StartsWith(HidPrefix, StringComparison.OrdinalIgnoreCase);

    public bool IsUsbParent(string instanceId) => instanceId.StartsWith(UsbPrefix, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The devices Pan may hide (#243 part B): only what Arcadia can emulate, found by Pan itself, never named in a
/// message. The new Steam Controller on its wireless puck <c>28DE:1304</c> and by cable <c>28DE:1302</c> (#345), every
/// HID instance of each. Other Valve devices (2015 controller, Triton by Bluetooth, Steam Deck, Index) stay visible
/// until verified (#248, #342).
/// Arcadia's copy for Steam (#242) has the same ids, but sits on VIIPER's software USB host: it is never
/// hidden nor restarted (<see cref="OnSoftwareHost"/>), or Steam would lose the very copy it is meant to use (#308).
/// </summary>
public static class HidingList
{
    /// <summary>The puck: a composite USB device, so its HID instances carry <c>&amp;MI_</c>; a restart restarts the composite parent.</summary>
    public static readonly ListedDevice Puck = new("puck", @"HID\VID_28DE&PID_1304&", @"USB\VID_28DE&PID_1304\", "VID_28DE&PID_1304");

    /// <summary>The Steam Controller by cable (spike #249): one HID interface, so no <c>&amp;MI_</c>; three collections.</summary>
    public static readonly ListedDevice Cable = new("cable", @"HID\VID_28DE&PID_1302&", @"USB\VID_28DE&PID_1302\", "VID_28DE&PID_1302");

    public static IReadOnlyList<ListedDevice> Devices { get; } = [Puck, Cable];

    public static bool IsListedHid(string instanceId) => DeviceOf(instanceId) is not null;

    public static bool IsListedUsbParent(string instanceId) => Devices.Any(device => device.IsUsbParent(instanceId));

    /// <summary>Whether a HID interface's symbolic link names a listed device.</summary>
    public static bool IsListedInterface(string symbolicLink) =>
        Devices.Any(device => symbolicLink.Contains(device.InterfaceMarker, StringComparison.OrdinalIgnoreCase));

    /// <summary>The listed device a HID instance id belongs to, or null.</summary>
    public static ListedDevice? DeviceOf(string hidInstanceId) => Devices.FirstOrDefault(device => device.IsHid(hidInstanceId));

    /// <summary>
    /// The bootloaders a Valve device restarts into to be flashed (#295): <c>28DE:1005</c> the Steam Controller by
    /// cable, <c>28DE:1007</c> the puck, each a USB device with a COM port and the device's own serial.
    /// </summary>
    public static bool IsBootloader(string instanceId) =>
        instanceId.StartsWith(@"USB\VID_28DE&PID_1005", StringComparison.OrdinalIgnoreCase) || IsPuckBootloader(instanceId);

    /// <summary>The puck's bootloader, which every restart of the puck passes through for a few seconds (spike #239).</summary>
    public static bool IsPuckBootloader(string instanceId) =>
        instanceId.StartsWith(@"USB\VID_28DE&PID_1007", StringComparison.OrdinalIgnoreCase);

    /// <summary>A device has a few USB and HID levels above it (interface, composite device, hubs); more means the tree is odd.</summary>
    private const int MaxLevels = 16;

    /// <summary>
    /// The device hangs below a software USB host (<c>ROOT\…</c> as usbip-win2 creates for VIIPER, or <c>SWD\…</c>):
    /// the copy, not the puck. Climbs <paramref name="parentOf"/> past the USB and HID levels to the host. A tree that
    /// can't be read counts as hardware: hiding the copy by mistake costs Steam the copy, not hiding the puck would
    /// give every input twice. Arcadia's <c>PuckDescriptors.IsRealPuck</c> fails the other way (it copies only a puck on
    /// a PCI or ACPI host), so a puck on another host is hidden but not copied, and the copy doesn't start.
    /// </summary>
    /// <param name="parentOf">The parent's instance id, or null when it can't be read.</param>
    public static bool OnSoftwareHost(string instanceId, Func<string, string?> parentOf) =>
        HostOf(instanceId, parentOf) is { } host && IsSoftwareHost(host);

    /// <summary>
    /// The host a device hangs below: the first parent past its USB and HID levels (a PCI or ACPI controller, or a
    /// software host <c>ROOT\…</c>/<c>SWD\…</c>). Null when a parent can't be read or the tree is deeper than
    /// <see cref="MaxLevels"/>. Also for an entry that isn't plugged in, when <paramref name="parentOf"/> gives the parent
    /// Windows remembers for it (#347).
    /// </summary>
    /// <param name="parentOf">The parent's instance id, or null when it can't be read.</param>
    public static string? HostOf(string instanceId, Func<string, string?> parentOf)
    {
        var current = instanceId;
        for (var level = 0; level < MaxLevels; level++)
        {
            if (parentOf(current) is not { Length: > 0 } parent)
            {
                return null;
            }

            if (!parent.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase) && !parent.StartsWith(@"HID\", StringComparison.OrdinalIgnoreCase))
            {
                return parent;
            }

            current = parent;
        }

        return null;
    }

    public static bool IsSoftwareHost(string instanceId) =>
        instanceId.StartsWith(@"ROOT\", StringComparison.OrdinalIgnoreCase) || instanceId.StartsWith(@"SWD\", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A HidHide call failed; <see cref="Reason"/> is what Arcadia is told.</summary>
public sealed class HidHideException(PanReason reason, string message) : Exception(message)
{
    public PanReason Reason { get; } = reason;
}

/// <summary>
/// HidHide's driver interface (its control device and IOCTLs), not its command line. Every call opens the control
/// device and closes it again: HidHide lets one program at a time have it open.
/// </summary>
public interface IHidHideDriver
{
    /// <summary>The installed driver's version, or null when HidHide isn't installed or its version can't be read.</summary>
    Version? InstalledVersion();

    /// <summary>HidHide's service is registered (it may still be stopped): its lists may still hide devices.</summary>
    bool IsServiceRegistered();

    /// <exception cref="HidHideException">HidHide can't be reached or refused the call.</exception>
    IReadOnlyList<string> GetHiddenDevices();

    /// <exception cref="HidHideException"/>
    void SetHiddenDevices(IReadOnlyList<string> instanceIds);

    /// <summary>The allow list: full image paths in Windows' device form (<c>\Device\HarddiskVolume3\…</c>).</summary>
    /// <exception cref="HidHideException"/>
    IReadOnlyList<string> GetAllowedApplications();

    /// <exception cref="HidHideException"/>
    void SetAllowedApplications(IReadOnlyList<string> imagePaths);

    /// <exception cref="HidHideException"/>
    bool GetCloaking();

    /// <exception cref="HidHideException"/>
    void SetCloaking(bool on);

    /// <summary>Whether the allow list is inverted (it then lists who does NOT see hidden devices).</summary>
    /// <exception cref="HidHideException"/>
    bool GetInverse();
}

/// <summary>The listed devices on this PC (<see cref="HidingList"/>); both calls may hang on Windows' PnP lock.</summary>
public interface ISteamControllerDevices
{
    /// <summary>The HID instance ids of the listed devices that are connected now.</summary>
    IReadOnlyList<string> PresentHidInstances();

    /// <summary>Restarts the listed USB devices, so Windows announces them again and open handles to them close.</summary>
    /// <param name="only">
    /// Only these kinds (a cable that arrived while the puck's copy holds the puck, #345), or null for every listed device.
    /// </param>
    Task RestartAsync(IReadOnlyCollection<ListedDevice>? only, CancellationToken cancellationToken);

    /// <summary>The instance ids of the Valve bootloaders (<see cref="HidingList.IsBootloader"/>) connected now: a firmware update is being flashed.</summary>
    IReadOnlyList<string> PresentBootloaders();
}

/// <summary>HidHide's state as read before a change.</summary>
public sealed record HidHideState(IReadOnlyList<string> Hidden, IReadOnlyList<string> Allowed, bool Cloaking);

/// <summary>
/// The rules for hiding (#243 part B, review point R13), free of Windows: which HidHide versions and states Pan
/// works with, and which entries are the user's and stay untouched. The guardian journals and applies the changes.
/// </summary>
/// <param name="controllers">Every other controller (#335); without it Pan answers their commands with <see cref="PanReply.Unsupported"/>.</param>
public sealed class SteamControllerHiding(IHidHideDriver hidHide, ISteamControllerDevices devices, IControllerDevices? controllers = null)
{
    /// <summary>
    /// The oldest HidHide driver Pan works with: the one of HidHide 1.5.230, verified on the owner's PC (spike #239) and
    /// the CI runner. Older ones may have the IOCTLs but were never tested, and "Aktualisieren" brings 1.5.230 keeping
    /// their lists (owner, 2026-10-27, after 1.2.93 passed unnoticed). A new major version may change the driver
    /// interface and is refused until verified.
    /// </summary>
    public static readonly Version MinimumVersion = HidHideRelease.DriverVersion;

    public const int SupportedMajorVersion = 1;

    public IHidHideDriver HidHide { get; } = hidHide;

    public ISteamControllerDevices Devices { get; } = devices;

    public IControllerDevices? Controllers { get; } = controllers;

    /// <summary>
    /// Whether Pan may hide now, and HidHide's state if so. Refused: HidHide missing, an unknown version or interface,
    /// inverse mode, or cloaking off while the user lists devices of their own (turning it on would hide those too).
    /// Entries Pan added itself (<paramref name="ours"/>) and listed devices don't count as the user's.
    /// </summary>
    public (PanReason Reason, HidHideState? State, string? Detail) Check(IReadOnlyCollection<string> ours)
    {
        Version? version;
        try
        {
            version = HidHide.InstalledVersion();
        }
        catch (HidHideException ex)
        {
            return (ex.Reason, null, ex.Message);
        }

        if (version is null)
        {
            return (PanReason.HidHideMissing, null, "HidHide is not installed");
        }

        if (!HidHideRelease.IsSupported(version))
        {
            return (PanReason.HidHideUnsupported, null, $"HidHide {version} is not supported (needs {MinimumVersion} up to {SupportedMajorVersion}.x)");
        }

        HidHideState state;
        try
        {
            if (HidHide.GetInverse())
            {
                return (PanReason.HidHideInverseMode, null, "HidHide's application list is inverted");
            }

            state = new HidHideState(HidHide.GetHiddenDevices(), HidHide.GetAllowedApplications(), HidHide.GetCloaking());
        }
        catch (HidHideException ex)
        {
            return (ex.Reason, null, ex.Message);
        }

        if (!state.Cloaking)
        {
            var foreign = state.Hidden.Where(id => !HidingList.IsListedHid(id) && !ours.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList();
            if (foreign.Count > 0)
            {
                return (PanReason.HidHideCloakingOffWithForeignEntries, null, $"cloaking is off and {foreign.Count} device(s) of the user's are listed");
            }
        }

        return (PanReason.None, state, null);
    }

    /// <summary>
    /// Why a hide Pan did for the client no longer holds in <paramref name="state"/> (#242), or null if it does: every
    /// instance it made sure of is still hidden, the client still allow-listed, cloaking still on. Entries are compared as
    /// HidHide does; the user may have added more of their own.
    /// </summary>
    public static string? WhyNotHidden(HidHideState state, string imagePath, IReadOnlyCollection<string> instances)
    {
        if (!state.Cloaking)
        {
            return "cloaking was turned off";
        }

        if (!Contains(state.Allowed, imagePath))
        {
            return "the client is no longer on the allow list";
        }

        var shown = instances.Count(id => !Contains(state.Hidden, id));
        return shown == 0 ? null : $"{shown} of {instances.Count} instance(s) no longer hidden";
    }

    /// <summary>The entries of <paramref name="list"/> without <paramref name="remove"/>, compared as HidHide does (ignoring case).</summary>
    public static List<string> Without(IEnumerable<string> list, IEnumerable<string> remove)
    {
        var removed = new HashSet<string>(remove, StringComparer.OrdinalIgnoreCase);
        return list.Where(entry => !removed.Contains(entry)).ToList();
    }

    public static bool Contains(IEnumerable<string> list, string entry) => list.Contains(entry, StringComparer.OrdinalIgnoreCase);
}
