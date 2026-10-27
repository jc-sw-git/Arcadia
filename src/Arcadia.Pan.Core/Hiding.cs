namespace Arcadia.Pan;

/// <summary>
/// The devices Pan may hide (#243 part B): only what Arcadia can emulate, found by Pan itself, never named in a
/// message. For now the new Steam Controller's wireless puck <c>28DE:1304</c>, every HID instance of it. Other Valve
/// devices (2015 controller, Triton by cable or Bluetooth, Steam Deck, Index) stay visible until verified (#248, #249).
/// </summary>
public static class HidingList
{
    /// <summary>Instance ids of the puck's HID collections start with this (all its interfaces and collections).</summary>
    public const string PuckHidPrefix = @"HID\VID_28DE&PID_1304&";

    /// <summary>The puck as USB device (the composite parent, not one of its <c>&amp;MI_</c> interfaces): what a restart restarts.</summary>
    public const string PuckUsbPrefix = @"USB\VID_28DE&PID_1304\";

    /// <summary>The part of a HID interface's symbolic link that names the puck, to pick its arrivals out cheaply.</summary>
    public const string PuckInterfaceMarker = "VID_28DE&PID_1304";

    public static bool IsListedHid(string instanceId) => instanceId.StartsWith(PuckHidPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsListedUsbParent(string instanceId) => instanceId.StartsWith(PuckUsbPrefix, StringComparison.OrdinalIgnoreCase);
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
    Task RestartAsync(CancellationToken cancellationToken);
}

/// <summary>HidHide's state as read before a change.</summary>
public sealed record HidHideState(IReadOnlyList<string> Hidden, IReadOnlyList<string> Allowed, bool Cloaking);

/// <summary>
/// The rules for hiding (#243 part B, review point R13), free of Windows: which HidHide versions and states Pan
/// works with, and which entries are the user's and stay untouched. The guardian journals and applies the changes.
/// </summary>
public sealed class SteamControllerHiding(IHidHideDriver hidHide, ISteamControllerDevices devices)
{
    /// <summary>
    /// The oldest HidHide Pan works with: the one verified on the owner's PC (spike #239) and the CI runner. Older ones
    /// may have the IOCTLs but were never tested, and "Aktualisieren" brings 1.5.230 keeping their lists (owner,
    /// 2026-10-27, after 1.2.93 passed unnoticed). A new major version may change the driver interface and is refused
    /// until verified.
    /// </summary>
    public static readonly Version MinimumVersion = new(1, 5, 230);

    public const int SupportedMajorVersion = 1;

    public IHidHideDriver HidHide { get; } = hidHide;

    public ISteamControllerDevices Devices { get; } = devices;

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

    /// <summary>The entries of <paramref name="list"/> without <paramref name="remove"/>, compared as HidHide does (ignoring case).</summary>
    public static List<string> Without(IEnumerable<string> list, IEnumerable<string> remove)
    {
        var removed = new HashSet<string>(remove, StringComparer.OrdinalIgnoreCase);
        return list.Where(entry => !removed.Contains(entry)).ToList();
    }

    public static bool Contains(IEnumerable<string> list, string entry) => list.Contains(entry, StringComparer.OrdinalIgnoreCase);
}
